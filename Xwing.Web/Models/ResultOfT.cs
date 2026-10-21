namespace Xwing.Web.Models;

public sealed record Result<T>
{
    public bool IsSuccess { get; init; }

    public T? Value { get; init; }

    public string? Error { get; init; }
}
