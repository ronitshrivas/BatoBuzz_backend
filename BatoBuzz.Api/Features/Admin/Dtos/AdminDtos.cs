using System.ComponentModel.DataAnnotations;

namespace BatoBuzz.Admin.Dtos;

// ── Dashboard ────────────────────────────────────────────────────────────────
public sealed record DashboardStatsDto(
    int TotalUsers, int SuspendedUsers,
    int TotalMerchants, int PendingMerchants, int ApprovedMerchants,
    int TotalPosts, int TotalReels, int TotalJobs,
    int TotalComments, int OpenReports,
    int ActiveAwardParticipants);

// ── Users ────────────────────────────────────────────────────────────────────
public sealed record AdminUserDto(
    Guid Id, string DisplayName, string Email, string? Phone, string? PhotoUrl,
    bool IsSuspended, bool IsDeleted, string? AdminNote, int TotalPoints, DateTime CreatedAt);

public sealed record AdminUserListPage(IReadOnlyList<AdminUserDto> Items, string? NextCursor, bool HasMore);

// ── Merchants ────────────────────────────────────────────────────────────────
public sealed record AdminMerchantDto(
    Guid Id, string BusinessName, string Phone, string? OwnerPhotoUrl,
    string Status, bool IsSuspended, bool IsDeleted, string? AdminNote, DateTime CreatedAt);

public sealed record AdminMerchantListPage(IReadOnlyList<AdminMerchantDto> Items, string? NextCursor, bool HasMore);

// ── Content ──────────────────────────────────────────────────────────────────
public sealed record AdminPostDto(
    Guid Id, Guid MerchantId, string Body, string PostType, bool IsDeleted, DateTime CreatedAt);

public sealed record AdminReportDto(
    Guid Id, Guid PostId, Guid ReporterId, string Reason, DateTime CreatedAt);

public sealed record AdminListPage<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);

// ── Requests ─────────────────────────────────────────────────────────────────
public sealed record SuspendRequest(bool Suspend, string? Note);
public sealed record AdminNoteRequest(string? Note);
public sealed record ReviewMerchantRequest([Required] bool Approve, string? Note);
public sealed record AdjustPointsRequest([Required] int Delta, [Required] string Reason);
public sealed record BroadcastRequest(
    [Required] string Audience,           // "users" | "merchants" | "all"
    [Required] string Title,
    [Required] string Body);
public sealed record BroadcastResult(int Recipients);

// ── Points ───────────────────────────────────────────────────────────────────
public sealed record AdminPointsDto(Guid UserId, int TotalPoints, DateTime UpdatedAt);

// ── Audit ────────────────────────────────────────────────────────────────────
public sealed record AuditEntryDto(
    Guid Id, Guid ActorId, string ActorName, string Action,
    string TargetType, string? TargetId, string? Detail, DateTime CreatedAt);