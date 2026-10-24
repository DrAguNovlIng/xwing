using Xwing.Web.Data;
using Xwing.Web.Models;
using Xwing.Web.Stores;

namespace Xwing.Tests;

[Collection("SQL Server Integration Tests")]
public sealed class TournamentTemplateStoreTests
{
    private readonly string connectionString;
    public TournamentTemplateStoreTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task AddMatchAsyncShouldPlaceMatchAtCanvasCenter()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Center test",
                TestContext.Current.CancellationToken);
            template.IsSuccess.ShouldBeTrue();
            (int expectedX, int expectedY) = BracketGraphLayout.CenterMatchPosition(
                template.Value!.GraphWidthCells,
                template.Value.GraphHeightCells);
            Result<TemplateMatch> match = await stores.Templates.AddMatchAsync(
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            match.IsSuccess.ShouldBeTrue();
            match.Value!.PositionX.ShouldBe(expectedX);
            match.Value.PositionY.ShouldBe(expectedY);
        }
    }

    [Fact]
    public async Task SetSlotFeedAsyncShouldWireWinnerChain()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Bracket",
                TestContext.Current.CancellationToken);
            template.IsSuccess.ShouldBeTrue();
            Result<TemplateMatch> match1 = await stores.Templates.AddMatchAsync(
                template.Value!.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result<TemplateMatch> match2 = await stores.Templates.AddMatchAsync(
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result feedResult = await stores.Templates.SetSlotFeedAsync(
                match2.Value!.TemplateMatchId,
                TemplateMatchSlot.A,
                match1.Value!.TemplateMatchId,
                TemplateFeedOutcome.Winner,
                match2.Value.RowVersion,
                TestContext.Current.CancellationToken);
            feedResult.IsSuccess.ShouldBeTrue();
            Result<TournamentTemplateGraph> graph = await stores.Templates.GetGraphAsync(
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            graph.Value!.Feeds.Count.ShouldBe(1);
            graph.Value.Feeds[0].SourceTemplateMatchId.ShouldBe(match1.Value.TemplateMatchId);
            graph.Value.Feeds[0].TargetTemplateMatchId.ShouldBe(match2.Value.TemplateMatchId);
        }
    }

    [Fact]
    public async Task SetSlotFeedAsyncShouldRejectSecondFeedIntoSameSlot()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            int templateId = await CreateTemplateWithThreeMatchesAsync(stores);
            Result<TournamentTemplateGraph> graph = await stores.Templates.GetGraphAsync(
                templateId,
                TestContext.Current.CancellationToken);
            TemplateMatch m1 = graph.Value!.Matches[0];
            TemplateMatch m2 = graph.Value.Matches[1];
            TemplateMatch m3 = graph.Value.Matches[2];
            (await stores.Templates.SetSlotFeedAsync(
                m3.TemplateMatchId,
                TemplateMatchSlot.A,
                m1.TemplateMatchId,
                TemplateFeedOutcome.Winner,
                m3.RowVersion,
                TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
            graph = await stores.Templates.GetGraphAsync(templateId, TestContext.Current.CancellationToken);
            m3 = graph.Value!.Matches.Single(m => m.TemplateMatchId == m3.TemplateMatchId);
            Result secondFeed = await stores.Templates.SetSlotFeedAsync(
                m3.TemplateMatchId,
                TemplateMatchSlot.B,
                m1.TemplateMatchId,
                TemplateFeedOutcome.Winner,
                m3.RowVersion,
                TestContext.Current.CancellationToken);
            secondFeed.IsSuccess.ShouldBeFalse();
            secondFeed.Error.ShouldBe("That outcome already feeds another slot.");
            graph = await stores.Templates.GetGraphAsync(templateId, TestContext.Current.CancellationToken);
            m3 = graph.Value!.Matches.Single(m => m.TemplateMatchId == m3.TemplateMatchId);
            (await stores.Templates.SetSlotFeedAsync(
                m3.TemplateMatchId,
                TemplateMatchSlot.A,
                m2.TemplateMatchId,
                TemplateFeedOutcome.Winner,
                m3.RowVersion,
                TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
            graph = await stores.Templates.GetGraphAsync(templateId, TestContext.Current.CancellationToken);
            graph.Value!.Feeds.Count.ShouldBe(1);
            graph.Value.Feeds[0].TargetSlot.ShouldBe(TemplateMatchSlot.A);
            graph.Value.Feeds[0].SourceTemplateMatchId.ShouldBe(m2.TemplateMatchId);
        }
    }

    [Fact]
    public async Task SetSlotFeedAsyncShouldRejectCycle()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            int templateId = await CreateTemplateWithThreeMatchesAsync(stores);
            Result<TournamentTemplateGraph> graph = await stores.Templates.GetGraphAsync(
                templateId,
                TestContext.Current.CancellationToken);
            TemplateMatch m1 = graph.Value!.Matches[0];
            TemplateMatch m2 = graph.Value.Matches[1];
            TemplateMatch m3 = graph.Value.Matches[2];
            (await stores.Templates.SetSlotFeedAsync(
                m2.TemplateMatchId,
                TemplateMatchSlot.A,
                m1.TemplateMatchId,
                TemplateFeedOutcome.Winner,
                m2.RowVersion,
                TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
            graph = await stores.Templates.GetGraphAsync(templateId, TestContext.Current.CancellationToken);
            m2 = graph.Value!.Matches.Single(m => m.TemplateMatchId == m2.TemplateMatchId);
            m3 = graph.Value.Matches.Single(m => m.TemplateMatchId == m3.TemplateMatchId);
            (await stores.Templates.SetSlotFeedAsync(
                m3.TemplateMatchId,
                TemplateMatchSlot.A,
                m2.TemplateMatchId,
                TemplateFeedOutcome.Winner,
                m3.RowVersion,
                TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
            graph = await stores.Templates.GetGraphAsync(templateId, TestContext.Current.CancellationToken);
            m1 = graph.Value!.Matches.Single(m => m.TemplateMatchId == m1.TemplateMatchId);
            m3 = graph.Value.Matches.Single(m => m.TemplateMatchId == m3.TemplateMatchId);
            Result cycle = await stores.Templates.SetSlotFeedAsync(
                m1.TemplateMatchId,
                TemplateMatchSlot.A,
                m3.TemplateMatchId,
                TemplateFeedOutcome.Winner,
                m1.RowVersion,
                TestContext.Current.CancellationToken);
            cycle.IsSuccess.ShouldBeFalse();
            cycle.Error.ShouldBe("That connection would create a cycle.");
        }
    }

    [Fact]
    public async Task SetSlotFeedAsyncShouldAllowWinnerAndLoserFromSameMatch()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Double elim seed",
                TestContext.Current.CancellationToken);
            Result<TemplateMatch> source = await stores.Templates.AddMatchAsync(
                template.Value!.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result<TemplateMatch> target = await stores.Templates.AddMatchAsync(
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            (await stores.Templates.SetSlotFeedAsync(
                target.Value!.TemplateMatchId,
                TemplateMatchSlot.A,
                source.Value!.TemplateMatchId,
                TemplateFeedOutcome.Winner,
                target.Value.RowVersion,
                TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
            Result<TournamentTemplateGraph> graph = await stores.Templates.GetGraphAsync(
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            TemplateMatch targetMatch = graph.Value!.Matches.Single(m => m.TemplateMatchId == target.Value.TemplateMatchId);
            (await stores.Templates.SetSlotFeedAsync(
                targetMatch.TemplateMatchId,
                TemplateMatchSlot.B,
                source.Value.TemplateMatchId,
                TemplateFeedOutcome.Loser,
                targetMatch.RowVersion,
                TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
            graph = await stores.Templates.GetGraphAsync(
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            graph.Value!.Feeds.Count.ShouldBe(2);
            graph.Value.Feeds.ShouldContain(f => f.SourceOutcome == TemplateFeedOutcome.Winner);
            graph.Value.Feeds.ShouldContain(f => f.SourceOutcome == TemplateFeedOutcome.Loser);
            targetMatch = graph.Value.Matches.Single(m => m.TemplateMatchId == target.Value.TemplateMatchId);
            Result duplicateLoser = await stores.Templates.SetSlotFeedAsync(
                targetMatch.TemplateMatchId,
                TemplateMatchSlot.A,
                source.Value.TemplateMatchId,
                TemplateFeedOutcome.Loser,
                targetMatch.RowVersion,
                TestContext.Current.CancellationToken);
            duplicateLoser.IsSuccess.ShouldBeFalse();
            duplicateLoser.Error.ShouldBe("That outcome already feeds another slot.");
        }
    }

    [Fact]
    public async Task UpdateSlotOpenLabelAsyncShouldPersistCustomEntrantText()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Labels",
                TestContext.Current.CancellationToken);
            Result<TemplateMatch> match = await stores.Templates.AddMatchAsync(
                template.Value!.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result<TemplateMatch> update = await stores.Templates.UpdateSlotOpenLabelAsync(
                match.Value!.TemplateMatchId,
                TemplateMatchSlot.A,
                "Player signup",
                match.Value.RowVersion,
                TestContext.Current.CancellationToken);
            update.IsSuccess.ShouldBeTrue();
            update.Value!.SlotAOpenLabel.ShouldBe("Player signup");
        }
    }

    [Fact]
    public async Task DeleteMatchAsyncShouldRemoveDependentFeeds()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Bracket",
                TestContext.Current.CancellationToken);
            Result<TemplateMatch> match1 = await stores.Templates.AddMatchAsync(
                template.Value!.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            Result<TemplateMatch> match2 = await stores.Templates.AddMatchAsync(
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            (await stores.Templates.SetSlotFeedAsync(
                match2.Value!.TemplateMatchId,
                TemplateMatchSlot.B,
                match1.Value!.TemplateMatchId,
                TemplateFeedOutcome.Winner,
                match2.Value.RowVersion,
                TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
            Result delete = await stores.Templates.DeleteMatchAsync(
                match1.Value.TemplateMatchId,
                match1.Value.RowVersion,
                TestContext.Current.CancellationToken);
            delete.IsSuccess.ShouldBeTrue();
            Result<TournamentTemplateGraph> graph = await stores.Templates.GetGraphAsync(
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            graph.Value!.Feeds.ShouldBeEmpty();
            graph.Value.Matches.Count.ShouldBe(1);
        }
    }

    [Fact]
    public async Task UpdateRoundsAsyncShouldSaveClearAndSyncMatchRound()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Rounds",
                TestContext.Current.CancellationToken);
            CanvasRoundInput[] rounds =
            [
                new(XwaScenario.ScrambleTheTransmissions, 15),
                new(XwaScenario.AncientKnowledge, 15),
            ];
            Result<TournamentTemplate> saved = await stores.Templates.UpdateRoundsAsync(
                template.Value!.TournamentTemplateId,
                rounds,
                template.Value.RowVersion,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeTrue();
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
            Result<TournamentTemplateGraph> graph = await stores.Templates.GetGraphAsync(
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            graph.Value!.CanvasRounds.Count.ShouldBe(2);
            Result<TournamentTemplate> cleared = await stores.Templates.UpdateRoundsAsync(
                template.Value.TournamentTemplateId,
                [],
                saved.Value!.RowVersion,
                TestContext.Current.CancellationToken);
            cleared.IsSuccess.ShouldBeTrue();
            graph = await stores.Templates.GetGraphAsync(
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            graph.Value!.CanvasRounds.ShouldBeEmpty();
            graph.Value.Matches.Single().RoundNumber.ShouldBeNull();
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
            Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
                "Format",
                TestContext.Current.CancellationToken);
            const string description = "Swiss\n5 rounds";
            Result<TournamentTemplate> saved = await stores.Templates.UpdateFormatDescriptionAsync(
                template.Value!.TournamentTemplateId,
                description,
                template.Value.RowVersion,
                TestContext.Current.CancellationToken);
            saved.IsSuccess.ShouldBeTrue();
            saved.Value!.FormatDescription.ShouldBe(description);
            Result<TournamentTemplateGraph> graph = await stores.Templates.GetGraphAsync(
                template.Value.TournamentTemplateId,
                TestContext.Current.CancellationToken);
            graph.Value!.Template.FormatDescription.ShouldBe(description);
            Result<TournamentTemplate> cleared = await stores.Templates.UpdateFormatDescriptionAsync(
                template.Value.TournamentTemplateId,
                "   ",
                saved.Value.RowVersion,
                TestContext.Current.CancellationToken);
            cleared.IsSuccess.ShouldBeTrue();
            cleared.Value!.FormatDescription.ShouldBeNull();
            string tooLong = new('x', 4001);
            Result<TournamentTemplate> rejected = await stores.Templates.UpdateFormatDescriptionAsync(
                template.Value.TournamentTemplateId,
                tooLong,
                cleared.Value.RowVersion,
                TestContext.Current.CancellationToken);
            rejected.IsSuccess.ShouldBeFalse();
        }
    }

    private static async Task<int> CreateTemplateWithThreeMatchesAsync(TestStores stores)
    {
        Result<TournamentTemplate> template = await stores.Templates.CreateAsync(
            "Three",
            TestContext.Current.CancellationToken);
        template.IsSuccess.ShouldBeTrue();
        for (int i = 0; i < 3; i++)
        {
            (await stores.Templates.AddMatchAsync(
                template.Value!.TournamentTemplateId,
                TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        }
        return template.Value!.TournamentTemplateId;
    }
}
