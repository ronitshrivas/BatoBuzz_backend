namespace BatoBuzz.Shared.Results;

/// Thrown for expected, user-facing failures. Caught by ExceptionMiddleware
/// and mapped to the given status code.
public sealed class AppException : Exception
{
    public int StatusCode { get; }

    public AppException(string message, int statusCode = 400) : base(message)
        => StatusCode = statusCode;

    public static AppException NotFound(string msg) => new(msg, 404);
    public static AppException Unauthorized(string msg) => new(msg, 401);
    public static AppException Forbidden(string msg) => new(msg, 403);
    public static AppException Conflict(string msg) => new(msg, 409);
}