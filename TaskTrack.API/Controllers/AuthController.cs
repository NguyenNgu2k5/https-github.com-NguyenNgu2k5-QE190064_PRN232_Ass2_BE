using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrack.Service.Dtos;
using TaskTrack.Service.Services;

namespace TaskTrack.API.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(AccountService service, TokenIssuer tokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct) => StatusCode(201, await service.RegisterAsync(request, ct));

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var account = await service.LoginAsync(request, ct);
        return account is null ? Unauthorized(new { message = "Invalid email or password." }) : Ok(tokens.Issue(account));
    }

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct) => Ok(await service.GetAsync(int.Parse(User.FindFirstValue("AccountID")!), ct));
}
