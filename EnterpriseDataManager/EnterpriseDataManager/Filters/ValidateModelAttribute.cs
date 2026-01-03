namespace EnterpriseDataManager.Filters;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

/// <summary>
/// Action filter attribute that validates the model state before the action executes.
/// Returns a 400 Bad Request with validation errors if the model is invalid.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class ValidateModelAttribute : ActionFilterAttribute
{
    /// <summary>
    /// Gets or sets whether to include detailed error messages in the response.
    /// Default is true.
    /// </summary>
    public bool IncludeDetails { get; set; } = true;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = new Dictionary<string, string[]>();

            foreach (var kvp in context.ModelState)
            {
                var errorMessages = kvp.Value.Errors
                    .Select(e => string.IsNullOrEmpty(e.ErrorMessage)
                        ? e.Exception?.Message ?? "Invalid value"
                        : e.ErrorMessage)
                    .ToArray();

                if (errorMessages.Length > 0)
                {
                    errors[kvp.Key] = errorMessages;
                }
            }

            var response = new ValidationProblemDetails(context.ModelState)
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest,
                Instance = context.HttpContext.Request.Path
            };

            if (!IncludeDetails)
            {
                response.Errors.Clear();
            }

            context.Result = new BadRequestObjectResult(response);
        }

        base.OnActionExecuting(context);
    }
}

/// <summary>
/// Action filter that ensures required parameters are not null.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ValidateNotNullAttribute : ActionFilterAttribute
{
    private readonly string[] _parameterNames;

    public ValidateNotNullAttribute(params string[] parameterNames)
    {
        _parameterNames = parameterNames;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        foreach (var paramName in _parameterNames)
        {
            if (context.ActionArguments.TryGetValue(paramName, out var value) && value == null)
            {
                context.ModelState.AddModelError(paramName, $"The {paramName} parameter is required.");
            }
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
        }

        base.OnActionExecuting(context);
    }
}

/// <summary>
/// Action filter that validates GUID parameters are not empty.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ValidateGuidAttribute : ActionFilterAttribute
{
    private readonly string[] _parameterNames;

    public ValidateGuidAttribute(params string[] parameterNames)
    {
        _parameterNames = parameterNames;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        foreach (var paramName in _parameterNames)
        {
            if (context.ActionArguments.TryGetValue(paramName, out var value))
            {
                if (value is Guid guid && guid == Guid.Empty)
                {
                    context.ModelState.AddModelError(paramName, $"The {paramName} parameter must be a valid non-empty GUID.");
                }
            }
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
        }

        base.OnActionExecuting(context);
    }
}
