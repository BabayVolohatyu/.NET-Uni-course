using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Clients;

namespace Platform.Aggregator.Controllers;

/// <summary>
/// Service-to-service call. The Aggregator asks IdentityServer for a client-credentials token
/// and calls Tenders (orders_api) and Catalog (products_api). The token has no auditor subject.
/// </summary>
[ApiController]
[Route("api/aggregator/service-identity")]
[AllowAnonymous]
public sealed class ServiceIdentityController(
    IdentityServerTokenClient tokens,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var accessToken = await tokens.GetAccessTokenAsync(ct);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var subject = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var clientId = jwt.Claims.FirstOrDefault(c => c.Type == "client_id")?.Value;

        var tenders = httpClientFactory.CreateClient("tenders-http");
        var catalog = httpClientFactory.CreateClient("catalog-http");

        var orders = await IdentityServerTokenClient.CallAsync(tenders, "/api/tenders/scopes/read", accessToken, ct);
        var products = await IdentityServerTokenClient.CallAsync(catalog, "/api/products/scopes/read", accessToken, ct);

        return Ok(new
        {
            clientId,
            // Client credentials identify the service. A human auditor id is not present.
            hasUserSubject = subject is not null && !string.Equals(subject, clientId, StringComparison.Ordinal) && !subject.EndsWith("@clients", StringComparison.Ordinal),
            subject,
            scopes = jwt.Claims.Where(c => c.Type == "scope").Select(c => c.Value).ToArray(),
            orders,
            products
        });
    }
}
