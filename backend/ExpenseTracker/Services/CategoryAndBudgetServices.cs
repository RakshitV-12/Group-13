using ExpenseTracker.Data;
using ExpenseTracker.DTOs;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services
{
    public class CategoryService
    {
        private readonly ExpenseTrackerDbContext _context;

        public CategoryService(ExpenseTrackerDbContext context)
        {
            _context = context;
        }

        public async Task<List<CategoryResponseDto>> GetCategoriesAsync(string userId)
        {
            return await _context.Categories
                .Where(c => c.UserId == null || c.UserId == userId)
                .OrderBy(c => c.IsDefault ? 0 : 1)
                .ThenBy(c => c.Name)
                .Select(c => new CategoryResponseDto
                {
                    CategoryId = c.CategoryId,
                    UserId = c.UserId,
                    Name = c.Name,
                    Type = c.Type,
                    Icon = c.Icon,
                    ColorCode = c.ColorCode,
                    IsDefault = c.IsDefault,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<CategoryResponseDto> CreateCategoryAsync(string userId, CreateCategoryDto dto)
        {
            var category = new Category
            {
                UserId = userId,
                Name = dto.Name.Trim(),
                Type = dto.Type,
                Icon = dto.Icon ?? "tag",
                ColorCode = dto.ColorCode ?? "#6c757d",
                IsDefault = false,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Categories.AddAsync(category);
            await _context.SaveChangesAsync();

            return new CategoryResponseDto
            {
                CategoryId = category.CategoryId,
                UserId = category.UserId,
                Name = category.Name,
                Type = category.Type,
                Icon = category.Icon,
                ColorCode = category.ColorCode,
                IsDefault = category.IsDefault,
                CreatedAt = category.CreatedAt
            };
        }
    }

    public class BudgetService
    {
        private readonly ExpenseTrackerDbContext _context;

        public BudgetService(ExpenseTrackerDbContext context)
        {
            _context = context;
        }

        public async Task<List<BudgetResponseDto>> GetBudgetsAsync(string userId, int? year = null, int? month = null)
        {
            var query = _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId)
                .AsNoTracking();

            if (year.HasValue) query = query.Where(b => b.PeriodYear == year.Value);
            if (month.HasValue) query = query.Where(b => b.PeriodMonth == month.Value);

            return await query
                .OrderByDescending(b => b.PeriodYear)
                .ThenByDescending(b => b.PeriodMonth)
                .Select(b => new BudgetResponseDto
                {
                    BudgetId = b.BudgetId,
                    UserId = b.UserId,
                    CategoryId = b.CategoryId,
                    CategoryName = b.Category != null ? b.Category.Name : "Overall",
                    CategoryIcon = b.Category != null ? b.Category.Icon : "pie-chart",
                    CategoryColor = b.Category != null ? b.Category.ColorCode : "#0d6efd",
                    Amount = b.Amount,
                    PeriodMonth = b.PeriodMonth,
                    PeriodYear = b.PeriodYear,
                    ThresholdPercent = b.ThresholdPercent,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<BudgetResponseDto> SetBudgetAsync(string userId, CreateBudgetDto dto)
        {
            if (dto.Amount <= 0)
                throw new ArgumentException("Budget amount must be greater than zero.");

            var existing = await _context.Budgets
                .Include(b => b.Category)
                .FirstOrDefaultAsync(b => b.UserId == userId &&
                                          b.CategoryId == dto.CategoryId &&
                                          b.PeriodMonth == dto.PeriodMonth &&
                                          b.PeriodYear == dto.PeriodYear);

            if (existing != null)
            {
                existing.Amount = dto.Amount;
                existing.ThresholdPercent = dto.ThresholdPercent;
                await _context.SaveChangesAsync();

                return new BudgetResponseDto
                {
                    BudgetId = existing.BudgetId,
                    UserId = existing.UserId,
                    CategoryId = existing.CategoryId,
                    CategoryName = existing.Category != null ? existing.Category.Name : "Overall",
                    CategoryIcon = existing.Category != null ? existing.Category.Icon : "pie-chart",
                    CategoryColor = existing.Category != null ? existing.Category.ColorCode : "#0d6efd",
                    Amount = existing.Amount,
                    PeriodMonth = existing.PeriodMonth,
                    PeriodYear = existing.PeriodYear,
                    ThresholdPercent = existing.ThresholdPercent,
                    CreatedAt = existing.CreatedAt
                };
            }

            var budget = new Budget
            {
                UserId = userId,
                CategoryId = dto.CategoryId,
                Amount = dto.Amount,
                PeriodMonth = dto.PeriodMonth,
                PeriodYear = dto.PeriodYear,
                ThresholdPercent = dto.ThresholdPercent,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Budgets.AddAsync(budget);
            await _context.SaveChangesAsync();

            var cat = dto.CategoryId.HasValue ? await _context.Categories.FindAsync(dto.CategoryId.Value) : null;

            return new BudgetResponseDto
            {
                BudgetId = budget.BudgetId,
                UserId = budget.UserId,
                CategoryId = budget.CategoryId,
                CategoryName = cat != null ? cat.Name : "Overall",
                CategoryIcon = cat != null ? cat.Icon : "pie-chart",
                CategoryColor = cat != null ? cat.ColorCode : "#0d6efd",
                Amount = budget.Amount,
                PeriodMonth = budget.PeriodMonth,
                PeriodYear = budget.PeriodYear,
                ThresholdPercent = budget.ThresholdPercent,
                CreatedAt = budget.CreatedAt
            };
        }

        public async Task<List<BudgetStatusDto>> GetBudgetStatusesAsync(string userId, int? year = null, int? month = null)
        {
            var targetYear = year ?? DateTime.UtcNow.Year;
            var targetMonth = month ?? DateTime.UtcNow.Month;

            var monthStart = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

            var budgets = await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId && b.PeriodYear == targetYear && b.PeriodMonth == targetMonth)
                .ToListAsync();

            var monthlyExpenses = await _context.Transactions
                .Where(t => t.UserId == userId && t.Type == "Expense" && t.TransactionDate >= monthStart && t.TransactionDate <= monthEnd)
                .ToListAsync();

            var list = new List<BudgetStatusDto>();

            foreach (var b in budgets)
            {
                var spent = b.CategoryId.HasValue
                    ? monthlyExpenses.Where(t => t.CategoryId == b.CategoryId.Value).Sum(t => t.Amount)
                    : monthlyExpenses.Sum(t => t.Amount);

                var util = b.Amount > 0 ? Math.Round((spent / b.Amount) * 100, 2) : 0;

                list.Add(new BudgetStatusDto
                {
                    BudgetId = b.BudgetId,
                    CategoryId = b.CategoryId,
                    CategoryName = b.Category != null ? b.Category.Name : "Overall",
                    BudgetAmount = b.Amount,
                    TotalSpent = spent,
                    UtilizationPercentage = util,
                    ThresholdPercent = b.ThresholdPercent,
                    IsWarning = util >= b.ThresholdPercent && spent < b.Amount,
                    IsExceeded = spent >= b.Amount,
                    PeriodMonth = b.PeriodMonth,
                    PeriodYear = b.PeriodYear
                });
            }

            return list;
        }

        public async Task<bool> DeleteBudgetAsync(string userId, int id)
        {
            var budget = await _context.Budgets.FirstOrDefaultAsync(b => b.BudgetId == id && b.UserId == userId);
            if (budget == null) return false;

            _context.Budgets.Remove(budget);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
