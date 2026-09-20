namespace Core.Dto;

public record OrderDto
(
    string Id,
    CustomerDto Customer,
    IReadOnlyList<ProductDto> Products,
    decimal TotalPrice
);

