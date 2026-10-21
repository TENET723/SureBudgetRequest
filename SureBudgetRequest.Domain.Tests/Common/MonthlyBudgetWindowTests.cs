using FluentAssertions;
using SureBudgetRequest.Domain.Common;
using Xunit;

namespace SureBudgetRequest.Domain.Tests.Common;

public class MonthlyBudgetWindowTests
{
    private static readonly TimeSpan MyanmarOffset = new(6, 30, 0);

    [Fact]
    public void CurrentUtc_StandardMidMonth_WithMyanmarOffset_ShouldReturnExactUtcWindow()
    {
        // Arrange: 2026-06-15 14:30:00 in Myanmar (+06:30)
        var businessNow = new DateTime(2026, 6, 15, 14, 30, 0, DateTimeKind.Unspecified);

        // Act
        var (startUtc, endUtc) = MonthlyBudgetWindow.CurrentUtc(businessNow, MyanmarOffset);

        // Assert:
        // Local start: 2026-06-01 00:00:00 -> minus 06:30 = 2026-05-31 17:30:00 UTC
        // Local end:   2026-07-01 00:00:00 -> minus 06:30 = 2026-06-30 17:30:00 UTC
        startUtc.Should().Be(new DateTime(2026, 5, 31, 17, 30, 0, DateTimeKind.Utc));
        endUtc.Should().Be(new DateTime(2026, 6, 30, 17, 30, 0, DateTimeKind.Utc));
        startUtc.Kind.Should().Be(DateTimeKind.Utc);
        endUtc.Kind.Should().Be(DateTimeKind.Utc);
        (endUtc - startUtc).Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void CurrentUtc_MonthStartExtreme_ShouldProduceSameWindowAsMidMonth()
    {
        // Arrange: Exact first millisecond of the month
        var startOfMonth = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var midOfMonth = new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Unspecified);

        // Act
        var windowFromStart = MonthlyBudgetWindow.CurrentUtc(startOfMonth, MyanmarOffset);
        var windowFromMid = MonthlyBudgetWindow.CurrentUtc(midOfMonth, MyanmarOffset);

        // Assert
        windowFromStart.StartUtc.Should().Be(windowFromMid.StartUtc);
        windowFromStart.EndUtc.Should().Be(windowFromMid.EndUtc);
    }

    [Fact]
    public void CurrentUtc_MonthEndExtreme_ShouldProduceSameWindowAsStartOfMonth()
    {
        // Arrange: Final millisecond before next month rollover
        var endOfMonth = new DateTime(2026, 8, 31, 23, 59, 59, 999, DateTimeKind.Unspecified);
        var startOfMonth = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);

        // Act
        var windowFromEnd = MonthlyBudgetWindow.CurrentUtc(endOfMonth, MyanmarOffset);
        var windowFromStart = MonthlyBudgetWindow.CurrentUtc(startOfMonth, MyanmarOffset);

