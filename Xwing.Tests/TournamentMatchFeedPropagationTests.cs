using Xwing.Web.Data;
using Xwing.Web.Models;
using Xwing.Web.Stores;

namespace Xwing.Tests;

public sealed class TournamentMatchFeedPropagationTests
{
    [Fact]
    public void ApplyOutgoingFeedsShouldAssignWinnerAndLoserToTargetSlots()
    {
        var source = new TournamentMatch
        {
            TournamentMatchId = 1,
            SlotAUserId = 10,
            SlotBUserId = 20,
            Winner = TournamentMatchWinner.SlotA,
        };
        var target = new TournamentMatch { TournamentMatchId = 2 };
        TournamentMatchFeed[] feeds =
        [
            new()
            {
                SourceTournamentMatchId = 1,
                TargetMatch = target,
                TargetSlot = TemplateMatchSlot.A,
                SourceOutcome = TemplateFeedOutcome.Winner,
            },
            new()
            {
                SourceTournamentMatchId = 1,
                TargetMatch = target,
                TargetSlot = TemplateMatchSlot.B,
                SourceOutcome = TemplateFeedOutcome.Loser,
            },
        ];

        Result? result = TournamentMatchFeedPropagation.ApplyOutgoingFeeds(source, feeds);

        result.ShouldBeNull();
        target.SlotAUserId.ShouldBe(10);
        target.SlotBUserId.ShouldBe(20);
    }

    [Fact]
    public void ApplyOutgoingFeedsShouldClearFedSlotsWhenWinnerCleared()
    {
        var source = new TournamentMatch
        {
            TournamentMatchId = 1,
            SlotAUserId = 10,
            SlotBUserId = 20,
            Winner = TournamentMatchWinner.None,
        };
        var target = new TournamentMatch
        {
            TournamentMatchId = 2,
            SlotAUserId = 10,
            SlotBUserId = 99,
        };
        TournamentMatchFeed[] feeds =
        [
            new()
            {
                SourceTournamentMatchId = 1,
                TargetMatch = target,
                TargetSlot = TemplateMatchSlot.A,
                SourceOutcome = TemplateFeedOutcome.Winner,
            },
        ];

        Result? result = TournamentMatchFeedPropagation.ApplyOutgoingFeeds(source, feeds);

        result.ShouldBeNull();
        target.SlotAUserId.ShouldBeNull();
        target.SlotBUserId.ShouldBe(99);
    }
}
