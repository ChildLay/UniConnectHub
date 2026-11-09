namespace UniConnect.Application.Common;

public sealed record PagedRequest(int PageNumber = 1, int PageSize = 20)
{
    public int SafePageNumber => Math.Max(PageNumber, 1);
    public int SafePageSize => Math.Clamp(PageSize, 1, 100);
    public int Skip => (SafePageNumber - 1) * SafePageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, long TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
