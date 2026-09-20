namespace Core.Dto;

public record ProductDto
(
    string Id,
    string Sku,
    string Name,
    decimal Price,
    int Quantity,
    string? Note = null
);
