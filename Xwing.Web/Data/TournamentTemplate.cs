namespace Xwing.Web.Data;



public sealed class TournamentTemplate : BaseModel

{

    public int TournamentTemplateId { get; set; }



    public string Name { get; set; } = string.Empty;

    public int GraphWidthCells { get; set; } = 30;

    public int GraphHeightCells { get; set; } = 30;

    public ICollection<TemplateMatch> Matches { get; } = [];

}


