using ExpenseTracker.DTOs;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class CategoriesController : BaseApiController
    {
        private readonly CategoryService _categoryService;

        public CategoriesController(CategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _categoryService.GetCategoriesAsync(GetUserId());
            return Ok(categories);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var category = await _categoryService.CreateCategoryAsync(GetUserId(), dto);
            return StatusCode(StatusCodes.Status201Created, category);
        }
    }

    [Authorize]
    [Route("api/[controller]")]
    public class BudgetsController : BaseApiController
    {
        private readonly BudgetService _budgetService;

        public BudgetsController(BudgetService budgetService)
        {
            _budgetService = budgetService;
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgets([FromQuery] int? year, [FromQuery] int? month)
        {
            var list = await _budgetService.GetBudgetsAsync(GetUserId(), year, month);
            return Ok(list);
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetBudgetStatuses([FromQuery] int? year, [FromQuery] int? month)
        {
            var statuses = await _budgetService.GetBudgetStatusesAsync(GetUserId(), year, month);
            return Ok(statuses);
        }

        [HttpPost]
        public async Task<IActionResult> SetBudget([FromBody] CreateBudgetDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _budgetService.SetBudgetAsync(GetUserId(), dto);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            var success = await _budgetService.DeleteBudgetAsync(GetUserId(), id);
            if (!success) return NotFound(new { message = $"Budget with ID {id} not found." });
            return NoContent();
        }
    }

    [Authorize]
    [Route("api/[controller]")]
    public class DashboardController : BaseApiController
    {
        private readonly DashboardService _dashboardService;

        public DashboardController(DashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int? year, [FromQuery] int? month)
        {
            var summary = await _dashboardService.GetSummaryAsync(GetUserId(), year, month);
            return Ok(summary);
        }
    }

    [Authorize]
    [Route("api/[controller]")]
    public class AIInsightsController : BaseApiController
    {
        private readonly AIChatbotService _chatbotService;

        public AIInsightsController(AIChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> AskChatbot([FromBody] ChatQueryDto query)
        {
            var response = await _chatbotService.AnswerUserQueryAsync(GetUserId(), query.Message);
            return Ok(response);
        }
    }
}
