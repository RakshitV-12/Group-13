using ExpenseTracker.Data;
using ExpenseTracker.DTOs;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services
{
    public class FinancialRuleService
    {
        private readonly ExpenseTrackerDbContext _context;

        public FinancialRuleService(ExpenseTrackerDbContext context)
        {
            _context = context;
        }

        public async Task<List<FinancialRuleDto>> GetAllRulesAsync(string userId)
        {
            var rules = await _context.FinancialRules
                .Include(r => r.Allocations)
                .Where(r => r.IsSystemDefault || r.UserId == userId)
                .AsNoTracking()
                .OrderByDescending(r => r.IsSystemDefault)
                .ThenBy(r => r.RuleId)
                .ToListAsync();

            return rules.Select(MapToDto).ToList();
        }

        public async Task<FinancialRuleDto?> GetRuleByIdAsync(int ruleId, string userId)
        {
            var rule = await _context.FinancialRules
                .Include(r => r.Allocations)
                .FirstOrDefaultAsync(r => r.RuleId == ruleId && (r.IsSystemDefault || r.UserId == userId));

            return rule != null ? MapToDto(rule) : null;
        }

        public async Task<FinancialRuleDto> CreateCustomRuleAsync(string userId, CreateCustomRuleRequestDto dto)
        {
            var rule = new FinancialRule
            {
                UserId = userId,
                Name = dto.Name,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? "Custom user-defined financial allocation strategy." : dto.Description,
                StrategyType = "Custom",
                IsSystemDefault = false,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var alloc in dto.Allocations)
            {
                rule.Allocations.Add(new FinancialRuleAllocation
                {
                    BucketName = alloc.BucketName,
                    Percentage = alloc.Percentage,
                    CategoryNamesCsv = alloc.CategoryNamesCsv
                });
            }

            await _context.FinancialRules.AddAsync(rule);
            await _context.SaveChangesAsync();

            return MapToDto(rule);
        }

        public async Task<FinancialRuleDto?> UpdateCustomRuleAsync(int ruleId, string userId, CreateCustomRuleRequestDto dto)
        {
            var rule = await _context.FinancialRules
                .Include(r => r.Allocations)
                .FirstOrDefaultAsync(r => r.RuleId == ruleId && r.UserId == userId && !r.IsSystemDefault);

            if (rule == null) return null;

            rule.Name = dto.Name;
            rule.Description = dto.Description;

            _context.FinancialRuleAllocations.RemoveRange(rule.Allocations);
            rule.Allocations.Clear();

            foreach (var alloc in dto.Allocations)
            {
                rule.Allocations.Add(new FinancialRuleAllocation
                {
                    RuleId = ruleId,
                    BucketName = alloc.BucketName,
                    Percentage = alloc.Percentage,
                    CategoryNamesCsv = alloc.CategoryNamesCsv
                });
            }

            await _context.SaveChangesAsync();
            return MapToDto(rule);
        }

        public async Task<UserActiveRuleStatusDto?> ActivateRuleAsync(string userId, int ruleId, decimal monthlyIncome)
        {
            var rule = await _context.FinancialRules
                .Include(r => r.Allocations)
                .FirstOrDefaultAsync(r => r.RuleId == ruleId && (r.IsSystemDefault || r.UserId == userId));

            if (rule == null) return null;

            var existingActive = await _context.UserActiveRules
                .FirstOrDefaultAsync(uar => uar.UserId == userId);

            if (existingActive != null)
            {
                existingActive.RuleId = ruleId;
                existingActive.MonthlyIncome = monthlyIncome;
                existingActive.ActivatedAt = DateTime.UtcNow;
            }
            else
            {
                var newActive = new UserActiveRule
                {
                    UserId = userId,
                    RuleId = ruleId,
                    MonthlyIncome = monthlyIncome,
                    ActivatedAt = DateTime.UtcNow
                };
                await _context.UserActiveRules.AddAsync(newActive);
            }

            await _context.SaveChangesAsync();
            return await EvaluateActiveRuleStatusAsync(userId);
        }

        public async Task<UserActiveRuleStatusDto?> EvaluateActiveRuleStatusAsync(string userId, int? month = null, int? year = null)
        {
            var activeRule = await _context.UserActiveRules
                .Include(uar => uar.Rule)
                .ThenInclude(r => r.Allocations)
                .AsNoTracking()
                .FirstOrDefaultAsync(uar => uar.UserId == userId);

            if (activeRule == null)
            {
                // Fallback to default 50/30/20 system rule if none activated yet
                var defaultRule = await _context.FinancialRules
                    .Include(r => r.Allocations)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.IsSystemDefault && r.Name.Contains("50 / 30 / 20"));

                if (defaultRule == null) return null;

                activeRule = new UserActiveRule
                {
                    UserId = userId,
                    RuleId = defaultRule.RuleId,
                    Rule = defaultRule,
                    MonthlyIncome = 50000m,
                    ActivatedAt = DateTime.UtcNow
                };
            }

            var targetMonth = month ?? DateTime.UtcNow.Month;
            var targetYear = year ?? DateTime.UtcNow.Year;

            // Fetch actual user transactions for target month
            var startDate = new DateTime(targetYear, targetMonth, 1);
            var endDate = startDate.AddMonths(1).AddTicks(-1);

            var transactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.TransactionDate >= startDate && t.TransactionDate <= endDate)
                .AsNoTracking()
                .ToListAsync();

            var totalIncomeTxns = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
            var totalExpenseTxns = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);

            // Effective Income for calculations
            var effectiveIncome = activeRule.MonthlyIncome > 0 ? activeRule.MonthlyIncome : (totalIncomeTxns > 0 ? totalIncomeTxns : 50000m);
            var totalSavings = Math.Max(0, effectiveIncome - totalExpenseTxns);

            var bucketStatuses = new List<RuleBucketStatusDto>();

            foreach (var alloc in activeRule.Rule.Allocations)
            {
                var targetAmount = (alloc.Percentage / 100.00m) * effectiveIncome;

                // Match categories for this bucket
                var catList = (alloc.CategoryNamesCsv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                decimal actualSpent = 0m;
                if (alloc.BucketName.Equals("Savings", StringComparison.OrdinalIgnoreCase) ||
                    alloc.BucketName.Equals("Savings & Investment", StringComparison.OrdinalIgnoreCase))
                {
                    // Savings calculation
                    var investmentSpent = transactions.Where(t => t.Type == "Expense" && catList.Contains(t.Category.Name)).Sum(t => t.Amount);
                    actualSpent = totalSavings + investmentSpent;
                }
                else
                {
                    actualSpent = transactions
                        .Where(t => t.Type == "Expense" && (catList.Length == 0 || catList.Contains(t.Category.Name)))
                        .Sum(t => t.Amount);
                }

                var variance = actualSpent - targetAmount;
                string status;

                if (alloc.BucketName.Contains("Savings") || alloc.BucketName.Contains("Investment"))
                {
                    status = actualSpent >= targetAmount ? "✓ On track" : $"⚠ Below target by ₹{Math.Abs(variance):N0}";
                }
                else
                {
                    status = actualSpent <= targetAmount ? "✓ Within target" : $"⚠ Over target by ₹{variance:N0}";
                }

                bucketStatuses.Add(new RuleBucketStatusDto
                {
                    BucketName = alloc.BucketName,
                    TargetPercentage = alloc.Percentage,
                    TargetAmount = Math.Round(targetAmount, 2),
                    ActualSpent = Math.Round(actualSpent, 2),
                    VarianceAmount = Math.Round(variance, 2),
                    Status = status,
                    CategoriesCsv = alloc.CategoryNamesCsv ?? ""
                });
            }

            return new UserActiveRuleStatusDto
            {
                ActiveRuleId = activeRule.Id,
                RuleId = activeRule.RuleId,
                RuleName = activeRule.Rule.Name,
                StrategyType = activeRule.Rule.StrategyType,
                RuleDescription = activeRule.Rule.Description,
                MonthlyIncome = effectiveIncome,
                TotalActualExpenses = totalExpenseTxns,
                TotalActualSavings = totalSavings,
                Buckets = bucketStatuses,
                EvaluatedAt = DateTime.UtcNow
            };
        }

        private static FinancialRuleDto MapToDto(FinancialRule r)
        {
            return new FinancialRuleDto
            {
                RuleId = r.RuleId,
                UserId = r.UserId,
                Name = r.Name,
                Description = r.Description,
                StrategyType = r.StrategyType,
                IsSystemDefault = r.IsSystemDefault,
                Allocations = r.Allocations.Select(a => new FinancialRuleAllocationDto
                {
                    AllocationId = a.AllocationId,
                    BucketName = a.BucketName,
                    Percentage = a.Percentage,
                    TargetAmount = a.TargetAmount,
                    CategoryNamesCsv = a.CategoryNamesCsv
                }).ToList()
            };
        }
    }
}
