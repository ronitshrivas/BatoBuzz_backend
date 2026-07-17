using System.Text.Json;
using BatoBuzz.Shared.Results;

namespace BatoBuzz.Feed.Middleware;

public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _log;

    /// MVC serializes responses as camelCase, but JsonSerializer defaults to
    /// PascalCase. Without this, a thrown AppException would come back as
    /// {"Success":false} while every successful response says {"success":true} —
    /// and the app's error parsing would silently miss the message.
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log)
        => (_next, _log) = (next, log);

    public async Task Invoke(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (AppException ex)
        {
            ctx.Response.StatusCode = ex.StatusCode;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(
                ApiResponse<object>.Fail(ex.Message), Json));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception");
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(
                ApiResponse<object>.Fail("Something went wrong. Please try again."), Json));
        }
    }
}