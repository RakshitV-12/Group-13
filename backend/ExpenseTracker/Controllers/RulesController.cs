using ExpenseTracker.DTOs;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RulesController : ControllerBase
    {
        private readonly FinancialRuleService _ruleService;

        public RulesController(FinancialRuleService ruleService)
        {
            _ruleService = ruleService;
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("User identification claim missing.");
        }

        [HttpGet]
        public async Task<IActionResult> GetAllRules()
        {
            var userId = GetUserId();
            var rules = await _ruleService.GetAllRulesAsync(userId);
            return Ok(rules);
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveRuleStatus()
        {
            var userId = GetUserId();
            var status = await _ruleService.EvaluateActiveRuleStatusAsync(userId);
            if (status == null)
            {
                return NotFound(new { message = "No active financial strategy selected yet." });
            }
            return Ok(status);
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus([FromQuery] int? month, [FromQuery] int? year)
        {
            var userId = GetUserId();
            var status = await _ruleService.EvaluateActiveRuleStatusAsync(userId, month, year);
            return Ok(status);
        }

        [HttpPost("activate")]
        public async Task<IActionResult> ActivateRule([FromBody] ActivateRuleRequestDto dto)
        {
            var userId = GetUserId();
            var result = await _ruleService.ActivateRuleAsync(userId, dto.RuleId, dto.MonthlyIncome);
            if (result == null)
            {
                return BadRequest(new { message = "Invalid rule ID or rule not accessible." });
            }
            return Ok(result);
        }

        [HttpPost("custom")]
        public async Task<IActionResult> CreateCustomRule([FromBody] CreateCustomRuleRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserId();
            var created = await _ruleService.CreateCustomRuleAsync(userId, dto);
            return CreatedAtAction(nameof(GetAllRules), new { id = created.RuleId }, created);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCustomRule(int id, [FromBody] CreateCustomRuleRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserId();
            var updated = await _ruleService.UpdateCustomRuleAsync(id, userId, dto);
            if (updated == null)
            {
                return NotFound(new { message = "Custom rule not found or not editable by current user." });
            }
            return Ok(updated);
        }
    }
}
