using ExpenseTracker.DTOs;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class GoalsController : BaseApiController
    {
        private readonly GoalService _goalService;

        public GoalsController(GoalService goalService)
        {
            _goalService = goalService;
        }

        [HttpGet]
        public async Task<IActionResult> GetGoals()
        {
            var goals = await _goalService.GetGoalsAsync(GetUserId());
            return Ok(goals);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetGoalById(int id)
        {
            var goal = await _goalService.GetGoalByIdAsync(GetUserId(), id);
            if (goal == null) return NotFound(new { message = $"Goal with ID {id} not found." });
            return Ok(goal);
        }

        [HttpPost]
        public async Task<IActionResult> CreateGoal([FromBody] CreateGoalDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var goal = await _goalService.CreateGoalAsync(GetUserId(), dto);
            return CreatedAtAction(nameof(GetGoalById), new { id = goal.GoalId }, goal);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateGoal(int id, [FromBody] UpdateGoalDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var updated = await _goalService.UpdateGoalAsync(GetUserId(), id, dto);
            if (updated == null) return NotFound(new { message = $"Goal with ID {id} not found." });
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteGoal(int id)
        {
            var success = await _goalService.DeleteGoalAsync(GetUserId(), id);
            if (!success) return NotFound(new { message = $"Goal with ID {id} not found." });
            return NoContent();
        }
    }
}
