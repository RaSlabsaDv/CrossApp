namespace Core.Reports;

public sealed record ReportBenchmark(
    int Items, int Iterations, double WithoutIntermediateListMs,
    double WithIntermediateListMs, bool EqualResults);
