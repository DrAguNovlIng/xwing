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
    public async Task AssignTemplateShouldCopyRoundAndFeeds()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Swiss",
                TestContext.Current.CancellationToken);
            Result<TemplateMatch> match = await stores.Templates.AddMatchAsync(
                template.Value!.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            await stores.Templates.UpdateMatchRoundAsync(
                match.Value!.TemplateMatchId,
                2,
                match.Value.RowVersion,
                TestContext.Current.CancellationToken);
            Result<Tournament> tournament = await stores.Tournaments.CreateAsync(
                "Event",
                DefaultStart,
                DefaultEnd,
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
            graph.Value!.Matches.Single().RoundNumber.ShouldBe(2);
            graph.Value.Matches.Single().Status.ShouldBe(TournamentMatchStatus.NotStarted);
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
                TestContext.Current.CancellationToken);
            await stores.Tournaments.JoinAsync(
                tournament.Value!.TournamentId,
                player.Value!.UserId,
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
                "https://example.com/replay",
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
        }
    }
}
