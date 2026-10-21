namespace Xwing.Web.Data;



public sealed class TemplateMatch : BaseModel

{

    public int TemplateMatchId { get; set; }



    public int TournamentTemplateId { get; set; }



    public TournamentTemplate TournamentTemplate { get; set; } = null!;



    public string? Name { get; set; }



    public int PositionX { get; set; }



    public int PositionY { get; set; }

    public string? SlotAOpenLabel { get; set; }

    public string? SlotBOpenLabel { get; set; }

    public ICollection<TemplateMatchFeed> IncomingFeeds { get; } = [];

    public ICollection<TemplateMatchFeed> OutgoingFeeds { get; } = [];

}


