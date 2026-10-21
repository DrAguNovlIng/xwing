using Microsoft.EntityFrameworkCore;
using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;

public sealed class TournamentStore : ITournamentStore
{
    private readonly XwingDbContext context;

    public TournamentStore(XwingDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.context = context;
    }

    public async Task<Result<Tournament>> CreateAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Fail<Tournament>("Tournament name is required.");
        }

        var tournament = new Tournament
        {
            Name = name.Trim(),
        };

        context.Tournaments.Add(tournament);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Ok(tournament);
    }

    public async Task<IReadOnlyList<Tournament>> ListAsync(CancellationToken cancellationToken)
    {
        return await context.Tournaments
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
