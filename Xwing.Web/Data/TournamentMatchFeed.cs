namespace Xwing.Web.Data;
public sealed class TournamentMatchFeed : BaseModel
{
    public int TournamentMatchFeedId { get; set; }
    public int TargetTournamentMatchId { get; set; }
    public TournamentMatch TargetMatch { get; set; } = null!;
    public TemplateMatchSlot TargetSlot { get; set; }
    public int SourceTournamentMatchId { get; set; }
    public TournamentMatch SourceMatch { get; set; } = null!;
    public TemplateFeedOutcome SourceOutcome { get; set; }
}
