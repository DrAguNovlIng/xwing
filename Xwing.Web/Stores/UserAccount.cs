using System.Diagnostics.CodeAnalysis;

namespace Xwing.Web.Stores;

public sealed record UserAccount
{
    public int UserId { get; init; }
    public string UserName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "EF concurrency token.")]
    public byte[] RowVersion { get; init; } = null!;
}
