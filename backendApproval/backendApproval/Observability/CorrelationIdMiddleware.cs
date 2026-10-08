using System.Diagnostics;

namespace backendApproval.Observability;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string Header = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext ctx, CorrelationContext correlation)
    {
        var incoming = ctx.Request.Headers[Header].ToString();
        var correlationId = incoming.Length is > 0 and <= 64 ? incoming : Guid.NewGuid().ToString("N");

        correlation.Id = correlationId;
        ctx.Items[Header] = correlationId;

        ctx.Response.OnStarting(() =>
        {
            ctx.Response.Headers[Header] = correlationId;
            return Task.CompletedTask;
        });

        var sw = Stopwatch.StartNew();
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            try
            {
                await next(ctx);
            }
            finally
            {
                logger.LogInformation("HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs} ms",
                    ctx.Request.Method, ctx.Request.Path, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
            }
        }
    }
}
