using Xwing.Web.Data;
using Xwing.Web.Stores;

namespace Xwing.Tests;

public sealed class TournamentStandingsTests
{
    private static readonly TournamentBracketParticipant PlayerA = new(1, "Alice", true);
    private static readonly TournamentBracketParticipant PlayerB = new(2, "Bob", true);

    [Fact]
    public void CalculateWinShouldIncrementWinsAndPoints()
    {
        var matches = new[]
        {
            Match(round: 1, slotA: 1, slotB: 2, winner: TournamentMatchWinner.SlotA, pointsA: 20, pointsB: 10),
        };
        var filter = new TournamentStandingsFilter(1, 1, IncludeMatchesWithoutRound: true);

        IReadOnlyList<TournamentStandingsRow> rows = TournamentStandings.Calculate(
            [PlayerA, PlayerB],
            matches,
            filter);

        TournamentStandingsRow alice = rows.Single(r => r.UserId == 1);
        alice.Wins.ShouldBe(1);
        alice.Losses.ShouldBe(0);
        alice.MatchesPlayed.ShouldBe(1);
        alice.Points.ShouldBe(20);

        TournamentStandingsRow bob = rows.Single(r => r.UserId == 2);
        bob.Wins.ShouldBe(0);
        bob.Losses.ShouldBe(1);
        bob.MatchesPlayed.ShouldBe(1);
        bob.Points.ShouldBe(10);
    }

    [Fact]
    public void CalculateNeitherWinnerShouldCountPlayedAndPointsWithoutWinOrLoss()
    {
        var matches = new[]
        {
            Match(round: 2, slotA: 1, slotB: 2, winner: TournamentMatchWinner.None, pointsA: 5, pointsB: 7),
        };
        var filter = new TournamentStandingsFilter(null, null, IncludeMatchesWithoutRound: true);

        IReadOnlyList<TournamentStandingsRow> rows = TournamentStandings.Calculate(
            [PlayerA, PlayerB],
            matches,
            filter);

        TournamentStandingsRow alice = rows.Single(r => r.UserId == 1);
        alice.Wins.ShouldBe(0);
        alice.Losses.ShouldBe(0);
        alice.MatchesPlayed.ShouldBe(1);
        alice.Points.ShouldBe(5);

        TournamentStandingsRow bob = rows.Single(r => r.UserId == 2);
        bob.Wins.ShouldBe(0);
        bob.Losses.ShouldBe(0);
        bob.MatchesPlayed.ShouldBe(1);
        bob.Points.ShouldBe(7);
    }

    [Fact]
    public void CalculateOutOfRoundRangeShouldExcludeMatch()
    {
        var matches = new[]
        {
            Match(round: 3, slotA: 1, slotB: 2, winner: TournamentMatchWinner.SlotA, pointsA: 20, pointsB: 10),
        };
        var filter = new TournamentStandingsFilter(1, 2, IncludeMatchesWithoutRound: true);

        IReadOnlyList<TournamentStandingsRow> rows = TournamentStandings.Calculate(
            [PlayerA, PlayerB],
            matches,
            filter);

        rows.Single(r => r.UserId == 1).MatchesPlayed.ShouldBe(0);
        rows.Single(r => r.UserId == 2).MatchesPlayed.ShouldBe(0);
    }

    [Fact]
    public void CalculateNullRoundExcludedWhenIncludeUnchecked()
    {
        var matches = new[]
        {
            Match(round: null, slotA: 1, slotB: 2, winner: TournamentMatchWinner.SlotB, pointsA: 3, pointsB: 4),
        };
        var filter = new TournamentStandingsFilter(null, null, IncludeMatchesWithoutRound: false);

        IReadOnlyList<TournamentStandingsRow> rows = TournamentStandings.Calculate(
            [PlayerA, PlayerB],
            matches,
            filter);

        rows.Single(r => r.UserId == 1).MatchesPlayed.ShouldBe(0);
        rows.Single(r => r.UserId == 2).MatchesPlayed.ShouldBe(0);
    }

    [Fact]
    public void CalculateNullRoundIncludedWhenIncludeChecked()
    {
        var matches = new[]
        {
            Match(round: null, slotA: 1, slotB: 2, winner: TournamentMatchWinner.SlotB, pointsA: 3, pointsB: 4),
        };
        var filter = new TournamentStandingsFilter(null, null, IncludeMatchesWithoutRound: true);

        IReadOnlyList<TournamentStandingsRow> rows = TournamentStandings.Calculate(
            [PlayerA, PlayerB],
            matches,
            filter);

        rows.Single(r => r.UserId == 1).Losses.ShouldBe(1);
        rows.Single(r => r.UserId == 2).Wins.ShouldBe(1);
    }

    private static TournamentMatch Match(
        int? round,
        int slotA,
        int slotB,
        TournamentMatchWinner winner,
        int? pointsA,
        int? pointsB) =>
        new()
        {
            RoundNumber = round,
            SlotAUserId = slotA,
            SlotBUserId = slotB,
            Winner = winner,
            SlotAPoints = pointsA,
            SlotBPoints = pointsB,
        };
}
