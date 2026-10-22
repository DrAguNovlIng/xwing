using Xwing.Web.Stores;

namespace Xwing.Tests;
internal sealed class TestStores : IDisposable
{
    public void Dispose()
    {
    }

    public TestStores(
        ITournamentStore tournaments,
        IUserStore users,
        ITournamentTemplateStore templates,
        ITournamentBracketStore brackets)
    {
        ArgumentNullException.ThrowIfNull(tournaments);
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(brackets);
        Tournaments = tournaments;
        Users = users;
        Templates = templates;
        Brackets = brackets;
    }

    public ITournamentStore Tournaments { get; }
    public IUserStore Users { get; }
    public ITournamentTemplateStore Templates { get; }
    public ITournamentBracketStore Brackets { get; }
}
