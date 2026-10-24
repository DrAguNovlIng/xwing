using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;

public static class TournamentMatchFeedPropagation
{
    public static Result? ApplyOutgoingFeeds(
        TournamentMatch sourceMatch,
        IReadOnlyList<TournamentMatchFeed> outgoingFeeds)
    {
        ArgumentNullException.ThrowIfNull(sourceMatch);
        ArgumentNullException.ThrowIfNull(outgoingFeeds);

        foreach (TournamentMatchFeed feed in outgoingFeeds)
        {
            int? userId = ResolveFedUserId(sourceMatch, feed.SourceOutcome);
            Result? slotResult = SetSlotUser(feed.TargetMatch, feed.TargetSlot, userId);
            if (slotResult is not null)
            {
                return slotResult;
            }
        }

        return null;
    }

    private static int? ResolveFedUserId(TournamentMatch source, TemplateFeedOutcome outcome)
    {
        if (source.Winner == TournamentMatchWinner.None)
        {
            return null;
        }

        int? winnerId = source.Winner == TournamentMatchWinner.SlotA ? source.SlotAUserId : source.SlotBUserId;
        int? loserId = source.Winner == TournamentMatchWinner.SlotA ? source.SlotBUserId : source.SlotAUserId;
        return outcome == TemplateFeedOutcome.Winner ? winnerId : loserId;
    }

    private static Result? SetSlotUser(TournamentMatch target, TemplateMatchSlot slot, int? userId)
    {
        if (slot == TemplateMatchSlot.A)
        {
            target.SlotAUserId = userId;
        }
        else
        {
            target.SlotBUserId = userId;
        }

        if (target.SlotAUserId is not null && target.SlotAUserId == target.SlotBUserId)
        {
            return Result.Fail("Feed assignment would put the same player in both slots.");
        }

        return null;
    }
}
