using ExpenseTracker.Data;
using ExpenseTracker.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ExpenseTracker.Services
{
    public class DashboardService
    {
        private readonly ExpenseTrackerDbContext _context;
        private readonly BudgetService _budgetService;

        public DashboardService(ExpenseTrackerDbContext context, BudgetService budgetService)
        {
            _context = context;
            _budgetService = budgetService;
        }

        public async Task<DashboardSummaryDto> GetSummaryAsync(string userId, int? year = null, int? month = null)
        {
            var targetYear = year ?? DateTime.UtcNow.Year;
            var targetMonth = month ?? DateTime.UtcNow.Month;

            var monthStart = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

            // Fetch current month transactions
            var monthlyTransactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.TransactionDate >= monthStart && t.TransactionDate <= monthEnd)
                .ToListAsync();

            var totalIncome = monthlyTransactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
            var totalExpenses = monthlyTransactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);

            // Category expenses
            var expenseList = monthlyTransactions.Where(t => t.Type == "Expense").ToList();
            var categoryBreakdown = expenseList
                .GroupBy(t => t.Category)
                .Select(g => new CategorySpendingDto
                {
                    CategoryId = g.Key.CategoryId,
                    CategoryName = g.Key.Name,
                    Icon = g.Key.Icon,
                    ColorCode = g.Key.ColorCode,
                    Amount = g.Sum(x => x.Amount),
                    Percentage = totalExpenses > 0 ? Math.Round((g.Sum(x => x.Amount) / totalExpenses) * 100, 2) : 0,
                    Count = g.Count()
                })
                .OrderByDescending(c => c.Amount)
                .ToList();

            // 6-month historical trends
            var trends = new List<MonthlyTrendDto>();
            for (int i = 5; i >= 0; i--)
            {
                var pDate = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i);
                var pStart = new DateTime(pDate.Year, pDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var pEnd = pStart.AddMonths(1).AddTicks(-1);

                var pTx = await _context.Transactions
                    .Where(t => t.UserId == userId && t.TransactionDate >= pStart && t.TransactionDate <= pEnd)
                    .ToListAsync();

                trends.Add(new MonthlyTrendDto
                {
                    Year = pDate.Year,
                    Month = pDate.Month,
                    MonthName = pDate.ToString("MMM yyyy"),
                    Income = pTx.Where(t => t.Type == "Income").Sum(t => t.Amount),
                    Expenses = pTx.Where(t => t.Type == "Expense").Sum(t => t.Amount)
                });
            }

            // Recent 5 transactions
            var recent = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.TransactionId)
                .Take(5)
                .Select(t => new TransactionResponseDto
                {
                    TransactionId = t.TransactionId,
                    UserId = t.UserId,
                    Amount = t.Amount,
                    Type = t.Type,
                    CategoryId = t.CategoryId,
                    CategoryName = t.Category.Name,
                    CategoryIcon = t.Category.Icon,
                    CategoryColor = t.Category.ColorCode,
                    TransactionDate = t.TransactionDate,
                    PaymentMethod = t.PaymentMethod,
                    Description = t.Description,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            // 50/30/20 Financial Rule calculation
            var needsCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Food", "Bills", "Healthcare", "Education", "Transport" };
            var wantsCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Shopping", "Entertainment", "Travel", "Other" };

            var needsAmount = expenseList.Where(t => needsCategories.Contains(t.Category.Name)).Sum(t => t.Amount);
            var wantsAmount = expenseList.Where(t => wantsCategories.Contains(t.Category.Name)).Sum(t => t.Amount);
            var savingsAmount = Math.Max(0, totalIncome - totalExpenses);

            var ruleDto = new FinancialRule50_30_20Dto
            {
                TotalIncome = totalIncome,
                NeedsAmount = needsAmount,
                NeedsPercentage = totalIncome > 0 ? Math.Round((needsAmount / totalIncome) * 100, 1) : 0,
                WantsAmount = wantsAmount,
                WantsPercentage = totalIncome > 0 ? Math.Round((wantsAmount / totalIncome) * 100, 1) : 0,
                SavingsAmount = savingsAmount,
                SavingsPercentage = totalIncome > 0 ? Math.Round((savingsAmount / totalIncome) * 100, 1) : 0
            };

            if (totalIncome > 0)
            {
                if (ruleDto.NeedsPercentage <= 50 && ruleDto.WantsPercentage <= 30 && ruleDto.SavingsPercentage >= 20)
                {
                    ruleDto.Status = "Excellent";
                    ruleDto.Recommendation = "Great job! Your spending is perfectly aligned with the 50/30/20 financial rule.";
                }
                else if (ruleDto.WantsPercentage > 30)
                {
                    ruleDto.Status = "High Discretionary Spend";
                    ruleDto.Recommendation = $"Your Wants spending ({ruleDto.WantsPercentage}%) exceeds the 30% target. Try reducing non-essential purchases.";
                }
                else if (ruleDto.NeedsPercentage > 50)
                {
                    ruleDto.Status = "High Essential Spend";
                    ruleDto.Recommendation = $"Your Needs spending ({ruleDto.NeedsPercentage}%) exceeds the 50% target. Review utility bills and recurring expenses.";
                }
                else
                {
                    ruleDto.Status = "Needs Attention";
                    ruleDto.Recommendation = "Focus on saving at least 20% of your income towards your emergency fund.";
                }
            }
            else
            {
                ruleDto.Status = "No Income Recorded";
                ruleDto.Recommendation = "Add income transactions to calculate your 50/30/20 financial rule breakdown.";
            }

            var budgets = await _budgetService.GetBudgetStatusesAsync(userId, targetYear, targetMonth);

            return new DashboardSummaryDto
            {
                TotalIncome = totalIncome,
                TotalExpenses = totalExpenses,
                CategoryExpenses = categoryBreakdown,
                MonthlySpending = trends,
                RecentTransactions = recent,
                FinancialRule = ruleDto,
                ActiveBudgets = budgets
            };
        }
    }

    public class AIChatbotService
    {
        private readonly ExpenseTrackerDbContext _context;

        public AIChatbotService(ExpenseTrackerDbContext context)
        {
            _context = context;
        }

        public async Task<ChatResponseDto> AnswerUserQueryAsync(string userId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return new ChatResponseDto { Answer = "Hello! Ask me questions about your expenses, such as 'How much did I spend this month?' or 'How much did I spend on food?'" };
            }

            var cleanMsg = message.Trim().ToLowerInvariant();
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

            // Fetch user's active transactions for the current month
            var monthlyTx = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.TransactionDate >= monthStart && t.TransactionDate <= monthEnd)
                .ToListAsync();

            var totalIncome = monthlyTx.Where(t => t.Type == "Income").Sum(t => t.Amount);
            var totalExpenses = monthlyTx.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            var totalSavings = totalIncome - totalExpenses;

            // 1. "How much did I spend this month?" / "Total expenses"
            if (cleanMsg.Contains("spend this month") || cleanMsg.Contains("total spend") || cleanMsg.Contains("total expense") || cleanMsg.Contains("how much i spent"))
            {
                return new ChatResponseDto
                {
                    Answer = $"You have spent a total of ₹{totalExpenses:N2} across {monthlyTx.Count(t => t.Type == "Expense")} expense transactions in {now:MMMM yyyy}.",
                    ComputedAmount = totalExpenses
                };
            }

            // 2. "How much did I save?" / "Savings"
            if (cleanMsg.Contains("save") || cleanMsg.Contains("savings") || cleanMsg.Contains("saving rate"))
            {
                var savingsRate = totalIncome > 0 ? Math.Round((totalSavings / totalIncome) * 100, 1) : 0;
                return new ChatResponseDto
                {
                    Answer = totalIncome > 0
                        ? $"Your net savings this month is ₹{totalSavings:N2} (Savings Rate: {savingsRate}% of your ₹{totalIncome:N2} income)."
                        : $"You have no income recorded this month. Your total expenses are ₹{totalExpenses:N2}.",
                    ComputedAmount = totalSavings
                };
            }

            // 3. "Where is most of my money going?" / "Top expense" / "Highest category"
            if (cleanMsg.Contains("most of my money") || cleanMsg.Contains("top expense") || cleanMsg.Contains("highest") || cleanMsg.Contains("where is my money"))
            {
                var topCat = monthlyTx
                    .Where(t => t.Type == "Expense")
                    .GroupBy(t => t.Category.Name)
                    .Select(g => new { Name = g.Key, Total = g.Sum(x => x.Amount) })
                    .OrderByDescending(x => x.Total)
                    .FirstOrDefault();

                if (topCat != null)
                {
                    var pct = totalExpenses > 0 ? Math.Round((topCat.Total / totalExpenses) * 100, 1) : 0;
                    return new ChatResponseDto
                    {
                        Answer = $"Most of your money is going towards **{topCat.Name}**, totaling ₹{topCat.Total:N2} ({pct}% of total monthly spending).",
                        Category = topCat.Name,
                        ComputedAmount = topCat.Total
                    };
                }
                return new ChatResponseDto { Answer = "You haven't recorded any expenses yet for this month." };
            }

            // 4. Specific category query (e.g. "food", "shopping", "transport", "bills", "uber", "rent")
            var categories = await _context.Categories.AsNoTracking().ToListAsync();
            foreach (var cat in categories)
            {
                if (cleanMsg.Contains(cat.Name.ToLowerInvariant()))
                {
                    var catSpent = monthlyTx
                        .Where(t => t.CategoryId == cat.CategoryId && t.Type == "Expense")
                        .Sum(t => t.Amount);

                    var count = monthlyTx.Count(t => t.CategoryId == cat.CategoryId && t.Type == "Expense");

                    return new ChatResponseDto
                    {
                        Answer = $"You have spent ₹{catSpent:N2} on **{cat.Name}** this month across {count} transactions.",
                        Category = cat.Name,
                        ComputedAmount = catSpent
                    };
                }
            }

            // 5. 50/30/20 rule query
            if (cleanMsg.Contains("50/30/20") || cleanMsg.Contains("rule") || cleanMsg.Contains("recommendation"))
            {
                var needsCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Food", "Bills", "Healthcare", "Education", "Transport" };
                var wantsCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Shopping", "Entertainment", "Travel", "Other" };

                var needs = monthlyTx.Where(t => t.Type == "Expense" && needsCategories.Contains(t.Category.Name)).Sum(t => t.Amount);
                var wants = monthlyTx.Where(t => t.Type == "Expense" && wantsCategories.Contains(t.Category.Name)).Sum(t => t.Amount);

                var needsPct = totalIncome > 0 ? Math.Round((needs / totalIncome) * 100, 1) : 0;
                var wantsPct = totalIncome > 0 ? Math.Round((wants / totalIncome) * 100, 1) : 0;
                var savPct = totalIncome > 0 ? Math.Round((totalSavings / totalIncome) * 100, 1) : 0;

                return new ChatResponseDto
                {
                    Answer = $"Under the 50/30/20 Rule: Needs = {needsPct}% (₹{needs:N2}, target ≤50%), Wants = {wantsPct}% (₹{wants:N2}, target ≤30%), Savings = {savPct}% (₹{totalSavings:N2}, target ≥20%)."
                };
            }

            // Default friendly response
            return new ChatResponseDto
            {
                Answer = $"I found {monthlyTx.Count} transactions for this month totaling ₹{totalExpenses:N2} in expenses and ₹{totalIncome:N2} in income. You can ask me: 'How much did I spend on food?', 'What is my top expense?', or 'How much did I save?'"
            };
        }
    }
}
