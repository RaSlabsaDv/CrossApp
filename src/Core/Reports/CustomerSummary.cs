namespace Core.Reports;

public sealed record CustomerSummary(
    string CustomerId, string CustomerName, int OrdersCount, decimal Total);
