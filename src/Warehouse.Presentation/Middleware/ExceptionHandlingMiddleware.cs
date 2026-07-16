using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Warehouse.Application.Common.Exceptions;
using Warehouse.Presentation.Contracts;

namespace Warehouse.Presentation.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while processing {Method} {Path}",
                    context.Request.Method, context.Request.Path);

                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var (statusCode, errorCode, message) = ex switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, "NotFound", ex.Message),
                BusinessRuleException => (StatusCodes.Status409Conflict, "Conflict", ex.Message),
                _ => (StatusCodes.Status500InternalServerError, "ServerError", "An unexpected error occurred.")
            };

            var response = new ApiErrorResponse
            {
                ErrorCode = errorCode,
                Message = message,
                TraceId = context.Items.TryGetValue("CorrelationId", out var cid) ? cid?.ToString() ?? context.TraceIdentifier : context.TraceIdentifier
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            return context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}