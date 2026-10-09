using Core.Dto;

namespace Core.Domain;

public sealed class OrderLine
{
    public string ProductId { get; }
    public string Sku { get; }
    public string Name { get; }
    public decimal Price { get; }
    public int Quantity { get; }
    public string? Note { get; }
    public decimal Total => Price * Quantity;

    private OrderLine(string productId, string sku, string name, decimal price,
        int quantity, string? note)
    {
        ProductId = productId;
        Sku = sku;
        Name = name;
        Price = price;
        Quantity = quantity;
        Note = note;
    }

    public static OrderLine Create(string productId, string name, decimal price,
        int quantity, string? sku = null, string? note = null)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("Product ID is required", nameof(productId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name cannot be empty", nameof(name));
        if (sku is not null && string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU cannot be empty", nameof(sku));
        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price), price,
                "Product price cannot be negative");
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity,
                "Order line quantity must be greater than zero");
        try
        {
            _ = price * quantity;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(price), price,
                "Order line total exceeds the maximum decimal value");
        }

        // Якщо окремого SKU немає, ключем товару слугує його ідентифікатор.
        return new OrderLine(productId.Trim(), (sku ?? productId).Trim(),
            name.Trim(), price, quantity, note);
    }

    public ProductDto ToDto() => new(ProductId, Sku, Name, Price, Quantity, Note);

    public static OrderLine FromDto(ProductDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.Sku))
            throw new ArgumentException("Product DTO SKU cannot be empty", nameof(dto));

        return Create(dto.Id, dto.Name, dto.Price, dto.Quantity, dto.Sku, dto.Note);
    }
}
