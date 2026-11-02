using Microsoft.EntityFrameworkCore;
namespace AJAI_Server.Data;
public static class DemoSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration config)
    {
        if (!config.GetValue<bool>("Demo:Enabled")) return;
        var adminPassword = config["Demo:AdminPassword"];
        var cameraPassword = config["Demo:CameraPassword"];
        if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length < 12 ||
            string.IsNullOrWhiteSpace(cameraPassword) || cameraPassword.Length < 12)
            throw new InvalidOperationException("Demo passwords must each contain at least 12 characters.");
        if (!await db.Users.AnyAsync())
            db.Users.Add(new User { Email = "admin@example.test", Password = BCrypt.Net.BCrypt.HashPassword(adminPassword) });
        if (!await db.Cameras.AnyAsync())
            db.Cameras.Add(new Camera { Email = "camera1@example.test", Password = BCrypt.Net.BCrypt.HashPassword(cameraPassword) });
        await db.SaveChangesAsync();
    }
}
