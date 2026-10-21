using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Tests;

[Collection("SQL Server Integration Tests")]
public sealed class TournamentStoreTests
{
    private readonly string connectionString;

    public TournamentStoreTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task CreateAsyncShouldCreateTournament()
    {
        DateTimeOffset expectedUtc = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(expectedUtc);

        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            timeProvider,
            TestContext.Current.CancellationToken);

        using (stores)
        {
            Result<Tournament> result = await stores.Tournaments.CreateAsync(
                "Regional Championship",
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value!.TournamentId.ShouldBeGreaterThan(0);
            result.Value.Name.ShouldBe("Regional Championship");
            result.Value.TimeCreatedUtc.ShouldBe(expectedUtc);
            result.Value.TimeUpdatedUtc.ShouldBe(expectedUtc);
        }
    }

    [Fact]
    public async Task CreateAsyncShouldRejectBlankName()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);

        using (stores)
        {
            Result<Tournament> result = await stores.Tournaments.CreateAsync(
                "   ",
                TestContext.Current.CancellationToken);

            result.IsSuccess.ShouldBeFalse();
            result.Error.ShouldBe("Tournament name is required.");
        }
    }

    [Fact]
    public async Task ListAsyncShouldReturnTournaments()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);

        using (stores)
        {
            Result<Tournament> alpha = await stores.Tournaments.CreateAsync(
                "Alpha",
                TestContext.Current.CancellationToken);
            Result<Tournament> bravo = await stores.Tournaments.CreateAsync(
                "Bravo",
                TestContext.Current.CancellationToken);

            alpha.IsSuccess.ShouldBeTrue();
            bravo.IsSuccess.ShouldBeTrue();

            IReadOnlyList<Tournament> all = await stores.Tournaments.ListAsync(
                TestContext.Current.CancellationToken);

            all.Count.ShouldBe(2);
            all[0].Name.ShouldBe("Alpha");
            all[1].Name.ShouldBe("Bravo");
        }
    }
}
