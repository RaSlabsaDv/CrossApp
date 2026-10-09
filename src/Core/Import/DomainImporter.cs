using Core.Domain;
using Core.Dto;

namespace Core.Import;

public static class DomainImporter
{
    public static ImportResult<OrderLine> ToOrderLines(ImportResult<ProductDto> result) =>
        Convert(result, OrderLine.FromDto);

    public static ImportResult<Order> ToOrders(ImportResult<OrderDto> result) =>
        Convert(result, Order.FromDto);

    private static ImportResult<TEntity> Convert<TDto, TEntity>(
        ImportResult<TDto> result, Func<TDto, TEntity> fromDto)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(result.Items);
        ArgumentNullException.ThrowIfNull(result.Errors);
        List<TEntity> entities = [];
        List<string> errors = new(result.Errors);
        for (int index = 0; index < result.Items.Count; index++)
        {
            try
            {
                entities.Add(fromDto(result.Items[index]));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                errors.Add($"Record {index + 1}: {ex.Message}");
            }
        }

        return new ImportResult<TEntity>(entities.AsReadOnly(), errors.AsReadOnly());
    }
}
