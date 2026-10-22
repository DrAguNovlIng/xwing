using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;

public interface ITournamentBracketStore
{
    Task<Result<TournamentBracketGraph>> GetGraphAsync(int tournamentId, CancellationToken cancellationToken);

    Task<Result> AssignTemplateAsync(int tournamentId, int tournamentTemplateId, CancellationToken cancellationToken);

    Task<Result<TournamentMatch>> AddMatchAsync(int tournamentId, CancellationToken cancellationToken);

    Task<Result<TournamentMatch>> UpdateMatchNameAsync(
        int tournamentMatchId,
        string? name,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result<TournamentMatch>> UpdateMatchPositionAsync(
        int tournamentMatchId,
        int positionX,
        int positionY,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result<TournamentMatch>> UpdateMatchRoundAsync(
        int tournamentMatchId,
        int? roundNumber,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result> DeleteMatchAsync(int tournamentMatchId, byte[] rowVersion, CancellationToken cancellationToken);

    Task<Result> SetSlotFeedAsync(
        int targetTournamentMatchId,
        TemplateMatchSlot targetSlot,
        int? sourceTournamentMatchId,
        TemplateFeedOutcome? sourceOutcome,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result<TournamentMatch>> UpdateSlotOpenLabelAsync(
        int tournamentMatchId,
        TemplateMatchSlot slot,
        string? openLabel,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result<TournamentMatch>> UpdateMatchResultAsync(
        int tournamentId,
        int tournamentMatchId,
        TournamentMatchResultUpdate update,
        byte[] rowVersion,
        CancellationToken cancellationToken);
}
