using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TaskTrack.Service.Dtos;

namespace TaskTrack.API;

public class TokenIssuer(IConfiguration configuration)
{
    public const string Issuer = "TaskTrack.API";
    public const string Audience = "TaskTrack.Web";
    public static string RoleName(short role) => role == 1 ? "Admin" : "Staff";
    public static SymmetricSecurityKey Key(IConfiguration configuration)
    {
        var secret = configuration["JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32) throw new InvalidOperationException("Configure JWT_SECRET with at least 32 bytes outside source control.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public object Issue(AccountResponse account)
    {
        var expires = DateTime.UtcNow.AddHours(24);
        var claims = new[] { new Claim("AccountID", account.AccountId.ToString()), new Claim("Email", account.Email), new Claim("Role", RoleName(account.Role)), new Claim("FullName", account.FullName), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) };
        var token = new JwtSecurityToken(Issuer, Audience, claims, expires: expires, signingCredentials: new SigningCredentials(Key(configuration), SecurityAlgorithms.HmacSha256));
        return new { token = new JwtSecurityTokenHandler().WriteToken(token), expiresAt = expires, account };
    }
}
