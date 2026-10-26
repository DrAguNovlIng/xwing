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
                false,
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
                false,
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
                false,
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
                false,
                TestContext.Current.CancellationToken);
            Result<Tournament> earlier = await stores.Tournaments.CreateAsync(
                "Alpha",
                new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
                false,
                TestContext.Current.CancellationToken);
            later.IsSuccess.ShouldBeTrue();
            earlier.IsSuccess.ShouldBeTrue();
            IReadOnlyList<TournamentListItem> all = await stores.Tournaments.ListAsync(
                TestContext.Current.CancellationToken);
            all.Count.ShouldBe(2);
            all[0].Tournament.Name.ShouldBe("Alpha");
            all[1].Tournament.Name.ShouldBe("Bravo");
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
                false,
                TestContext.Current.CancellationToken);
            created.IsSuccess.ShouldBeTrue();
            DateTimeOffset newStart = new(2026, 12, 10, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset newEnd = new(2026, 12, 12, 0, 0, 0, TimeSpan.Zero);
            Result<Tournament> updated = await stores.Tournaments.UpdateAsync(
                created.Value!.TournamentId,
                "Renamed",
                newStart,
                newEnd,
                false,
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
                false,
                TestContext.Current.CancellationToken);
            created.IsSuccess.ShouldBeTrue();
            Result deleted = await stores.Tournaments.DeleteAsync(
                created.Value!.TournamentId,
                TestContext.Current.CancellationToken);
            deleted.IsSuccess.ShouldBeTrue();
            IReadOnlyList<TournamentListItem> all = await stores.Tournaments.ListAsync(
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
                false,
                TestContext.Current.CancellationToken);
            created.IsSuccess.ShouldBeTrue();
            byte[] staleToken = new byte[created.Value!.RowVersion.Length];
            Result<Tournament> updated = await stores.Tournaments.UpdateAsync(
                created.Value.TournamentId,
                "Changed",
                DefaultStart,
                DefaultEnd,
                false,
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
            await stores.Tournaments.CreateAsync("Regional Open", DefaultStart, DefaultEnd, false, TestContext.Current.CancellationToken);
            await stores.Tournaments.CreateAsync("Local League", DefaultStart, DefaultEnd, false, TestContext.Current.CancellationToken);
            IReadOnlyList<TournamentListItem> results = await stores.Tournaments.SearchAsync(
                new TournamentSearchQuery { NameSearch = "regional" },
                TestContext.Current.CancellationToken);
            results.Count.ShouldBe(1);
            results[0].Tournament.Name.ShouldBe("Regional Open");
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
                false,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.CreateAsync(
                "In progress",
                new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero),
                false,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.CreateAsync(
                "Upcoming",
                new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero),
                false,
                TestContext.Current.CancellationToken);
            IReadOnlyList<TournamentListItem> active = await stores.Tournaments.SearchAsync(
                new TournamentSearchQuery { WhenFilter = TournamentWhenFilter.Active },
                TestContext.Current.CancellationToken);
            active.Count.ShouldBe(2);
            active.ShouldContain(t => t.Tournament.Name == "In progress");
            active.ShouldContain(t => t.Tournament.Name == "Upcoming");
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
                false,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.CreateAsync(
                "Still active",
                new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero),
                false,
                TestContext.Current.CancellationToken);
            IReadOnlyList<TournamentListItem> past = await stores.Tournaments.SearchAsync(
                new TournamentSearchQuery { WhenFilter = TournamentWhenFilter.Past },
                TestContext.Current.CancellationToken);
            past.Count.ShouldBe(1);
            past[0].Tournament.Name.ShouldBe("Past only");
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
            await stores.Tournaments.CreateAsync("Alpha", DefaultStart, DefaultEnd, false, TestContext.Current.CancellationToken);
            await stores.Tournaments.CreateAsync("Zulu", DefaultStart, DefaultEnd, false, TestContext.Current.CancellationToken);
            IReadOnlyList<TournamentListItem> results = await stores.Tournaments.SearchAsync(
                new TournamentSearchQuery
                {
                    SortField = TournamentSortField.Name,
                    SortDescending = true,
                },
                TestContext.Current.CancellationToken);
            results[0].Tournament.Name.ShouldBe("Zulu");
            results[1].Tournament.Name.ShouldBe("Alpha");
        }
    }

    [Fact]
    public async Task ListAsyncShouldIncludeParticipantCount()
    {
        DateTimeOffset today = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(today);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            timeProvider,
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> user1 = await stores.Users.CreateAsync(
                "one",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<UserAccount> user2 = await stores.Users.CreateAsync(
                "two",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Counted",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                user1.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value.TournamentId,
                user2.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            IReadOnlyList<TournamentListItem> all = await stores.Tournaments.ListAsync(
                TestContext.Current.CancellationToken);
            all.Count.ShouldBe(1);
            all[0].ParticipantCount.ShouldBe(2);
        }
    }

    [Fact]
    public async Task JoinAsyncShouldAddUserAndListMembersReturnsUserName()
    {
        DateTimeOffset today = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(today);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            timeProvider,
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            user.IsSuccess.ShouldBeTrue();
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Open",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            tournament.IsSuccess.ShouldBeTrue();
            Result join = await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                user.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            join.IsSuccess.ShouldBeTrue();
            IReadOnlyList<TournamentMember> members = await stores.Tournaments.ListMembersAsync(
                tournament.Value.TournamentId,
                null,
                TestContext.Current.CancellationToken);
            members.Count.ShouldBe(1);
            members[0].UserName.ShouldBe("player");
        }
    }

    [Fact]
    public async Task JoinAsyncShouldRejectDuplicateMembership()
    {
        DateTimeOffset today = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(today);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            timeProvider,
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Open",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                user.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            Result secondJoin = await stores.Tournaments.JoinAsync(
                tournament.Value.TournamentId,
                user.Value.UserId,
                null,
                TestContext.Current.CancellationToken);
            secondJoin.IsSuccess.ShouldBeFalse();
            secondJoin.Error.ShouldBe("You are already in this tournament.");
        }
    }

    [Fact]
    public async Task JoinAsyncShouldRejectMissingTournamentOrUser()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result missingTournament = await stores.Tournaments.JoinAsync(
                9999,
                user.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            missingTournament.IsSuccess.ShouldBeFalse();
            missingTournament.Error.ShouldBe("Tournament not found.");
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Open",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            Result missingUser = await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                9999,
                null,
                TestContext.Current.CancellationToken);
            missingUser.IsSuccess.ShouldBeFalse();
            missingUser.Error.ShouldBe("User not found.");
        }
    }

    [Fact]
    public async Task JoinAsyncShouldRejectWhenStartDateHasArrived()
    {
        DateTimeOffset today = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(today);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            timeProvider,
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Started",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            Result join = await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                user.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            join.IsSuccess.ShouldBeFalse();
            join.Error.ShouldBe("The roster is closed for this tournament.");
        }
    }

    [Fact]
    public async Task LeaveAsyncShouldRemoveUserAndRejectWhenNotJoined()
    {
        DateTimeOffset today = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(today);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            timeProvider,
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Open",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            Result notJoined = await stores.Tournaments.LeaveAsync(
                tournament.Value!.TournamentId,
                user.Value!.UserId,
                TestContext.Current.CancellationToken);
            notJoined.IsSuccess.ShouldBeFalse();
            notJoined.Error.ShouldBe("You are not in this tournament.");
            await stores.Tournaments.JoinAsync(
                tournament.Value.TournamentId,
                user.Value.UserId,
                null,
                TestContext.Current.CancellationToken);
            Result leave = await stores.Tournaments.LeaveAsync(
                tournament.Value.TournamentId,
                user.Value.UserId,
                TestContext.Current.CancellationToken);
            leave.IsSuccess.ShouldBeTrue();
            IReadOnlyList<TournamentMember> members = await stores.Tournaments.ListMembersAsync(
                tournament.Value.TournamentId,
                null,
                TestContext.Current.CancellationToken);
            members.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task LeaveAsyncShouldRejectWhenTournamentHasStarted()
    {
        DateTimeOffset beforeStart = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            new FixedTimeProvider(beforeStart),
            TestContext.Current.CancellationToken);
        int tournamentId;
        int userId;
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "In progress",
                new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero),
                false,
                TestContext.Current.CancellationToken);
            userId = user.Value!.UserId;
            tournamentId = tournament.Value!.TournamentId;
            await stores.Tournaments.JoinAsync(
                tournamentId,
                userId,
                null,
                TestContext.Current.CancellationToken);
        }
        DateTimeOffset afterStart = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        using (TestStores storesAfterStart = TestServiceFactory.CreateStores(connectionString, new FixedTimeProvider(afterStart)))
        {
            Result leave = await storesAfterStart.Tournaments.LeaveAsync(
                tournamentId,
                userId,
                TestContext.Current.CancellationToken);
            leave.IsSuccess.ShouldBeFalse();
            leave.Error.ShouldBe("The roster is closed for this tournament.");
        }
    }

    [Fact]
    public async Task DeleteTournamentShouldSucceedWhenParticipantExists()
    {
        DateTimeOffset today = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(today);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            timeProvider,
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Open",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                user.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            Result deleted = await stores.Tournaments.DeleteAsync(
                tournament.Value.TournamentId,
                TestContext.Current.CancellationToken);
            deleted.IsSuccess.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task DeactivateUserShouldKeepParticipantWhenInTournament()
    {
        DateTimeOffset today = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(today);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            timeProvider,
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<UserAccount> admin = await stores.Users.CreateAsync(
                "admin2",
                "secret",
                UserRoles.Admin,
                TestContext.Current.CancellationToken);
            admin.IsSuccess.ShouldBeTrue();
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Open",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                user.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            Result deactivated = await stores.Users.DeactivateAsync(
                user.Value.UserId,
                TestContext.Current.CancellationToken);
            deactivated.IsSuccess.ShouldBeTrue();
            IReadOnlyList<TournamentMember> membersAfter = await stores.Tournaments.ListMembersAsync(
                tournament.Value!.TournamentId,
                null,
                TestContext.Current.CancellationToken);
            membersAfter.Any(m => m.UserId == user.Value.UserId).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task SearchAsyncShouldIncludeCurrentUserJoinAndSquadFlags()
    {
        DateTimeOffset today = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(today);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            timeProvider,
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> noSquad = await stores.Tournaments.CreateAsync(
                "No squad yet",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            Result<Tournament> withSquad = await stores.Tournaments.CreateAsync(
                "With squad",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            Result<Tournament> notJoined = await stores.Tournaments.CreateAsync(
                "Not joined",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            int userId = user.Value!.UserId;
            await stores.Tournaments.JoinAsync(
                noSquad.Value!.TournamentId,
                userId,
                null,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                withSquad.Value!.TournamentId,
                userId,
                null,
                TestContext.Current.CancellationToken);
            Result saved = await stores.Tournaments.SetSquadAsync(
                withSquad.Value.TournamentId,
                userId,
                SampleSquadJson,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeTrue();
            IReadOnlyList<TournamentListItem> results = await stores.Tournaments.SearchAsync(
                new TournamentSearchQuery { WhenFilter = TournamentWhenFilter.All, CurrentUserId = userId },
                TestContext.Current.CancellationToken);
            TournamentListItem noSquadItem = results.Single(r => r.Tournament.TournamentId == noSquad.Value.TournamentId);
            noSquadItem.CurrentUserJoined.ShouldBeTrue();
            noSquadItem.CurrentUserHasSquad.ShouldBeFalse();
            TournamentListItem withSquadItem = results.Single(r => r.Tournament.TournamentId == withSquad.Value.TournamentId);
            withSquadItem.CurrentUserJoined.ShouldBeTrue();
            withSquadItem.CurrentUserHasSquad.ShouldBeTrue();
            TournamentListItem notJoinedItem = results.Single(r => r.Tournament.TournamentId == notJoined.Value!.TournamentId);
            notJoinedItem.CurrentUserJoined.ShouldBeFalse();
            notJoinedItem.CurrentUserHasSquad.ShouldBeFalse();
        }
    }

    private const string SampleSquadJson = """
        {
          "faction": "rebelalliance",
          "pilots": [{ "id": "lukeskywalker", "ship": "t-65-x-wing" }],
          "vendor": { "yasb": { "url": "https://yasb.app/?d=test" } }
        }
        """;

    [Fact]
    public async Task SetSquadAsyncShouldPersistAndList()
    {
        DateTimeOffset today = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(today);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            timeProvider,
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Open",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                user.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            Result saved = await stores.Tournaments.SetSquadAsync(
                tournament.Value.TournamentId,
                user.Value.UserId,
                SampleSquadJson,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeTrue();
            IReadOnlyList<TournamentMember> members = await stores.Tournaments.ListMembersAsync(
                tournament.Value.TournamentId,
                user.Value.UserId,
                TestContext.Current.CancellationToken);
            TournamentMember member = members.Single(m => m.UserId == user.Value.UserId);
            member.SquadFactionDisplay.ShouldBe("Rebel Alliance");
            member.SquadShips.Single().PilotLabel.ShouldBe("Luke Skywalker");
            member.SquadUrl.ShouldBe("https://yasb.app/?d=test");
        }
    }

    [Fact]
    public async Task SetSquadAsyncShouldFailWhenUserHasNotJoined()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Open",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            Result saved = await stores.Tournaments.SetSquadAsync(
                tournament.Value!.TournamentId,
                user.Value!.UserId,
                SampleSquadJson,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeFalse();
            saved.Error.ShouldBe("You are not in this tournament.");
        }
    }

    [Fact]
    public async Task SetSquadAsyncShouldFailWhenRosterIsClosed()
    {
        DateTimeOffset beforeStart = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            new FixedTimeProvider(beforeStart),
            TestContext.Current.CancellationToken);
        int tournamentId;
        int userId;
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Started",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            userId = user.Value!.UserId;
            tournamentId = tournament.Value!.TournamentId;
            await stores.Tournaments.JoinAsync(
                tournamentId,
                userId,
                null,
                TestContext.Current.CancellationToken);
        }
        using (TestStores storesAfterStart = TestServiceFactory.CreateStores(
            connectionString,
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero))))
        {
            Result saved = await storesAfterStart.Tournaments.SetSquadAsync(
                tournamentId,
                userId,
                SampleSquadJson,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeFalse();
            saved.Error.ShouldBe("The roster is closed for this tournament.");
        }
    }

    private const string EmpireSquadJson = """
        {
          "faction": "galacticempire",
          "pilots": [{ "id": "darthvader", "ship": "tie-ln-fighter" }],
          "vendor": { "yasb": { "url": "https://yasb.app/?d=empire" } }
        }
        """;

    [Fact]
    public async Task JoinAsyncShouldRejectWhenUniqueFactionsRequiresChoice()
    {
        DateTimeOffset today = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            new FixedTimeProvider(today),
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Unique",
                DefaultStart,
                DefaultEnd,
                true,
                TestContext.Current.CancellationToken);
            Result join = await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                user.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            join.IsSuccess.ShouldBeFalse();
            join.Error.ShouldBe("Choose a faction to join this tournament.");
        }
    }

    [Fact]
    public async Task JoinAsyncShouldRejectWhenFactionIsAlreadyTaken()
    {
        DateTimeOffset today = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            new FixedTimeProvider(today),
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> first = await stores.Users.CreateAsync(
                "first",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<UserAccount> second = await stores.Users.CreateAsync(
                "second",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Unique",
                DefaultStart,
                DefaultEnd,
                true,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                first.Value!.UserId,
                "Rebel Alliance",
                TestContext.Current.CancellationToken);
            Result secondJoin = await stores.Tournaments.JoinAsync(
                tournament.Value.TournamentId,
                second.Value!.UserId,
                "Rebel Alliance",
                TestContext.Current.CancellationToken);
            secondJoin.IsSuccess.ShouldBeFalse();
            secondJoin.Error.ShouldBe("That faction is already taken.");
        }
    }

    [Fact]
    public async Task JoinAsyncShouldStoreClaimedFactionWhenUniqueFactionsOn()
    {
        DateTimeOffset today = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            new FixedTimeProvider(today),
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Unique",
                DefaultStart,
                DefaultEnd,
                true,
                TestContext.Current.CancellationToken);
            Result join = await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                user.Value!.UserId,
                "Galactic Empire",
                TestContext.Current.CancellationToken);
            join.IsSuccess.ShouldBeTrue();
            IReadOnlyList<TournamentMember> members = await stores.Tournaments.ListMembersAsync(
                tournament.Value.TournamentId,
                user.Value.UserId,
                TestContext.Current.CancellationToken);
            TournamentMember self = members.Single();
            self.SquadFactionDisplay.ShouldBe("Galactic Empire");
            self.SquadShips.ShouldBeEmpty();
            self.SquadUrl.ShouldBeNull();
        }
    }

    [Fact]
    public async Task SetSquadAsyncShouldRejectWhenFactionDoesNotMatchClaim()
    {
        DateTimeOffset today = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            new FixedTimeProvider(today),
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> user = await stores.Users.CreateAsync(
                "player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Unique",
                DefaultStart,
                DefaultEnd,
                true,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                user.Value!.UserId,
                "Rebel Alliance",
                TestContext.Current.CancellationToken);
            Result saved = await stores.Tournaments.SetSquadAsync(
                tournament.Value.TournamentId,
                user.Value.UserId,
                EmpireSquadJson,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeFalse();
            saved.Error.ShouldBe("Your squad must use Rebel Alliance.");
        }
    }

    [Fact]
    public async Task ListMembersAsyncShouldHideOtherSquadsBeforeStartWhenUniqueFactionsOff()
    {
        DateTimeOffset today = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            new FixedTimeProvider(today),
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> owner = await stores.Users.CreateAsync(
                "owner",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<UserAccount> viewer = await stores.Users.CreateAsync(
                "viewer",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Open",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                owner.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value.TournamentId,
                viewer.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.SetSquadAsync(
                tournament.Value.TournamentId,
                owner.Value.UserId,
                SampleSquadJson,
                TestContext.Current.CancellationToken);
            IReadOnlyList<TournamentMember> members = await stores.Tournaments.ListMembersAsync(
                tournament.Value.TournamentId,
                viewer.Value.UserId,
                TestContext.Current.CancellationToken);
            TournamentMember ownerRow = members.Single(m => m.UserId == owner.Value.UserId);
            ownerRow.SquadFactionDisplay.ShouldBeNull();
            ownerRow.SquadShips.ShouldBeEmpty();
            ownerRow.SquadUrl.ShouldBeNull();
        }
    }

    [Fact]
    public async Task ListMembersAsyncShouldShowOtherFactionOnlyBeforeStartWhenUniqueFactionsOn()
    {
        DateTimeOffset today = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            new FixedTimeProvider(today),
            TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> owner = await stores.Users.CreateAsync(
                "owner",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<UserAccount> viewer = await stores.Users.CreateAsync(
                "viewer",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Unique",
                DefaultStart,
                DefaultEnd,
                true,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                owner.Value!.UserId,
                "Rebel Alliance",
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value.TournamentId,
                viewer.Value!.UserId,
                "Galactic Empire",
                TestContext.Current.CancellationToken);
            await stores.Tournaments.SetSquadAsync(
                tournament.Value.TournamentId,
                owner.Value.UserId,
                SampleSquadJson,
                TestContext.Current.CancellationToken);
            IReadOnlyList<TournamentMember> members = await stores.Tournaments.ListMembersAsync(
                tournament.Value.TournamentId,
                viewer.Value.UserId,
                TestContext.Current.CancellationToken);
            TournamentMember ownerRow = members.Single(m => m.UserId == owner.Value.UserId);
            ownerRow.SquadFactionDisplay.ShouldBe("Rebel Alliance");
            ownerRow.SquadShips.ShouldBeEmpty();
            ownerRow.SquadUrl.ShouldBeNull();
        }
    }
}
