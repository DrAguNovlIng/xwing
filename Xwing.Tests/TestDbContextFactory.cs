using Microsoft.EntityFrameworkCore;
using Xwing.Web.Data;

namespace Xwing.Tests;

internal sealed class TestDbContextFactory : IDbContextFactory<XwingDbContext>
{
    private readonly DbContextOptions<XwingDbContext> options;
    private readonly TimeProvider timeProvider;

    public TestDbContextFactory(string connectionString, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var optionsBuilder = new DbContextOptionsBuilder<XwingDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        options = optionsBuilder.Options;
        this.timeProvider = timeProvider;
    }

    public XwingDbContext CreateDbContext()
    {
        return new XwingDbContext(options, timeProvider);
    }

    public Task<XwingDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateDbContext());
    }
}
