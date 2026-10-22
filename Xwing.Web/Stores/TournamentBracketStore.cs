using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;
public sealed class TournamentBracketStore : ITournamentBracketStore
{
    private readonly IDbContextFactory<XwingDbContext> contextFactory;
    private readonly ITournamentTemplateStore templateStore;
    private readonly StoreConcurrencyHelpers concurrencyHelpers;
    public TournamentBracketStore(
        IDbContextFactory<XwingDbContext> contextFactory,
        ITournamentTemplateStore templateStore,
        StoreConcurrencyHelpers concurrencyHelpers)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(templateStore);
        ArgumentNullException.ThrowIfNull(concurrencyHelpers);
        this.contextFactory = contextFactory;
        this.templateStore = templateStore;
        this.concurrencyHelpers = concurrencyHelpers;
    }

    public async Task<Result<TournamentBracketGraph>> GetGraphAsync(int tournamentId, CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Tournament? tournament = await context.Tournaments
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.TournamentId == tournamentId, cancellationToken)
            .ConfigureAwait(false);
        if (tournament is null)
        {
            return Result.Fail<TournamentBracketGraph>("Tournament not found.");
        }

        List<TournamentMatch> matches = await context.TournamentMatches
            .AsNoTracking()
            .Include(m => m.SlotAUser)
            .Include(m => m.SlotBUser)
            .Where(m => m.TournamentId == tournamentId)
            .OrderBy(m => m.TournamentMatchId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        int[] matchIds = matches.Select(m => m.TournamentMatchId).ToArray();
        List<TournamentMatchFeed> feeds = matchIds.Length == 0
            ? []
            : await context.TournamentMatchFeeds
                .AsNoTracking()
                .Where(f => matchIds.Contains(f.TargetTournamentMatchId) || matchIds.Contains(f.SourceTournamentMatchId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        List<TournamentBracketParticipant> participants = await context.TournamentParticipants
            .AsNoTracking()
            .Where(p => p.TournamentId == tournamentId)
            .Join(
                context.Users.AsNoTracking(),
                p => p.UserId,
                u => u.UserId,
                (p, u) => new { u.UserId, u.UserName, u.IsActive })
            .OrderBy(x => x.UserName)
            .Select(x => new TournamentBracketParticipant(x.UserId, x.UserName, x.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result.Ok(new TournamentBracketGraph(tournament, matches, feeds, participants));
    }

    public async Task<Result> AssignTemplateAsync(int tournamentId, int tournamentTemplateId, CancellationToken cancellationToken)
    {
        Result<TournamentTemplateGraph> templateGraph = await templateStore
            .GetGraphAsync(tournamentTemplateId, cancellationToken)
            .ConfigureAwait(false);
        if (!templateGraph.IsSuccess)
        {
            return Result.Fail(templateGraph.Error!);
        }

        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Tournament? tournament = await context.Tournaments
            .SingleOrDefaultAsync(t => t.TournamentId == tournamentId, cancellationToken)
            .ConfigureAwait(false);
        if (tournament is null)
        {
            return Result.Fail("Tournament not found.");
        }

        await ClearBracketAsync(context, tournamentId, cancellationToken).ConfigureAwait(false);
        var idMap = new Dictionary<int, int>();
        foreach (TemplateMatch templateMatch in templateGraph.Value!.Matches)
        {
            var copy = new TournamentMatch
            {
                TournamentId = tournamentId,
                Name = templateMatch.Name,
                PositionX = templateMatch.PositionX,
                PositionY = templateMatch.PositionY,
                SlotAOpenLabel = templateMatch.SlotAOpenLabel,
                SlotBOpenLabel = templateMatch.SlotBOpenLabel,
                RoundNumber = templateMatch.RoundNumber,
                Status = TournamentMatchStatus.NotStarted,
                PlayMode = TournamentMatchPlayMode.Offline,
                Winner = TournamentMatchWinner.None,
            };
            context.TournamentMatches.Add(copy);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            idMap[templateMatch.TemplateMatchId] = copy.TournamentMatchId;
        }

        foreach (TemplateMatchFeed templateFeed in templateGraph.Value.Feeds)
        {
            if (!idMap.TryGetValue(templateFeed.SourceTemplateMatchId, out int sourceId) ||
                !idMap.TryGetValue(templateFeed.TargetTemplateMatchId, out int targetId))
            {
                continue;
            }

            context.TournamentMatchFeeds.Add(new TournamentMatchFeed
            {
                SourceTournamentMatchId = sourceId,
                TargetTournamentMatchId = targetId,
                TargetSlot = templateFeed.TargetSlot,
                SourceOutcome = templateFeed.SourceOutcome,
            });
        }

        TournamentTemplate template = templateGraph.Value!.Template;
        tournament.TournamentTemplateId = tournamentTemplateId;
        tournament.GraphWidthCells = template.GraphWidthCells;
        tournament.GraphHeightCells = template.GraphHeightCells;
        return await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<Tournament>> UpdateGraphSizeAsync(
        int tournamentId,
        int graphWidthCells,
        int graphHeightCells,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        Result? validation = BracketGraphLayout.ValidateGraphSize(graphWidthCells, graphHeightCells);
        if (validation is not null)
        {
            return Result.Fail<Tournament>(validation.Error!);
        }

        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Tournament? tournament = await context.Tournaments
            .SingleOrDefaultAsync(t => t.TournamentId == tournamentId, cancellationToken)
            .ConfigureAwait(false);
        if (tournament is null)
        {
            return Result.Fail<Tournament>("Tournament not found.");
        }

        tournament.GraphWidthCells = graphWidthCells;
        tournament.GraphHeightCells = graphHeightCells;
        context.Entry(tournament).Property(e => e.RowVersion).OriginalValue = rowVersion;
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<Tournament>(saveResult.Error!);
        }

        return Result.Ok(tournament);
    }

    public async Task<Result<TournamentMatch>> AddMatchAsync(int tournamentId, CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Tournament? tournament = await context.Tournaments
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.TournamentId == tournamentId, cancellationToken)
            .ConfigureAwait(false);
        if (tournament is null)
        {
            return Result.Fail<TournamentMatch>("Tournament not found.");
        }

        (int positionX, int positionY) = BracketGraphLayout.CenterMatchPosition(
            tournament.GraphWidthCells,
            tournament.GraphHeightCells);
        var match = new TournamentMatch
        {
            TournamentId = tournamentId,
            PositionX = positionX,
            PositionY = positionY,
            Status = TournamentMatchStatus.NotStarted,
            PlayMode = TournamentMatchPlayMode.Offline,
            Winner = TournamentMatchWinner.None,
        };
        context.TournamentMatches.Add(match);
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<TournamentMatch>(saveResult.Error!);
        }

        return Result.Ok(match);
    }

    public async Task<Result<TournamentMatch>> UpdateMatchNameAsync(
        int tournamentMatchId,
        string? name,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        return await UpdateMatchAsync(
            tournamentMatchId,
            rowVersion,
            match => match.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<TournamentMatch>> UpdateMatchPositionAsync(
        int tournamentMatchId,
        int positionX,
        int positionY,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        return await UpdateMatchAsync(
            tournamentMatchId,
            rowVersion,
            match =>
            {
                match.PositionX = positionX;
                match.PositionY = positionY;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<TournamentMatch>> UpdateMatchRoundAsync(
        int tournamentMatchId,
        int? roundNumber,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        if (roundNumber is < 1)
        {
            return Result.Fail<TournamentMatch>("Round must be at least 1.");
        }

        return await UpdateMatchAsync(
            tournamentMatchId,
            rowVersion,
            match => match.RoundNumber = roundNumber,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> DeleteMatchAsync(int tournamentMatchId, byte[] rowVersion, CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TournamentMatch? match = await context.TournamentMatches
            .SingleOrDefaultAsync(m => m.TournamentMatchId == tournamentMatchId, cancellationToken)
            .ConfigureAwait(false);
        if (match is null)
        {
            return Result.Fail("Match not found.");
        }

        List<TournamentMatchFeed> relatedFeeds = await context.TournamentMatchFeeds
            .Where(f => f.SourceTournamentMatchId == tournamentMatchId || f.TargetTournamentMatchId == tournamentMatchId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.TournamentMatchFeeds.RemoveRange(relatedFeeds);
        context.Entry(match).Property(e => e.RowVersion).OriginalValue = rowVersion;
        context.TournamentMatches.Remove(match);
        return await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> SetSlotFeedAsync(
        int targetTournamentMatchId,
        TemplateMatchSlot targetSlot,
        int? sourceTournamentMatchId,
        TemplateFeedOutcome? sourceOutcome,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TournamentMatch? targetMatch = await context.TournamentMatches
            .SingleOrDefaultAsync(m => m.TournamentMatchId == targetTournamentMatchId, cancellationToken)
            .ConfigureAwait(false);
        if (targetMatch is null)
        {
            return Result.Fail("Match not found.");
        }

        context.Entry(targetMatch).Property(e => e.RowVersion).OriginalValue = rowVersion;
        TournamentMatchFeed? existingSlotFeed = await context.TournamentMatchFeeds
            .SingleOrDefaultAsync(
                f => f.TargetTournamentMatchId == targetTournamentMatchId && f.TargetSlot == targetSlot,
                cancellationToken)
            .ConfigureAwait(false);
        if (sourceTournamentMatchId is null || sourceOutcome is null)
        {
            if (existingSlotFeed is not null)
            {
                context.TournamentMatchFeeds.Remove(existingSlotFeed);
            }

            return await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        }

        if (sourceTournamentMatchId == targetTournamentMatchId)
        {
            return Result.Fail("A match cannot feed its own slot.");
        }

        TournamentMatch? sourceMatch = await context.TournamentMatches
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.TournamentMatchId == sourceTournamentMatchId, cancellationToken)
            .ConfigureAwait(false);
        if (sourceMatch is null)
        {
            return Result.Fail("Source match not found.");
        }

        if (sourceMatch.TournamentId != targetMatch.TournamentId)
        {
            return Result.Fail("Source match belongs to a different tournament.");
        }

        TournamentMatchFeed? existingOutcomeFeed = await context.TournamentMatchFeeds
            .SingleOrDefaultAsync(
                f => f.SourceTournamentMatchId == sourceTournamentMatchId && f.SourceOutcome == sourceOutcome,
                cancellationToken)
            .ConfigureAwait(false);
        if (existingOutcomeFeed is not null
            && (existingSlotFeed is null || existingOutcomeFeed.TournamentMatchFeedId != existingSlotFeed.TournamentMatchFeedId))
        {
            return Result.Fail("That outcome already feeds another slot.");
        }

        int tournamentId = targetMatch.TournamentId;
        int[] tournamentMatchIds = await context.TournamentMatches
            .AsNoTracking()
            .Where(m => m.TournamentId == tournamentId)
            .Select(m => m.TournamentMatchId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        List<TournamentMatchFeed> tournamentFeeds = tournamentMatchIds.Length == 0
            ? []
            : await context.TournamentMatchFeeds
                .AsNoTracking()
                .Where(f => tournamentMatchIds.Contains(f.TargetTournamentMatchId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        List<(int Source, int Target)> edges = tournamentFeeds
            .Where(f => existingSlotFeed is null || f.TournamentMatchFeedId != existingSlotFeed.TournamentMatchFeedId)
            .Select(f => (f.SourceTournamentMatchId, f.TargetTournamentMatchId))
            .ToList();
        edges.Add((sourceTournamentMatchId.Value, targetTournamentMatchId));
        if (MatchFeedCycleHelper.WouldCreateCycle(sourceTournamentMatchId.Value, targetTournamentMatchId, edges))
        {
            return Result.Fail("That connection would create a cycle.");
        }

        if (existingSlotFeed is not null)
        {
            existingSlotFeed.SourceTournamentMatchId = sourceTournamentMatchId.Value;
            existingSlotFeed.SourceOutcome = sourceOutcome.Value;
        }
        else
        {
            context.TournamentMatchFeeds.Add(new TournamentMatchFeed
            {
                TargetTournamentMatchId = targetTournamentMatchId,
                TargetSlot = targetSlot,
                SourceTournamentMatchId = sourceTournamentMatchId.Value,
                SourceOutcome = sourceOutcome.Value,
            });
        }

        ClearOpenLabel(targetMatch, targetSlot);
        return await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<TournamentMatch>> UpdateSlotOpenLabelAsync(
        int tournamentMatchId,
        TemplateMatchSlot slot,
        string? openLabel,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TournamentMatch? match = await context.TournamentMatches
            .SingleOrDefaultAsync(m => m.TournamentMatchId == tournamentMatchId, cancellationToken)
            .ConfigureAwait(false);
        if (match is null)
        {
            return Result.Fail<TournamentMatch>("Match not found.");
        }

        bool hasFeed = await context.TournamentMatchFeeds
            .AnyAsync(
                f => f.TargetTournamentMatchId == tournamentMatchId && f.TargetSlot == slot,
                cancellationToken)
            .ConfigureAwait(false);
        if (hasFeed)
        {
            return Result.Fail<TournamentMatch>("Clear the feed before setting custom entrant text.");
        }

        string? trimmed = string.IsNullOrWhiteSpace(openLabel) ? null : openLabel.Trim();
        if (slot == TemplateMatchSlot.A)
        {
            match.SlotAOpenLabel = trimmed;
        }
        else
        {
            match.SlotBOpenLabel = trimmed;
        }

        context.Entry(match).Property(e => e.RowVersion).OriginalValue = rowVersion;
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<TournamentMatch>(saveResult.Error!);
        }

        return Result.Ok(match);
    }

    public async Task<Result<TournamentMatch>> UpdateMatchResultAsync(
        int tournamentId,
        int tournamentMatchId,
        TournamentMatchResultUpdate update,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        Result? validation = ValidateResultUpdate(tournamentId, update);
        if (validation is not null)
        {
            return Result.Fail<TournamentMatch>(validation.Error!);
        }

        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TournamentMatch? match = await context.TournamentMatches
            .SingleOrDefaultAsync(
                m => m.TournamentMatchId == tournamentMatchId && m.TournamentId == tournamentId,
                cancellationToken)
            .ConfigureAwait(false);
        if (match is null)
        {
            return Result.Fail<TournamentMatch>("Match not found.");
        }

        Result? participantValidation = await ValidateParticipantsAsync(
            context,
            tournamentId,
            update,
            match,
            cancellationToken).ConfigureAwait(false);
        if (participantValidation is not null)
        {
            return Result.Fail<TournamentMatch>(participantValidation.Error!);
        }

        string? replayUrl = NormalizeReplayUrl(update.ReplayUrl);
        if (update.ReplayUrl is not null && replayUrl is null && !string.IsNullOrWhiteSpace(update.ReplayUrl))
        {
            return Result.Fail<TournamentMatch>("Replay URL must be a valid http or https address.");
        }

        match.Status = update.Status;
        match.PlayMode = update.PlayMode;
        match.ReplayUrl = replayUrl;
        match.SlotAUserId = update.SlotAUserId;
        match.SlotBUserId = update.SlotBUserId;
        match.SlotAPoints = update.SlotAPoints;
        match.SlotBPoints = update.SlotBPoints;
        match.Winner = update.Winner;
        context.Entry(match).Property(e => e.RowVersion).OriginalValue = rowVersion;
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<TournamentMatch>(saveResult.Error!);
        }

        return Result.Ok(match);
    }

    private static Result? ValidateResultUpdate(int tournamentId, TournamentMatchResultUpdate update)
    {
        if (tournamentId <= 0)
        {
            return Result.Fail("Tournament not found.");
        }

        if (update.SlotAPoints is < 0 || update.SlotBPoints is < 0)
        {
            return Result.Fail("Points cannot be negative.");
        }

        if (update.SlotAUserId is not null && update.SlotBUserId is not null &&
            update.SlotAUserId == update.SlotBUserId)
        {
            return Result.Fail("Each slot must have a different player.");
        }

        return update.Winner switch
        {
            TournamentMatchWinner.SlotA when update.SlotAUserId is null =>
                Result.Fail("Winner slot A requires a player in slot A."),
            TournamentMatchWinner.SlotB when update.SlotBUserId is null =>
                Result.Fail("Winner slot B requires a player in slot B."),
            _ => null,
        };
    }

    private static async Task<Result?> ValidateParticipantsAsync(
        XwingDbContext context,
        int tournamentId,
        TournamentMatchResultUpdate update,
        TournamentMatch match,
        CancellationToken cancellationToken)
    {
        int[] userIds = new int?[] { update.SlotAUserId, update.SlotBUserId }
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        if (userIds.Length == 0)
        {
            return null;
        }

        HashSet<int> participantIds = await context.TournamentParticipants
            .AsNoTracking()
            .Where(p => p.TournamentId == tournamentId)
            .Select(p => p.UserId)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (int userId in userIds)
        {
            bool onMatchAlready = match.SlotAUserId == userId || match.SlotBUserId == userId;
            if (!participantIds.Contains(userId) && !onMatchAlready)
            {
                return Result.Fail("Each assigned player must be in the tournament.");
            }
        }

        List<User> users = await context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (int userId in userIds)
        {
            User? user = users.SingleOrDefault(u => u.UserId == userId);
            if (user is null)
            {
                return Result.Fail("Player not found.");
            }

            if (user.IsActive)
            {
                continue;
            }

            bool keptOnSlotA = update.SlotAUserId == userId && match.SlotAUserId == userId;
            bool keptOnSlotB = update.SlotBUserId == userId && match.SlotBUserId == userId;
            if (!keptOnSlotA && !keptOnSlotB)
            {
                return Result.Fail("Inactive players cannot be newly assigned to a match.");
            }
        }

        return null;
    }

    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Parsed before persistence.")]
    private static string? NormalizeReplayUrl(string? replayUrl)
    {
        if (string.IsNullOrWhiteSpace(replayUrl))
        {
            return null;
        }

        string trimmed = replayUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return trimmed;
    }

    private async Task<Result<TournamentMatch>> UpdateMatchAsync(
        int tournamentMatchId,
        byte[] rowVersion,
        Action<TournamentMatch> apply,
        CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TournamentMatch? match = await context.TournamentMatches
            .SingleOrDefaultAsync(m => m.TournamentMatchId == tournamentMatchId, cancellationToken)
            .ConfigureAwait(false);
        if (match is null)
        {
            return Result.Fail<TournamentMatch>("Match not found.");
        }

        apply(match);
        context.Entry(match).Property(e => e.RowVersion).OriginalValue = rowVersion;
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<TournamentMatch>(saveResult.Error!);
        }

        return Result.Ok(match);
    }

    private static async Task ClearBracketAsync(XwingDbContext context, int tournamentId, CancellationToken cancellationToken)
    {
        int[] matchIds = await context.TournamentMatches
            .Where(m => m.TournamentId == tournamentId)
            .Select(m => m.TournamentMatchId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (matchIds.Length == 0)
        {
            return;
        }

        List<TournamentMatchFeed> feeds = await context.TournamentMatchFeeds
            .Where(f => matchIds.Contains(f.SourceTournamentMatchId) || matchIds.Contains(f.TargetTournamentMatchId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.TournamentMatchFeeds.RemoveRange(feeds);
        List<TournamentMatch> matches = await context.TournamentMatches
            .Where(m => m.TournamentId == tournamentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.TournamentMatches.RemoveRange(matches);
    }

    private static void ClearOpenLabel(TournamentMatch match, TemplateMatchSlot slot)
    {
        if (slot == TemplateMatchSlot.A)
        {
            match.SlotAOpenLabel = null;
        }
        else
        {
            match.SlotBOpenLabel = null;
        }
    }
}
