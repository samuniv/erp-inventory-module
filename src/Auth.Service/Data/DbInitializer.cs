using Auth.Service.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Service.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AuthDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Apply any pending migrations
        if (context.Database.GetPendingMigrations().Any())
        {
            await context.Database.MigrateAsync();
        }

        // Seed roles
        await SeedRolesAsync(roleManager);

        // Seed users
        await SeedUsersAsync(userManager);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        var roles = new[] { "Admin", "Manager", "Clerk" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager)
    {
        // Seed Admin user
        var adminEmail = "admin@erp.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "System",
                LastName = "Administrator",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // Seed Manager user
        var managerEmail = "manager@erp.com";
        if (await userManager.FindByEmailAsync(managerEmail) == null)
        {
            var managerUser = new ApplicationUser
            {
                UserName = managerEmail,
                Email = managerEmail,
                FirstName = "John",
                LastName = "Manager",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(managerUser, "Manager123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(managerUser, "Manager");
            }
        }

        // Seed Clerk user
        var clerkEmail = "clerk@erp.com";
        if (await userManager.FindByEmailAsync(clerkEmail) == null)
        {
            var clerkUser = new ApplicationUser
            {
                UserName = clerkEmail,
                Email = clerkEmail,
                FirstName = "Jane",
                LastName = "Clerk",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(clerkUser, "Clerk123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(clerkUser, "Clerk");
            }
        }
    }
}
