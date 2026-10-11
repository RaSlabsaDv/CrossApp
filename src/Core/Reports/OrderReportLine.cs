namespace Core.Reports;

public sealed record OrderReportLine(
    string OrderId, string CustomerId, string CustomerName,
    int LinesCount, decimal Total, string Status);
