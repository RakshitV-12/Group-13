using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class InsightsController : ControllerBase
    {
        private readonly AnalyticsEngineService _analyticsService;

        public InsightsController(AnalyticsEngineService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("User identification claim missing.");
        }

        [HttpGet("anomalies")]
        public async Task<IActionResult> GetAnomalies()
        {
            var userId = GetUserId();
            var result = await _analyticsService.GetSpendingAnomaliesAsync(userId);
            return Ok(result);
        }

        [HttpGet("predictions")]
        public async Task<IActionResult> GetPredictions()
        {
            var userId = GetUserId();
            var result = await _analyticsService.GetSpendingPredictionAsync(userId);
            return Ok(result);
        }

        [HttpGet("recurring")]
        public async Task<IActionResult> GetRecurringExpenses()
        {
            var userId = GetUserId();
            var result = await _analyticsService.GetRecurringExpensesAsync(userId);
            return Ok(result);
        }

        [HttpGet("health-score")]
        public async Task<IActionResult> GetHealthScore()
        {
            var userId = GetUserId();
            var result = await _analyticsService.CalculateFinancialHealthScoreAsync(userId);
            return Ok(result);
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetCompleteOverview()
        {
            var userId = GetUserId();
            var result = await _analyticsService.GetCompleteAnalyticsAsync(userId);
            return Ok(result);
        }
    }
}
