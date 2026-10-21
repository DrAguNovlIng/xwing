namespace Xwing.Web.Data;

public sealed class TemplateMatchFeed : BaseModel
{
    public int TemplateMatchFeedId { get; set; }

    public int TargetTemplateMatchId { get; set; }

    public TemplateMatch TargetMatch { get; set; } = null!;

    public TemplateMatchSlot TargetSlot { get; set; }

    public int SourceTemplateMatchId { get; set; }

    public TemplateMatch SourceMatch { get; set; } = null!;

    public TemplateFeedOutcome SourceOutcome { get; set; }
}
