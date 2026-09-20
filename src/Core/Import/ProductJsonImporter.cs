using System.Text.Json;
using Core.Dto;

namespace Core.Import;

public static class ProductJsonImporter
{
    public static ImportResult<ProductDto> Load(string path)
    {
        string json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        List<ProductDto> items;
        try
        {
            items = JsonSerializer.Deserialize<List<ProductDto>>(json, options) ?? [];
        }
        catch (JsonException ex)
        {
            return new ImportResult<ProductDto>([], [$"JSON parse error: {ex.Message}"]);
        }

        return new ImportResult<ProductDto>(items, []);
    }
}