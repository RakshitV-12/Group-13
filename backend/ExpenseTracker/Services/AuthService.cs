using ExpenseTracker.DTOs;
using ExpenseTracker.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ExpenseTracker.Services
{
    public class AuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration _configuration;

        public AuthService(UserManager<User> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            var existing = await _userManager.FindByEmailAsync(dto.Email);
            if (existing != null)
                throw new InvalidOperationException("Email is already registered.");

            var user = new User
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Registration failed: {errors}");
            }

            return GenerateJwtToken(user);
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
                throw new UnauthorizedAccessException("Invalid email or password.");

            return GenerateJwtToken(user);
        }

        public async Task<UserProfileDto?> GetProfileAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return null;

            return new UserProfileDto
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName
            };
        }

        private AuthResponseDto GenerateJwtToken(User user)
        {
            var key = _configuration["Jwt:Key"] ?? "ExpenseTrackerSecretKeyMustBe32BytesLong!";
            var issuer = _configuration["Jwt:Issuer"] ?? "ExpenseTracker";
            var audience = _configuration["Jwt:Audience"] ?? "ExpenseTrackerUsers";

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, user.FullName)
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: credentials);

            return new AuthResponseDto
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName
            };
        }
    }
}
