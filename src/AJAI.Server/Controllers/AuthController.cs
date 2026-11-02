using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AJAI_Server.Data;
using AJAI_Server.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
namespace AJAI_Server.Controllers;
[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, IConfiguration config) : ControllerBase
{
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(UserLoginDto request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        var camera = user is null ? await db.Cameras.FirstOrDefaultAsync(c => c.Email == email) : null;
        var hash = user?.Password ?? camera?.Password;
        var valid = BCrypt.Net.BCrypt.Verify(request.Password, hash ?? DummyHash);
        if (hash is null || !valid) return Unauthorized(new { message = "Invalid email or password." });
        var role = user is not null ? "User" : "Camera";
        if (camera is not null)
        {
            camera.IsActive = true;
            camera.LastPing = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"], audience: config["Jwt:Audience"],
            claims: new[] { new Claim(ClaimTypes.NameIdentifier, email), new Claim(ClaimTypes.Role, role) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
        return Ok(new { role, token = new JwtSecurityTokenHandler().WriteToken(token), message = "Logged in successfully" });
    }
}
