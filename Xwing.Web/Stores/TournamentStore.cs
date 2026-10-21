using Microsoft.EntityFrameworkCore;
using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;

public sealed class TournamentStore : ITournamentStore
{
    private readonly IDbContextFactory<XwingDbContext> contextFactory;

    private readonly StoreConcurrencyHelpers concurrencyHelpers;

    private readonly TimeProvider timeProvider;

    public TournamentStore(
        IDbContextFactory<XwingDbContext> contextFactory,
        StoreConcurrencyHelpers concurrencyHelpers,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(concurrencyHelpers);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.contextFactory = contextFactory;
        this.concurrencyHelpers = concurrencyHelpers;
        this.timeProvider = timeProvider;
    }

    public async Task<Result<Tournament>> CreateAsync(
        string name,
        DateTimeOffset startDateUtc,
        DateTimeOffset endDateUtc,
        CancellationToken cancellationToken)
    {
        Result? validation = ValidateTournamentInput(name, startDateUtc, endDateUtc);
        if (validation is not null)
        {
            return Result.Fail<Tournament>(validation.Error!);
        }
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var tournament = new Tournament
        {
            Name = name.Trim(),
            StartDateUtc = NormalizeToUtcDate(startDateUtc),
            EndDateUtc = NormalizeToUtcDate(endDateUtc),
        };
        context.Tournaments.Add(tournament);
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<Tournament>(saveResult.Error!);
        }
        return Result.Ok(tournament);
    }

    public async Task<IReadOnlyList<TournamentListItem>> ListAsync(CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        List<Tournament> tournaments = await context.Tournaments
            .AsNoTracking()
            .OrderBy(t => t.StartDateUtc)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return await ToListItemsAsync(context, tournaments, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TournamentListItem>> SearchAsync(
        TournamentSearchQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        IQueryable<Tournament> tournaments = context.Tournaments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.NameSearch))
        {
            string pattern = $"%{query.NameSearch.Trim()}%";
            tournaments = tournaments.Where(t => EF.Functions.Like(t.Name, pattern));
        }
        DateTimeOffset todayUtc = TournamentSchedule.TodayUtc(timeProvider);
        tournaments = query.WhenFilter switch
        {
            TournamentWhenFilter.Active => tournaments.Where(t => t.EndDateUtc >= todayUtc),
            TournamentWhenFilter.Past => tournaments.Where(t => t.EndDateUtc < todayUtc),
            _ => tournaments,
        };
        tournaments = ApplySort(tournaments, query.SortField, query.SortDescending);
        List<Tournament> results = await tournaments.ToListAsync(cancellationToken).ConfigureAwait(false);
        return await ToListItemsAsync(context, results, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Tournament?> GetAsync(int tournamentId, CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Tournaments
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.TournamentId == tournamentId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<Tournament>> UpdateAsync(
        int tournamentId,
        string name,
        DateTimeOffset startDateUtc,
        DateTimeOffset endDateUtc,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        Result? validation = ValidateTournamentInput(name, startDateUtc, endDateUtc);
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
        tournament.Name = name.Trim();
        tournament.StartDateUtc = NormalizeToUtcDate(startDateUtc);
        tournament.EndDateUtc = NormalizeToUtcDate(endDateUtc);
        context.Entry(tournament).Property(t => t.RowVersion).OriginalValue = rowVersion;
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<Tournament>(saveResult.Error!);
        }
        return Result.Ok(tournament);
    }

    public async Task<Result> DeleteAsync(int tournamentId, CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Tournament? tournament = await context.Tournaments
            .SingleOrDefaultAsync(t => t.TournamentId == tournamentId, cancellationToken)
            .ConfigureAwait(false);
        if (tournament is null)
        {
            return Result.Fail("Tournament not found.");
        }
        context.Tournaments.Remove(tournament);
        return await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TournamentMember>> ListMembersAsync(
        int tournamentId,
        CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TournamentParticipants
            .AsNoTracking()
            .Where(p => p.TournamentId == tournamentId)
            .OrderBy(p => p.User.UserName)
            .Select(p => new TournamentMember
            {
                UserId = p.UserId,
                UserName = p.User.UserName,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result> JoinAsync(int tournamentId, int userId, CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Tournament? tournament = await context.Tournaments
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.TournamentId == tournamentId, cancellationToken)
            .ConfigureAwait(false);
        if (tournament is null)
        {
            return Result.Fail("Tournament not found.");
        }
        Result? rosterWindow = ValidateRosterOpen(tournament);
        if (rosterWindow is not null)
        {
            return rosterWindow;
        }
        bool userExists = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (!userExists)
        {
            return Result.Fail("User not found.");
        }
        bool alreadyJoined = await context.TournamentParticipants
            .AsNoTracking()
            .AnyAsync(p => p.TournamentId == tournamentId && p.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (alreadyJoined)
        {
            return Result.Fail("You are already in this tournament.");
        }
        context.TournamentParticipants.Add(new TournamentParticipant
        {
            TournamentId = tournamentId,
            UserId = userId,
        });
        return await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> LeaveAsync(int tournamentId, int userId, CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Tournament? tournament = await context.Tournaments
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.TournamentId == tournamentId, cancellationToken)
            .ConfigureAwait(false);
        if (tournament is null)
        {
            return Result.Fail("Tournament not found.");
        }
        Result? rosterWindow = ValidateRosterOpen(tournament);
        if (rosterWindow is not null)
        {
            return rosterWindow;
        }
        TournamentParticipant? participant = await context.TournamentParticipants
            .SingleOrDefaultAsync(
                p => p.TournamentId == tournamentId && p.UserId == userId,
                cancellationToken)
            .ConfigureAwait(false);
        if (participant is null)
        {
            return Result.Fail("You are not in this tournament.");
        }
        context.TournamentParticipants.Remove(participant);
        return await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private Result? ValidateRosterOpen(Tournament tournament)
    {
        DateTimeOffset todayUtc = TournamentSchedule.TodayUtc(timeProvider);
        if (TournamentSchedule.GetStatus(tournament, todayUtc) != TournamentLifecycleStatus.Upcoming)
        {
            return Result.Fail("The roster is closed for this tournament.");
        }
        return null;
    }

    private static async Task<IReadOnlyList<TournamentListItem>> ToListItemsAsync(
        XwingDbContext context,
        List<Tournament> tournaments,
        CancellationToken cancellationToken)
    {
        if (tournaments.Count == 0)
        {
            return [];
        }
        int[] tournamentIds = tournaments.Select(t => t.TournamentId).ToArray();
        Dictionary<int, int> counts = await context.TournamentParticipants
            .AsNoTracking()
            .Where(p => tournamentIds.Contains(p.TournamentId))
            .GroupBy(p => p.TournamentId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken)
            .ConfigureAwait(false);
        return tournaments
            .Select(t => new TournamentListItem
            {
                Tournament = t,
                ParticipantCount = counts.GetValueOrDefault(t.TournamentId),
            })
            .ToList();
    }

    private static IQueryable<Tournament> ApplySort(
        IQueryable<Tournament> query,
        TournamentSortField sortField,
        bool sortDescending)
    {
        return (sortField, sortDescending) switch
        {
            (TournamentSortField.Name, false) => query.OrderBy(t => t.Name).ThenBy(t => t.StartDateUtc),
            (TournamentSortField.Name, true) => query.OrderByDescending(t => t.Name).ThenByDescending(t => t.StartDateUtc),
            (TournamentSortField.EndDate, false) => query.OrderBy(t => t.EndDateUtc).ThenBy(t => t.Name),
            (TournamentSortField.EndDate, true) => query.OrderByDescending(t => t.EndDateUtc).ThenByDescending(t => t.Name),
            (TournamentSortField.StartDate, true) => query.OrderByDescending(t => t.StartDateUtc).ThenByDescending(t => t.Name),
            _ => query.OrderBy(t => t.StartDateUtc).ThenBy(t => t.Name),
        };
    }

    private static Result? ValidateTournamentInput(string name, DateTimeOffset startDateUtc, DateTimeOffset endDateUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Fail("Tournament name is required.");
        }
        DateTimeOffset start = NormalizeToUtcDate(startDateUtc);
        DateTimeOffset end = NormalizeToUtcDate(endDateUtc);
        if (end < start)
        {
            return Result.Fail("End date must be on or after the start date.");
        }
        return null;
    }

    private static DateTimeOffset NormalizeToUtcDate(DateTimeOffset value) =>
        new(value.UtcDateTime.Date, TimeSpan.Zero);
}

public sealed record TournamentListItem
{
    public required Tournament Tournament { get; init; }
    public int ParticipantCount { get; init; }
}

public sealed record TournamentMember
{
    public int UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
}
