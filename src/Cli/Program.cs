using Core.Dto;
using Cli;
using Cli.Notifications;
using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Events;
using Core.Import;
using Core.Services;
using Core.Storage;

try
{
    return Run(args);
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
    or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or NotSupportedException)
{
    Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    return 1;
}

static int Run(string[] args)
{
    if (args is ["--demo"])
        return RunDomainDemo();
    if (args is ["--extras-demo"])
        return RunExtraDemo();
    if (args.Length > 0 && args[0] == "--events-demo")
        return RunEventsDemo(args[1..]);
    if (args is ["--lab06-extras-demo"])
        return RunLab06ExtrasDemo();

    if (args.Length == 0 || args[0].StartsWith("--"))
        return RunStorageDemo(args);
    if (args.Length != 1)
        throw new ArgumentException("Import mode requires exactly one file path", nameof(args));

    return RunImport(args[0]);
}

static int RunImport(string path)
{

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

    ImportResult<OrderLine> domainResult = DomainImporter.ToOrderLines(result);
    Console.WriteLine($"Доменних рядків: {domainResult.Items.Count}, помилок імпорту та інваріантів: {domainResult.Errors.Count}");
    foreach (string error in domainResult.Errors.Skip(result.Errors.Count))
        Console.WriteLine($"  ! {error}");

    return 0;
}

static int RunStorageDemo(string[] args)
{
    StoreOptions options = StoreFactory.Parse(args);
    IOrderStore store;
    if (options.UseFactory)
        store = StoreFactory.Create(args);
    else
    {
        // Composition root: concrete stores are selected here, not in the service.
        store = options.UseFile
            ? new FileOrderStore(options.DataPath)
            : new InMemoryOrderStore(SampleData.Orders());
        if (options.UseCache)
            store = new CachingOrderStore(store);
    }
    OrderService service = new(store);
    Console.WriteLine($"Сховище: {store.GetType().Name}");
    Console.WriteLine(options.UseFactory ? "Створення: StoreFactory.Create(args)" : "Створення: Program.cs");
    if (options.UseFile)
        Console.WriteLine($"Файл: {options.DataPath}");
    int initialCount = service.All().Count;
    Console.WriteLine($"Записів до операцій: {initialCount}");

    CustomerDto customer = new($"DEMO-{Guid.NewGuid():N}", "Олена", "olena@example.com");
    Order created = service.CreateOrder(customer);
    Console.WriteLine("Створено замовлення:");
    PrintOrder(created);
    service.AddLine(created.Id, "P-001", "Клавіатура", 1200m, 2, "SKU-001");
    service.AddLine(created.Id, "P-002", "Миша", 450m, 1, "SKU-002");
    service.Confirm(created.Id);
    Order found = service.Find(created.Id)!;
    Console.WriteLine("Знайдено за ID після додавання рядків і підтвердження:");
    PrintOrder(found);
    Console.WriteLine("Список замовлень:");
    foreach (Order order in service.All())
        PrintOrder(order);

    Console.WriteLine("Пошук: підтверджені замовлення із сумою >= 1000:");
    foreach (Order order in service.Search(order => order.IsConfirmed && order.Total >= 1000m))
        PrintOrder(order);

    Console.WriteLine("Сценарії відмови:");
    bool passed = TryDo("дублікат ID", () => store.Add(found));
    string missingId = $"MISSING-{Guid.NewGuid():N}";
    passed &= TryDo("неіснуючий ID", () => service.AddLine(missingId, "P-003", "Кабель", 90m, 1));
    Console.WriteLine($"Пошук відсутнього ID повертає null: {service.Find(missingId) is null}");

    Order temporary = service.CreateOrder(customer);
    bool removed = service.Remove(temporary.Id);
    bool removedAgain = service.Remove(temporary.Id);
    Console.WriteLine($"Видалення тимчасового замовлення: {removed}; повторне: {removedAgain}");
    passed &= removed && !removedAgain && service.Find(temporary.Id) is null;

    if (store is CachingOrderStore cached)
    {
        int hitsBefore = cached.CacheHits;
        service.Find(created.Id);
        service.Find(created.Id);
        service.All();
        service.All();
        int extraHits = cached.CacheHits - hitsBefore;
        Console.WriteLine($"Кеш: повторні читання дали {extraHits} влучань; загалом влучань = {cached.CacheHits}, промахів = {cached.CacheMisses}");
        passed &= extraHits >= 2;
    }
    int finalCount = service.All().Count;
    Console.WriteLine($"Записів після операцій: {finalCount}");
    passed &= finalCount == initialCount + 1 && found.IsConfirmed && found.Total == 2850m;
    return passed ? 0 : 1;
}

