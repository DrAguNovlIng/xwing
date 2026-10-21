using Xwing.Web.Stores;

namespace Xwing.Tests;

internal sealed class TestStores : IDisposable
{
    public void Dispose()
    {
    }

    public TestStores(ITournamentStore tournaments, IUserStore users)
    {
        ArgumentNullException.ThrowIfNull(tournaments);
        ArgumentNullException.ThrowIfNull(users);
        Tournaments = tournaments;
        Users = users;
    }

    public ITournamentStore Tournaments { get; }
    public IUserStore Users { get; }
}
