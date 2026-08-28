namespace CustomerSupport.Application.Common.Models;

/// <summary>Base for list queries: paging, free-text search and sorting, validated centrally.</summary>
public abstract class PagedQuery
{
    private const int MaxPageSize = 200;

    public int Page { get; set; } = 1;

    private int _pageSize = 20;

    /// <summary>Clamped to a hard ceiling so a caller cannot ask for the whole table.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => value,
        };
    }

    public string? Search { get; set; }

    /// <summary>Property name to order by. Handlers map this against an allow-list.</summary>
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }

    public int Skip => (Page - 1) * PageSize;
}
