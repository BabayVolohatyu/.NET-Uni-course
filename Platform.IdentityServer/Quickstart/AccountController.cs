using System.ComponentModel.DataAnnotations;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Platform.IdentityServer.Quickstart;

/// <summary>
/// Login and logout pages adapted from DuendeSoftware/IdentityServer.Quickstart.UI.
/// Authorization Code Flow lands here before the code is returned to Postman or Swagger.
/// </summary>
[AllowAnonymous]
[SecurityHeaders]
public sealed class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IIdentityServerInteractionService interaction) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInputModel model, string? returnUrl)
    {
        returnUrl ??= model.ReturnUrl;
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(new LoginViewModel { Username = model.Username, ReturnUrl = returnUrl });

        var user = await users.FindByNameAsync(model.Username);
        if (user is null || !user.EmailConfirmed)
        {
            ModelState.AddModelError(string.Empty, "Unknown auditor or email is not confirmed.");
            return View(new LoginViewModel { Username = model.Username, ReturnUrl = returnUrl });
        }

        var result = await signIn.PasswordSignInAsync(user, model.Password, model.RememberLogin, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(new LoginViewModel { Username = model.Username, ReturnUrl = returnUrl });
        }

        if (await interaction.GetAuthorizationContextAsync(returnUrl, CancellationToken.None) is not null || Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl!);

        return Redirect("~/");
    }

    [HttpGet]
    public async Task<IActionResult> Logout(string? logoutId)
    {
        await signIn.SignOutAsync();
        var context = await interaction.GetLogoutContextAsync(logoutId, CancellationToken.None);
        return View("LoggedOut", new LoggedOutViewModel
        {
            PostLogoutRedirectUri = context?.PostLogoutRedirectUri,
            ClientName = context?.ClientName,
            SignOutIframeUrl = context?.SignOutIFrameUrl
        });
    }
}

public sealed class LoginViewModel
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool RememberLogin { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class LoginInputModel
{
    [Required]
    public string Username { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";

    public bool RememberLogin { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class LoggedOutViewModel
{
    public string? PostLogoutRedirectUri { get; set; }
    public string? ClientName { get; set; }
    public string? SignOutIframeUrl { get; set; }
}
