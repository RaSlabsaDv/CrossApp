using Core.Dto;

namespace Core.Domain;

public sealed class Order
{
    private readonly List<OrderLine> _lines = [];
    private bool _isConfirmed;

    public string Id { get; }
    public CustomerDto Customer { get; }
    public string CustomerId => Customer.Id;
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
    public bool IsConfirmed => _isConfirmed;
    public decimal Total => _lines.Sum(line => line.Total);

    private Order(string id, CustomerDto customer)
    {
        Id = id;
        Customer = customer;
    }

    public static Order Create(string id, CustomerDto customer)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Order ID is required", nameof(id));
        ArgumentNullException.ThrowIfNull(customer);
        if (string.IsNullOrWhiteSpace(customer.Id))
            throw new ArgumentException("Customer ID is required", nameof(customer));
        if (string.IsNullOrWhiteSpace(customer.Name))
            throw new ArgumentException("Customer name cannot be empty", nameof(customer));
        if (string.IsNullOrWhiteSpace(customer.Email))
            throw new ArgumentException("Customer email is required", nameof(customer));

        return new Order(id.Trim(), new CustomerDto(customer.Id.Trim(),
            customer.Name.Trim(), customer.Email.Trim()));
    }

    public void AddLine(string productId, string name, decimal price, int quantity,
        string? sku = null, string? note = null)
    {
        EnsureDraft();
        OrderLine line = OrderLine.Create(productId, name, price, quantity, sku, note);
        AddValidatedLine(line);
    }

    public void Confirm()
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new InvalidOperationException(
                $"Cannot confirm empty order {Id}");

        _isConfirmed = true;
    }

    public OrderDto ToDto() => new(Id, Customer,
        _lines.Select(line => line.ToDto()).ToArray(), Total, IsConfirmed);

    public static Order FromDto(OrderDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(dto.Products);

        Order order = Create(dto.Id, dto.Customer);
        foreach (ProductDto product in dto.Products)
            order.AddValidatedLine(OrderLine.FromDto(product));

        if (dto.TotalPrice != order.Total)
            throw new ArgumentException(
                $"Order {order.Id} DTO total ({dto.TotalPrice}) does not match the sum of its lines ({order.Total})",
                nameof(dto));
        if (dto.IsConfirmed)
            order.Confirm();

        return order;
    }

    private void EnsureDraft()
    {
        if (_isConfirmed)
            throw new InvalidOperationException(
                $"Order {Id} is already confirmed and cannot be modified or confirmed again");
    }

    private void AddValidatedLine(OrderLine line)
    {
        EnsureDraft();
        if (line.Total > decimal.MaxValue - Total)
            throw new InvalidOperationException(
                $"Adding product {line.ProductId} would exceed the maximum total for order {Id}");

        _lines.Add(line);
    }
}
