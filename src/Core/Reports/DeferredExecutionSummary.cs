namespace Core.Reports;

public sealed record DeferredExecutionSummary(
    int BeforeChange, int AfterChange, int SnapshotCount, int AfterClear);
