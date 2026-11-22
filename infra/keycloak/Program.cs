using Procurement.Keycloak;
using Pulumi;
using Pulumi.Keycloak;
using Pulumi.Keycloak.OpenId;
using KeycloakProvider = Pulumi.Keycloak.Provider;

return await Deployment.RunAsync(() =>
{
    var config = new Pulumi.Config();
    var url = config.Get("url") ?? "http://localhost:8080";
    var admin = config.Get("admin") ?? "admin";
    var password = config.Get("password") ?? "admin123";

    var provider = new KeycloakProvider("keycloak", new ProviderArgs
    {
        Url = url,
        Realm = "master",
        ClientId = "admin-cli",
        Username = admin,
        Password = password,
        TlsInsecureSkipVerify = true,
    });
    var options = new CustomResourceOptions { Provider = provider };

    var realm = new Realm("dev-realm-drivers", new RealmArgs
    {
        RealmName = "DevRealmDrivers",
        Enabled = true,
        DisplayName = "Аудит закупівель відбудови",
        DisplayNameHtml = "<b>Аудит закупівель відбудови</b>",
        RegistrationAllowed = true,
        ResetPasswordAllowed = true,
        RememberMe = true,
        VerifyEmail = true,
        LoginWithEmailAllowed = true,
        DuplicateEmailsAllowed = false,
        SslRequired = "none",
        LoginTheme = "keycloak",
        AccountTheme = "keycloak",
        AdminTheme = "keycloak",
        EmailTheme = "keycloak",
        PasswordPolicy = "length(8) and digits(1) and upperCase(1) and lowerCase(1) and specialChars(1)",
    }, options);

    var realmOptions = new CustomResourceOptions { Provider = provider, Parent = realm };

    var adminRole = RealmRole("admin", "Full audit access, including reconstruction-fund disbursement.");
    var managerRole = RealmRole("manager", "Reviews tenders and work packages. Cannot delete tenders or move funds.");
    var userRole = RealmRole("user", "Opens tenders and reads the contractor registry.");
    var guestRole = RealmRole("guest", "Public catalogue only. No tenders and no payments.");

    // Groups the clerk baseline so another role can inherit it without copying scopes.
    _ = new Role("role-procurement-auditor", new RoleArgs
    {
        RealmId = realm.Id,
        Name = "procurement-auditor",
        Description = "Composite: a reconstruction auditor inherits the clerk (user) baseline.",
        CompositeRoles = new[] { userRole.Id },
    }, realmOptions);

    var roles = new Dictionary<string, Role>
    {
        ["admin"] = adminRole,
        ["manager"] = managerRole,
        ["user"] = userRole,
        ["guest"] = guestRole,
    };

    var orders = ProcurementResourceServer.Create(
        "orders-api", "orders-api", realm.Id, "orders-api-dev-secret",
        new Dictionary<string, string[]>
        {
            ["admin"] = ["orders:read", "orders:create", "orders:update", "orders:delete", "orders:approve"],
            ["manager"] = ["orders:read", "orders:update", "orders:approve"],
            ["user"] = ["orders:read", "orders:create"],
            ["guest"] = [],
        },
        roles, realmOptions);
    var products = ProcurementResourceServer.Create(
        "products-api", "products-api", realm.Id, "products-api-dev-secret",
        new Dictionary<string, string[]>
        {
            ["admin"] = ["products:read", "products:create", "products:update", "products:delete", "products:publish"],
            ["manager"] = ["products:read", "products:update", "products:publish"],
            ["user"] = ["products:read"],
            ["guest"] = ["products:read"],
        },
        roles, realmOptions);
    var payments = ProcurementResourceServer.Create(
        "payments-api", "payments-api", realm.Id, "payments-api-dev-secret",
        new Dictionary<string, string[]>
        {
            ["admin"] = ["payments:read", "payments:process", "payments:refund"],
            ["manager"] = [],
            ["user"] = [],
            ["guest"] = [],
        },
        roles, realmOptions);

    // Client-level role on the payments resource server. Only the admin composite includes it,
    // so a service account cannot disburse funds by holding the manager or user role.
    var fundDisbursement = new Role("payments-fund-disbursement", new RoleArgs
    {
        RealmId = realm.Id,
        ClientId = payments.Id,
        Name = "fund-disbursement",
        Description = "Service-account role for paying an awarded reconstruction tender.",
    }, realmOptions);

    _ = new Role("role-fund-controller", new RoleArgs
    {
        RealmId = realm.Id,
        Name = "fund-controller",
        Description = "Composite of realm admin and the payments client role fund-disbursement.",
        CompositeRoles = new[] { adminRole.Id, fundDisbursement.Id },
    }, realmOptions);

    var tenderReader = new Role("orders-tender-reader", new RoleArgs
    {
        RealmId = realm.Id,
        ClientId = orders.Id,
        Name = "tender-reader",
        Description = "Service account may read tenders. It is not a human auditor.",
    }, realmOptions);

    string[] audiences = ["orders-api", "products-api", "payments-api"];
    string[] defaultScopes = ["openid", "profile", "email", "roles", "web-origins"];

    var postman = OpenIdClientFactory.AuthorizationCode(
            "orders-postman", "orders.postman", realm.Id, publicClient: false, "postman-dev-secret", realmOptions,
            "https://oauth.pstmn.io/v1/callback")
        .WithAudienceMapper(realm.Id, realmOptions, audiences)
        .WithDefaultScopes(realm.Id, realmOptions, defaultScopes)
        .WithPermissionsProtocolMapper(realm.Id, realmOptions);

    var swagger = OpenIdClientFactory.AuthorizationCode(
            "orders-swagger", "orders.swagger", realm.Id, publicClient: true, secret: null, realmOptions,
            "http://localhost:5000/swagger/oauth2-redirect.html",
            "https://localhost:5001/swagger/oauth2-redirect.html")
        .WithAudienceMapper(realm.Id, realmOptions, audiences)
        .WithDefaultScopes(realm.Id, realmOptions, defaultScopes)
        .WithPermissionsProtocolMapper(realm.Id, realmOptions);

    var bff = OpenIdClientFactory.ClientCredentials(
            "orders-bff", "orders.bff", realm.Id, "bff-dev-secret", realmOptions)
        .WithAudienceMapper(realm.Id, realmOptions, audiences)
        .WithDefaultScopes(realm.Id, realmOptions, defaultScopes)
        .WithPermissionsProtocolMapper(realm.Id, realmOptions);

    _ = new ClientServiceAccountRole("bff-tender-reader", new()
    {
        RealmId = realm.Id,
        ServiceAccountUserId = bff.ServiceAccountUserId,
        ClientId = orders.Id,
        Role = tenderReader.Name,
    }, realmOptions);

    _ = new ClientServiceAccountRealmRole("bff-realm-user", new()
    {
        RealmId = realm.Id,
        ServiceAccountUserId = bff.ServiceAccountUserId,
        Role = userRole.Name,
    }, realmOptions);

    CreateUser("alice", "Alice", "Admin123!", adminRole);
    CreateUser("bob", "Bob", "Manager123!", managerRole);
    CreateUser("john.manager", "John", "Manager123!", managerRole);
    CreateUser("carol.user", "Carol", "User12345!", userRole);
    CreateUser("dave.guest", "Dave", "Guest1234!", guestRole);

    return new Dictionary<string, object?>
    {
        ["RealmUrl"] = Output.Format($"{url}/realms/{realm.RealmName}"),
        ["AdminConsoleUrl"] = Output.Format($"{url}/admin/{realm.RealmName}/console"),
        ["ClientIds"] = Output.Tuple(
            postman.ClientId, swagger.ClientId, bff.ClientId,
            orders.ClientId, products.ClientId, payments.ClientId).Apply(ids => new Dictionary<string, string>
        {
            ["orders.postman"] = ids.Item1,
            ["orders.swagger"] = ids.Item2,
            ["orders.bff"] = ids.Item3,
            ["orders-api"] = ids.Item4,
            ["products-api"] = ids.Item5,
            ["payments-api"] = ids.Item6,
        }),
        ["ClientSecrets"] = Output.Tuple(postman.ClientSecret, bff.ClientSecret, orders.ClientSecret, products.ClientSecret, payments.ClientSecret)
            .Apply(secrets => new Dictionary<string, string>
            {
                ["orders.postman"] = secrets.Item1,
                ["orders.bff"] = secrets.Item2,
                ["orders-api"] = secrets.Item3,
                ["products-api"] = secrets.Item4,
                ["payments-api"] = secrets.Item5,
            }),
    };

    Role RealmRole(string name, string description) => new($"role-{name}", new RoleArgs
    {
        RealmId = realm.Id,
        Name = name,
        Description = description,
    }, realmOptions);

    void CreateUser(string username, string firstName, string temporaryPassword, Role role)
    {
        var user = new User($"user-{username}", new UserArgs
        {
            RealmId = realm.Id,
            Username = username,
            Email = $"{username}@reconstruction-audit.local",
            EmailVerified = true,
            Enabled = true,
            FirstName = firstName,
            LastName = "Audit",
            InitialPassword = new Pulumi.Keycloak.Inputs.UserInitialPasswordArgs
            {
                Value = temporaryPassword,
                Temporary = true,
            },
        }, realmOptions);

        _ = new UserRoles($"roles-{username}", new UserRolesArgs
        {
            RealmId = realm.Id,
            UserId = user.Id,
            RoleIds = new[] { role.Id },
        }, realmOptions);
    }
});
