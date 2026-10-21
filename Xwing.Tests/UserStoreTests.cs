using Xwing.Web.Data;
using Xwing.Web.Models;
using Xwing.Web.Stores;

namespace Xwing.Tests;

[Collection("SQL Server Integration Tests")]
public sealed class UserStoreTests
{
    private readonly string connectionString;

    public UserStoreTests(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task CreateAsyncShouldHashPasswordAndVerifyLogin()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> created = await stores.Users.CreateAsync(
                "admin",
                "P@ssw0rd!Strong",
                UserRoles.Admin,
                TestContext.Current.CancellationToken);
            created.IsSuccess.ShouldBeTrue();
            created.Value!.UserName.ShouldBe("admin");
            created.Value.Role.ShouldBe(UserRoles.Admin);
            Result<UserAccount> login = await stores.Users.VerifyLoginAsync(
                "admin",
                "P@ssw0rd!Strong",
                TestContext.Current.CancellationToken);
            login.IsSuccess.ShouldBeTrue();
            login.Value!.Role.ShouldBe(UserRoles.Admin);
        }
    }

    [Fact]
    public async Task CreateAsyncShouldDefaultMemberRoleWhenSpecified()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> created = await stores.Users.CreateAsync(
                "member",
                "secret",
                UserRoles.Member,
                TestContext.Current.CancellationToken);
            created.IsSuccess.ShouldBeTrue();
            created.Value!.Role.ShouldBe(UserRoles.Member);
        }
    }

    [Fact]
    public async Task VerifyLoginAsyncShouldRejectWrongPassword()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            await stores.Users.CreateAsync("admin", "correct", UserRoles.Admin, TestContext.Current.CancellationToken);
            Result<UserAccount> login = await stores.Users.VerifyLoginAsync(
                "admin",
                "wrong",
                TestContext.Current.CancellationToken);
            login.IsSuccess.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task CreateAsyncShouldRejectDuplicateUsername()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            await stores.Users.CreateAsync("admin", "one", UserRoles.Admin, TestContext.Current.CancellationToken);
            Result<UserAccount> duplicate = await stores.Users.CreateAsync(
                "admin",
                "two",
                UserRoles.Admin,
                TestContext.Current.CancellationToken);
            duplicate.IsSuccess.ShouldBeFalse();
            duplicate.Error.ShouldBe("Username already exists.");
        }
    }

    [Fact]
    public async Task UpdateAsyncShouldReplacePasswordWhenProvided()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> created = await stores.Users.CreateAsync(
                "admin",
                "old-password",
                UserRoles.Admin,
                TestContext.Current.CancellationToken);
            created.IsSuccess.ShouldBeTrue();
            Result<UserAccount> updated = await stores.Users.UpdateAsync(
                created.Value!.UserId,
                "admin",
                "new-password",
                UserRoles.Admin,
                created.Value.RowVersion,
                forbidRoleChangeForUserId: null,
                TestContext.Current.CancellationToken);
            updated.IsSuccess.ShouldBeTrue();
            Result<UserAccount> oldLogin = await stores.Users.VerifyLoginAsync(
                "admin",
                "old-password",
                TestContext.Current.CancellationToken);
            oldLogin.IsSuccess.ShouldBeFalse();
            Result<UserAccount> newLogin = await stores.Users.VerifyLoginAsync(
                "admin",
                "new-password",
                TestContext.Current.CancellationToken);
            newLogin.IsSuccess.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task UpdateAsyncShouldKeepPasswordWhenBlank()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> created = await stores.Users.CreateAsync(
                "admin",
                "keep-me",
                UserRoles.Admin,
                TestContext.Current.CancellationToken);
            created.IsSuccess.ShouldBeTrue();
            Result<UserAccount> updated = await stores.Users.UpdateAsync(
                created.Value!.UserId,
                "renamed",
                "   ",
                UserRoles.Admin,
                created.Value.RowVersion,
                forbidRoleChangeForUserId: null,
                TestContext.Current.CancellationToken);
            updated.IsSuccess.ShouldBeTrue();
            updated.Value!.UserName.ShouldBe("renamed");
            Result<UserAccount> login = await stores.Users.VerifyLoginAsync(
                "renamed",
                "keep-me",
                TestContext.Current.CancellationToken);
            login.IsSuccess.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task UpdateAsyncShouldRejectOwnRoleChange()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> created = await stores.Users.CreateAsync(
                "admin",
                "secret",
                UserRoles.Admin,
                TestContext.Current.CancellationToken);
            created.IsSuccess.ShouldBeTrue();
            int userId = created.Value!.UserId;
            Result<UserAccount> updated = await stores.Users.UpdateAsync(
                userId,
                "admin",
                null,
                UserRoles.Member,
                created.Value.RowVersion,
                forbidRoleChangeForUserId: userId,
                TestContext.Current.CancellationToken);
            updated.IsSuccess.ShouldBeFalse();
            updated.Error.ShouldBe("You cannot change your own role.");
        }
    }

    [Fact]
    public async Task DeleteAsyncShouldRejectLastAdmin()
    {
        TestStores stores = await TestServiceFactory.CreateStoresWithResetAsync(
            connectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (stores)
        {
            Result<UserAccount> created = await stores.Users.CreateAsync(
                "only-admin",
                "secret",
                UserRoles.Admin,
                TestContext.Current.CancellationToken);
            created.IsSuccess.ShouldBeTrue();
            Result deleted = await stores.Users.DeleteAsync(
                created.Value!.UserId,
                TestContext.Current.CancellationToken);
            deleted.IsSuccess.ShouldBeFalse();
            deleted.Error.ShouldBe("Cannot delete the last admin user.");
        }
    }
}
