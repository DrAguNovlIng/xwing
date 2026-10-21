using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xwing.Web.Data;

namespace Xwing.Tests;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer container;

    public string ConnectionString { get; private set; } = string.Empty;

    public SqlServerFixture()
    {
        container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
            .WithPassword("P@ssw0rd!Strong")
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync().ConfigureAwait(false);
        ConnectionString = container.GetConnectionString();

        var options = new DbContextOptionsBuilder<XwingDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        await using XwingDbContext db = new(options, TimeProvider.System);
        await db.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await container.DisposeAsync().ConfigureAwait(false);
    }
}
