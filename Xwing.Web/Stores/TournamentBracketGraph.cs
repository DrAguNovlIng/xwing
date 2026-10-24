using System.Diagnostics.CodeAnalysis;
using Xwing.Web.Data;

namespace Xwing.Web.Stores;
public sealed record TournamentBracketGraph(
    Tournament Tournament,
    IReadOnlyList<TournamentMatch> Matches,
    IReadOnlyList<TournamentMatchFeed> Feeds,
    IReadOnlyList<TournamentBracketParticipant> Participants,
    IReadOnlyList<TournamentCanvasRound> CanvasRounds);
public sealed record TournamentBracketParticipant(int UserId, string UserName, bool IsActive);
public sealed record TournamentMatchResultUpdate(
    TournamentMatchStatus Status,
    TournamentMatchPlayMode PlayMode,
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Form binding uses string URLs.")]
    string? ReplayUrl,
    int? SlotAUserId,
    int? SlotBUserId,
    int? SlotAPoints,
    int? SlotBPoints,
    TournamentMatchWinner Winner);
