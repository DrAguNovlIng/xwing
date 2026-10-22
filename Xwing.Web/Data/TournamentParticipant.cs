using System.Diagnostics.CodeAnalysis;

namespace Xwing.Web.Data;
public sealed class TournamentParticipant : BaseModel
{
    public int TournamentParticipantId { get; set; }
    public int TournamentId { get; set; }
    public int UserId { get; set; }
    public string? SquadFaction { get; set; }
    public string? SquadShipsJson { get; set; }
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "EF maps URL as nvarchar for squad builder links.")]
    public string? SquadUrl { get; set; }
    public Tournament Tournament { get; set; } = null!;
    public User User { get; set; } = null!;
}
