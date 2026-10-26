using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;
public interface ITournamentBracketStore
{
    Task<Result<TournamentBracketGraph>> GetGraphAsync(int tournamentId, CancellationToken cancellationToken);

    Task<Result> AssignTemplateAsync(int tournamentId, int tournamentTemplateId, CancellationToken cancellationToken);

    Task<Result<Tournament>> UpdateGraphSizeAsync(

        int tournamentId,
        int graphWidthCells,
        int graphHeightCells,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result<Tournament>> UpdateFormatDescriptionAsync(
        int tournamentId,
        string? formatDescription,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result<Tournament>> UpdateRoundsAsync(
        int tournamentId,
        IReadOnlyList<CanvasRoundInput> rounds,
        byte[] rowVersion,
        CancellationToken cancellationToken);

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

    Task<Result<TournamentMatch>> UpdateReplayCodeAsync(
        int tournamentId,
        int tournamentMatchId,
        string? replayCodeInput,
        int actingUserId,
        bool actingUserIsAdmin,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result<Tournament>> SetFinaleMatchAsync(
        int tournamentId,
        int? tournamentMatchId,
        byte[] rowVersion,
        CancellationToken cancellationToken);
}
