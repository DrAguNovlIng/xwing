using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;
public interface ITournamentStore
{
    Task<Result<Tournament>> CreateAsync(
        string name,
        DateTimeOffset startDateUtc,
        DateTimeOffset endDateUtc,
        bool uniqueFactions,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<TournamentListItem>> ListAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TournamentListItem>> SearchAsync(
        TournamentSearchQuery query,
        CancellationToken cancellationToken);
    Task<Tournament?> GetAsync(int tournamentId, CancellationToken cancellationToken);
    Task<Result<Tournament>> UpdateAsync(
        int tournamentId,
        string name,
        DateTimeOffset startDateUtc,
        DateTimeOffset endDateUtc,
        bool uniqueFactions,
        byte[] rowVersion,
        CancellationToken cancellationToken);
    Task<Result> DeleteAsync(int tournamentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TournamentMember>> ListMembersAsync(
        int tournamentId,
        int? viewingUserId,
        CancellationToken cancellationToken);
    Task<Result> JoinAsync(
        int tournamentId,
        int userId,
        string? factionDisplayName,
        CancellationToken cancellationToken);
    Task<Result> LeaveAsync(int tournamentId, int userId, CancellationToken cancellationToken);
    Task<Result> SetSquadAsync(int tournamentId, int userId, string squadJson, CancellationToken cancellationToken);
    Task<IReadOnlyList<TournamentSquadExport>> ListSquadExportsAsync(
        int tournamentId,
        CancellationToken cancellationToken);

    Task<Result<Tournament>> SetWinnerAsync(
        int tournamentId,
        int? userId,
        byte[] rowVersion,
        CancellationToken cancellationToken);
}
