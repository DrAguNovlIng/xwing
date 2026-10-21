namespace Xwing.Web.Stores;

public sealed record TournamentSearchQuery
{
    public string? NameSearch { get; init; }
    public TournamentWhenFilter WhenFilter { get; init; } = TournamentWhenFilter.All;

    public TournamentSortField SortField { get; init; } = TournamentSortField.StartDate;

    public bool SortDescending { get; init; }
}
