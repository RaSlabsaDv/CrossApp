using Core.Reports;

namespace Core.Tests;

public sealed class ReportServiceTests
{
    [Theory]
    [InlineData(100, 1)]
    [InlineData(10, 0)]
    [InlineData(101, 3)]
    public void BelowTotal_CountsOnlyStrictlyBelow(decimal threshold, int expected) =>
        Assert.Equal(expected, ReportService.BelowTotal(TestData.Items(), threshold).Count);

    [Fact]
    public void TopOrders_TiesUseOrdinalIdRegardlessOfInputOrder()
    {
        string[] expected = ["ORD-A", "ORD-B"];
        Assert.Equal(expected, ReportService.TopOrders(TestData.Items(), 2).Select(x => x.OrderId));
        Assert.Equal(expected, ReportService.TopOrders(TestData.Items().Reverse(), 2).Select(x => x.OrderId));
    }

    [Fact]
    public void Totals_EmptyInput_ReturnsZeroes() =>
        Assert.Equal((0, 0m, 0m, 0m), ReportService.Totals([]));
}
