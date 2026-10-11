using System.Globalization;

namespace Cli;

public sealed record ReportOptions(string Command, string[] StoreArgs, decimal Threshold, int Take)
{
    public static ReportOptions? TryParse(string[] args)
    {
        bool reportMode = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--path" or "--threshold" or "--take") { i++; continue; }
            if (args[i] == "report" || args[i].StartsWith("report-", StringComparison.Ordinal)
                || args[i] == "--lab07-extras-demo") reportMode = true;
        }
        if (!reportMode) return null;
        string? command = null;
        decimal threshold = 1000m;
        int take = 5;
        List<string> storeArgs = [];
        HashSet<string> seen = [];
        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            if (option is "report" or "--lab07-extras-demo" || option.StartsWith("report-", StringComparison.Ordinal))
            {
                if (command is not null) throw new ArgumentException("Choose exactly one report command");
                command = option switch { "report" => "report-top", "--lab07-extras-demo" => "report-extras", _ => option };
                continue;
            }
            if (!seen.Add(option)) throw new ArgumentException($"Duplicate option: {option}");
            switch (option)
            {
                case "--threshold":
                    if (++i >= args.Length || !decimal.TryParse(args[i], NumberStyles.Number,
                        CultureInfo.InvariantCulture, out threshold) || threshold < 0)
                        throw new ArgumentException("Option --threshold requires a nonnegative decimal number");
                    break;
                case "--take":
                    if (++i >= args.Length || !int.TryParse(args[i], out take) || take < 0)
                        throw new ArgumentException("Option --take requires a nonnegative integer");
                    break;
                case "--path":
                    storeArgs.Add(option);
                    if (++i >= args.Length) throw new ArgumentException("Option --path requires a file path");
                    storeArgs.Add(args[i]);
                    break;
                case "--file": case "--cache": case "--factory":
                    storeArgs.Add(option);
                    break;
                default: throw new ArgumentException($"Unknown report option: {option}");
            }
        }
        return new(command ?? "report-top", storeArgs.ToArray(), threshold, take);
    }
}
