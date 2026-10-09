using Core.Dto;
using Core.Domain;
using Core.Import;

if (args is ["--demo"])
    return RunDomainDemo();

string path = args.Length > 0 ? args[0] : Path.Combine("../../data", "sample.csv");

if (!File.Exists(path))
{
    Console.WriteLine($"Файл не знайдено: {Path.GetFullPath(path)}");
    return 1;
}

ImportResult<ProductDto> result = Path.GetExtension(path).ToLowerInvariant() switch
{
    ".csv" => ProductCsvImporter.Load(path),
    ".json" => ProductJsonImporter.Load(path),
    var ext => throw new NotSupportedException($"Unsupported file extension: {ext}")
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

static int RunDomainDemo()
{
    Console.WriteLine("=== Сценарій 1: успіх ===");
    CustomerDto customer = new("C-001", "Олена", "olena@example.com");
    Order order = Order.Create("ORD-001", customer);
    PrintOrder(order);
    order.AddLine("P-001", "Клавіатура", 1200m, 2, "SKU-001");
    order.AddLine("P-002", "Миша", 450m, 1, "SKU-002");
    PrintOrder(order);
    order.Confirm();
    PrintOrder(order);

    Order restored = Order.FromDto(order.ToDto());
    Console.WriteLine("Відновлено через ToDto / FromDto:");
    PrintOrder(restored);

    Console.WriteLine();
    Console.WriteLine("=== Сценарій 2: порушення інваріантів ===");
    Order draft = Order.Create("ORD-002", customer);
    bool passed = true;
    passed &= TryDo("порожній номер", () => Order.Create(" ", customer));
    passed &= TryDo("нульова кількість", () => draft.AddLine("P-001", "Клавіатура", 1200m, 0));
    passed &= TryDo("від'ємна ціна", () => draft.AddLine("P-001", "Клавіатура", -1m, 1));
    passed &= TryDo("підтвердження порожнього замовлення", () => draft.Confirm());
    passed &= TryDo("зміна підтвердженого замовлення", () => order.AddLine("P-003", "Кабель", 90m, 1));
    passed &= TryDo("повторне підтвердження", () => order.Confirm());
    passed &= TryDo("некоректна сума у DTO", () => Order.FromDto(order.ToDto() with { TotalPrice = -1m }));

    Console.WriteLine("Стан після відмов:");
    PrintOrder(draft);
    PrintOrder(order);
    bool unchanged = draft.Lines.Count == 0 && draft.Total == 0m && !draft.IsConfirmed
        && order.Lines.Count == 2 && order.Total == 2850m && order.IsConfirmed;
    Console.WriteLine(unchanged ? "Стан замовлень не змінився." : "Помилка: стан замовлень змінився!");
    return passed && unchanged ? 0 : 1;
}

static void PrintOrder(Order order) => Console.WriteLine(
    $"{order.Id}: рядків = {order.Lines.Count}, сума = {order.Total:F2}, підтверджено = {order.IsConfirmed}");

static bool TryDo(string title, Action action)
{
    try
    {
        action();
        Console.WriteLine($"  {title}: виняток НЕ спрацював — інваріант відсутній!");
        return false;
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        Console.WriteLine($"  {title}: {ex.GetType().Name} — {ex.Message}");
        return true;
    }
}
