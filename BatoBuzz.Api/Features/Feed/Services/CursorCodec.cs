using System.Text;
using System.Text.Json;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.WebUtilities;

namespace BatoBuzz.Feed.Services;

/// Keyset-pagination cursor.
///
/// Firestore handed back a document snapshot; over HTTP we need something the
/// client can round-trip as a string. This encodes the sort key of the last row
/// on a page, so the next page is a `WHERE (sortKey, id) < (…)` seek rather
/// than an OFFSET — the cost stays flat no matter how deep the user scrolls,
/// and rows inserted meanwhile can't cause skips or duplicates.
public sealed record FeedCursor(DateTime CreatedAt, long Score, Guid Id);

public static class CursorCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Encode(FeedCursor cursor)
    {
        var json = JsonSerializer.Serialize(cursor, Options);
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
    }

    public static FeedCursor Decode(string value)
    {
        try
        {
            var json = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value));
            return JsonSerializer.Deserialize<FeedCursor>(json, Options)
                   ?? throw new AppException("Invalid cursor.");
        }
        catch (Exception ex) when (ex is not AppException)
        {
            throw new AppException("Invalid cursor.");
        }
    }
}
