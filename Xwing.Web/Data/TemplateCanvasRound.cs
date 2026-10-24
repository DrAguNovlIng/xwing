namespace Xwing.Web.Data;

public sealed class TemplateCanvasRound : BaseModel
{
    public int TemplateCanvasRoundId { get; set; }
    public int TournamentTemplateId { get; set; }
    public TournamentTemplate TournamentTemplate { get; set; } = null!;
    public int SortOrder { get; set; }
    public int WidthCells { get; set; }
    public XwaScenario? Scenario { get; set; }
}
