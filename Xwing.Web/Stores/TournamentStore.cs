using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;

public sealed class TournamentStore : ITournamentStore
{
    private static readonly JsonSerializerOptions SquadShipJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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
        List<TournamentParticipant> participants = await context.TournamentParticipants
            .AsNoTracking()
            .Include(p => p.User)
            .Where(p => p.TournamentId == tournamentId)
            .OrderBy(p => p.User.UserName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return participants.Select(MapMember).ToList();
    }

    public async Task<Result> SetSquadAsync(
        int tournamentId,
        int userId,
        string squadJson,
        CancellationToken cancellationToken)
    {
        Result<XwaSquad> parsed = XwaSquad.Parse(squadJson);
        if (!parsed.IsSuccess)
        {
            return Result.Fail(parsed.Error!);
        }
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
        XwaSquad squad = parsed.Value!;
        participant.SquadFaction = squad.FactionDisplayName;
        var shipEntries = squad.Pilots
            .Select(p => new SquadShipJsonEntry { Ship = p.ShipId, Pilot = p.PilotId })
            .ToList();
        participant.SquadShipsJson = JsonSerializer.Serialize(shipEntries, SquadShipJsonOptions);
        participant.SquadUrl = squad.Url;
        return await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private static TournamentMember MapMember(TournamentParticipant participant)
    {
        IReadOnlyList<SquadShipDisplay> ships = ParseSquadShipsJson(participant.SquadShipsJson);
        bool hasSquad = !string.IsNullOrEmpty(participant.SquadFaction) &&
                        ships.Count > 0 &&
                        !string.IsNullOrEmpty(participant.SquadUrl);
        return new TournamentMember
        {
            UserId = participant.UserId,
            UserName = participant.User.UserName,
            SquadFactionDisplay = hasSquad ? participant.SquadFaction : null,
            SquadShips = hasSquad ? ships : [],
            SquadUrl = hasSquad ? participant.SquadUrl : null,
        };
    }

    public static IReadOnlyList<SquadShipDisplay> ParseSquadShipsJson(string? squadShipsJson)
    {
        if (string.IsNullOrEmpty(squadShipsJson))
        {
            return [];
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(squadShipsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var ships = new List<SquadShipDisplay>();
            foreach (JsonElement item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    string? shipId = item.GetString();
                    if (string.IsNullOrWhiteSpace(shipId))
                    {
                        continue;
                    }

                    string trimmedShipId = shipId.Trim();
                    ships.Add(new SquadShipDisplay
                    {
                        ShipId = trimmedShipId,
                        PilotLabel = XwaSquad.FormatShipDisplayName(trimmedShipId),
                    });
                    continue;
                }

                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!item.TryGetProperty("ship", out JsonElement shipElement) ||
                    shipElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string? parsedShipId = shipElement.GetString();
                if (string.IsNullOrWhiteSpace(parsedShipId))
                {
                    continue;
                }

                string trimmed = parsedShipId.Trim();
                string? pilotId = null;
                if (item.TryGetProperty("pilot", out JsonElement pilotElement) &&
                    pilotElement.ValueKind == JsonValueKind.String)
                {
                    pilotId = pilotElement.GetString()?.Trim();
                }

                ships.Add(new SquadShipDisplay
                {
                    ShipId = trimmed,
                    PilotLabel = XwsPilotCatalog.ResolvePilotLabel(pilotId, trimmed),
                });
            }

            return ships;
        }
        catch (JsonException)
        {
            return [];
        }
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
        User? user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        if (!user.IsActive)
        {
            return Result.Fail("This account is inactive.");
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
    public string? SquadFactionDisplay { get; init; }
    public IReadOnlyList<SquadShipDisplay> SquadShips { get; init; } = [];

    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Blazor href binding uses string URLs.")]
    public string? SquadUrl { get; init; }
}

public sealed record SquadShipDisplay
{
    public required string ShipId { get; init; }

    public required string PilotLabel { get; init; }
}

internal sealed record SquadShipJsonEntry
{
    public required string Ship { get; init; }

    public string? Pilot { get; init; }
}
