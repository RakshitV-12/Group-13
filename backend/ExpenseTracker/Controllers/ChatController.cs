using ExpenseTracker.DTOs;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpenseTracker.Controllers
{
    public class ChatQueryRequestDto
    {
        public string Message { get; set; } = string.Empty;
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly AIChatbotService _chatService;

        public ChatController(AIChatbotService chatService)
        {
            _chatService = chatService;
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("User identification claim missing.");
        }

        [HttpPost]
        public async Task<IActionResult> PostQuery([FromBody] ChatQueryRequestDto request)
        {
            var userId = GetUserId();
            var response = await _chatService.AnswerUserQueryAsync(userId, request.Message);
            return Ok(response);
        }
    }
}
