namespace Xwing.Web.Data;

public sealed class TournamentParticipant : BaseModel
{
    public int TournamentParticipantId { get; set; }
    public int TournamentId { get; set; }
    public int UserId { get; set; }

    public Tournament Tournament { get; set; } = null!;

    public User User { get; set; } = null!;
}
