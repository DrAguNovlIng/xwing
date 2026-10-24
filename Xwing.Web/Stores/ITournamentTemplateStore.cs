using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;

public interface ITournamentTemplateStore
{
    Task<IReadOnlyList<TournamentTemplate>> ListAsync(CancellationToken cancellationToken);

    Task<Result<TournamentTemplateGraph>> GetGraphAsync(int tournamentTemplateId, CancellationToken cancellationToken);

    Task<Result<TournamentTemplate>> CreateAsync(string name, CancellationToken cancellationToken);

    Task<Result<TournamentTemplate>> UpdateNameAsync(
        int tournamentTemplateId,
        string name,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result<TournamentTemplate>> UpdateGraphSizeAsync(
        int tournamentTemplateId,
        int graphWidthCells,
        int graphHeightCells,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result<TournamentTemplate>> UpdateFormatDescriptionAsync(
        int tournamentTemplateId,
        string? formatDescription,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result<TournamentTemplate>> UpdateRoundsAsync(
        int tournamentTemplateId,
        IReadOnlyList<CanvasRoundInput> rounds,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result> DeleteAsync(int tournamentTemplateId, byte[] rowVersion, CancellationToken cancellationToken);

    Task<Result<TemplateMatch>> AddMatchAsync(int tournamentTemplateId, CancellationToken cancellationToken);

    Task<Result<TemplateMatch>> UpdateMatchNameAsync(
        int templateMatchId,
        string? name,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result<TemplateMatch>> UpdateMatchPositionAsync(
        int templateMatchId,
        int positionX,
        int positionY,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result> DeleteMatchAsync(int templateMatchId, byte[] rowVersion, CancellationToken cancellationToken);

    Task<Result> SetSlotFeedAsync(
        int targetTemplateMatchId,
        TemplateMatchSlot targetSlot,
        int? sourceTemplateMatchId,
        TemplateFeedOutcome? sourceOutcome,
        byte[] rowVersion,
        CancellationToken cancellationToken);

    Task<Result<TemplateMatch>> UpdateSlotOpenLabelAsync(
        int templateMatchId,
        TemplateMatchSlot slot,
        string? openLabel,
        byte[] rowVersion,
        CancellationToken cancellationToken);
}

public sealed record TournamentTemplateGraph(
    TournamentTemplate Template,
    IReadOnlyList<TemplateMatch> Matches,
    IReadOnlyList<TemplateMatchFeed> Feeds,
    IReadOnlyList<TemplateCanvasRound> CanvasRounds);
