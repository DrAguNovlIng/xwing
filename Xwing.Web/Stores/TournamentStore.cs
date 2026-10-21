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

    public async Task<IReadOnlyList<Tournament>> ListAsync(CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Tournaments
            .AsNoTracking()
            .OrderBy(t => t.StartDateUtc)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Tournament>> SearchAsync(
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
        return await tournaments.ToListAsync(cancellationToken).ConfigureAwait(false);
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
