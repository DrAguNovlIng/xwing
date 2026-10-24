namespace Xwing.Web.Data;

public sealed class TournamentCanvasRound : BaseModel
{
    public int TournamentCanvasRoundId { get; set; }
    public int TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;
    public int SortOrder { get; set; }
    public int WidthCells { get; set; }
    public XwaScenario? Scenario { get; set; }
}
