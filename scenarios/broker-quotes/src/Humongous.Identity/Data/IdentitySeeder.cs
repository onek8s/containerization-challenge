using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Humongous.Identity.Data;

public sealed class ClientOptions
{
    public string ClientId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public List<string> RedirectUris { get; set; } = [];
    public List<string> PostLogoutRedirectUris { get; set; } = [];
}

/// <summary>Creates the schema (development), the OIDC clients from configuration, roles and development users.</summary>
public sealed class IdentitySeeder(IServiceProvider services, IConfiguration configuration, IWebHostEnvironment environment, ILogger<IdentitySeeder> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityContext>();
        if (configuration.GetValue<bool>("Database:EnsureCreated"))
            await db.Database.EnsureCreatedAsync(ct);

        await SeedClientsAsync(scope.ServiceProvider, ct);
        await SeedScopesAsync(scope.ServiceProvider, ct);

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }

        if (configuration.GetValue<bool>("Database:SeedSampleUsers") && !await db.Users.AnyAsync(ct))
            await SeedUsersAsync(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
    }

    private async Task SeedClientsAsync(IServiceProvider sp, CancellationToken ct)
    {
        var manager = sp.GetRequiredService<IOpenIddictApplicationManager>();
        var clients = configuration.GetSection("Clients").Get<List<ClientOptions>>() ?? [];
        foreach (var client in clients)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = client.ClientId,
                ClientSecret = client.ClientSecret,
                DisplayName = client.DisplayName,
                ClientType = ClientTypes.Confidential,
                ConsentType = ConsentTypes.Implicit,
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.Endpoints.Logout,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.ResponseTypes.Code,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Roles,
                    Permissions.Prefixes.Scope + CustomClaims.Scope
                },
                Requirements = { Requirements.Features.ProofKeyForCodeExchange }
            };
            foreach (var uri in client.RedirectUris) descriptor.RedirectUris.Add(new Uri(uri));
            foreach (var uri in client.PostLogoutRedirectUris) descriptor.PostLogoutRedirectUris.Add(new Uri(uri));

            var existing = await manager.FindByClientIdAsync(client.ClientId, ct);
            if (existing is null)
            {
                await manager.CreateAsync(descriptor, ct);
                logger.LogInformation("Registered client {ClientId}", client.ClientId);
            }
            else
            {
                await manager.UpdateAsync(existing, descriptor, ct);
            }
        }
    }

    private static async Task SeedScopesAsync(IServiceProvider sp, CancellationToken ct)
    {
        var manager = sp.GetRequiredService<IOpenIddictScopeManager>();
        if (await manager.FindByNameAsync(CustomClaims.Scope, ct) is null)
        {
            await manager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = CustomClaims.Scope,
                DisplayName = "Broker Quotes API",
                Resources = { CustomClaims.ApiResource }
            }, ct);
        }
    }

    private async Task SeedUsersAsync(UserManager<ApplicationUser> users)
    {
        // Development and training users. Password for all: Humongous!2024
        var samples = new (string Email, string Name, string? Agency, string[] Roles)[]
        {
            ("broker@alpine-brokers.example", "Amy Clarke", "ALP01", [Roles.Broker]),
            ("admin@alpine-brokers.example", "Ben Hollis", "ALP01", [Roles.Broker, Roles.BrokerAdmin]),
            ("broker@northbridge.example", "Priya Shah", "NWB02", [Roles.Broker]),
            ("uw1@humongousinsurance.example", "Tom Reeve", null, [Roles.Underwriter]),
            ("suw@humongousinsurance.example", "Grace Okafor", null, [Roles.Underwriter, Roles.SeniorUnderwriter]),
            ("itadmin@humongousinsurance.example", "Platform Admin", null, [Roles.Administrator]),
        };
        foreach (var (email, name, agency, roles) in samples)
        {
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = name, AgencyCode = agency };
            var result = await users.CreateAsync(user, "Humongous!2024");
            if (!result.Succeeded)
            {
                logger.LogWarning("Could not create {Email}: {Errors}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
                continue;
            }
            await users.AddToRolesAsync(user, roles);
        }
        logger.LogInformation("Seeded {Count} sample users (environment {Environment})", samples.Length, environment.EnvironmentName);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
