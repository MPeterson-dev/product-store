using Microsoft.AspNetCore.Identity;

namespace ClothingStore.Api.Data;

public static class OwnerAccountSetup
{
    public static async Task InitializeAsync(WebApplication app)
    {
        // This bootstrap process is only for local development.
        if (!app.Environment.IsDevelopment()) return;

        var email = app.Configuration["OwnerBootstrap:Email"];
        var password = app.Configuration["OwnerBootstrap:Password"];

        // After setup, we can remove these secrets.
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
            return;

        using var scope = app.Services.CreateScope();

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        var roles = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        var user = await users.FindByEmailAsync(email);

        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = email,
                Email = email
            };

            Check(await users.CreateAsync(user, password));
        }
        else if (!await users.CheckPasswordAsync(user, password))
        {
            throw new InvalidOperationException(
                "The existing owner account password does not match the bootstrap settings."
            );
        }

        if (!await roles.RoleExistsAsync("Owner"))
            Check(await roles.CreateAsync(new IdentityRole("Owner")));

        if (!await users.IsInRoleAsync(user, "Owner"))
            Check(await users.AddToRoleAsync(user, "Owner"));
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(error => error.Description))
            );
        }
    }
}