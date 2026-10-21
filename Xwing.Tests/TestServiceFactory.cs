using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xwing.Web.Data;
using Xwing.Web.Stores;

namespace Xwing.Tests;

internal static class TestServiceFactory
{
    public static TestStores CreateStores(string connectionString, TimeProvider? timeProvider = null)
    {
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        var contextFactory = new TestDbContextFactory(connectionString, clock);
        var concurrencyHelpers = new StoreConcurrencyHelpers(NullLogger<StoreConcurrencyHelpers>.Instance);
        var passwordHasher = new PasswordHasher<User>();
        return new TestStores(
            new TournamentStore(contextFactory, concurrencyHelpers, clock),
            new UserStore(contextFactory, concurrencyHelpers, passwordHasher));
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
        var factory = new TestDbContextFactory(connectionString, TimeProvider.System);
        await using XwingDbContext db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.TournamentParticipants.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Tournaments.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Users.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
