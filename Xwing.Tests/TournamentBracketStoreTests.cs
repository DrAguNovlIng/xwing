using Xwing.Web.Data;
using Xwing.Web.Models;
using Xwing.Web.Stores;

namespace Xwing.Tests;

[Collection("SQL Server Integration Tests")]
public sealed class TournamentBracketStoreTests
{
    private readonly string connectionString;
    private static readonly DateTimeOffset DefaultStart = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DefaultEnd = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
    public TournamentBracketStoreTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task AssignTemplateShouldCopyCanvasRoundsAndDeriveMatchRound()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Swiss",
                TestContext.Current.CancellationToken);
            CanvasRoundInput[] rounds =
            [
                new(XwaScenario.ChanceEngagement, 15),
                new(XwaScenario.SalvageMission, 15),
            ];
            await stores.Templates.UpdateRoundsAsync(
                template.Value!.TournamentTemplateId,
                rounds,
                template.Value.RowVersion,
                TestContext.Current.CancellationToken);
            Result<TemplateMatch> match = await stores.Templates.AddMatchAsync(
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result<TemplateMatch> moved = await stores.Templates.UpdateMatchPositionAsync(
                match.Value!.TemplateMatchId,
                16,
                match.Value.PositionY,
                match.Value.RowVersion,
                TestContext.Current.CancellationToken);
            moved.Value!.RoundNumber.ShouldBe(2);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Event",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            Result assigned = await stores.Brackets.AssignTemplateAsync(
                tournament.Value!.TournamentId,
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            assigned.IsSuccess.ShouldBeTrue();
            Result<TournamentBracketGraph> graph = await stores.Brackets.GetGraphAsync(
                tournament.Value.TournamentId,
                TestContext.Current.CancellationToken);
            graph.IsSuccess.ShouldBeTrue();
            graph.Value!.CanvasRounds.Count.ShouldBe(2);
            graph.Value.CanvasRounds[1].Scenario.ShouldBe(XwaScenario.SalvageMission);
            graph.Value.Matches.Single().RoundNumber.ShouldBe(2);
            graph.Value.Matches.Single().Status.ShouldBe(TournamentMatchStatus.NotStarted);
        }
    }

    [Fact]
    public async Task UpdateRoundsAsyncShouldRejectWidthsThatDoNotFillCanvas()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Event",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            CanvasRoundInput[] rounds =
            [
                new(XwaScenario.AssaultAtSatelliteArray, 10),
                new(XwaScenario.AssaultAtSatelliteArray, 10),
            ];
            Result<Tournament> saved = await stores.Brackets.UpdateRoundsAsync(
                tournament.Value!.TournamentId,
                rounds,
                tournament.Value.RowVersion,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task UpdateGraphSizeShouldRejectShrinkWhenLastRoundTooNarrow()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Sized",
                TestContext.Current.CancellationToken);
            CanvasRoundInput[] rounds =
            [
                new(XwaScenario.AssaultAtSatelliteArray, 15),
                new(XwaScenario.AssaultAtSatelliteArray, 15),
            ];
            await stores.Templates.UpdateRoundsAsync(
                template.Value!.TournamentTemplateId,
                rounds,
                template.Value.RowVersion,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Event",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            await stores.Brackets.AssignTemplateAsync(
                tournament.Value!.TournamentId,
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result<TournamentBracketGraph> graph = await stores.Brackets.GetGraphAsync(
                tournament.Value.TournamentId,
                TestContext.Current.CancellationToken);
            int lastColumnWidth = graph.Value!.CanvasRounds[^1].WidthCells;
            int tooNarrowWidth = graph.Value.Tournament.GraphWidthCells
                - lastColumnWidth
                + BracketRoundLayout.MinColumnWidthCells
                - 1;
            Result<Tournament> shrunk = await stores.Brackets.UpdateGraphSizeAsync(
                tournament.Value.TournamentId,
                tooNarrowWidth,
                graph.Value.Tournament.GraphHeightCells,
                graph.Value.Tournament.RowVersion,
                TestContext.Current.CancellationToken);
            shrunk.IsSuccess.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task UpdateFormatDescriptionAsyncShouldSaveAndClear()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Event",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            const string description = "Single elimination\nBest of 3";
            Result<Tournament> saved = await stores.Brackets.UpdateFormatDescriptionAsync(
                tournament.Value!.TournamentId,
                description,
                tournament.Value.RowVersion,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeTrue();
            saved.Value!.FormatDescription.ShouldBe(description);
            Result<TournamentBracketGraph> graph = await stores.Brackets.GetGraphAsync(
                tournament.Value.TournamentId,
                TestContext.Current.CancellationToken);
            graph.Value!.Tournament.FormatDescription.ShouldBe(description);
            Result<Tournament> cleared = await stores.Brackets.UpdateFormatDescriptionAsync(
                tournament.Value.TournamentId,
                string.Empty,
                saved.Value.RowVersion,
                TestContext.Current.CancellationToken);
            cleared.IsSuccess.ShouldBeTrue();
            cleared.Value!.FormatDescription.ShouldBeNull();
        }
    }

    [Fact]
    public async Task AssignTemplateShouldCopyFormatDescription()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Swiss",
                TestContext.Current.CancellationToken);
            const string description = "4 rounds Swiss, cut to top 8";
            Result<TournamentTemplate> withFormat = await stores.Templates.UpdateFormatDescriptionAsync(
                template.Value!.TournamentTemplateId,
                description,
                template.Value.RowVersion,
                TestContext.Current.CancellationToken);
            withFormat.IsSuccess.ShouldBeTrue();
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Event",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            Result assigned = await stores.Brackets.AssignTemplateAsync(
                tournament.Value!.TournamentId,
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            assigned.IsSuccess.ShouldBeTrue();
            Result<TournamentBracketGraph> graph = await stores.Brackets.GetGraphAsync(
                tournament.Value.TournamentId,
                TestContext.Current.CancellationToken);
            graph.Value!.Tournament.FormatDescription.ShouldBe(description);
        }
    }

    [Fact]
    public async Task AssignTemplateShouldCopyGraphSize()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Sized",
                TestContext.Current.CancellationToken);
            Result<TournamentTemplate> resized = await stores.Templates.UpdateGraphSizeAsync(
                template.Value!.TournamentTemplateId,
                42,
                24,
                template.Value.RowVersion,
                TestContext.Current.CancellationToken);
            resized.IsSuccess.ShouldBeTrue();
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Event",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            Result assigned = await stores.Brackets.AssignTemplateAsync(
                tournament.Value!.TournamentId,
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            assigned.IsSuccess.ShouldBeTrue();
            Result<TournamentBracketGraph> graph = await stores.Brackets.GetGraphAsync(
                tournament.Value.TournamentId,
                TestContext.Current.CancellationToken);
            graph.Value!.Tournament.GraphWidthCells.ShouldBe(42);
            graph.Value.Tournament.GraphHeightCells.ShouldBe(24);
        }
    }

    [Fact]
    public async Task UpdateMatchResultShouldRejectWinnerWithoutPlayer()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Final",
                TestContext.Current.CancellationToken);
            await stores.Templates.AddMatchAsync(
                template.Value!.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Event",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            await stores.Brackets.AssignTemplateAsync(
                tournament.Value!.TournamentId,
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result<TournamentBracketGraph> graph = await stores.Brackets.GetGraphAsync(
                tournament.Value.TournamentId,
                TestContext.Current.CancellationToken);
            TournamentMatch match = graph.Value!.Matches.Single();
            var update = new TournamentMatchResultUpdate(
                TournamentMatchStatus.Finished,
                TournamentMatchPlayMode.Offline,
                null,
                null,
                null,
                null,
                null,
                TournamentMatchWinner.SlotA);
            Result<TournamentMatch> saved = await stores.Brackets.UpdateMatchResultAsync(
                tournament.Value.TournamentId,
                match.TournamentMatchId,
                update,
                match.RowVersion,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task UpdateMatchResultShouldAcceptValidResult()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> player = await stores.Users.CreateAsync(
                "bracket-player",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Final",
                TestContext.Current.CancellationToken);
            await stores.Templates.AddMatchAsync(
                template.Value!.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Event",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                player.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            await stores.Brackets.AssignTemplateAsync(
                tournament.Value.TournamentId,
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result<TournamentBracketGraph> graph = await stores.Brackets.GetGraphAsync(
                tournament.Value.TournamentId,
                TestContext.Current.CancellationToken);
            TournamentMatch match = graph.Value!.Matches.Single();
            var update = new TournamentMatchResultUpdate(
                TournamentMatchStatus.InProgress,
                TournamentMatchPlayMode.Online,
                "my-replay_01",
                player.Value.UserId,
                null,
                3,
                null,
                TournamentMatchWinner.SlotA);
            Result<TournamentMatch> saved = await stores.Brackets.UpdateMatchResultAsync(
                tournament.Value.TournamentId,
                match.TournamentMatchId,
                update,
                match.RowVersion,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeTrue();
            saved.Value!.Status.ShouldBe(TournamentMatchStatus.InProgress);
            saved.Value.SlotAPoints.ShouldBe(3);
            saved.Value.ReplayUrl.ShouldBe("my-replay_01");
        }
    }

    [Fact]
    public async Task UpdateMatchResultShouldRejectReplayUrlInsteadOfCode()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            (Tournament tournament, TournamentMatch match) = await CreateTournamentWithSingleMatchAsync(stores);
            var update = new TournamentMatchResultUpdate(
                TournamentMatchStatus.NotStarted,
                TournamentMatchPlayMode.Online,
                "https://example.com/replay",
                null,
                null,
                null,
                null,
                TournamentMatchWinner.None);
            Result<TournamentMatch> saved = await stores.Brackets.UpdateMatchResultAsync(
                tournament.TournamentId,
                match.TournamentMatchId,
                update,
                match.RowVersion,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task UpdateReplayCodeShouldAllowMatchParticipant()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> player = await stores.Users.CreateAsync(
                "slot-a",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            (Tournament tournament, TournamentMatch match) = await CreateTournamentWithSingleMatchAsync(stores);
            await stores.Tournaments.JoinAsync(
                tournament.TournamentId,
                player.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            var setup = new TournamentMatchResultUpdate(
                TournamentMatchStatus.InProgress,
                TournamentMatchPlayMode.Online,
                null,
                player.Value.UserId,
                null,
                null,
                null,
                TournamentMatchWinner.None);
            Result<TournamentMatch> seeded = await stores.Brackets.UpdateMatchResultAsync(
                tournament.TournamentId,
                match.TournamentMatchId,
                setup,
                match.RowVersion,
                TestContext.Current.CancellationToken);
            seeded.IsSuccess.ShouldBeTrue();
            Result<TournamentMatch> saved = await stores.Brackets.UpdateReplayCodeAsync(
                tournament.TournamentId,
                match.TournamentMatchId,
                "live-code",
                player.Value.UserId,
                actingUserIsAdmin: false,
                seeded.Value!.RowVersion,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeTrue();
            saved.Value!.ReplayUrl.ShouldBe("live-code");
        }
    }

    [Fact]
    public async Task UpdateReplayCodeShouldRejectNonParticipant()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> slotPlayer = await stores.Users.CreateAsync(
                "slot-a",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<UserAccount> outsider = await stores.Users.CreateAsync(
                "outsider",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            (Tournament tournament, TournamentMatch match) = await CreateTournamentWithSingleMatchAsync(stores);
            await stores.Tournaments.JoinAsync(
                tournament.TournamentId,
                slotPlayer.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            var setup = new TournamentMatchResultUpdate(
                TournamentMatchStatus.InProgress,
                TournamentMatchPlayMode.Online,
                null,
                slotPlayer.Value.UserId,
                null,
                null,
                null,
                TournamentMatchWinner.None);
            Result<TournamentMatch> seeded = await stores.Brackets.UpdateMatchResultAsync(
                tournament.TournamentId,
                match.TournamentMatchId,
                setup,
                match.RowVersion,
                TestContext.Current.CancellationToken);
            seeded.IsSuccess.ShouldBeTrue();
            Result<TournamentMatch> saved = await stores.Brackets.UpdateReplayCodeAsync(
                tournament.TournamentId,
                match.TournamentMatchId,
                "nope",
                outsider.Value!.UserId,
                actingUserIsAdmin: false,
                seeded.Value!.RowVersion,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task UpdateReplayCodeShouldAllowAdmin()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> admin = await stores.Users.CreateAsync(
                "admin",
                "secret",
                UserRoles.Admin,
                TestContext.Current.CancellationToken);
            (Tournament tournament, TournamentMatch match) = await CreateTournamentWithSingleMatchAsync(stores);
            Result<TournamentMatch> saved = await stores.Brackets.UpdateReplayCodeAsync(
                tournament.TournamentId,
                match.TournamentMatchId,
                "admin-set",
                admin.Value!.UserId,
                actingUserIsAdmin: true,
                match.RowVersion,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeTrue();
            saved.Value!.ReplayUrl.ShouldBe("admin-set");
        }
    }

    [Fact]
    public async Task UpdateMatchResultShouldPropagateWinnerAndLoserFeeds()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> playerA = await stores.Users.CreateAsync(
                "player-a",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<UserAccount> playerB = await stores.Users.CreateAsync(
                "player-b",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Bracket",
                TestContext.Current.CancellationToken);
            Result<TemplateMatch> templateMatch1 = await stores.Templates.AddMatchAsync(
                template.Value!.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result<TemplateMatch> templateMatch2 = await stores.Templates.AddMatchAsync(
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            await stores.Templates.SetSlotFeedAsync(
                templateMatch2.Value!.TemplateMatchId,
                TemplateMatchSlot.A,
                templateMatch1.Value!.TemplateMatchId,
                TemplateFeedOutcome.Winner,
                templateMatch2.Value.RowVersion,
                TestContext.Current.CancellationToken);
            await stores.Templates.SetSlotFeedAsync(
                templateMatch2.Value.TemplateMatchId,
                TemplateMatchSlot.B,
                templateMatch1.Value.TemplateMatchId,
                TemplateFeedOutcome.Loser,
                templateMatch2.Value.RowVersion,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Event",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                playerA.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value.TournamentId,
                playerB.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            await stores.Brackets.AssignTemplateAsync(
                tournament.Value.TournamentId,
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result<TournamentBracketGraph> graph = await stores.Brackets.GetGraphAsync(
                tournament.Value.TournamentId,
                TestContext.Current.CancellationToken);
            TournamentMatch[] matches = graph.Value!.Matches.OrderBy(m => m.TournamentMatchId).ToArray();
            TournamentMatch semifinal = matches[0];
            TournamentMatch finalMatch = matches[1];
            var semifinalResult = new TournamentMatchResultUpdate(
                TournamentMatchStatus.Finished,
                TournamentMatchPlayMode.Online,
                null,
                playerA.Value.UserId,
                playerB.Value.UserId,
                20,
                10,
                TournamentMatchWinner.SlotA);
            Result<TournamentMatch> saved = await stores.Brackets.UpdateMatchResultAsync(
                tournament.Value.TournamentId,
                semifinal.TournamentMatchId,
                semifinalResult,
                semifinal.RowVersion,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeTrue();
            Result<TournamentBracketGraph> after = await stores.Brackets.GetGraphAsync(
                tournament.Value.TournamentId,
                TestContext.Current.CancellationToken);
            TournamentMatch updatedFinal = after.Value!.Matches.Single(m => m.TournamentMatchId == finalMatch.TournamentMatchId);
            updatedFinal.SlotAUserId.ShouldBe(playerA.Value.UserId);
            updatedFinal.SlotBUserId.ShouldBe(playerB.Value.UserId);
        }
    }

    [Fact]
    public async Task SetFinaleMatchAsyncShouldPersistAndRejectForeignMatch()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            (Tournament tournament, TournamentMatch match) = await CreateTournamentWithSingleMatchAsync(stores);
            Result<Tournament> setFinale = await stores.Brackets.SetFinaleMatchAsync(
                tournament.TournamentId,
                match.TournamentMatchId,
                tournament.RowVersion,
                TestContext.Current.CancellationToken);
            setFinale.IsSuccess.ShouldBeTrue();
            setFinale.Value!.FinaleTournamentMatchId.ShouldBe(match.TournamentMatchId);

            Result<TournamentTemplate> otherTemplate = await stores.Templates.CreateAsync(
                "Other",
                TestContext.Current.CancellationToken);
            await stores.Templates.AddMatchAsync(
                otherTemplate.Value!.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result<Tournament> otherTournament = await stores.Tournaments.CreateAsync(
                "Other event",
                DefaultStart,
                DefaultEnd,
                false,
                TestContext.Current.CancellationToken);
            await stores.Brackets.AssignTemplateAsync(
                otherTournament.Value!.TournamentId,
                otherTemplate.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result<TournamentBracketGraph> otherGraph = await stores.Brackets.GetGraphAsync(
                otherTournament.Value.TournamentId,
                TestContext.Current.CancellationToken);
            TournamentMatch foreignMatch = otherGraph.Value!.Matches.Single();

            Result<Tournament> reject = await stores.Brackets.SetFinaleMatchAsync(
                tournament.TournamentId,
                foreignMatch.TournamentMatchId,
                setFinale.Value.RowVersion,
                TestContext.Current.CancellationToken);
            reject.IsSuccess.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task UpdateMatchResultOnFinaleShouldSetTournamentWinnerUserId()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> player = await stores.Users.CreateAsync(
                "champion",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            (Tournament tournament, TournamentMatch match) = await CreateTournamentWithSingleMatchAsync(stores);
            await stores.Tournaments.JoinAsync(
                tournament.TournamentId,
                player.Value!.UserId,
                null,
                TestContext.Current.CancellationToken);
            Result<Tournament> setFinale = await stores.Brackets.SetFinaleMatchAsync(
                tournament.TournamentId,
                match.TournamentMatchId,
                tournament.RowVersion,
                TestContext.Current.CancellationToken);
            setFinale.IsSuccess.ShouldBeTrue();
            Result<TournamentBracketGraph> graph = await stores.Brackets.GetGraphAsync(
                tournament.TournamentId,
                TestContext.Current.CancellationToken);
            match = graph.Value!.Matches.Single();
            var update = new TournamentMatchResultUpdate(
                TournamentMatchStatus.Finished,
                TournamentMatchPlayMode.Offline,
                null,
                player.Value.UserId,
                null,
                1,
                null,
                TournamentMatchWinner.SlotA);
            Result<TournamentMatch> saved = await stores.Brackets.UpdateMatchResultAsync(
                tournament.TournamentId,
                match.TournamentMatchId,
                update,
                match.RowVersion,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeTrue();
            Tournament? refreshed = await stores.Tournaments.GetAsync(
                tournament.TournamentId,
                TestContext.Current.CancellationToken);
            refreshed!.WinnerUserId.ShouldBe(player.Value.UserId);
        }
    }

    [Fact]
    public async Task DeleteFinaleMatchShouldClearFinaleDesignation()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            (Tournament tournament, TournamentMatch match) = await CreateTournamentWithSingleMatchAsync(stores);
            Result<Tournament> setFinale = await stores.Brackets.SetFinaleMatchAsync(
                tournament.TournamentId,
                match.TournamentMatchId,
                tournament.RowVersion,
                TestContext.Current.CancellationToken);
            setFinale.IsSuccess.ShouldBeTrue();
            Result delete = await stores.Brackets.DeleteMatchAsync(
                match.TournamentMatchId,
                match.RowVersion,
                TestContext.Current.CancellationToken);
            delete.IsSuccess.ShouldBeTrue();
            Tournament? refreshed = await stores.Tournaments.GetAsync(
                tournament.TournamentId,
                TestContext.Current.CancellationToken);
            refreshed!.FinaleTournamentMatchId.ShouldBeNull();
        }
    }

    private static async Task<(Tournament Tournament, TournamentMatch Match)> CreateTournamentWithSingleMatchAsync(
        TestStores stores)
    {
        Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
            "Bracket",
            TestContext.Current.CancellationToken);
        await stores.Templates.AddMatchAsync(
            template.Value!.TournamentTemplateId,
            TestContext.Current.CancellationToken);
        Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
            "Event",
            DefaultStart,
            DefaultEnd,
            false,
            TestContext.Current.CancellationToken);
        await stores.Brackets.AssignTemplateAsync(
            tournament.Value!.TournamentId,
            template.Value.TournamentTemplateId,
            TestContext.Current.CancellationToken);
        Tournament? reloaded = await stores.Tournaments.GetAsync(
            tournament.Value.TournamentId,
            TestContext.Current.CancellationToken);
        Result<TournamentBracketGraph> graph = await stores.Brackets.GetGraphAsync(
            tournament.Value.TournamentId,
            TestContext.Current.CancellationToken);
        return (reloaded!, graph.Value!.Matches.Single());
    }
}
