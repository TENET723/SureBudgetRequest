using FluentAssertions;
using SureBudgetRequest.Infrastructure.Time;
using Xunit;

namespace SureBudgetRequest.Infrastructure.Tests.Time;

public class SystemDateTimeProviderTests
{
    private readonly SystemDateTimeProvider _provider = new();

    [Fact]
    public void BusinessUtcOffset_ShouldBeSixHoursAndThirtyMinutes()
    {
        _provider.BusinessUtcOffset.Should().Be(new TimeSpan(6, 30, 0));
        SystemDateTimeProvider.MyanmarOffset.Should().Be(new TimeSpan(6, 30, 0));
    }

    [Fact]
    public void UtcNow_ShouldBeCloseToCurrentUtcTime_AndHaveUtcKind()
    {
        var before = DateTime.UtcNow;
        var now = _provider.UtcNow;
        var after = DateTime.UtcNow;

        now.Kind.Should().Be(DateTimeKind.Utc);
        now.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void BusinessNow_ShouldReflectMyanmarOffset_AndHaveUnspecifiedKind()
    {
        var beforeUtc = DateTime.UtcNow;
        var businessNow = _provider.BusinessNow;
        var afterUtc = DateTime.UtcNow;

        businessNow.Kind.Should().Be(DateTimeKind.Unspecified);

        var expectedBefore = beforeUtc.Add(SystemDateTimeProvider.MyanmarOffset);
        var expectedAfter = afterUtc.Add(SystemDateTimeProvider.MyanmarOffset);

        businessNow.Should().BeOnOrAfter(expectedBefore).And.BeOnOrBefore(expectedAfter);
    }

    [Fact]
    public void BusinessNow_ShouldDifferFromUtcNow_ByBusinessUtcOffset()
    {
        var utc = _provider.UtcNow;
        var business = _provider.BusinessNow;

        var diff = business - utc;
        diff.TotalMinutes.Should().BeApproximately(390, 1); // 6.5 hours = 390 minutes
    }
}
