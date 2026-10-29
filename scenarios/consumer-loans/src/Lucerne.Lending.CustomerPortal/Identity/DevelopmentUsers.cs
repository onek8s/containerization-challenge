using Lucerne.Lending.Domain;
using Microsoft.AspNetCore.Identity;

namespace Lucerne.Lending.CustomerPortal.Identity;

/// <summary>Creates the online service users listed under Seed:Users (training and test environments).</summary>
public static class DevelopmentUsers
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration section, ILogger logger)
    {
        var users = services.GetRequiredService<UserManager<PortalUser>>();
        foreach (var entry in section.GetChildren())
        {
            var email = entry["Email"];
            var pic = entry["PersonalIdentityCode"];
            if (string.IsNullOrEmpty(email) || !PersonalIdentityCode.IsValid(pic) || await users.FindByEmailAsync(email) != null) continue;
            var user = new PortalUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                PhoneNumber = entry["Phone"],
                PersonalIdentityCode = pic!.ToUpperInvariant(),
                FirstName = entry["FirstName"] ?? "",
                LastName = entry["LastName"] ?? ""
            };
            var result = await users.CreateAsync(user, entry["Password"] ?? throw new InvalidOperationException($"Seed user {email} has no password."));
            if (result.Succeeded) logger.LogInformation("Seed user {Email} created", email);
            else logger.LogWarning("Seed user {Email} not created: {Errors}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