        // Assert
        windowFromEnd.StartUtc.Should().Be(windowFromStart.StartUtc);
        windowFromEnd.EndUtc.Should().Be(windowFromStart.EndUtc);
    }

    [Fact]
    public void CurrentUtc_YearRollover_DecemberToJanuary_ShouldSpanAcrossYears()
    {
        // Arrange: 2026-12-31 in Myanmar
        var newYearsEve = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Unspecified);

        // Act
        var (startUtc, endUtc) = MonthlyBudgetWindow.CurrentUtc(newYearsEve, MyanmarOffset);

        // Assert:
        // Local start: 2026-12-01 00:00:00 -> UTC: 2026-11-30 17:30:00
        // Local end:   2027-01-01 00:00:00 -> UTC: 2026-12-31 17:30:00
        startUtc.Should().Be(new DateTime(2026, 11, 30, 17, 30, 0, DateTimeKind.Utc));
        endUtc.Should().Be(new DateTime(2026, 12, 31, 17, 30, 0, DateTimeKind.Utc));
        (endUtc - startUtc).Should().Be(TimeSpan.FromDays(31));
    }

    [Fact]
    public void CurrentUtc_YearStart_January_ShouldStartInPreviousYearUtc_ForPositiveOffset()
    {
        // Arrange: 2026-01-01 in Myanmar
        var newYearDay = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        // Act
        var (startUtc, endUtc) = MonthlyBudgetWindow.CurrentUtc(newYearDay, MyanmarOffset);

        // Assert:
        // Local start: 2026-01-01 00:00:00 -> UTC: 2025-12-31 17:30:00 (Previous year in UTC)
        // Local end:   2026-02-01 00:00:00 -> UTC: 2026-01-31 17:30:00
        startUtc.Should().Be(new DateTime(2025, 12, 31, 17, 30, 0, DateTimeKind.Utc));
        endUtc.Should().Be(new DateTime(2026, 1, 31, 17, 30, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(2024, 29)] // Leap year
    [InlineData(2025, 28)] // Non-leap year
    [InlineData(2026, 28)] // Non-leap year
    [InlineData(2028, 29)] // Leap year
    [InlineData(2000, 29)] // Century leap year
    [InlineData(2100, 28)] // Century non-leap year
    public void CurrentUtc_February_ShouldReflectLeapAndNonLeapDaysCorrectly(int year, int expectedDays)
    {
        // Arrange
        var februaryDate = new DateTime(year, 2, 14, 10, 0, 0, DateTimeKind.Unspecified);

        // Act
        var (startUtc, endUtc) = MonthlyBudgetWindow.CurrentUtc(februaryDate, MyanmarOffset);

        // Assert
        (endUtc - startUtc).Should().Be(TimeSpan.FromDays(expectedDays));
    }

    [Fact]
    public void CurrentUtc_LeapDayItself_February29_ShouldCalculateCorrectWindow()
    {
        // Arrange: 2024-02-29 23:59:59
        var leapDay = new DateTime(2024, 2, 29, 23, 59, 59, DateTimeKind.Unspecified);

        // Act
        var (startUtc, endUtc) = MonthlyBudgetWindow.CurrentUtc(leapDay, MyanmarOffset);

        // Assert:
        // Local start: 2024-02-01 00:00:00 -> UTC: 2024-01-31 17:30:00
        // Local end:   2024-03-01 00:00:00 -> UTC: 2024-02-29 17:30:00
        startUtc.Should().Be(new DateTime(2024, 1, 31, 17, 30, 0, DateTimeKind.Utc));
        endUtc.Should().Be(new DateTime(2024, 2, 29, 17, 30, 0, DateTimeKind.Utc));
        (endUtc - startUtc).Should().Be(TimeSpan.FromDays(29));
    }

    [Theory]
    [InlineData(1, 31)]
    [InlineData(2, 28)] // 2026 is non-leap
    [InlineData(3, 31)]
    [InlineData(4, 30)]
    [InlineData(5, 31)]
    [InlineData(6, 30)]
    [InlineData(7, 31)]
    [InlineData(8, 31)]
    [InlineData(9, 30)]
    [InlineData(10, 31)]
    [InlineData(11, 30)]
    [InlineData(12, 31)]
    public void CurrentUtc_AllTwelveMonths_ShouldSpanExactMonthDays(int month, int expectedDays)
    {
        // Arrange
        var date = new DateTime(2026, month, 15, 12, 0, 0, DateTimeKind.Unspecified);

        // Act
        var (startUtc, endUtc) = MonthlyBudgetWindow.CurrentUtc(date, MyanmarOffset);

        // Assert
        (endUtc - startUtc).Should().Be(TimeSpan.FromDays(expectedDays));
    }

    [Theory]
    [InlineData(0, 0)]      // UTC
    [InlineData(6, 30)]     // Myanmar (+06:30)
    [InlineData(9, 0)]      // Japan (+09:00)
    [InlineData(14, 0)]     // Line Islands / Kiribati (+14:00)
    [InlineData(-5, 0)]     // New York EST (-05:00)
    [InlineData(-12, 0)]    // Baker Island (-12:00)
    [InlineData(5, 45)]     // Nepal (+05:45)
    public void CurrentUtc_AcrossVariousTimezones_ShouldShiftStartAndEndEqually(int hours, int minutes)
    {
        // Arrange
        var offset = new TimeSpan(hours, minutes, 0);
        var businessNow = new DateTime(2026, 7, 20, 15, 0, 0, DateTimeKind.Unspecified);

        // Act
        var (startUtc, endUtc) = MonthlyBudgetWindow.CurrentUtc(businessNow, offset);

        // Assert
        var expectedLocalStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var expectedLocalEnd = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);

        startUtc.Should().Be(DateTime.SpecifyKind(expectedLocalStart - offset, DateTimeKind.Utc));
        endUtc.Should().Be(DateTime.SpecifyKind(expectedLocalEnd - offset, DateTimeKind.Utc));
        startUtc.Kind.Should().Be(DateTimeKind.Utc);
        endUtc.Kind.Should().Be(DateTimeKind.Utc);
        (endUtc - startUtc).Should().Be(TimeSpan.FromDays(31));
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void CurrentUtc_ShouldDisregardInputDateTimeKind_AndExtractYearMonthCleanly(DateTimeKind kind)
    {
        // Arrange
        var businessNow = DateTime.SpecifyKind(new DateTime(2026, 4, 10, 8, 30, 0), kind);

        // Act
        var (startUtc, endUtc) = MonthlyBudgetWindow.CurrentUtc(businessNow, MyanmarOffset);

        // Assert
        startUtc.Kind.Should().Be(DateTimeKind.Utc);
        endUtc.Kind.Should().Be(DateTimeKind.Utc);
        startUtc.Should().Be(new DateTime(2026, 3, 31, 17, 30, 0, DateTimeKind.Utc));
        endUtc.Should().Be(new DateTime(2026, 4, 30, 17, 30, 0, DateTimeKind.Utc));
    }
}
