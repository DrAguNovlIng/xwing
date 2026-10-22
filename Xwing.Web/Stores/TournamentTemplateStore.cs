using Microsoft.EntityFrameworkCore;
using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;
public sealed class TournamentTemplateStore : ITournamentTemplateStore
{
    private const int NewMatchColumnsPerRow = 12;
    private const int NewMatchStrideX = 5;
    private const int NewMatchStrideY = 3;
    private readonly IDbContextFactory<XwingDbContext> contextFactory;
    private readonly StoreConcurrencyHelpers concurrencyHelpers;
    public TournamentTemplateStore(
        IDbContextFactory<XwingDbContext> contextFactory,
        StoreConcurrencyHelpers concurrencyHelpers)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(concurrencyHelpers);
        this.contextFactory = contextFactory;
        this.concurrencyHelpers = concurrencyHelpers;
    }

    public async Task<IReadOnlyList<TournamentTemplate>> ListAsync(CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TournamentTemplates
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<TournamentTemplateGraph>> GetGraphAsync(
        int tournamentTemplateId,
        CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TournamentTemplate? template = await context.TournamentTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.TournamentTemplateId == tournamentTemplateId, cancellationToken)
            .ConfigureAwait(false);
        if (template is null)
        {
            return Result.Fail<TournamentTemplateGraph>("Template not found.");
        }
        List<TemplateMatch> matches = await context.TemplateMatches
            .AsNoTracking()
            .Where(m => m.TournamentTemplateId == tournamentTemplateId)
            .OrderBy(m => m.TemplateMatchId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        int[] matchIds = matches.Select(m => m.TemplateMatchId).ToArray();
        List<TemplateMatchFeed> feeds = matchIds.Length == 0
            ? []
            : await context.TemplateMatchFeeds
                .AsNoTracking()
                .Where(f => matchIds.Contains(f.TargetTemplateMatchId) || matchIds.Contains(f.SourceTemplateMatchId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        return Result.Ok(new TournamentTemplateGraph(template, matches, feeds));
    }

    public async Task<Result<TournamentTemplate>> CreateAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Fail<TournamentTemplate>("Name is required.");
        }
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var template = new TournamentTemplate
        {
            Name = name.Trim(),
            GraphWidthCells = BracketGraphLayout.DefaultGraphWidthCells,
            GraphHeightCells = BracketGraphLayout.DefaultGraphHeightCells,
        };
        context.TournamentTemplates.Add(template);
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<TournamentTemplate>(saveResult.Error!);
        }
        return Result.Ok(template);
    }

    public async Task<Result<TournamentTemplate>> UpdateNameAsync(
        int tournamentTemplateId,
        string name,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Fail<TournamentTemplate>("Name is required.");
        }
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TournamentTemplate? template = await context.TournamentTemplates
            .SingleOrDefaultAsync(t => t.TournamentTemplateId == tournamentTemplateId, cancellationToken)
            .ConfigureAwait(false);
        if (template is null)
        {
            return Result.Fail<TournamentTemplate>("Template not found.");
        }
        template.Name = name.Trim();
        context.Entry(template).Property(e => e.RowVersion).OriginalValue = rowVersion;
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<TournamentTemplate>(saveResult.Error!);
        }
        return Result.Ok(template);
    }

    public async Task<Result<TournamentTemplate>> UpdateGraphSizeAsync(
        int tournamentTemplateId,
        int graphWidthCells,
        int graphHeightCells,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        Result? validation = BracketGraphLayout.ValidateGraphSize(graphWidthCells, graphHeightCells);
        if (validation is not null)
        {
            return Result.Fail<TournamentTemplate>(validation.Error!);
        }

        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TournamentTemplate? template = await context.TournamentTemplates
            .SingleOrDefaultAsync(t => t.TournamentTemplateId == tournamentTemplateId, cancellationToken)
            .ConfigureAwait(false);
        if (template is null)
        {
            return Result.Fail<TournamentTemplate>("Template not found.");
        }

        template.GraphWidthCells = graphWidthCells;
        template.GraphHeightCells = graphHeightCells;
        context.Entry(template).Property(e => e.RowVersion).OriginalValue = rowVersion;
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<TournamentTemplate>(saveResult.Error!);
        }

        return Result.Ok(template);
    }

    public async Task<Result> DeleteAsync(int tournamentTemplateId, byte[] rowVersion, CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TournamentTemplate? template = await context.TournamentTemplates
            .SingleOrDefaultAsync(t => t.TournamentTemplateId == tournamentTemplateId, cancellationToken)
            .ConfigureAwait(false);
        if (template is null)
        {
            return Result.Fail("Template not found.");
        }
        int[] matchIds = await context.TemplateMatches
            .AsNoTracking()
            .Where(m => m.TournamentTemplateId == tournamentTemplateId)
            .Select(m => m.TemplateMatchId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (matchIds.Length > 0)
        {
            List<TemplateMatchFeed> feeds = await context.TemplateMatchFeeds
                .Where(f => matchIds.Contains(f.SourceTemplateMatchId) || matchIds.Contains(f.TargetTemplateMatchId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            context.TemplateMatchFeeds.RemoveRange(feeds);
            List<TemplateMatch> matches = await context.TemplateMatches
                .Where(m => m.TournamentTemplateId == tournamentTemplateId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            context.TemplateMatches.RemoveRange(matches);
        }
        context.Entry(template).Property(e => e.RowVersion).OriginalValue = rowVersion;
        context.TournamentTemplates.Remove(template);
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        return saveResult;
    }

    public async Task<Result<TemplateMatch>> AddMatchAsync(int tournamentTemplateId, CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        bool templateExists = await context.TournamentTemplates
            .AsNoTracking()
            .AnyAsync(t => t.TournamentTemplateId == tournamentTemplateId, cancellationToken)
            .ConfigureAwait(false);
        if (!templateExists)
        {
            return Result.Fail<TemplateMatch>("Template not found.");
        }
        int matchCount = await context.TemplateMatches
            .CountAsync(m => m.TournamentTemplateId == tournamentTemplateId, cancellationToken)
            .ConfigureAwait(false);
        var match = new TemplateMatch
        {
            TournamentTemplateId = tournamentTemplateId,
            PositionX = (matchCount % NewMatchColumnsPerRow) * NewMatchStrideX,
            PositionY = (matchCount / NewMatchColumnsPerRow) * NewMatchStrideY,
        };
        context.TemplateMatches.Add(match);
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<TemplateMatch>(saveResult.Error!);
        }
        return Result.Ok(match);
    }

    public async Task<Result<TemplateMatch>> UpdateMatchNameAsync(
        int templateMatchId,
        string? name,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TemplateMatch? match = await context.TemplateMatches
            .SingleOrDefaultAsync(m => m.TemplateMatchId == templateMatchId, cancellationToken)
            .ConfigureAwait(false);
        if (match is null)
        {
            return Result.Fail<TemplateMatch>("Match not found.");
        }
        match.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        context.Entry(match).Property(e => e.RowVersion).OriginalValue = rowVersion;
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<TemplateMatch>(saveResult.Error!);
        }
        return Result.Ok(match);
    }

    public async Task<Result<TemplateMatch>> UpdateMatchPositionAsync(
        int templateMatchId,
        int positionX,
        int positionY,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TemplateMatch? match = await context.TemplateMatches
            .SingleOrDefaultAsync(m => m.TemplateMatchId == templateMatchId, cancellationToken)
            .ConfigureAwait(false);
        if (match is null)
        {
            return Result.Fail<TemplateMatch>("Match not found.");
        }
        match.PositionX = positionX;
        match.PositionY = positionY;
        context.Entry(match).Property(e => e.RowVersion).OriginalValue = rowVersion;
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<TemplateMatch>(saveResult.Error!);
        }
        return Result.Ok(match);
    }

    public async Task<Result> DeleteMatchAsync(int templateMatchId, byte[] rowVersion, CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TemplateMatch? match = await context.TemplateMatches
            .SingleOrDefaultAsync(m => m.TemplateMatchId == templateMatchId, cancellationToken)
            .ConfigureAwait(false);
        if (match is null)
        {
            return Result.Fail("Match not found.");
        }
        List<TemplateMatchFeed> relatedFeeds = await context.TemplateMatchFeeds
            .Where(f => f.SourceTemplateMatchId == templateMatchId || f.TargetTemplateMatchId == templateMatchId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.TemplateMatchFeeds.RemoveRange(relatedFeeds);
        context.Entry(match).Property(e => e.RowVersion).OriginalValue = rowVersion;
        context.TemplateMatches.Remove(match);
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        return saveResult;
    }

    public async Task<Result> SetSlotFeedAsync(
        int targetTemplateMatchId,
        TemplateMatchSlot targetSlot,
        int? sourceTemplateMatchId,
        TemplateFeedOutcome? sourceOutcome,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TemplateMatch? targetMatch = await context.TemplateMatches
            .SingleOrDefaultAsync(m => m.TemplateMatchId == targetTemplateMatchId, cancellationToken)
            .ConfigureAwait(false);
        if (targetMatch is null)
        {
            return Result.Fail("Match not found.");
        }
        context.Entry(targetMatch).Property(e => e.RowVersion).OriginalValue = rowVersion;
        TemplateMatchFeed? existingSlotFeed = await context.TemplateMatchFeeds
            .SingleOrDefaultAsync(
                f => f.TargetTemplateMatchId == targetTemplateMatchId && f.TargetSlot == targetSlot,
                cancellationToken)
            .ConfigureAwait(false);
        if (sourceTemplateMatchId is null || sourceOutcome is null)
        {
            if (existingSlotFeed is not null)
            {
                context.TemplateMatchFeeds.Remove(existingSlotFeed);
            }
            return await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        }

        if (sourceTemplateMatchId == targetTemplateMatchId)
        {
            return Result.Fail("A match cannot feed its own slot.");
        }

        TemplateMatch? sourceMatch = await context.TemplateMatches
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.TemplateMatchId == sourceTemplateMatchId, cancellationToken)
            .ConfigureAwait(false);
        if (sourceMatch is null)
        {
            return Result.Fail("Source match not found.");
        }
        if (sourceMatch.TournamentTemplateId != targetMatch.TournamentTemplateId)
        {
            return Result.Fail("Source match belongs to a different template.");
        }

        TemplateMatchFeed? existingOutcomeFeed = await context.TemplateMatchFeeds
            .SingleOrDefaultAsync(
                f => f.SourceTemplateMatchId == sourceTemplateMatchId && f.SourceOutcome == sourceOutcome,
                cancellationToken)
            .ConfigureAwait(false);
        if (existingOutcomeFeed is not null
            && (existingSlotFeed is null || existingOutcomeFeed.TemplateMatchFeedId != existingSlotFeed.TemplateMatchFeedId))
        {
            return Result.Fail("That outcome already feeds another slot.");
        }

        int tournamentTemplateId = targetMatch.TournamentTemplateId;
        int[] templateMatchIds = await context.TemplateMatches
            .AsNoTracking()
            .Where(m => m.TournamentTemplateId == tournamentTemplateId)
            .Select(m => m.TemplateMatchId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        List<TemplateMatchFeed> templateFeeds = templateMatchIds.Length == 0
            ? []
            : await context.TemplateMatchFeeds
                .AsNoTracking()
                .Where(f => templateMatchIds.Contains(f.TargetTemplateMatchId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        List<(int Source, int Target)> edges = templateFeeds
            .Where(f => existingSlotFeed is null || f.TemplateMatchFeedId != existingSlotFeed.TemplateMatchFeedId)
            .Select(f => (f.SourceTemplateMatchId, f.TargetTemplateMatchId))
            .ToList();
        edges.Add((sourceTemplateMatchId.Value, targetTemplateMatchId));
        if (MatchFeedCycleHelper.WouldCreateCycle(sourceTemplateMatchId.Value, targetTemplateMatchId, edges))
        {
            return Result.Fail("That connection would create a cycle.");
        }

        if (existingSlotFeed is not null)
        {
            existingSlotFeed.SourceTemplateMatchId = sourceTemplateMatchId.Value;
            existingSlotFeed.SourceOutcome = sourceOutcome.Value;
        }
        else
        {
            context.TemplateMatchFeeds.Add(new TemplateMatchFeed
            {
                TargetTemplateMatchId = targetTemplateMatchId,
                TargetSlot = targetSlot,
                SourceTemplateMatchId = sourceTemplateMatchId.Value,
                SourceOutcome = sourceOutcome.Value,
            });
        }

        ClearOpenLabel(targetMatch, targetSlot);
        return await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<TemplateMatch>> UpdateSlotOpenLabelAsync(
        int templateMatchId,
        TemplateMatchSlot slot,
        string? openLabel,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TemplateMatch? match = await context.TemplateMatches
            .SingleOrDefaultAsync(m => m.TemplateMatchId == templateMatchId, cancellationToken)
            .ConfigureAwait(false);
        if (match is null)
        {
            return Result.Fail<TemplateMatch>("Match not found.");
        }
        bool hasFeed = await context.TemplateMatchFeeds
            .AnyAsync(
                f => f.TargetTemplateMatchId == templateMatchId && f.TargetSlot == slot,
                cancellationToken)
            .ConfigureAwait(false);
        if (hasFeed)
        {
            return Result.Fail<TemplateMatch>("Clear the feed before setting custom entrant text.");
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
            return Result.Fail<TemplateMatch>(saveResult.Error!);
        }
        return Result.Ok(match);
    }

    private static void ClearOpenLabel(TemplateMatch match, TemplateMatchSlot slot)
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

    public async Task<Result<TemplateMatch>> UpdateMatchRoundAsync(
        int templateMatchId,
        int? roundNumber,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        if (roundNumber is < 1)
        {
            return Result.Fail<TemplateMatch>("Round must be at least 1.");
        }

        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TemplateMatch? match = await context.TemplateMatches
            .SingleOrDefaultAsync(m => m.TemplateMatchId == templateMatchId, cancellationToken)
            .ConfigureAwait(false);
        if (match is null)
        {
            return Result.Fail<TemplateMatch>("Match not found.");
        }

        match.RoundNumber = roundNumber;
        context.Entry(match).Property(e => e.RowVersion).OriginalValue = rowVersion;
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<TemplateMatch>(saveResult.Error!);
        }

        return Result.Ok(match);
    }
}
