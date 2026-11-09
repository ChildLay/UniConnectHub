using UniConnect.Application.Common;

namespace UniConnect.Application.Tests;

public sealed class PaginationTests
{
    [Fact]
    public void Page_size_is_bounded_and_page_is_positive()
    {
        var request = new PagedRequest(0, 500);
        Assert.Equal(1, request.SafePageNumber);
        Assert.Equal(100, request.SafePageSize);
        Assert.Equal(0, request.Skip);
    }
}
