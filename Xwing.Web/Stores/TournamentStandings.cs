using Xwing.Web.Data;

namespace Xwing.Web.Stores;

public sealed record TournamentStandingsFilter(
    int? MinRoundInclusive,
    int? MaxRoundInclusive,
    bool IncludeMatchesWithoutRound = true);

public sealed record TournamentStandingsRow(
    int UserId,
    string UserName,
    int Wins,
    int Losses,
    int MatchesPlayed,
    int Points);

public static class TournamentStandings
{
    public static IReadOnlyList<TournamentStandingsRow> Calculate(
        IReadOnlyList<TournamentBracketParticipant> participants,
        IReadOnlyList<TournamentMatch> matches,
        TournamentStandingsFilter filter)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(filter);

        var rows = new List<TournamentStandingsRow>(participants.Count);
        foreach (TournamentBracketParticipant participant in participants)
        {
            int wins = 0;
            int losses = 0;
            int played = 0;
            int points = 0;

            foreach (TournamentMatch match in matches)
            {
                if (!MatchesRoundFilter(match, filter))
                {
                    continue;
                }

                bool inSlotA = match.SlotAUserId == participant.UserId;
                bool inSlotB = match.SlotBUserId == participant.UserId;
                if (!inSlotA && !inSlotB)
                {
                    continue;
                }

                if (match.Status != TournamentMatchStatus.Finished)
                {
                    continue;
                }

                played++;
                points += inSlotA ? match.SlotAPoints ?? 0 : match.SlotBPoints ?? 0;

                switch (GetParticipantOutcome(match, inSlotA))
                {
                    case ParticipantMatchOutcome.Win:
                        wins++;
                        break;
                    case ParticipantMatchOutcome.Loss:
                        losses++;
                        break;
                }
            }

            rows.Add(new TournamentStandingsRow(
                participant.UserId,
                participant.UserName,
                wins,
                losses,
                played,
                points));
        }

        rows.Sort(static (a, b) =>
        {
            int byWins = b.Wins.CompareTo(a.Wins);
            if (byWins != 0)
            {
                return byWins;
            }

            int byPoints = b.Points.CompareTo(a.Points);
            if (byPoints != 0)
            {
                return byPoints;
            }

            return string.Compare(a.UserName, b.UserName, StringComparison.OrdinalIgnoreCase);
        });

        return rows;
    }

    internal static bool MatchesRoundFilter(TournamentMatch match, TournamentStandingsFilter filter)
    {
        if (match.RoundNumber is not int round)
        {
            return filter.IncludeMatchesWithoutRound;
        }

        if (filter.MinRoundInclusive is int min && round < min)
        {
            return false;
        }

        if (filter.MaxRoundInclusive is int max && round > max)
        {
            return false;
        }

        return true;
    }

    private static ParticipantMatchOutcome GetParticipantOutcome(TournamentMatch match, bool participantInSlotA) =>
        match.Winner switch
        {
            TournamentMatchWinner.SlotA when participantInSlotA => ParticipantMatchOutcome.Win,
            TournamentMatchWinner.SlotB when !participantInSlotA => ParticipantMatchOutcome.Win,
            TournamentMatchWinner.SlotA when !participantInSlotA => ParticipantMatchOutcome.Loss,
            TournamentMatchWinner.SlotB when participantInSlotA => ParticipantMatchOutcome.Loss,
            _ => ParticipantMatchOutcome.Neither,
        };

    private enum ParticipantMatchOutcome
    {
        Neither,
        Win,
        Loss,
    }
}
