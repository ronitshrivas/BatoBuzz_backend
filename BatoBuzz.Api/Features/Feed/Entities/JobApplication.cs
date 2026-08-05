using BatoBuzz.Feed.Enums;
using BatoBuzz.Feed.Enums;

namespace BatoBuzz.Feed.Entities;

/// A user's application to a job post. One per (post, user): the app keys the
/// doc `{postId}_{userId}`, so a unique index on (PostId, UserId) enforces that
/// a user applies to a given vacancy once.
///
/// Applicant contact details and job info are denormalized onto the row (as the
/// app does) so the merchant's applicant list and the user's "my applications"
/// screen render without extra lookups into Identity or the post.
public class JobApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PostId { get; set; }          // the job vacancy (a Post)
    public Post? Post { get; set; }

    public Guid UserId { get; set; }          // applicant
    public Guid MerchantId { get; set; }      // vacancy owner

    // Applicant snapshot
    public string ApplicantName { get; set; } = string.Empty;
    public string ApplicantPhone { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public string? ApplicantPhoto { get; set; }
    public string? ResumeImageUrl { get; set; }
    public string? CoverNote { get; set; }

    // Job snapshot (denormalized from the post at apply time)
    public string JobTitle { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? JobLocation { get; set; }

    public JobApplicationStatus Status { get; set; } = JobApplicationStatus.Pending;

    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}