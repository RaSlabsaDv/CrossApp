namespace Core.Dto;

public record ImportResult<T>(IReadOnlyList<T> Items, IReadOnlyList<string> Errors);