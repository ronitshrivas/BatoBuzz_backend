namespace BatoBuzz.Chat.Enums;

/// Message content type. Wire values match the Flutter model's `type` strings
/// exactly ("text", "image", "video", "file", "audio") so no client mapping.
public enum MessageType { Text = 0, Image = 1, Video = 2, File = 3, Audio = 4 }

public static class MessageTypeMap
{
    public static string ToWire(this MessageType t) => t switch
    {
        MessageType.Image => "image",
        MessageType.Video => "video",
        MessageType.File => "file",
        MessageType.Audio => "audio",
        _ => "text",
    };

    public static MessageType Parse(string? raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "image" => MessageType.Image,
        "video" => MessageType.Video,
        "file" or "document" => MessageType.File,
        "audio" => MessageType.Audio,
        _ => MessageType.Text,
    };
}