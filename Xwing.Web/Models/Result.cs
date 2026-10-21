namespace Xwing.Web.Models;

public sealed record Result
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
    public static Result Ok() => new() { IsSuccess = true };

    public static Result<T> Ok<T>(T value) => new() { IsSuccess = true, Value = value };

    public static Result Fail(string error) => new() { IsSuccess = false, Error = error };

    public static Result<T> Fail<T>(string error) => new() { IsSuccess = false, Error = error };
}
