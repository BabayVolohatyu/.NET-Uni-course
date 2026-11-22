using System.Net.Http.Headers;
using System.Text.Json;

namespace Platform.Aggregator.Clients;

/// <summary>
/// Client Credentials against Duende IdentityServer. The token identifies the Aggregator,
/// not an auditor: there is no human <c>sub</c>.
/// </summary>
public sealed class IdentityServerTokenClient(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<IdentityServerTokenClient> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _cached;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cached is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cached is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _cached;

            var authority = configuration["IdentityServer:Authority"] ?? "http://localhost:5100";
            var client = httpClientFactory.CreateClient("identityserver-token");
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = configuration["IdentityServer:ClientId"] ?? "aggregator_service",
                ["client_secret"] = configuration["IdentityServer:ClientSecret"] ?? "aggregator_secret",
                ["scope"] = configuration["IdentityServer:Scope"]
                    ?? "orders_api products_api orders.read orders.write products.read products.write"
            });

            using var response = await client.PostAsync($"{authority.TrimEnd('/')}/connect/token", content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("IdentityServer token request failed {Status}: {Body}", (int)response.StatusCode, body);
                response.EnsureSuccessStatusCode();
            }

            using var document = JsonDocument.Parse(body);
            _cached = document.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("IdentityServer did not return an access_token.");
            var expiresIn = document.RootElement.TryGetProperty("expires_in", out var seconds) ? seconds.GetInt32() : 300;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, expiresIn - 30));
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    public static async Task<JsonElement> CallAsync(HttpClient http, string path, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.SerializeToElement(new
        {
            status = (int)response.StatusCode,
            body
        });
    }
}
