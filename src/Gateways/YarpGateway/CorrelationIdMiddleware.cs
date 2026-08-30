using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace YarpGateway;

/// <summary>
/// Middleware responsible for handling distributed tracing via Correlation ID.
/// Implements IMiddleware to support clean Dependency Injection (DI) lifecycle.
/// </summary>
public class CorrelationIdMiddleware : IMiddleware
{
    private const string CorrelationIdHeaderKey = "X-Correlation-ID";

    /// <summary>
    /// Executes the middleware logic to enrich log context and HTTP headers with a unique tracking identifier.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="next">The next delegate in the HTTP request pipeline.</param>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Check if the incoming request already contains a Correlation ID header from the client
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeaderKey, out StringValues correlationId))
        {
            // If no identifier is provided, generate a new unique GUID as a fallback
            correlationId = Guid.NewGuid().ToString();
        }

        // Add or overwrite the Correlation ID header in the HTTP response to assist client-side debugging
        context.Response.Headers[CorrelationIdHeaderKey] = correlationId;

        // Push the Correlation ID into Serilog's logical LogContext.
        // This ensures every log statement written during this specific asynchronous request will include this ID.
        using (LogContext.PushProperty("CorrelationId", correlationId.ToString()))
        {
            // Call the next middleware component in the processing pipeline
            await next(context);
        }
    }
}
