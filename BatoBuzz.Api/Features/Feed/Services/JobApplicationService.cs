using BatoBuzz.Feed.Dtos;
using BatoBuzz.Feed.Entities;
using BatoBuzz.Feed.Enums;
using BatoBuzz.Feed.Services;
using BatoBuzz.Feed.Data;
using BatoBuzz.Feed.Dtos.Feed;
using BatoBuzz.Feed.Entities;
using BatoBuzz.Feed.Enums;
using BatoBuzz.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Feed.Services;

public interface IJobApplicationService
{
    Task<JobApplicationDto> ApplyAsync(Guid postId, ApplyToJobRequest req, CancellationToken ct);
    Task<HasAppliedResult> HasAppliedAsync(Guid postId, CancellationToken ct);
    Task<ApplicationsPage> GetMyApplicationsAsync(string? cursor, int pageSize, CancellationToken ct);
    Task WithdrawAsync(Guid postId, CancellationToken ct);
    Task<ApplicationsPage> GetApplicantsAsync(Guid postId, string? cursor, int pageSize, CancellationToken ct);
    Task<ApplicationsPage> GetAllMyJobApplicantsAsync(string? cursor, int pageSize, CancellationToken ct);
    Task<JobApplicationDto> UpdateStatusAsync(Guid applicationId, string status, CancellationToken ct);
}

/// Job applications. Users apply to job posts (once each); merchants see who
/// applied to their vacancies and move them through a status pipeline.
public sealed class JobApplicationService : IJobApplicationService
{
    private readonly FeedDbContext _db;
    private readonly ICurrentActor _actor;
    private readonly IResumeStorage _resumes;

    public JobApplicationService(FeedDbContext db, ICurrentActor actor, IResumeStorage resumes)
        => (_db, _actor, _resumes) = (db, actor, resumes);

    /// Apply to a job post. One application per (post, user) — re-applying is
    /// rejected with 409, matching the app's one-doc-per-applicant model. The
    /// job details are snapshotted from the post so the merchant's list and the
    /// applicant's history render without re-reading the post.
    public async Task<JobApplicationDto> ApplyAsync(Guid postId, ApplyToJobRequest req, CancellationToken ct)
    {
        if (_actor.IsMerchant)
            throw AppException.Forbidden("Merchants can't apply to jobs.");

        var userId = _actor.Id;

        var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId, ct)
            ?? throw AppException.NotFound("That job post doesn't exist.");
        if (post.PostType != PostType.Job)
            throw new AppException("That post isn't a job vacancy.");

        var already = await _db.JobApplications
            .AnyAsync(a => a.PostId == postId && a.UserId == userId, ct);
        if (already)
            throw new AppException("You've already applied to this job.", 409);

        string? resumeUrl = null;
        if (req.Resume is not null)
            resumeUrl = await _resumes.SaveAsync(req.Resume, postId, userId, ct);

