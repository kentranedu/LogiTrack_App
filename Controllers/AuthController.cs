using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.ComponentModel.DataAnnotations;
using LogiTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace LogiTrack.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class AuthController : ControllerBase
	{
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly SignInManager<ApplicationUser> _signInManager;
		private readonly IConfiguration _configuration;

		public AuthController(
			UserManager<ApplicationUser> userManager,
			SignInManager<ApplicationUser> signInManager,
			IConfiguration configuration)
		{
			_userManager = userManager;
			_signInManager = signInManager;
			_configuration = configuration;
		}

		[AllowAnonymous]
		[HttpPost("register")]
		public async Task<IActionResult> Register(RegisterRequest request)
		{
			var normalizedEmail = request.Email.Trim().ToLowerInvariant();

			var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
			if (existingUser is not null)
			{
				return BadRequest(new { message = "Email is already registered." });
			}

			var user = new ApplicationUser
			{
				UserName = normalizedEmail,
				Email = normalizedEmail
			};

			var result = await _userManager.CreateAsync(user, request.Password);
			if (!result.Succeeded)
			{
				var errors = result.Errors.Select(error => error.Description);
				return BadRequest(new { errors });
			}

			return Ok(new { message = "User registered successfully." });
		}

		[AllowAnonymous]
		[HttpPost("login")]
		public async Task<IActionResult> Login(LoginRequest request)
		{
			var normalizedEmail = request.Email.Trim().ToLowerInvariant();
			var user = await _userManager.FindByEmailAsync(normalizedEmail);
			if (user is null)
			{
				return Unauthorized(new { message = "Invalid email or password." });
			}

			var passwordResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
			if (!passwordResult.Succeeded)
			{
				return Unauthorized(new { message = "Invalid email or password." });
			}

			var roles = await _userManager.GetRolesAsync(user);
			var token = await GenerateJwtToken(user);
			return Ok(new { token, roles });
		}

		private async Task<string> GenerateJwtToken(ApplicationUser user)
		{
			var jwtSection = _configuration.GetSection("Jwt");
			var key = jwtSection["Key"] ?? throw new InvalidOperationException("JWT key is missing.");
			var issuer = jwtSection["Issuer"];
			var audience = jwtSection["Audience"];
			var expiresInMinutes = int.TryParse(jwtSection["ExpiresInMinutes"], out var minutes) ? minutes : 60;
			var roles = await _userManager.GetRolesAsync(user);

			var claims = new List<Claim>
			{
				new(JwtRegisteredClaimNames.Sub, user.Id),
				new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
				new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
				new(ClaimTypes.NameIdentifier, user.Id),
				new(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty)
			};

			claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

			var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
			var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

			var token = new JwtSecurityToken(
				issuer: issuer,
				audience: audience,
				claims: claims,
				expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
				signingCredentials: credentials);

			return new JwtSecurityTokenHandler().WriteToken(token);
		}

		public class RegisterRequest
		{
			[Required]
			[EmailAddress]
			[StringLength(256)]
			public string Email { get; set; } = string.Empty;

			[Required]
			[MinLength(12)]
			public string Password { get; set; } = string.Empty;
		}

		public class LoginRequest
		{
			[Required]
			[EmailAddress]
			[StringLength(256)]
			public string Email { get; set; } = string.Empty;

			[Required]
			public string Password { get; set; } = string.Empty;
		}
	}
}
