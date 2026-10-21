using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xwing.Web.Auth;
using Xwing.Web.Data;
using Xwing.Web.Models;
using Xwing.Web.Stores;

namespace Xwing.Web.Controllers;

[Route("account")]
public sealed class AccountController : Controller
{
    private readonly IUserStore userStore;

    public AccountController(IUserStore userStore)
    {
        ArgumentNullException.ThrowIfNull(userStore);
        this.userStore = userStore;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        [FromForm] string userName,
        [FromForm] string password,
        [FromForm] string? returnUrl,
        CancellationToken cancellationToken)
    {
        Result<UserAccount> result = await userStore.VerifyLoginAsync(userName, password, cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return RedirectToLoginWithError(returnUrl);
        }
        await SignInAsync(result.Value!).ConfigureAwait(false);
        return LocalRedirect(GetSafeReturnUrl(returnUrl));
    }

    [HttpPost("logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        return Redirect("/login");
    }

    [HttpPost("first-admin")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FirstAdmin(
        [FromForm] string userName,
        [FromForm] string password,
        CancellationToken cancellationToken)
    {
        if (await userStore.AnyAdminExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return Redirect("/login");
        }
        Result<UserAccount> result = await userStore.CreateAsync(
            userName,
            password,
            UserRoles.Admin,
            cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return Redirect("/users?error=" + Uri.EscapeDataString(result.Error ?? "Could not create admin."));
        }
        await SignInAsync(result.Value!).ConfigureAwait(false);
        return Redirect("/users");
    }

    private async Task SignInAsync(UserAccount account)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.UserId.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, account.UserName),
            new(ClaimTypes.Role, account.Role),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(AuthCookieSettings.PersistentCookieDays),
            AllowRefresh = true,
        };
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties).ConfigureAwait(false);
    }

    private RedirectResult RedirectToLoginWithError(string? returnUrl)
    {
        string safeReturnUrl = GetSafeReturnUrl(returnUrl);
        string path = $"/login?error={Uri.EscapeDataString("Invalid username or password.")}&returnUrl={Uri.EscapeDataString(safeReturnUrl)}";
        return Redirect(path);
    }

    private static string GetSafeReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative) && returnUrl.StartsWith('/'))
        {
            return returnUrl;
        }
        return "/tournaments";
    }
}
