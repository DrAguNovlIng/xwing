namespace Xwing.Web.Data;

public sealed class User : BaseModel
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = UserRoles.Member;
}
