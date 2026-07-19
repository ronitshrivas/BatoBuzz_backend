using System.Text.Json;
using BatoBuzz.Shared.Results;

namespace BatoBuzz.Api.Middleware;

/// Single exception handler for the whole monolith. Maps AppException to its
/// status code and returns the ApiResponse envelope in camelCase (matching MVC,
/// so the apps parse success and error responses with one shape).
public sealed class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _log;

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
