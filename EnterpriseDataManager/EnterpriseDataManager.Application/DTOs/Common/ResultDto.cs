namespace EnterpriseDataManager.Application.DTOs.Common;

public sealed record ResultDto
{
    public bool IsSuccess { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static ResultDto Success(string? message = null) =>
        new() { IsSuccess = true, Message = message };

    public static ResultDto Failure(string error) =>
        new() { IsSuccess = false, Errors = [error] };

    public static ResultDto Failure(IEnumerable<string> errors) =>
        new() { IsSuccess = false, Errors = errors.ToList() };
}

public sealed record ResultDto<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static ResultDto<T> Success(T data, string? message = null) =>
        new() { IsSuccess = true, Data = data, Message = message };

    public static ResultDto<T> Failure(string error) =>
        new() { IsSuccess = false, Errors = [error] };

    public static ResultDto<T> Failure(IEnumerable<string> errors) =>
        new() { IsSuccess = false, Errors = errors.ToList() };
}

public sealed record PagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public static PagedResultDto<T> Create(IEnumerable<T> items, int totalCount, int pageNumber, int pageSize) =>
        new()
        {
            Items = items.ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
}
