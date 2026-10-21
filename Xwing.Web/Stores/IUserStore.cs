using Xwing.Web.Models;

namespace Xwing.Web.Stores;

public interface IUserStore
{
    Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken);
    Task<Result<UserAccount>> VerifyLoginAsync(string userName, string password, CancellationToken cancellationToken);
    Task<Result<UserAccount>> CreateAsync(
        string userName,
        string password,
        string role,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<UserAccount>> ListAsync(CancellationToken cancellationToken);
    Task<Result<UserAccount>> UpdateAsync(
        int userId,
        string userName,
        string? newPassword,
        string role,
        byte[] rowVersion,
        int? forbidRoleChangeForUserId,
        CancellationToken cancellationToken);
    Task<Result> DeleteAsync(int userId, CancellationToken cancellationToken);
}
