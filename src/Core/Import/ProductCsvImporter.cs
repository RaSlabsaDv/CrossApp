using System.Globalization;
using Core.Dto;

namespace Core.Import;

public static class ProductCsvImporter
{
    private const char Separator = ';';

    public static ImportResult<ProductDto> Load(string path)
    {
        var items = new List<ProductDto>();
        var errors = new List<string>();

        string[] lines = File.ReadAllLines(path);

        for (int i = 0; i < lines.Length; i++)
        {
            int number = i + 1;
            string line = lines[i];

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;
            if (number == 1 && line.StartsWith("id", StringComparison.OrdinalIgnoreCase))
                continue;

            switch (ParseLine(line))
            {
                case ParseOk ok:
                    items.Add(ok.Value);
                    break;

                case ParseFailed failed:
                    errors.Add($"line {number}: {failed.Reason}");
                    break;
            }
        }

        return new ImportResult<ProductDto>(items, errors);
    }

    private static ParseOutcome ParseLine(string line)
    {
        string[] parts = line.Split(Separator, StringSplitOptions.TrimEntries);

        return parts switch
        {
            { Length: < 5 } => new ParseFailed($"очікую 5 колонок, отримав {parts.Length}"),

            [_, "", _, _, _] or [_, _, "", _, _]
                => new ParseFailed("SKU або назва порожні"),

            [_, _, _, var price, _] when !decimal.TryParse(price, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                => new ParseFailed($"ціна '{price}' не є коректним числом"),

            [_, _, _, _, var qty] when !int.TryParse(qty, out int q) || q < 0
                => new ParseFailed($"кількість '{qty}' не є невід'ємним числом"),

            [var id, var sku, var name, var price, var qty]
                => new ParseOk(new ProductDto(
                    id,
                    sku,
                    name,
                    decimal.Parse(price, NumberStyles.Number, CultureInfo.InvariantCulture),
                    int.Parse(qty))),

            _ => new ParseFailed($"занадто багато колонок: {parts.Length}")
        };
    }

    private abstract record ParseOutcome;
    private sealed record ParseOk(ProductDto Value) : ParseOutcome;
    private sealed record ParseFailed(string Reason) : ParseOutcome;
}