        var application = new JobApplication
        {
            PostId = postId,
            UserId = userId,
            MerchantId = post.MerchantId,
            ApplicantName = req.ApplicantName.Trim(),
            ApplicantPhone = req.ApplicantPhone.Trim(),
            ApplicantEmail = req.ApplicantEmail.Trim(),
            CoverNote = req.CoverNote?.Trim(),
            ResumeImageUrl = resumeUrl,
            JobTitle = post.JobTitle ?? post.Body,
            CompanyName = post.CompanyName,
            JobLocation = post.JobLocation,
            Status = JobApplicationStatus.Pending,
        };
        _db.JobApplications.Add(application);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost the race on the unique index — they already applied.
            throw new AppException("You've already applied to this job.", 409);
        }

        return ToDto(application);
    }

    public async Task<HasAppliedResult> HasAppliedAsync(Guid postId, CancellationToken ct)
    {
        var userId = _actor.Id;
        var applied = await _db.JobApplications
            .AnyAsync(a => a.PostId == postId && a.UserId == userId, ct);
        return new HasAppliedResult(applied);
    }

    /// The caller's applications, newest first.
    public async Task<ApplicationsPage> GetMyApplicationsAsync(string? cursor, int pageSize, CancellationToken ct)
    {
        var userId = _actor.Id;
        return await PageAsync(
            _db.JobApplications.AsNoTracking().Where(a => a.UserId == userId),
            cursor, pageSize, ct);
    }

    /// Withdraw (delete) the caller's application to a job.
    public async Task WithdrawAsync(Guid postId, CancellationToken ct)
    {
        var userId = _actor.Id;
        var application = await _db.JobApplications
            .FirstOrDefaultAsync(a => a.PostId == postId && a.UserId == userId, ct)
            ?? throw AppException.NotFound("You haven't applied to this job.");

        _db.JobApplications.Remove(application);
        await _db.SaveChangesAsync(ct);
    }

    /// Applicants for one of the merchant's vacancies. Merchant-only, and only
    /// for their own post.
    public async Task<ApplicationsPage> GetApplicantsAsync(Guid postId, string? cursor, int pageSize, CancellationToken ct)
    {
        var merchantId = RequireMerchant();

        var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId, ct)
            ?? throw AppException.NotFound("That job post doesn't exist.");
        if (post.MerchantId != merchantId)
            throw AppException.Forbidden("That's not your job post.");

        return await PageAsync(
            _db.JobApplications.AsNoTracking().Where(a => a.PostId == postId),
            cursor, pageSize, ct);
    }

    /// Every application across all of the merchant's vacancies, newest first —
    /// a single inbox of applicants.
    public async Task<ApplicationsPage> GetAllMyJobApplicantsAsync(string? cursor, int pageSize, CancellationToken ct)
    {
        var merchantId = RequireMerchant();
        return await PageAsync(
            _db.JobApplications.AsNoTracking().Where(a => a.MerchantId == merchantId),
            cursor, pageSize, ct);
    }

    /// Move an application through the pipeline. Only the merchant who owns the
    /// vacancy can change its status.
    public async Task<JobApplicationDto> UpdateStatusAsync(Guid applicationId, string status, CancellationToken ct)
    {
        var merchantId = RequireMerchant();

        var application = await _db.JobApplications.FirstOrDefaultAsync(a => a.Id == applicationId, ct)
            ?? throw AppException.NotFound("Application not found.");
        if (application.MerchantId != merchantId)
            throw AppException.Forbidden("That application isn't for one of your jobs.");

        application.Status = JobApplicationStatusMap.Parse(status);
        application.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ToDto(application);
    }

    // ── internals ────────────────────────────────────────────────────────────

    private Guid RequireMerchant()
    {
        if (!_actor.IsMerchant)
            throw AppException.Forbidden("Only merchants can manage job applicants.");
        return _actor.Id;
    }

    private async Task<ApplicationsPage> PageAsync(
        IQueryable<JobApplication> q, string? cursor, int pageSize, CancellationToken ct)
    {
        pageSize = pageSize is < 1 or > 50 ? 20 : pageSize;

        if (!string.IsNullOrWhiteSpace(cursor) && TryDecodeCursor(cursor, out var before))
            q = q.Where(a => a.AppliedAt < before);

        var rows = await q.OrderByDescending(a => a.AppliedAt).Take(pageSize + 1).ToListAsync(ct);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);

        var next = hasMore && rows.Count > 0 ? EncodeCursor(rows[^1].AppliedAt) : null;
        return new ApplicationsPage(rows.Select(ToDto).ToList(), next, hasMore);
    }

    private static JobApplicationDto ToDto(JobApplication a) => new(
        a.Id, a.PostId, a.UserId, a.MerchantId,
        a.ApplicantName, a.ApplicantPhone, a.ApplicantEmail, a.ApplicantPhoto,
        a.ResumeImageUrl, a.CoverNote,
        a.JobTitle, a.CompanyName, a.JobLocation,
        a.Status.ToWire(), a.AppliedAt);

    private static string EncodeCursor(DateTime t)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(t.ToString("O")));

    private static bool TryDecodeCursor(string cursor, out DateTime before)
    {
        before = default;
        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out before);
        }
        catch { return false; }
    }
}