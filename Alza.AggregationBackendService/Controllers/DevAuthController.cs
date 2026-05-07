using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Alza.AggregationBackendService.Controllers;

[ApiController]
[Route("dev")]
public class DevAuthController : ControllerBase
{
    public const string KEY = "demo-development-secret-auth-key-123456789";

    [HttpGet("token")]
    public string GetToken()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "dev-user"),
            new Claim("role", "admin")
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(KEY));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "demo",
            audience: "demo",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}