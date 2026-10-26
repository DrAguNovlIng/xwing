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
        bool uniqueFactions,
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
            GraphWidthCells = BracketGraphLayout.DefaultGraphWidthCells,
            GraphHeightCells = BracketGraphLayout.DefaultGraphHeightCells,
            UniqueFactions = uniqueFactions,
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
        return await ToListItemsAsync(context, tournaments, currentUserId: null, cancellationToken).ConfigureAwait(false);
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
        return await ToListItemsAsync(context, results, query.CurrentUserId, cancellationToken).ConfigureAwait(false);
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
        bool uniqueFactions,
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

        if (uniqueFactions && !tournament.UniqueFactions)
        {
            Result? enableValidation = await ValidateCanEnableUniqueFactionsAsync(
                context,
                tournamentId,
                cancellationToken).ConfigureAwait(false);
            if (enableValidation is not null)
            {
                return Result.Fail<Tournament>(enableValidation.Error!);
            }
        }

        tournament.Name = name.Trim();
        tournament.StartDateUtc = NormalizeToUtcDate(startDateUtc);
        tournament.EndDateUtc = NormalizeToUtcDate(endDateUtc);
        tournament.UniqueFactions = uniqueFactions;
        context.Entry(tournament).Property(t => t.RowVersion).OriginalValue = rowVersion;
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<Tournament>(saveResult.Error!);
        }
        return Result.Ok(tournament);
    }

    public async Task<Result<Tournament>> SetWinnerAsync(
        int tournamentId,
        int? userId,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Tournament? tournament = await context.Tournaments
            .SingleOrDefaultAsync(t => t.TournamentId == tournamentId, cancellationToken)
            .ConfigureAwait(false);
        if (tournament is null)
        {
            return Result.Fail<Tournament>("Tournament not found.");
        }

        if (userId is int winnerUserId)
        {
            bool isParticipant = await context.TournamentParticipants
                .AnyAsync(p => p.TournamentId == tournamentId && p.UserId == winnerUserId, cancellationToken)
                .ConfigureAwait(false);
            if (!isParticipant)
            {
                return Result.Fail<Tournament>("Winner must be a tournament participant.");
            }
        }

        tournament.WinnerUserId = userId;
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
        int? viewingUserId,
        CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Tournament? tournament = await context.Tournaments
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.TournamentId == tournamentId, cancellationToken)
            .ConfigureAwait(false);
        if (tournament is null)
        {
            return [];
        }

        List<TournamentParticipant> participants = await context.TournamentParticipants
            .AsNoTracking()
            .Include(p => p.User)
            .Where(p => p.TournamentId == tournamentId)
            .OrderBy(p => p.User.UserName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset todayUtc = TournamentSchedule.TodayUtc(timeProvider);
        return participants
            .Select(p => MapMember(p, tournament, viewingUserId, todayUtc))
            .ToList();
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
        if (tournament.UniqueFactions)
        {
            if (string.IsNullOrEmpty(participant.SquadFaction))
            {
                return Result.Fail("Choose a faction when you join this tournament.");
            }

            if (!string.Equals(participant.SquadFaction, squad.FactionDisplayName, StringComparison.Ordinal))
            {
                return Result.Fail($"Your squad must use {participant.SquadFaction}.");
            }
        }

        participant.SquadFaction = squad.FactionDisplayName;
        var shipEntries = squad.Pilots
            .Select(p => new SquadShipJsonEntry { Ship = p.ShipId, Pilot = p.PilotId })
            .ToList();
        participant.SquadShipsJson = JsonSerializer.Serialize(shipEntries, SquadShipJsonOptions);
        participant.SquadUrl = squad.Url;
        return await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
    }

    internal static TournamentMember MapMember(
        TournamentParticipant participant,
        Tournament tournament,
        int? viewingUserId,
        DateTimeOffset todayUtc)
    {
        IReadOnlyList<SquadShipDisplay> ships = ParseSquadShipsJson(participant.SquadShipsJson);
        bool hasFullSquad = !string.IsNullOrEmpty(participant.SquadFaction) &&
                            ships.Count > 0 &&
                            !string.IsNullOrEmpty(participant.SquadUrl);
        bool hasFactionClaim = !string.IsNullOrEmpty(participant.SquadFaction);
        bool isSelf = viewingUserId == participant.UserId;
        bool rosterLocked = TournamentSchedule.GetStatus(tournament, todayUtc) != TournamentLifecycleStatus.Upcoming;

        if (isSelf)
        {
            return new TournamentMember
            {
                UserId = participant.UserId,
                UserName = participant.User.UserName,
                SquadFactionDisplay = hasFactionClaim ? participant.SquadFaction : null,
                SquadShips = hasFullSquad ? ships : [],
                SquadUrl = hasFullSquad ? participant.SquadUrl : null,
            };
        }

        if (rosterLocked)
        {
            return new TournamentMember
            {
                UserId = participant.UserId,
                UserName = participant.User.UserName,
                SquadFactionDisplay = hasFactionClaim ? participant.SquadFaction : null,
                SquadShips = hasFullSquad ? ships : [],
                SquadUrl = hasFullSquad ? participant.SquadUrl : null,
            };
        }

        if (tournament.UniqueFactions && hasFactionClaim)
        {
            return new TournamentMember
            {
                UserId = participant.UserId,
                UserName = participant.User.UserName,
                SquadFactionDisplay = participant.SquadFaction,
                SquadShips = [],
                SquadUrl = null,
            };
        }

        return new TournamentMember
        {
            UserId = participant.UserId,
            UserName = participant.User.UserName,
            SquadFactionDisplay = null,
            SquadShips = [],
            SquadUrl = null,
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

    public async Task<Result> JoinAsync(
        int tournamentId,
        int userId,
        string? factionDisplayName,
        CancellationToken cancellationToken)
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

        string? storedFaction = null;
        if (tournament.UniqueFactions)
        {
            if (string.IsNullOrWhiteSpace(factionDisplayName))
            {
                return Result.Fail("Choose a faction to join this tournament.");
            }

            string trimmedFaction = factionDisplayName.Trim();
            if (!XwaFactionCatalog.IsKnownDisplayName(trimmedFaction))
            {
                return Result.Fail("Choose a valid faction.");
            }

            bool factionTaken = await context.TournamentParticipants
                .AsNoTracking()
                .AnyAsync(
                    p => p.TournamentId == tournamentId && p.SquadFaction == trimmedFaction,
                    cancellationToken)
                .ConfigureAwait(false);
            if (factionTaken)
            {
                return Result.Fail("That faction is already taken.");
            }

            storedFaction = trimmedFaction;
        }
        else if (!string.IsNullOrWhiteSpace(factionDisplayName))
        {
            return Result.Fail("This tournament does not use unique factions.");
        }

        context.TournamentParticipants.Add(new TournamentParticipant
        {
            TournamentId = tournamentId,
            UserId = userId,
            SquadFaction = storedFaction,
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

    private static async Task<Result?> ValidateCanEnableUniqueFactionsAsync(
        XwingDbContext context,
        int tournamentId,
        CancellationToken cancellationToken)
    {
        List<TournamentParticipant> participants = await context.TournamentParticipants
            .AsNoTracking()
            .Where(p => p.TournamentId == tournamentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (participants.Count == 0)
        {
            return null;
        }

        if (participants.Any(p => string.IsNullOrEmpty(p.SquadFaction)))
        {
            return Result.Fail("Every participant must have a faction before unique factions can be enabled.");
        }

        int distinctFactions = participants
            .Select(p => p.SquadFaction!)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (distinctFactions != participants.Count)
        {
            return Result.Fail("Each participant must have a different faction before unique factions can be enabled.");
        }

        return null;
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
        int? currentUserId,
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
        Dictionary<int, TournamentParticipant> currentUserParticipants = [];
        if (currentUserId is int userId)
        {
            List<TournamentParticipant> rows = await context.TournamentParticipants
                .AsNoTracking()
                .Where(p => p.UserId == userId && tournamentIds.Contains(p.TournamentId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            currentUserParticipants = rows.ToDictionary(p => p.TournamentId);
        }

        int[] winnerUserIds = tournaments
            .Select(t => t.WinnerUserId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        Dictionary<int, string> winnerNames = winnerUserIds.Length == 0
            ? []
            : await context.Users
                .AsNoTracking()
                .Where(u => winnerUserIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.UserName, cancellationToken)
                .ConfigureAwait(false);

        return tournaments
            .Select(t =>
            {
                bool joined = currentUserParticipants.ContainsKey(t.TournamentId);
                bool hasSquad = joined &&
                                ParticipantHasSquad(currentUserParticipants[t.TournamentId]);
                string? winnerUserName = t.WinnerUserId is int winnerId
                    ? winnerNames.GetValueOrDefault(winnerId)
                    : null;
                return new TournamentListItem
                {
                    Tournament = t,
                    ParticipantCount = counts.GetValueOrDefault(t.TournamentId),
                    CurrentUserJoined = joined,
                    CurrentUserHasSquad = hasSquad,
                    WinnerUserName = winnerUserName,
                };
            })
            .ToList();
    }

    private static bool ParticipantHasSquad(TournamentParticipant participant)
    {
        IReadOnlyList<SquadShipDisplay> ships = ParseSquadShipsJson(participant.SquadShipsJson);
        return !string.IsNullOrEmpty(participant.SquadFaction) &&
               ships.Count > 0 &&
               !string.IsNullOrEmpty(participant.SquadUrl);
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
    public bool CurrentUserJoined { get; init; }
    public bool CurrentUserHasSquad { get; init; }
    public string? WinnerUserName { get; init; }
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
