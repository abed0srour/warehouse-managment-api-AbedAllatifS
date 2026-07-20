using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Warehouse.Presentation.Middleware
{
    public class RequestTimingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestTimingMiddleware> _logger;

        public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var startTime = DateTime.UtcNow;

            context.Response.OnStarting(() =>
            {
                var duration = DateTime.UtcNow - startTime;
                context.Response.Headers["X-Response-Time-ms"] = duration.TotalMilliseconds.ToString();
                return Task.CompletedTask;
            });

            await _next(context);

            var endTime = DateTime.UtcNow;
            var totalDuration = endTime - startTime;

            _logger.LogInformation("Request {Method} {Path} executed in {Duration} ms",
                context.Request.Method, context.Request.Path, totalDuration.TotalMilliseconds);
        }
    }
}