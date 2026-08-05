using BatoBuzz.Feed.Dtos;
using BatoBuzz.Feed.Dtos.Feed;
using BatoBuzz.Feed.Services;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Feed.Controllers;

/// Applying to job posts and managing applicants. Job vacancies themselves are
/// created through the normal merchant-posts endpoint (postType = "job"); this
/// controller is only the application layer on top.
[ApiController]
[Route("api/feed/jobs")]
[Authorize]
public sealed class JobApplicationsController : ControllerBase
{
    private readonly IJobApplicationService _jobs;
    public JobApplicationsController(IJobApplicationService jobs) => _jobs = jobs;

    // ── Applicant side (user) ──────────────────────────────────────────────

    /// Apply to a job post. Multipart so an optional resume image can ride along.
    [HttpPost("{postId:guid}/apply")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Apply(Guid postId, [FromForm] ApplyToJobRequest req, CancellationToken ct)
        => Ok(ApiResponse<JobApplicationDto>.Ok(await _jobs.ApplyAsync(postId, req, ct), "Application submitted."));

    /// Whether the caller has already applied to a job — for the apply button state.
    [HttpGet("{postId:guid}/applied")]
    public async Task<IActionResult> HasApplied(Guid postId, CancellationToken ct)
        => Ok(ApiResponse<HasAppliedResult>.Ok(await _jobs.HasAppliedAsync(postId, ct)));

    /// The caller's applications, newest first.
    [HttpGet("applications/mine")]
    public async Task<IActionResult> MyApplications([FromQuery] string? cursor,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<ApplicationsPage>.Ok(await _jobs.GetMyApplicationsAsync(cursor, pageSize, ct)));

    /// Withdraw the caller's application.
    [HttpDelete("{postId:guid}/apply")]
    public async Task<IActionResult> Withdraw(Guid postId, CancellationToken ct)
    {
        await _jobs.WithdrawAsync(postId, ct);
        return Ok(ApiResponse<object>.Ok(null, "Application withdrawn."));
    }

    // ── Merchant side ───────────────────────────────────────────────────────

    /// Applicants for one of the merchant's vacancies.
    [HttpGet("{postId:guid}/applicants")]
    public async Task<IActionResult> Applicants(Guid postId, [FromQuery] string? cursor,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<ApplicationsPage>.Ok(await _jobs.GetApplicantsAsync(postId, cursor, pageSize, ct)));

    /// Every applicant across all of the merchant's vacancies.
    [HttpGet("applicants/mine")]
    public async Task<IActionResult> AllApplicants([FromQuery] string? cursor,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<ApplicationsPage>.Ok(await _jobs.GetAllMyJobApplicantsAsync(cursor, pageSize, ct)));

    /// Move an application through the pipeline (reviewed/shortlisted/rejected/hired).
    [HttpPut("applications/{applicationId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid applicationId, UpdateApplicationStatusRequest req, CancellationToken ct)
        => Ok(ApiResponse<JobApplicationDto>.Ok(await _jobs.UpdateStatusAsync(applicationId, req.Status, ct)));
}