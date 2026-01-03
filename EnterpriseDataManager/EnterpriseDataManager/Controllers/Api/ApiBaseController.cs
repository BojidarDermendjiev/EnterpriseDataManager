namespace EnterpriseDataManager.Controllers.Api;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Base controller for all API controllers with common functionality.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public abstract class ApiBaseController : ControllerBase
{
    /// <summary>
    /// Returns a successful response with the provided data.
    /// </summary>
    protected ActionResult<ApiResponse<T>> Success<T>(T data, string? message = null)
    {
        return Ok(new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        });
    }

    /// <summary>
    /// Returns a successful response with pagination information.
    /// </summary>
    protected ActionResult<PagedApiResponse<T>> Success<T>(IEnumerable<T> data, int total, int page, int pageSize, string? message = null)
    {
        return Ok(new PagedApiResponse<T>
        {
            Success = true,
            Data = data.ToList(),
            Message = message,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    /// <summary>
    /// Returns a created response with location header.
    /// </summary>
    protected ActionResult<ApiResponse<T>> Created<T>(T data, string actionName, object routeValues, string? message = null)
    {
        return CreatedAtAction(actionName, routeValues, new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message ?? "Resource created successfully"
        });
    }

    /// <summary>
    /// Returns a not found response.
    /// </summary>
    protected ActionResult<ApiResponse<T>> NotFoundResponse<T>(string? message = null)
    {
        return NotFound(new ApiResponse<T>
        {
            Success = false,
            Message = message ?? "Resource not found"
        });
    }

    /// <summary>
    /// Returns a bad request response with validation errors.
    /// </summary>
    protected ActionResult<ApiResponse<T>> BadRequestResponse<T>(string message, IDictionary<string, string[]>? errors = null)
    {
        return BadRequest(new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = errors
        });
    }

    /// <summary>
    /// Gets the current user's identifier from claims.
    /// </summary>
    protected string GetCurrentUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? "unknown";
    }

    /// <summary>
    /// Gets the current user's name from claims.
    /// </summary>
    protected string GetCurrentUserName()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? "unknown";
    }
}

/// <summary>
/// Standard API response wrapper.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
}

/// <summary>
/// Paginated API response wrapper.
/// </summary>
public class PagedApiResponse<T> : ApiResponse<List<T>>
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
