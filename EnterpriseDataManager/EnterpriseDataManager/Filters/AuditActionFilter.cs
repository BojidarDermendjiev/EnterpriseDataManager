namespace EnterpriseDataManager.Filters;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using EnterpriseDataManager.Core.Interfaces.Services;
using System.Security.Claims;
using System.Text.Json;

/// <summary>
/// Action filter that automatically logs audit records for controller actions and Razor Pages handlers.
/// Respects SkipAuditAttribute and AuditActionAttribute.
/// </summary>
    
public class AuditActionFilter : IAsyncActionFilter
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditActionFilter> _logger;

    public AuditActionFilter(IAuditService auditService, ILogger<AuditActionFilter> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Skip audit if attribute present at class or method level
        if (ShouldSkipAudit(context))
        {
            await next();
            return;
        }

        var (resolvedActionName, explicitResourceType) = ResolveActionName(context);
        var actor = GetActor(context.HttpContext);
        var ipAddress = GetClientIpAddress(context.HttpContext);
        var userAgent = context.HttpContext.Request.Headers.UserAgent.ToString();
        var correlationId = GetCorrelationId(context.HttpContext);

        var resourceInfo = ExtractResourceInfo(context, explicitResourceType);

        var startTime = DateTimeOffset.UtcNow;
        ActionExecutedContext? executedContext = null;
        Exception? exception = null;

        try
        {
            executedContext = await next();
            exception = executedContext.Exception;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            var success = exception == null && (executedContext?.Result is not ObjectResult objResult || (objResult.StatusCode ?? 200) < 400);
            var duration = DateTimeOffset.UtcNow - startTime;

            try
            {
                var details = BuildAuditDetails(context, executedContext, duration, exception, ipAddress, userAgent, correlationId);

                if (!string.IsNullOrEmpty(resourceInfo.ResourceType) && !string.IsNullOrEmpty(resourceInfo.ResourceId))
                {
                    await _auditService.LogWithResourceAsync(
                        actor,
                        resolvedActionName,
                        resourceInfo.ResourceType!,
                        resourceInfo.ResourceId!,
                        success);
                }
                else
                {
                    await _auditService.LogWithDetailsAsync(actor, resolvedActionName, details, success);
                }
            }
            catch (Exception auditEx)
            {
                _logger.LogWarning(auditEx, "Failed to log audit record for action {Action}", resolvedActionName);
            }
        }
    }

    private static bool ShouldSkipAudit(ActionExecutingContext context)
    {
        var actionHasSkip = context.ActionDescriptor.FilterDescriptors
            .Any(fd => fd.Filter is SkipAuditAttribute);
        var controllerHasSkip = context.Controller.GetType()
            .GetCustomAttributes(typeof(SkipAuditAttribute), inherit: true)
            .Any();
        return actionHasSkip || controllerHasSkip;
    }

    private static (string ActionName, string? ExplicitResourceType) ResolveActionName(ActionExecutingContext context)
    {
        // Prefer AuditActionAttribute on method
        var auditAttr = context.ActionDescriptor.FilterDescriptors
            .Select(fd => fd.Filter)
            .OfType<AuditActionAttribute>()
            .FirstOrDefault();

        string? resourceTypeFromAttr = auditAttr?.ResourceType;

        if (!string.IsNullOrWhiteSpace(auditAttr?.ActionName))
        {
            return (auditAttr!.ActionName, resourceTypeFromAttr);
        }

        // Derive action name based on MVC controller or Razor Page
        var controllerName = context.RouteData.Values.TryGetValue("controller", out var controllerObj)
            ? controllerObj?.ToString()
            : null;

        var actionDisplay = context.ActionDescriptor.DisplayName ?? "UnknownAction";

        // Razor Pages: route has "page" and optionally "handler"
        var pageName = context.RouteData.Values.TryGetValue("page", out var pageObj)
            ? pageObj?.ToString()
            : null;
        var handlerName = context.RouteData.Values.TryGetValue("handler", out var handlerObj)
            ? handlerObj?.ToString()
            : null;

        string actionName;
        if (!string.IsNullOrEmpty(pageName))
        {
            // Normalize: Pages/<Area>/Path -> Page: <pageName> Handler: <handlerName>
            actionName = string.IsNullOrEmpty(handlerName)
                ? $"Page:{pageName}"
                : $"Page:{pageName} Handler:{handlerName}";
        }
        else if (!string.IsNullOrEmpty(controllerName))
        {
            actionName = $"{controllerName}.{actionDisplay}";
        }
        else
        {
            actionName = actionDisplay;
        }

        return (actionName, resourceTypeFromAttr);
    }

    private static string GetActor(HttpContext context)
    {
        var user = context.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            return user.FindFirstValue(ClaimTypes.Name)
                ?? user.FindFirstValue(ClaimTypes.Email)
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? "authenticated-user";
        }

        return "anonymous";
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').First().Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string? GetCorrelationId(HttpContext context)
    {
        // Common correlation id headers
        var candidate = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? context.Request.Headers["X-Request-ID"].FirstOrDefault()
            ?? context.TraceIdentifier;

        return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
    }

    private static (string? ResourceType, string? ResourceId) ExtractResourceInfo(ActionExecutingContext context, string? explicitResourceType)
    {
        string? resourceType = explicitResourceType;
        string? resourceId = null;

        // Razor Pages: use page route segment as resource type if not explicitly provided
        if (string.IsNullOrEmpty(resourceType) &&
            context.RouteData.Values.TryGetValue("page", out var pageValue) &&
            pageValue is string pageStr && !string.IsNullOrWhiteSpace(pageStr))
        {
            // e.g., /Plans/Edit -> Plans
            resourceType = pageStr.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }

        // Controllers: fall back to controller name without 'Controller'
        if (string.IsNullOrEmpty(resourceType) &&
            context.RouteData.Values.TryGetValue("controller", out var controller))
        {
            resourceType = controller?.ToString()?.Replace("Controller", "");
        }

        // Try common id argument names
        if (context.ActionArguments.TryGetValue("id", out var id))
        {
            resourceId = id?.ToString();
        }
        else if (context.ActionArguments.TryGetValue("planId", out var planId))
        {
            resourceId = planId?.ToString();
        }
        else if (context.ActionArguments.TryGetValue("jobId", out var jobId))
        {
            resourceId = jobId?.ToString();
        }
        else if (context.ActionArguments.TryGetValue("providerId", out var providerId))
        {
            resourceId = providerId?.ToString();
        }
        else if (context.ActionArguments.TryGetValue("policyId", out var policyId))
        {
            resourceId = policyId?.ToString();
        }
        else if (context.RouteData.Values.TryGetValue("id", out var idRoute))
        {
            resourceId = idRoute?.ToString();
        }

        return (resourceType, resourceId);
    }

    private static string BuildAuditDetails(
        ActionExecutingContext context,
        ActionExecutedContext? executedContext,
        TimeSpan duration,
        Exception? exception,
        string? ipAddress,
        string? userAgent,
        string? correlationId)
    {
        var details = new
        {
            Method = context.HttpContext.Request.Method,
            Path = context.HttpContext.Request.Path.Value,
            QueryString = context.HttpContext.Request.QueryString.Value,
            DurationMs = duration.TotalMilliseconds,
            StatusCode = (executedContext?.Result as ObjectResult)?.StatusCode
                ?? (executedContext?.Result as StatusCodeResult)?.StatusCode
                ?? (exception != null ? 500 : 200),
            Error = exception?.Message,
            Client = new
            {
                IpAddress = ipAddress,
                UserAgent = userAgent
            },
            CorrelationId = correlationId
        };

        return JsonSerializer.Serialize(details);
    }
}

/// <summary>
/// Attribute to specify custom audit action name.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class AuditActionAttribute : Attribute
{
    public string ActionName { get; }
    public string? ResourceType { get; set; }
    public bool SkipAudit { get; set; }

    public AuditActionAttribute(string actionName)
    {
        ActionName = actionName;
    }
}

/// <summary>
/// Attribute to skip audit logging for specific actions.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class SkipAuditAttribute : Attribute
{
}
