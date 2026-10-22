using System.Diagnostics.CodeAnalysis;

namespace Xwing.Web.Data;

public sealed class TournamentMatch : BaseModel
{
    public int TournamentMatchId { get; set; }

    public int TournamentId { get; set; }

    public Tournament Tournament { get; set; } = null!;

    public string? Name { get; set; }

    public int PositionX { get; set; }

    public int PositionY { get; set; }

    public string? SlotAOpenLabel { get; set; }

    public string? SlotBOpenLabel { get; set; }

    public int? RoundNumber { get; set; }

    public TournamentMatchStatus Status { get; set; }

    public TournamentMatchPlayMode PlayMode { get; set; }

    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "EF maps URL as nvarchar.")]
    public string? ReplayUrl { get; set; }

    public int? SlotAUserId { get; set; }

    public User? SlotAUser { get; set; }

    public int? SlotBUserId { get; set; }

    public User? SlotBUser { get; set; }

    public int? SlotAPoints { get; set; }

    public int? SlotBPoints { get; set; }

    public TournamentMatchWinner Winner { get; set; }

    public ICollection<TournamentMatchFeed> IncomingFeeds { get; } = [];

    public ICollection<TournamentMatchFeed> OutgoingFeeds { get; } = [];
}
