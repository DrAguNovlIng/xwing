namespace Xwing.Web.Data;
public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Member = "Member";
    public static IReadOnlyList<string> All { get; } = [Admin, Member];
    public static bool IsValid(string role) => All.Contains(role, StringComparer.Ordinal);
}
