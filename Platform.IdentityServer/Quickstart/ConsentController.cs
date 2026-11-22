using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Platform.IdentityServer.Quickstart;

/// <summary>
/// Consent page from the Duende Quickstart UI. Public clients (Swagger) and Postman
/// stop here when the client requires consent before the code is issued.
/// </summary>
[Authorize]
[SecurityHeaders]
public sealed class ConsentController(IIdentityServerInteractionService interaction) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? returnUrl)
    {
        var request = await interaction.GetAuthorizationContextAsync(returnUrl, CancellationToken.None);
        if (request is null)
            return Redirect("~/");

        return View(BuildViewModel(returnUrl, request));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ConsentInputModel model)
    {
        var request = await interaction.GetAuthorizationContextAsync(model.ReturnUrl, CancellationToken.None);
        if (request is null)
            return Redirect("~/");

        if (model.Button == "no")
        {
            await interaction.DenyAuthorizationAsync(request, InteractionError.AccessDenied, CancellationToken.None);
            return Redirect(model.ReturnUrl!);
        }

        if (model.ScopesConsented is null || model.ScopesConsented.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "At least one scope must be selected.");
            return View(BuildViewModel(model.ReturnUrl, request));
        }

        await interaction.GrantConsentAsync(request, new ConsentResponse
        {
            RememberConsent = model.RememberConsent,
            ScopesValuesConsented = model.ScopesConsented
        }, CancellationToken.None);

        return Redirect(model.ReturnUrl!);
    }

    private static ConsentViewModel BuildViewModel(string? returnUrl, AuthorizationRequest request)
        => new()
        {
            ReturnUrl = returnUrl,
            ClientName = request.Client.ClientName ?? request.Client.ClientId,
            Scopes = request.ValidatedResources.Resources.ApiScopes
                .Select(s => s.Name)
                .Concat(request.ValidatedResources.Resources.IdentityResources.Select(s => s.Name))
                .Distinct()
                .ToArray()
        };
}

public sealed class ConsentViewModel
{
    public string? ReturnUrl { get; set; }
    public string? ClientName { get; set; }
    public bool RememberConsent { get; set; } = true;
    public IReadOnlyList<string> Scopes { get; set; } = [];
}

public sealed class ConsentInputModel
{
    public string? Button { get; set; }
    public string? ReturnUrl { get; set; }
    public bool RememberConsent { get; set; } = true;
    public List<string> ScopesConsented { get; set; } = [];
}
