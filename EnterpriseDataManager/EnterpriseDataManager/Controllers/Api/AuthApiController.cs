namespace EnterpriseDataManager.Controllers.Api;

using Asp.Versioning;
using EnterpriseDataManager.Core.Entities;
using EnterpriseDataManager.Core.Interfaces.Services;
using EnterpriseDataManager.Data;
using EnterpriseDataManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[ApiController]
[AllowAnonymous]
[Produces("application/json")]
[EnableRateLimiting("api")]
public class AuthApiController : ControllerBase
{
    private const int RefreshTokenExpiryDays = 30;

    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly EnterpriseDataManagerDbContext _db;

    public AuthApiController(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IJwtTokenService jwtTokenService,
        IOptions<JwtOptions> jwtOptions,
        EnterpriseDataManagerDbContext db)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _jwtOptions = jwtOptions.Value;
        _db = db;
    }

    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Token([FromBody] TokenRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return Unauthorized();

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var tokenWithJti = _jwtTokenService.GenerateTokenWithJti(user.Id, user.Email!, roles);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);

        var refreshToken = await CreateRefreshTokenAsync(user.Id, tokenWithJti.Jti, GetRemoteIp());

        return Ok(new TokenResponse(tokenWithJti.Token, refreshToken.Token, expiresAt, _jwtOptions.ExpiryMinutes * 60));
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var storedToken = await _db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken && !t.IsDeleted);

        if (storedToken == null || storedToken.IsRevoked || storedToken.ExpiresAt <= DateTimeOffset.UtcNow)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(storedToken.UserId);
        if (user == null)
            return Unauthorized();

        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == request.RefreshToken);
        if (existing != null)
        {
            existing.IsRevoked = true;
            await _db.SaveChangesAsync();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var tokenWithJti = _jwtTokenService.GenerateTokenWithJti(user.Id, user.Email!, roles);
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);

        var newRefreshToken = await CreateRefreshTokenAsync(user.Id, tokenWithJti.Jti, GetRemoteIp());

        return Ok(new TokenResponse(tokenWithJti.Token, newRefreshToken.Token, expiresAt, _jwtOptions.ExpiryMinutes * 60));
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (!string.IsNullOrWhiteSpace(jti))
        {
            var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.JwtId == jti && !t.IsRevoked);
            if (token != null)
            {
                token.IsRevoked = true;
                await _db.SaveChangesAsync();
            }
        }

        return Ok();
    }

    private async Task<RefreshToken> CreateRefreshTokenAsync(string userId, string jwtId, string createdByIp)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var token = new RefreshToken
        {
            UserId = userId,
            Token = Convert.ToBase64String(tokenBytes),
            JwtId = jwtId,
            IsRevoked = false,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenExpiryDays),
            CreatedByIp = createdByIp
        };

        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync();

        return token;
    }

    private string GetRemoteIp()
        => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

public record TokenRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record TokenResponse(string Token, string RefreshToken, DateTime ExpiresAt, int ExpiresIn);
