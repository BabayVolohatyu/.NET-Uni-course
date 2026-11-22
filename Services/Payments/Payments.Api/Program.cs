using Platform.ServiceDefaults.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProcurementJwt(
    builder.Configuration,
    identityServerAudience: "payments_api",
    validateIdentityServerAudience: false);

// Lab 09 Keycloak (trajectory B). Payments audience is the resource server payments-api.
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer("keycloak", realm: "DevRealmDrivers", options =>
    {
        options.Audience = "payments-api";
        if (builder.Environment.IsDevelopment())
            options.RequireHttpsMetadata = false;
    });

builder.Services.AddPermissionAndScopePolicies(
    permissions:
    [
        "payments:read",
        "payments:process",
        "payments:refund"
    ],
    scopes: []);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Reconstruction fund disbursements",
        Version = "v1",
        Description = "Stub payments API. Only the admin role may read, process, or refund awarded-tender payments."
    });
});
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();
app.Run();

public partial class Program;
