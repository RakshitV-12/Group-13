using ExpenseTracker.Data;
using ExpenseTracker.DTOs;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services
{
    public class GoalService
    {
        private readonly ExpenseTrackerDbContext _context;
        private readonly NotificationService _notificationService;

        public GoalService(ExpenseTrackerDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<List<GoalResponseDto>> GetGoalsAsync(string userId)
        {
            var goals = await _context.Goals
                .Where(g => g.UserId == userId)
                .OrderByDescending(g => g.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            return goals.Select(g => MapToDto(g)).ToList();
        }

        public async Task<GoalResponseDto?> GetGoalByIdAsync(string userId, int id)
        {
            var goal = await _context.Goals
                .FirstOrDefaultAsync(g => g.GoalId == id && g.UserId == userId);

            return goal == null ? null : MapToDto(goal);
        }

        public async Task<GoalResponseDto> CreateGoalAsync(string userId, CreateGoalDto dto)
        {
            if (dto.TargetAmount <= 0)
                throw new ArgumentException("Target amount must be greater than zero.");

            var isAchieved = dto.CurrentAmount >= dto.TargetAmount;
            var goal = new Goal
            {
                UserId = userId,
                Name = dto.Name.Trim(),
                TargetAmount = dto.TargetAmount,
                CurrentAmount = dto.CurrentAmount,
                DueDate = dto.DueDate,
                Notes = dto.Notes?.Trim(),
                Status = isAchieved ? "Achieved" : "In Progress",
                CreatedAt = DateTime.UtcNow
            };

            await _context.Goals.AddAsync(goal);
            await _context.SaveChangesAsync();

            if (isAchieved)
            {
                await TriggerGoalAchievedNotificationAsync(userId, goal);
            }

            return MapToDto(goal);
        }

        public async Task<GoalResponseDto?> UpdateGoalAsync(string userId, int id, UpdateGoalDto dto)
        {
            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.GoalId == id && g.UserId == userId);
            if (goal == null) return null;

            if (dto.TargetAmount <= 0)
                throw new ArgumentException("Target amount must be greater than zero.");

            goal.Name = dto.Name.Trim();
            goal.TargetAmount = dto.TargetAmount;
            goal.CurrentAmount = dto.CurrentAmount;
            goal.DueDate = dto.DueDate;
            goal.Notes = dto.Notes?.Trim();
            goal.UpdatedAt = DateTime.UtcNow;

            if (goal.CurrentAmount >= goal.TargetAmount)
            {
                goal.Status = "Achieved";
                await TriggerGoalAchievedNotificationAsync(userId, goal);
            }
            else
            {
                goal.Status = !string.IsNullOrWhiteSpace(dto.Status) ? dto.Status : "In Progress";
            }

            await _context.SaveChangesAsync();
            return MapToDto(goal);
        }

        public async Task<bool> DeleteGoalAsync(string userId, int id)
        {
            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.GoalId == id && g.UserId == userId);
            if (goal == null) return false;

            _context.Goals.Remove(goal);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task TriggerGoalAchievedNotificationAsync(string userId, Goal goal)
        {
            var title = "🎉 Goal Achieved";
            var message = $"Congratulations! You achieved your goal: \"{goal.Name}\". Target: ₹{goal.TargetAmount:N0}.";
            var referenceKey = $"GoalAchieved-{goal.GoalId}";

            await _notificationService.CreateNotificationIfNotExistsAsync(userId, "GoalAchieved", title, message, referenceKey);
        }

        private static GoalResponseDto MapToDto(Goal g)
        {
            return new GoalResponseDto
            {
                GoalId = g.GoalId,
                UserId = g.UserId,
                Name = g.Name,
                TargetAmount = g.TargetAmount,
                CurrentAmount = g.CurrentAmount,
                DueDate = g.DueDate,
                Notes = g.Notes,
                Status = g.Status,
                CreatedAt = g.CreatedAt,
                UpdatedAt = g.UpdatedAt
            };
        }
    }
}
