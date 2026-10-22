using System.Diagnostics.CodeAnalysis;

namespace Xwing.Web.Data;
public abstract class BaseModel
{
    public DateTimeOffset TimeCreatedUtc { get; set; }
    public DateTimeOffset TimeUpdatedUtc { get; set; }
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "EF Core rowversion concurrency token.")]
    public byte[] RowVersion { get; set; } = null!;
}
