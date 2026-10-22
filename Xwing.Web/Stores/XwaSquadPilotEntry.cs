namespace Xwing.Web.Stores;

public sealed record XwaSquadPilotEntry
{
    public required string ShipId { get; init; }

    public string? PilotId { get; init; }
}
