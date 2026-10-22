using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xwing.Web.Data;
using Xwing.Web.Models;

namespace Xwing.Web.Stores;

public sealed class UserStore : IUserStore
{
    private readonly IDbContextFactory<XwingDbContext> contextFactory;
    private readonly StoreConcurrencyHelpers concurrencyHelpers;
    private readonly PasswordHasher<User> passwordHasher;

    public UserStore(
        IDbContextFactory<XwingDbContext> contextFactory,
        StoreConcurrencyHelpers concurrencyHelpers,
        PasswordHasher<User> passwordHasher)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(concurrencyHelpers);
        ArgumentNullException.ThrowIfNull(passwordHasher);
        this.contextFactory = contextFactory;
        this.concurrencyHelpers = concurrencyHelpers;
        this.passwordHasher = passwordHasher;
    }

    public async Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Role == UserRoles.Admin, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<UserAccount>> VerifyLoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            return Result.Fail<UserAccount>("Invalid username or password.");
        }
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        User? user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserName == userName.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return Result.Fail<UserAccount>("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            return Result.Fail<UserAccount>("Invalid username or password.");
        }

        PasswordVerificationResult verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification is PasswordVerificationResult.Failed)
        {
            return Result.Fail<UserAccount>("Invalid username or password.");
        }
        return Result.Ok(ToAccount(user));
    }

    public async Task<Result<UserAccount>> CreateAsync(
        string userName,
        string password,
        string role,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Result.Fail<UserAccount>("Username is required.");
        }
        if (string.IsNullOrEmpty(password))
        {
            return Result.Fail<UserAccount>("Password is required.");
        }
        if (!UserRoles.IsValid(role))
        {
            return Result.Fail<UserAccount>("Role is invalid.");
        }
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        string trimmedUserName = userName.Trim();
        bool exists = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.UserName == trimmedUserName, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return Result.Fail<UserAccount>("Username already exists.");
        }
        var user = new User
        {
            UserName = trimmedUserName,
            Role = role,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        context.Users.Add(user);
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<UserAccount>(saveResult.Error!);
        }
        return Result.Ok(ToAccount(user));
    }

    public async Task<IReadOnlyList<UserAccount>> ListAsync(CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new UserAccount
            {
                UserId = u.UserId,
                UserName = u.UserName,
                Role = u.Role,
                IsActive = u.IsActive,
                RowVersion = u.RowVersion,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<UserAccount>> UpdateAsync(
        int userId,
        string userName,
        string? newPassword,
        string role,
        byte[] rowVersion,
        int? forbidRoleChangeForUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Result.Fail<UserAccount>("Username is required.");
        }
        if (!UserRoles.IsValid(role))
        {
            return Result.Fail<UserAccount>("Role is invalid.");
        }
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        User? user = await context.Users
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return Result.Fail<UserAccount>("User not found.");
        }
        if (forbidRoleChangeForUserId == userId && !string.Equals(role, user.Role, StringComparison.Ordinal))
        {
            return Result.Fail<UserAccount>("You cannot change your own role.");
        }
        string trimmedUserName = userName.Trim();
        bool duplicate = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.UserId != userId && u.UserName == trimmedUserName, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
        {
            return Result.Fail<UserAccount>("Username already exists.");
        }
        user.UserName = trimmedUserName;
        user.Role = role;
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        }
        context.Entry(user).Property(u => u.RowVersion).OriginalValue = rowVersion;
        Result saveResult = await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result.Fail<UserAccount>(saveResult.Error!);
        }
        return Result.Ok(ToAccount(user));
    }

    public async Task<Result> DeactivateAsync(int userId, CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        User? user = await context.Users
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        if (!user.IsActive)
        {
            return Result.Fail("User is already inactive.");
        }

        int activeAdminCount = await context.Users
            .CountAsync(u => u.Role == UserRoles.Admin && u.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (activeAdminCount <= 1 && user.Role == UserRoles.Admin)
        {
            return Result.Fail("Cannot deactivate the last active admin user.");
        }

        user.IsActive = false;
        return await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> ActivateAsync(int userId, CancellationToken cancellationToken)
    {
        await using XwingDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        User? user = await context.Users
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return Result.Fail("User not found.");
        }

        if (user.IsActive)
        {
            return Result.Fail("User is already active.");
        }

        user.IsActive = true;
        return await concurrencyHelpers.SaveChangesAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private static UserAccount ToAccount(User user) =>
        new()
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Role = user.Role,
            IsActive = user.IsActive,
            RowVersion = user.RowVersion,
        };
}
