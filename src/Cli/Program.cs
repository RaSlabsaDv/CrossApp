using Core.Dto;
using Core.Import;

string path = args.Length > 0 ? args[0] : Path.Combine("data", "sample.csv");

if (!File.Exists(path))
{
    Console.WriteLine($"Файл не знайдено: {Path.GetFullPath(path)}");
    return 1;
}

ImportResult<ProductDto> result = Path.GetExtension(path).ToLowerInvariant() switch
{
    ".csv" => ProductCsvImporter.Load(path),
    ".json" => ProductJsonImporter.Load(path),
    var ext => throw new NotSupportedException($"Непідтримуване розширення: {ext}")
};

Console.WriteLine($"Завантажено записів: {result.Items.Count}");

foreach (ProductDto p in result.Items.Take(5))
    Console.WriteLine($"  {p.Id,-6} {p.Sku,-10} {p.Name,-26} {p.Price,10:F2} {p.Quantity,5}");

if (result.Errors.Count > 0)
{
    Console.WriteLine($"Пропущено рядків: {result.Errors.Count}");
    foreach (string e in result.Errors)
        Console.WriteLine($"  ! {e}");
}

int total = result.Items.Count + result.Errors.Count;
double errorRate = total == 0 ? 0 : (double)result.Errors.Count / total * 100;

Console.WriteLine($"Усього: {total}, прийнято: {result.Items.Count}, пропущено: {result.Errors.Count}, помилок: {errorRate:F1}%");

return 0;