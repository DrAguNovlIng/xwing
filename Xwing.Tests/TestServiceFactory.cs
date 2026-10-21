using Microsoft.EntityFrameworkCore;
using Xwing.Web.Data;
using Xwing.Web.Stores;

namespace Xwing.Tests;

internal static class TestServiceFactory
{
    public static TestStores CreateStores(string connectionString, TimeProvider? timeProvider = null)
    {
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        XwingDbContext context = CreateContext(connectionString, clock);
        return new TestStores(context, new TournamentStore(context));
    }

    public static async Task<TestStores> CreateStoresWithResetAsync(
        string connectionString,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        await ResetMutableDataAsync(connectionString, cancellationToken).ConfigureAwait(false);
        return CreateStores(connectionString, timeProvider);
    }

    private static async Task ResetMutableDataAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using XwingDbContext db = CreateContext(connectionString, TimeProvider.System);
        await db.Tournaments.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static XwingDbContext CreateContext(string connectionString, TimeProvider timeProvider)
    {
        var options = new DbContextOptionsBuilder<XwingDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new XwingDbContext(options, timeProvider);
    }
}
