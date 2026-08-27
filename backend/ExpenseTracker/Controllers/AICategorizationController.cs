using ExpenseTracker.DTOs;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    [Route("api/ai")]
    public class AICategorizationController : BaseApiController
    {
        private readonly AICategorizationService _aiService;

        public AICategorizationController(AICategorizationService aiService)
        {
            _aiService = aiService;
        }

        [HttpPost("categorize")]
        public async Task<IActionResult> CategorizeSingle([FromBody] AICategorizeRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _aiService.CategorizeSingleAsync(GetUserId(), dto.Name, dto.Amount);
            return Ok(result);
        }

        [HttpPost("categorize-batch")]
        public async Task<IActionResult> CategorizeBatch([FromBody] AICategorizeBatchRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var results = await _aiService.CategorizeBatchAsync(GetUserId(), dto.Items);
            return Ok(results);
        }
    }
}
