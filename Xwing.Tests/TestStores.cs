using Xwing.Web.Data;
using Xwing.Web.Stores;

namespace Xwing.Tests;

internal sealed class TestStores : IDisposable
{
    private readonly XwingDbContext context;

    public TestStores(XwingDbContext context, ITournamentStore tournaments)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tournaments);
        this.context = context;
        Tournaments = tournaments;
    }

    public ITournamentStore Tournaments { get; }

    public void Dispose()
    {
        context.Dispose();
    }
}
