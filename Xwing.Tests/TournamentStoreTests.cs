using Xwing.Web.Data;
using Xwing.Web.Models;
using Xwing.Web.Stores;

namespace Xwing.Tests;

[Collection("SQL Server Integration Tests")]
public sealed class TournamentStoreTests
{
    private static readonly DateTimeOffset DefaultStart = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DefaultEnd = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

    private readonly string connectionString;

    public TournamentStoreTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task CreateAsyncShouldCreateTournament()
    {
        DateTimeOffset expectedUtc = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(expectedUtc);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            timeProvider,
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<Tournament> result = await stores.Tournaments.CreateAsync(
                "Regional Championship",
                DefaultStart,
                DefaultEnd,
                TestContext.Current.CancellationToken);
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value!.TournamentId.ShouldBeGreaterThan(0);
            result.Value.Name.ShouldBe("Regional Championship");
            result.Value.StartDateUtc.ShouldBe(DefaultStart);
            result.Value.EndDateUtc.ShouldBe(DefaultEnd);
            result.Value.TimeCreatedUtc.ShouldBe(expectedUtc);
            result.Value.TimeUpdatedUtc.ShouldBe(expectedUtc);
        }
    }

    [Fact]
    public async Task CreateAsyncShouldRejectBlankName()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<Tournament> result = await stores.Tournaments.CreateAsync(
                "   ",
                DefaultStart,
                DefaultEnd,
                TestContext.Current.CancellationToken);
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldBe("Tournament name is required.");
        }
    }

    [Fact]
    public async Task CreateAsyncShouldRejectEndBeforeStart()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<Tournament> result = await stores.Tournaments.CreateAsync(
                "Bad dates",
                DefaultEnd,
                DefaultStart,
                TestContext.Current.CancellationToken);
            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldBe("End date must be on or after the start date.");
        }
    }

    [Fact]
    public async Task ListAsyncShouldReturnTournamentsOrderedByStartDate()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<Tournament> later = await stores.Tournaments.CreateAsync(
                "Bravo",
                new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 12, 2, 0, 0, 0, TimeSpan.Zero),
                TestContext.Current.CancellationToken);
            Result<Tournament> earlier = await stores.Tournaments.CreateAsync(
                "Alpha",
                new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
                TestContext.Current.CancellationToken);
            later.IsSuccess.ShouldBeTrue();
            earlier.IsSuccess.ShouldBeTrue();
            IReadOnlyList<Tournament> all = await stores.Tournaments.ListAsync(
                TestContext.Current.CancellationToken);
            all.Count.ShouldBe(2);
            all[0].Name.ShouldBe("Alpha");
            all[1].Name.ShouldBe("Bravo");
        }
    }

    [Fact]
    public async Task UpdateAsyncShouldUpdateNameAndDates()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<Tournament> created = await stores.Tournaments.CreateAsync(
                "Original",
                DefaultStart,
                DefaultEnd,
                TestContext.Current.CancellationToken);
            created.IsSuccess.ShouldBeTrue();
            DateTimeOffset newStart = new(2026, 12, 10, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset newEnd = new(2026, 12, 12, 0, 0, 0, TimeSpan.Zero);
            Result<Tournament> updated = await stores.Tournaments.UpdateAsync(
                created.Value!.TournamentId,
                "Renamed",
                newStart,
                newEnd,
                created.Value.RowVersion,
                TestContext.Current.CancellationToken);
            updated.IsSuccess.ShouldBeTrue();
            updated.Value!.Name.ShouldBe("Renamed");
            updated.Value.StartDateUtc.ShouldBe(newStart);
            updated.Value.EndDateUtc.ShouldBe(newEnd);
        }
    }

    [Fact]
    public async Task DeleteAsyncShouldRemoveTournament()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<Tournament> created = await stores.Tournaments.CreateAsync(
                "ToDelete",
                DefaultStart,
                DefaultEnd,
                TestContext.Current.CancellationToken);
            created.IsSuccess.ShouldBeTrue();
            Result deleted = await stores.Tournaments.DeleteAsync(
                created.Value!.TournamentId,
                TestContext.Current.CancellationToken);
            deleted.IsSuccess.ShouldBeTrue();
            IReadOnlyList<Tournament> all = await stores.Tournaments.ListAsync(
                TestContext.Current.CancellationToken);
            all.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task UpdateAsyncShouldFailWhenRowVersionIsStale()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<Tournament> created = await stores.Tournaments.CreateAsync(
                "Stale",
                DefaultStart,
                DefaultEnd,
                TestContext.Current.CancellationToken);
            created.IsSuccess.ShouldBeTrue();
            byte[] staleToken = new byte[created.Value!.RowVersion.Length];
            Result<Tournament> updated = await stores.Tournaments.UpdateAsync(
                created.Value.TournamentId,
                "Changed",
                DefaultStart,
                DefaultEnd,
                staleToken,
                TestContext.Current.CancellationToken);
            updated.IsSuccess.ShouldBeFalse();
            updated.Error.ShouldBe("The record was changed by someone else. Refresh and try again.");
        }
    }

    [Fact]
    public async Task SearchAsyncShouldFilterByName()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            await stores.Tournaments.CreateAsync("Regional Open", DefaultStart, DefaultEnd, TestContext.Current.CancellationToken);
            await stores.Tournaments.CreateAsync("Local League", DefaultStart, DefaultEnd, TestContext.Current.CancellationToken);
            IReadOnlyList<Tournament> results = await stores.Tournaments.SearchAsync(
                new TournamentSearchQuery { NameSearch = "regional" },
                TestContext.Current.CancellationToken);
            results.Count.ShouldBe(1);
            results[0].Name.ShouldBe("Regional Open");
        }
    }

    [Fact]
    public async Task SearchAsyncActiveShouldIncludeUpcomingAndInProgress()
    {
        DateTimeOffset today = new(2026, 11, 2, 15, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(today);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            timeProvider,
            TestContext.Current.CancellationToken);
        using (stores)
        {
            await stores.Tournaments.CreateAsync(
                "Past",
                new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero),
                TestContext.Current.CancellationToken);
            await stores.Tournaments.CreateAsync(
                "In progress",
                new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero),
                TestContext.Current.CancellationToken);
            await stores.Tournaments.CreateAsync(
                "Upcoming",
                new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero),
                TestContext.Current.CancellationToken);
            IReadOnlyList<Tournament> active = await stores.Tournaments.SearchAsync(
                new TournamentSearchQuery { WhenFilter = TournamentWhenFilter.Active },
                TestContext.Current.CancellationToken);
            active.Count.ShouldBe(2);
            active.ShouldContain(t => t.Name == "In progress");
            active.ShouldContain(t => t.Name == "Upcoming");
        }
    }

    [Fact]
    public async Task SearchAsyncPastShouldExcludeActiveTournaments()
    {
        DateTimeOffset today = new(2026, 11, 2, 15, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(today);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            timeProvider,
            TestContext.Current.CancellationToken);
        using (stores)
        {
            await stores.Tournaments.CreateAsync(
                "Past only",
                new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero),
                TestContext.Current.CancellationToken);
            await stores.Tournaments.CreateAsync(
                "Still active",
                new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero),
                TestContext.Current.CancellationToken);
            IReadOnlyList<Tournament> past = await stores.Tournaments.SearchAsync(
                new TournamentSearchQuery { WhenFilter = TournamentWhenFilter.Past },
                TestContext.Current.CancellationToken);
            past.Count.ShouldBe(1);
            past[0].Name.ShouldBe("Past only");
        }
    }

    [Fact]
    public async Task SearchAsyncShouldSortByNameDescending()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            await stores.Tournaments.CreateAsync("Alpha", DefaultStart, DefaultEnd, TestContext.Current.CancellationToken);
            await stores.Tournaments.CreateAsync("Zulu", DefaultStart, DefaultEnd, TestContext.Current.CancellationToken);
            IReadOnlyList<Tournament> results = await stores.Tournaments.SearchAsync(
                new TournamentSearchQuery
                {
                    SortField = TournamentSortField.Name,
                    SortDescending = true,
                },
                TestContext.Current.CancellationToken);
            results[0].Name.ShouldBe("Zulu");
            results[1].Name.ShouldBe("Alpha");
        }
    }
}
