using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;

public interface ITournamentStore
{
    Task<Result<Tournament>> CreateAsync(string name, CancellationToken cancellationToken);

    Task<IReadOnlyList<Tournament>> ListAsync(CancellationToken cancellationToken);
}
