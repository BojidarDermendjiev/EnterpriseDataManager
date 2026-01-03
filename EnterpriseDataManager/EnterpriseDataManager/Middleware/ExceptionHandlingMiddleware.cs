namespace EnterpriseDataManager.Middleware;

using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

/// <summary>
/// Middleware that handles exceptions globally and returns consistent error responses.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private readonly ExceptionHandlingOptions _options;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment,
        ExceptionHandlingOptions? options = null)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
        _options = options ?? new ExceptionHandlingOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Response.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        // Log the exception
        LogException(exception, correlationId);

        // Determine the response based on exception type
        var (statusCode, errorResponse) = MapExceptionToResponse(exception, correlationId);

        // Set response properties
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        // Write the response
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, jsonOptions));
    }

    private void LogException(Exception exception, string correlationId)
    {
        var logLevel = exception switch
        {
            ValidationException => LogLevel.Warning,
            NotFoundException => LogLevel.Warning,
            UnauthorizedAccessException => LogLevel.Warning,
            ConflictException => LogLevel.Warning,
            _ => LogLevel.Error
        };

        _logger.Log(
            logLevel,
            exception,
            "Unhandled exception occurred. CorrelationId: {CorrelationId}, Type: {ExceptionType}, Message: {Message}",
            correlationId,
            exception.GetType().Name,
            exception.Message);
    }

    private (HttpStatusCode StatusCode, ProblemDetails Response) MapExceptionToResponse(
        Exception exception,
        string correlationId)
    {
        var statusCode = exception switch
        {
            ValidationException => HttpStatusCode.BadRequest,
            NotFoundException => HttpStatusCode.NotFound,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            ForbiddenException => HttpStatusCode.Forbidden,
            ConflictException => HttpStatusCode.Conflict,
            BusinessRuleException => HttpStatusCode.UnprocessableEntity,
            OperationCanceledException => HttpStatusCode.BadRequest,
            TimeoutException => HttpStatusCode.GatewayTimeout,
            _ => HttpStatusCode.InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = GetErrorTitle(statusCode),
            Type = GetErrorTypeUri(statusCode),
            Instance = correlationId
        };

        // Add detail message based on environment and exception type
        if (_environment.IsDevelopment() || _options.IncludeExceptionDetails || IsClientError(statusCode))
        {
            problemDetails.Detail = exception.Message;
        }
        else
        {
            problemDetails.Detail = "An unexpected error occurred. Please try again later.";
        }

        // Add extensions
        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow;

        if (_environment.IsDevelopment() && _options.IncludeStackTrace)
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
            problemDetails.Extensions["exceptionType"] = exception.GetType().FullName;

            if (exception.InnerException != null)
            {
                problemDetails.Extensions["innerException"] = new
                {
                    message = exception.InnerException.Message,
                    type = exception.InnerException.GetType().FullName
                };
            }
        }

        // Add validation errors if applicable
        if (exception is ValidationException validationEx && validationEx.Errors.Any())
        {
            problemDetails.Extensions["errors"] = validationEx.Errors;
        }

        return (statusCode, problemDetails);
    }

    private static bool IsClientError(HttpStatusCode statusCode)
    {
        return (int)statusCode >= 400 && (int)statusCode < 500;
    }

    private static string GetErrorTitle(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "Bad Request",
            HttpStatusCode.Unauthorized => "Unauthorized",
            HttpStatusCode.Forbidden => "Forbidden",
            HttpStatusCode.NotFound => "Not Found",
            HttpStatusCode.Conflict => "Conflict",
            HttpStatusCode.UnprocessableEntity => "Unprocessable Entity",
            HttpStatusCode.GatewayTimeout => "Gateway Timeout",
            HttpStatusCode.InternalServerError => "Internal Server Error",
            _ => "Error"
        };
    }

    private static string GetErrorTypeUri(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            HttpStatusCode.Unauthorized => "https://tools.ietf.org/html/rfc7235#section-3.1",
            HttpStatusCode.Forbidden => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            HttpStatusCode.NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            HttpStatusCode.Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            HttpStatusCode.UnprocessableEntity => "https://tools.ietf.org/html/rfc4918#section-11.2",
            HttpStatusCode.GatewayTimeout => "https://tools.ietf.org/html/rfc7231#section-6.6.5",
            HttpStatusCode.InternalServerError => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };
    }
}

/// <summary>
/// Options for configuring exception handling middleware.
/// </summary>
public class ExceptionHandlingOptions
{
    public bool IncludeExceptionDetails { get; set; } = false;
    public bool IncludeStackTrace { get; set; } = true;
    public bool LogAllExceptions { get; set; } = true;
}

/// <summary>
/// Extension methods for adding exception handling middleware.
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }

    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder, ExceptionHandlingOptions options)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>(options);
    }

    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder, Action<ExceptionHandlingOptions> configure)
    {
        var options = new ExceptionHandlingOptions();
        configure(options);
        return builder.UseMiddleware<ExceptionHandlingMiddleware>(options);
    }
}

#region Custom Exceptions

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException() : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors) : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string propertyName, string errorMessage) : base(errorMessage)
    {
        Errors = new Dictionary<string, string[]>
        {
            { propertyName, new[] { errorMessage } }
        };
    }
}

/// <summary>
/// Exception thrown when a requested resource is not found.
/// </summary>
public class NotFoundException : Exception
{
    public string? ResourceType { get; }
    public string? ResourceId { get; }

    public NotFoundException() : base("The requested resource was not found.")
    {
    }

    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string resourceType, object resourceId)
        : base($"{resourceType} with id '{resourceId}' was not found.")
    {
        ResourceType = resourceType;
        ResourceId = resourceId?.ToString();
    }
}

/// <summary>
/// Exception thrown when access to a resource is forbidden.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException() : base("Access to this resource is forbidden.")
    {
    }

    public ForbiddenException(string message) : base(message)
    {
    }
}

/// <summary>
/// Exception thrown when a conflict occurs (e.g., duplicate resource).
/// </summary>
public class ConflictException : Exception
{
    public ConflictException() : base("A conflict occurred with the current state of the resource.")
    {
    }

    public ConflictException(string message) : base(message)
    {
    }
}

/// <summary>
/// Exception thrown when a business rule is violated.
/// </summary>
public class BusinessRuleException : Exception
{
    public string? RuleCode { get; }

    public BusinessRuleException() : base("A business rule was violated.")
    {
    }

    public BusinessRuleException(string message) : base(message)
    {
    }

    public BusinessRuleException(string ruleCode, string message) : base(message)
    {
        RuleCode = ruleCode;
    }
}

#endregion
