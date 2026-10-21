namespace Xwing.Web.Data;

public sealed class Tournament : BaseModel
{
    public int TournamentId { get; set; }

    public string Name { get; set; } = string.Empty;
}
