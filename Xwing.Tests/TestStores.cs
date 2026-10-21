using Xwing.Web.Stores;

namespace Xwing.Tests;

internal sealed class TestStores : IDisposable
{
    public void Dispose()
    {
    }

    public TestStores(ITournamentStore tournaments, IUserStore users, ITournamentTemplateStore templates)
    {
        ArgumentNullException.ThrowIfNull(tournaments);
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(templates);
        Tournaments = tournaments;
        Users = users;
        Templates = templates;
    }

    public ITournamentStore Tournaments { get; }
    public IUserStore Users { get; }
    public ITournamentTemplateStore Templates { get; }
}
