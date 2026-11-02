using Core.Services;
using Core.Storage;

namespace Cli;

internal static class OrderImportCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Потрібен шлях до JSON-файлу.");
        string path = args[0];
        bool streaming = false;
        int? timeoutMs = null;
        var storeArgs = new List<string>();
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--stream": streaming = true; break;
                case "--timeout-ms":
                    if (++i >= args.Length || !int.TryParse(args[i], out int ms) || ms < 0)
                        throw new ArgumentException("--timeout-ms потребує невід'ємного цілого числа.");
                    timeoutMs = ms;
                    break;
                case "--file": storeArgs.Add("--file"); break;
                case "--path":
                    if (++i >= args.Length) throw new ArgumentException("--path потребує шляху.");
                    storeArgs.AddRange(["--path", args[i]]);
                    break;
                default: throw new ArgumentException($"Невідомий параметр: {args[i]}");
            }
        }
        if (storeArgs.Contains("--path") && !storeArgs.Contains("--file"))
            throw new ArgumentException("--path потребує --file.");
        using var userCts = new CancellationTokenSource();
        using var timeoutCts = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userCts.Token, timeoutCts.Token);
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; userCts.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            if (timeoutMs == 0) timeoutCts.Cancel();
            else if (timeoutMs.HasValue) timeoutCts.CancelAfter(timeoutMs.Value);
            var store = storeArgs.Contains("--file")
                ? StoreFactory.Create(storeArgs.ToArray())
                : new InMemoryOrderStore();
            var service = new OrderService(store);
            int added = streaming
                ? await service.ImportStreamAsync(new JsonOrderImporter(), path, linked.Token)
                : await service.ImportAsync(new JsonOrderImporter(), path, linked.Token, new ConsoleProgress());
            Console.WriteLine($"Імпортовано записів: {added}");
            return 0;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    // Synchronous delivery keeps console output ordered and avoids queued callbacks after completion.
    private sealed class ConsoleProgress : IProgress<int>
    {
        private int _last = -1;
        public void Report(int value)
        {
            if (value == _last) return;
            _last = value;
            Console.WriteLine($"Хід імпорту: {value}%");
        }
    }
}
