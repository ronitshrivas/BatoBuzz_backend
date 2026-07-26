namespace BatoBuzz.Points.Enums;

/// Actions that earn points. Wire values match the app's strings exactly
/// ("like", "comment", "share", "qr_scan") so the client needs no mapping.
public enum PointAction { Like = 0, Comment = 1, Share = 2, QrScan = 3 }

public static class PointValues
{
    public const int Like = 10;
    public const int Comment = 15;
    public const int Share = 20;
    public const int QrScan = 50;

    public static int ForAction(PointAction a) => a switch
    {
        PointAction.Like => Like,
        PointAction.Comment => Comment,
        PointAction.Share => Share,
        PointAction.QrScan => QrScan,
        _ => 0,
    };

    public static string ToWire(this PointAction a) => a switch
    {
        PointAction.Comment => "comment",
        PointAction.Share => "share",
        PointAction.QrScan => "qr_scan",
        _ => "like",
    };

    public static PointAction? Parse(string? raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "like" => PointAction.Like,
        "comment" => PointAction.Comment,
        "share" => PointAction.Share,
        "qr_scan" => PointAction.QrScan,
        _ => null,
    };
}