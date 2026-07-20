using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Warehouse.Presentation.Filters
{
    public class ActionLoggingFilter : IActionFilter
    {
        private readonly ILogger<ActionLoggingFilter> _logger;

        public ActionLoggingFilter(ILogger<ActionLoggingFilter> logger)
        {
            _logger = logger;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var actionName = context.ActionDescriptor.DisplayName;
            var parameters = context.ActionArguments.Select(kv => $"{kv.Key}: {kv.Value}").ToArray();
            var parameterString = string.Join(", ", parameters);

            _logger.LogInformation("Executing action {ActionName} with parameters: {Parameters}", actionName, parameterString);
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            var actionName = context.ActionDescriptor.DisplayName;
            var resultType = context.Result?.GetType().Name ?? "No result";

            _logger.LogInformation("Executed action {ActionName} with result type: {ResultType}", actionName, resultType);
        }
    }
}