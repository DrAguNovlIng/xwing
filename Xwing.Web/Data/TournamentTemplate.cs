namespace Xwing.Web.Data;



public sealed class TournamentTemplate : BaseModel

{

    public int TournamentTemplateId { get; set; }



    public string Name { get; set; } = string.Empty;



    public ICollection<TemplateMatch> Matches { get; } = [];

}