static int RunEventsDemo(string[] args)
{
    IOrderStore store = StoreFactory.Create(args);
    OrderService service = new(store);
    string logPath = Path.Combine(AppContext.BaseDirectory, "logs", "app.log");
    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
    int changes = 0;
    int loggedChanges = 0;
    int unloggedChanges = 0;
    bool subscriptionOrderCorrect = true;
    bool loggingSucceeded = true;
    Console.WriteLine($"Сховище: {store.GetType().Name}");
    Console.WriteLine($"Журнал: {logPath}");

    // Крок 3: перший підписник — локальний метод, консоль і файл.
    void OnOrderChanged(object? sender, OrderChangedEventArgs e)
    {
        subscriptionOrderCorrect &= ReferenceEquals(sender, service) && changes == loggedChanges + unloggedChanges;
        string line = $"{e.At:HH:mm:ss} {e.Kind,-16} {e.OrderId} → рядків {e.LinesCount}, сума {e.Total:F2}, стан {e.Status}";
        Console.WriteLine(line);
        try
        {
            File.AppendAllText(logPath, line + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            loggingSucceeded = false;
            Console.WriteLine($"Помилка журналу: {ex.Message}");
        }
        loggedChanges++;
    }
    service.Changed += OnOrderChanged;

    // Крок 4: другий підписник захоплює локальний лічильник.
    EventHandler<OrderChangedEventArgs> countChanges = (_, _) => changes++;
    service.Changed += countChanges;
    bool passed;
    try
    {
        Console.WriteLine("Крок 5: створення, два рядки, підтвердження.");
        CustomerDto customer = new($"LAB06-{Guid.NewGuid():N}", "Олена", "olena@example.com");
        Order created = service.CreateOrder(customer);
        service.AddLine(created.Id, "P-001", "Клавіатура", 1200m, 2);
        service.AddLine(created.Id, "P-002", "Миша", 450m, 1);
        service.Confirm(created.Id);
        Console.WriteLine($"Подій отримано: {changes}; рядків журналу за цей запуск: {loggedChanges}");
        Console.WriteLine($"Порядок: обробник консолі й файлу → лямбда-лічильник: {subscriptionOrderCorrect}");
        passed = changes == 4 && loggedChanges == 4 && subscriptionOrderCorrect && loggingSucceeded;

        service.Changed -= OnOrderChanged;
        service.Cancel(created.Id);
        unloggedChanges++;
        Console.WriteLine("Після відписки: скасування виконано без рядка події у консолі та файлі.");
        PrintOrder(service.Find(created.Id)!);
        Console.WriteLine($"Подій отримано: {changes}; рядків журналу за цей запуск: {loggedChanges}");
        passed &= changes == 5 && loggedChanges == 4 && service.Find(created.Id)!.Status == OrderStatus.Cancelled;

        // Крок 6: підписуємо назад, щоб відсутність події при відмові була видимою.
        service.Changed -= countChanges;
        service.Changed += OnOrderChanged;
        service.Changed += countChanges;
        Console.WriteLine("Крок 6: відмови — обидва підписники активні.");
        Order draft = service.CreateOrder(customer);
        int beforeFailure = changes;
        int loggedBeforeFailure = loggedChanges;
        passed &= TryDo("від'ємна кількість", () => service.AddLine(draft.Id, "P-003", "Кабель", 90m, -1));
        passed &= TryDo("неіснуючий ID", () => service.Confirm($"MISSING-{Guid.NewGuid():N}"));
        passed &= TryDo("підтвердження порожнього замовлення", () => service.Confirm(draft.Id));
        bool noFailureEvents = changes == beforeFailure && loggedChanges == loggedBeforeFailure;
        Console.WriteLine($"Подій від невдалих операцій: {changes - beforeFailure}; стан чернетки незмінний: {service.Find(draft.Id)!.Lines.Count == 0}");
        passed &= noFailureEvents && service.Find(draft.Id)!.Lines.Count == 0;

        Console.WriteLine("Крок 7: FindAll(order => order.Total < 1000m):");
        IReadOnlyList<Order> matches = service.FindAll(order => order.Total < 1000m);
        foreach (Order order in matches)
            PrintOrder(order);
        passed &= matches.Any(order => order.Id == draft.Id) && matches.All(order => order.Total < 1000m);

        // Прибирання тимчасової чернетки також є успішною доменною зміною.
        passed &= service.Remove(draft.Id);
        int beforeMissingRemove = changes;
        passed &= !service.Remove(draft.Id) && changes == beforeMissingRemove;
    }
    finally
    {
        service.Changed -= OnOrderChanged;
        service.Changed -= countChanges;
    }
    passed &= loggingSucceeded && subscriptionOrderCorrect;
    Console.WriteLine(passed ? "Самоперевірка лабораторної 6: успішно." : "Самоперевірка лабораторної 6: помилка.");
    return passed ? 0 : 1;
}

static int RunLab06ExtrasDemo()
{
    Console.WriteLine("Додаткове 1: INotificationSink і ручний DI.");
    string logPath = Path.Combine(AppContext.BaseDirectory, "logs", "sinks.log");
    INotificationSink sink = new CompositeNotificationSink(new ConsoleSink(), new FileSink(logPath));
    IOrderStore store = new InMemoryOrderStore();
    OrderService service = new(store, sink);
    int linesBefore = File.Exists(logPath) ? File.ReadLines(logPath).Count() : 0;
    int changes = 0;
    EventHandler<OrderChangedEventArgs> counter = (_, _) => changes++;
    service.Changed += counter;
    bool passed;
    try
    {
        Order created = service.CreateOrder(new CustomerDto("EXTRA-06", "Олена", "olena@example.com"));
        service.AddLine(created.Id, "P-001", "Кабель", 90m, 2);
        service.Confirm(created.Id);
        passed = changes == 3 && File.ReadLines(logPath).Count() - linesBefore == 3;
        service.Changed -= counter;
        service.Cancel(created.Id);
        Console.WriteLine($"Після -=: лічильник подій = {changes}; sink продовжив виведення і запис.");
        passed &= changes == 3 && File.ReadLines(logPath).Count() - linesBefore == 4
            && service.Find(created.Id)!.Status == OrderStatus.Cancelled;
        int logBeforeFailure = File.ReadLines(logPath).Count();
        passed &= TryDo("повторне скасування", () => service.Cancel(created.Id));
        passed &= File.ReadLines(logPath).Count() == logBeforeFailure;
    }
    finally
    {
        service.Changed -= counter;
    }
    Console.WriteLine($"Журнал sink: {logPath}");

    Console.WriteLine("Додаткове 2: Action<string> повідомляє про пропущені рядки CSV.");
    string directory = Path.Combine(Path.GetTempPath(), $"lab06-import-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        string path = Path.Combine(directory, "products.csv");
        File.WriteAllLines(path, new[]
        {
            "id;sku;name;price;quantity",
            "P-001;SKU-001;Кабель;90;2",
            "P-002;;Миша;450;1",
            "P-003;SKU-003;Хаб;not-a-price;1",
            "P-004;SKU-004;Адаптер;100;-1"
        });
        List<string> skipped = [];
        ImportResult<ProductDto> result = ProductCsvImporter.Load(path, message =>
        {
            skipped.Add(message);
            Console.WriteLine($"Пропущено: {message}");
        });
        ImportResult<ProductDto> withoutCallback = ProductCsvImporter.Load(path);
        Console.WriteLine($"Імпортовано: {result.Items.Count}; пропущено: {result.Errors.Count}; викликів Action: {skipped.Count}");
        passed &= result.Items.Count == 1 && result.Errors.Count == 3
            && skipped.SequenceEqual(result.Errors)
            && withoutCallback.Items.SequenceEqual(result.Items)
            && withoutCallback.Errors.SequenceEqual(result.Errors);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }

    Console.WriteLine("Додаткове 3: async-лямбда для EventHandler повертає void (async void).");
    Console.WriteLine("Invoke не очікує завершення після await; виняток після await не ловиться try/catch навколо операції сервісу.");
    Console.WriteLine("Письмове пояснення та приклад: звіт lab6, розділ додаткових завдань.");
    Console.WriteLine(passed ? "Додаткові завдання лабораторної 6: успішно." : "Додаткові завдання лабораторної 6: помилка.");
    return passed ? 0 : 1;
}

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

static int RunExtraDemo()
{
    Console.WriteLine("=== Додаткове 1: імпорт DTO у сутності ===");
    ImportResult<ProductDto> products = new(
        new ProductDto[]
        {
            new("P-001", "SKU-001", "Клавіатура", 1200m, 2),
            new("P-002", "SKU-002", "Миша", 450m, 0),
            new("P-003", "SKU-003", "Кабель", -90m, 1)
        },
        new[] { "Line 5: invalid CSV format" });
    ImportResult<OrderLine> lines = DomainImporter.ToOrderLines(products);
    Console.WriteLine($"Створено рядків: {lines.Items.Count}, помилок: {lines.Errors.Count}");
    foreach (string error in lines.Errors)
        Console.WriteLine($"  ! {error}");
    bool passed = lines.Items.Count == 1 && lines.Errors.Count == 3;

    CustomerDto customer = new("C-001", "Олена", "olena@example.com");
    Console.WriteLine();
    Console.WriteLine("=== Додаткове 2: ліміт замовлень клієнта ===");
    IOrderStore store = new InMemoryOrderStore();
    OrderService service = new(store);
    for (int index = 1; index <= OrderService.MaxConfirmedOrdersPerCustomer; index++)
    {
        Order existing = service.CreateOrder(customer, $"ORD-{index:D3}");
        service.AddLine(existing.Id, "P-001", "Клавіатура", 1200m, 1);
        service.Confirm(existing.Id);
    }
    Order next = service.CreateOrder(customer, "ORD-006");
    service.AddLine(next.Id, "P-002", "Миша", 450m, 1);
    passed &= TryDo("шосте підтверджене замовлення", () => service.Confirm(next.Id));
    next = service.Find(next.Id)!;
    passed &= next.Status == OrderStatus.Draft;
    PrintOrder(next);
    service.Cancel("ORD-001");
    service.Confirm(next.Id);
    next = service.Find(next.Id)!;
    Console.WriteLine("Після скасування одного замовлення підтвердження дозволено:");
    PrintOrder(next);
    passed &= next.IsConfirmed;

    Console.WriteLine();
    Console.WriteLine("=== Додаткове 3: стани замовлення ===");
    next.Cancel();
    PrintOrder(next);
    passed &= TryDo("підтвердження скасованого замовлення", () => next.Confirm());
    passed &= TryDo("повторне скасування", () => next.Cancel());
    passed &= TryDo("зміна скасованого замовлення", () => next.AddLine("P-003", "Кабель", 90m, 1));
    Order restored = Order.FromDto(next.ToDto());
    Console.WriteLine("Скасоване замовлення після відновлення DTO:");
    PrintOrder(restored);
    passed &= restored.Status == OrderStatus.Cancelled && restored.Total == next.Total;
    Order empty = Order.Create("ORD-007", customer);
    empty.Cancel();
    PrintOrder(empty);
    passed &= empty.Status == OrderStatus.Cancelled;

    ImportResult<Order> importedOrders = DomainImporter.ToOrders(new ImportResult<OrderDto>(
        new[] { next.ToDto(), next.ToDto() with { TotalPrice = -1m } }, Array.Empty<string>()));
    Console.WriteLine($"Імпорт замовлень: створено {importedOrders.Items.Count}, помилок {importedOrders.Errors.Count}");
    foreach (string error in importedOrders.Errors)
        Console.WriteLine($"  ! {error}");
    passed &= importedOrders.Items.Count == 1 && importedOrders.Errors.Count == 1;
    return passed ? 0 : 1;
}

static void PrintOrder(Order order) => Console.WriteLine(
    $"{order.Id}: рядків = {order.Lines.Count}, сума = {order.Total:F2}, стан = {order.Status}");

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
