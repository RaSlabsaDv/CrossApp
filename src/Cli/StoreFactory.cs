using Core;
using Core.Abstractions;
using Core.Storage;

namespace Cli;

public sealed record StoreOptions(bool UseFile, bool UseCache, bool UseFactory, string DataPath);

public static class StoreFactory
{
    public static IOrderStore Create(string[] args)
    {
        StoreOptions options = Parse(args);
        IOrderStore store = options.UseFile
            ? new FileOrderStore(options.DataPath)
            : new InMemoryOrderStore(SampleData.Orders());
        return options.UseCache ? new CachingOrderStore(store) : store;
    }

    public static StoreOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string path = Path.Combine(AppContext.BaseDirectory, "data", "orders.json");
        HashSet<string> seen = [];
        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (!seen.Add(option))
                throw new ArgumentException($"Duplicate option: {option}", nameof(args));
            switch (option)
            {
                case "--file":
                case "--cache":
                case "--factory":
                    break;
                case "--path":
                    if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith("--"))
                        throw new ArgumentException("Option --path requires a file path", nameof(args));
                    path = Path.GetFullPath(args[index]);
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {option}", nameof(args));
            }
        }
        if (seen.Contains("--path") && !seen.Contains("--file"))
            throw new ArgumentException("Option --path requires --file", nameof(args));
        return new StoreOptions(seen.Contains("--file"), seen.Contains("--cache"),
            seen.Contains("--factory"), path);
    }
}
