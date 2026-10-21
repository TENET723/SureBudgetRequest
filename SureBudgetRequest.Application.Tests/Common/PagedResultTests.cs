using FluentAssertions;
using SureBudgetRequest.Application.Common;
using Xunit;

namespace SureBudgetRequest.Application.Tests.Common;

public class PagedResultTests
{
    [Fact]
    public void Properties_ShouldBeAssignedCorrectly()
    {
        var items = new List<string> { "item1", "item2" };
        var result = new PagedResult<string>(items, 50, 2, 10);

        result.Items.Should().BeEquivalentTo(items);
        result.TotalCount.Should().Be(50);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
    }

    [Theory]
    [InlineData(0, 10, 0)]      // TotalCount 0 -> 0 pages
    [InlineData(1, 10, 1)]      // 1 item, page size 10 -> 1 page
    [InlineData(10, 10, 1)]     // 10 items, page size 10 -> 1 page
    [InlineData(11, 10, 2)]     // 11 items, page size 10 -> 2 pages
    [InlineData(20, 10, 2)]     // 20 items, page size 10 -> 2 pages
    [InlineData(25, 10, 3)]     // 25 items, page size 10 -> 3 pages
    [InlineData(100, 25, 4)]    // 100 items, page size 25 -> 4 pages
    [InlineData(50, 0, 0)]      // PageSize 0 -> 0 pages (prevents division by zero)
    [InlineData(50, -5, 0)]     // PageSize negative -> 0 pages
    public void TotalPages_ShouldCalculateCorrectly(int totalCount, int pageSize, int expectedTotalPages)
    {
        var result = new PagedResult<int>(Array.Empty<int>(), totalCount, 1, pageSize);

        result.TotalPages.Should().Be(expectedTotalPages);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    public void HasPrev_ShouldBeTrue_OnlyWhenPageIsGreaterThanOne(int page, bool expectedHasPrev)
    {
        var result = new PagedResult<string>(Array.Empty<string>(), 50, page, 10);

        result.HasPrev.Should().Be(expectedHasPrev);
    }

    [Theory]
    [InlineData(1, 30, 10, true)]   // Page 1 of 3 -> has next
    [InlineData(2, 30, 10, true)]   // Page 2 of 3 -> has next
    [InlineData(3, 30, 10, false)]  // Page 3 of 3 -> no next
    [InlineData(4, 30, 10, false)]  // Page 4 of 3 -> no next
    [InlineData(1, 0, 10, false)]   // Page 1 of 0 -> no next
    [InlineData(1, 50, 0, false)]   // PageSize 0 (TotalPages 0) -> no next
    public void HasNext_ShouldBeTrue_OnlyWhenPageIsLessThanTotalPages(
        int page,
        int totalCount,
        int pageSize,
        bool expectedHasNext)
    {
        var result = new PagedResult<string>(Array.Empty<string>(), totalCount, page, pageSize);

        result.HasNext.Should().Be(expectedHasNext);
    }
}
