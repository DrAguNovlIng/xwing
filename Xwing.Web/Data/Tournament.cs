namespace Xwing.Web.Data;
public sealed class Tournament : BaseModel
{
    public int TournamentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset StartDateUtc { get; set; }
    public DateTimeOffset EndDateUtc { get; set; }
    public int? TournamentTemplateId { get; set; }
    public TournamentTemplate? TournamentTemplate { get; set; }
    public int GraphWidthCells { get; set; } = 30;
    public int GraphHeightCells { get; set; } = 10;
    public string? FormatDescription { get; set; }
    public bool UniqueFactions { get; set; }
}
