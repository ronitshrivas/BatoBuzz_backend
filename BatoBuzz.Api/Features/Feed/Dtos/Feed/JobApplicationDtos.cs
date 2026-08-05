using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.Feed.Dtos.Feed;

/// An application as either side reads it. Matches the app's UserAppliedJobModel.
public sealed record JobApplicationDto(
    Guid Id,
    Guid PostId,
    Guid UserId,
    Guid MerchantId,
    string ApplicantName,
    string ApplicantPhone,
    string ApplicantEmail,
    string? ApplicantPhoto,
    string? ResumeImageUrl,
    string? CoverNote,
    string JobTitle,
    string? CompanyName,
    string? JobLocation,
    string Status,
    DateTime AppliedAt);

/// Apply to a job. Contact details travel with the application so the merchant
/// can reach the applicant. Resume image is optional and sent via the multipart
/// variant of the endpoint.
public sealed class ApplyToJobRequest
{
    [Required, MaxLength(200)] public string ApplicantName { get; set; } = string.Empty;
    [Required, MaxLength(40)] public string ApplicantPhone { get; set; } = string.Empty;
    [MaxLength(200)] public string ApplicantEmail { get; set; } = string.Empty;
    [MaxLength(2000)] public string? CoverNote { get; set; }
    public IFormFile? Resume { get; set; }
}

public sealed record ApplicationsPage(
    IReadOnlyList<JobApplicationDto> Items,
    string? NextCursor,
    bool HasMore);

public sealed record UpdateApplicationStatusRequest([Required] string Status);

public sealed record HasAppliedResult(bool HasApplied);