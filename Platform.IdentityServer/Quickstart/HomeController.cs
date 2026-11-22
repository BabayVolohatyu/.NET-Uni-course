using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Platform.IdentityServer.Quickstart;

[AllowAnonymous]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
