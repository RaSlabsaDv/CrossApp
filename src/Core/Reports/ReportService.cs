using System.Diagnostics;
using Core.Domain;
using Core.Dto;

namespace Core.Reports;

public static class ReportService
{
    public static IReadOnlyList<OrderReportLine> AboveTotal(IEnumerable<Order> source, decimal threshold)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(threshold);
        return source.Where(order => order.Total > threshold)
            .OrderBy(order => order.Total).ThenBy(order => order.Id, StringComparer.Ordinal)
            .Select(ToLine).ToList().AsReadOnly();
    }

    public static IReadOnlyList<OrderReportLine> BelowTotal(IEnumerable<Order> source, decimal threshold)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(threshold);
        return source.Where(order => order.Total < threshold)
            .OrderBy(order => order.Total).ThenBy(order => order.Id, StringComparer.Ordinal)
            .Select(ToLine).ToList().AsReadOnly();
    }

    public static IReadOnlyList<CustomerSummary> TopCustomers(IEnumerable<Order> source, int n)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GroupBy(order => order.CustomerId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CustomerSummary(
                group.Select(order => order.CustomerId).Min(StringComparer.Ordinal)!,
                group.OrderBy(order => order.Id, StringComparer.Ordinal).First().Customer.Name,
                group.Count(), group.Sum(order => order.Total)))
            .TopBy(row => row.Total, row => row.CustomerId, n).ToList().AsReadOnly();
    }

    public static IReadOnlyList<OrderReportLine> TopOrders(IEnumerable<Order> source, int n)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TopBy(order => order.Total, order => order.Id, n)
            .Select(ToLine).ToList().AsReadOnly();
    }

    public static IReadOnlyList<GroupSummary> ByStatus(IEnumerable<Order> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.GroupBy(order => order.Status)
            .Select(group => new GroupSummary(group.Key.ToString(), group.Count(), group.Sum(order => order.Total)))
            .OrderByDescending(row => row.Total).ThenBy(row => row.Key, StringComparer.Ordinal)
            .ToList().AsReadOnly();
    }

    public static (int Items, decimal Total, decimal Average, decimal Max) Totals(IEnumerable<Order> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var amounts = source.Select(order => order.Total).ToList();
        return amounts.Count == 0 ? (0, 0m, 0m, 0m)
            : (amounts.Count, amounts.Sum(), amounts.Average(), amounts.Max());
    }

    public static DeferredExecutionSummary DemonstrateDeferred(IEnumerable<Order> source, decimal threshold)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(threshold);
        var local = source.ToList();
        var query = local.Where(order => order.Total < threshold);
        int before = query.Count();
        local.Add(Order.Create("DEFERRED-TEMP", new CustomerDto("DEFERRED", "Демо", "demo@example.com")));
        int after = query.Count();
        // Project to immutable DTOs; later entity or list mutations cannot change this result.
        var snapshot = query.Select(ToLine).ToList();
        local.Clear();
        return new(before, after, snapshot.Count, query.Count());
    }

    public static IReadOnlyList<CustomerSummary> CustomersWithOrders(
        IEnumerable<CustomerDto> customers, IEnumerable<Order> orders)
    {
        ArgumentNullException.ThrowIfNull(customers);
        ArgumentNullException.ThrowIfNull(orders);
        return customers.GroupJoin(orders, customer => customer.Id, order => order.CustomerId,
                (customer, group) => new CustomerSummary(customer.Id, customer.Name,
                    group.Count(), group.Sum(order => order.Total)), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(row => row.Total).ThenBy(row => row.CustomerId, StringComparer.Ordinal)
            .ToList().AsReadOnly();
    }

    public static IReadOnlyList<GroupSummary> ByStatusQuery(IEnumerable<Order> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var query = from order in source
                    group order by order.Status into g
                    select new GroupSummary(g.Key.ToString(), g.Count(), g.Sum(order => order.Total));
        // Explicit ordinal comparison matches the method syntax on every culture.
        return query.OrderByDescending(row => row.Total).ThenBy(row => row.Key, StringComparer.Ordinal)
            .ToList().AsReadOnly();
    }

    public static ReportBenchmark Benchmark(IEnumerable<Order> source, decimal threshold, int n, int iterations = 5)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(threshold);
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        var items = source.ToList(); // Same materialized input for both measured variants.
        IReadOnlyList<OrderReportLine> Run(bool intermediateList)
        {
            var filtered = items.Where(order => order.Total > threshold);
            IEnumerable<Order> input = intermediateList ? filtered.ToList() : filtered;
            return input.TopBy(order => order.Total, order => order.Id, n)
                .Select(ToLine).ToList();
        }
        var direct = Run(false); // Warm both variants before timing.
        var materialized = Run(true);
        double directMs = 0, materializedMs = 0;
        double Measure(bool intermediateList)
        {
            long start = Stopwatch.GetTimestamp();
            Run(intermediateList);
            return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        for (int i = 0; i < iterations; i++)
        {
            // Alternate first runner to reduce ordering bias.
            if (i % 2 == 0) { directMs += Measure(false); materializedMs += Measure(true); }
            else { materializedMs += Measure(true); directMs += Measure(false); }
        }
        return new(items.Count, iterations, directMs / iterations, materializedMs / iterations,
            direct.SequenceEqual(materialized));
    }

    private static OrderReportLine ToLine(Order order) => new(
        order.Id, order.CustomerId, order.Customer.Name, order.Lines.Count, order.Total, order.Status.ToString());
}
