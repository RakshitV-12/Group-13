using ExpenseTracker.DTOs;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpenseTracker.Controllers
{
    [ApiController]
    public abstract class BaseApiController : ControllerBase
    {
        protected string GetUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("Unauthorized or missing user context.");
            return userId;
        }
    }

    [Route("api/[controller]")]
    public class AuthController : BaseApiController
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.RegisterAsync(dto);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.LoginAsync(dto);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetProfile()
        {
            var profile = await _authService.GetProfileAsync(GetUserId());
            if (profile == null) return NotFound();
            return Ok(profile);
        }
    }
}
