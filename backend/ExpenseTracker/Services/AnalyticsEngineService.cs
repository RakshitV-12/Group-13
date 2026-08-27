using ExpenseTracker.Data;
using ExpenseTracker.DTOs;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services
{
    public class AnalyticsEngineService
    {
        private readonly ExpenseTrackerDbContext _context;

        public AnalyticsEngineService(ExpenseTrackerDbContext context)
        {
            _context = context;
        }

        public async Task<List<AnomalyAlertDto>> GetSpendingAnomaliesAsync(string userId)
        {
            var now = DateTime.UtcNow;
            var currentMonthStart = new DateTime(now.Year, now.Month, 1);

            // Fetch transactions for the past 6 months
            var sixMonthsAgo = currentMonthStart.AddMonths(-6);

            var transactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Type == "Expense" && t.TransactionDate >= sixMonthsAgo)
                .AsNoTracking()
                .ToListAsync();

            var currentMonthTxns = transactions.Where(t => t.TransactionDate >= currentMonthStart).ToList();
            var historicalTxns = transactions.Where(t => t.TransactionDate < currentMonthStart).ToList();

            var alerts = new List<AnomalyAlertDto>();

            var currentByCategory = currentMonthTxns
                .GroupBy(t => new { t.CategoryId, CategoryName = t.Category.Name })
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

            foreach (var item in currentByCategory)
            {
                var catId = item.Key.CategoryId;
                var catName = item.Key.CategoryName;
                var currentSpend = item.Value;

                // Monthly historical spends for this category
                var monthlySpends = historicalTxns
                    .Where(t => t.CategoryId == catId)
                    .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                    .Select(g => g.Sum(t => t.Amount))
                    .ToList();

                if (!monthlySpends.Any())
                {
                    // If no historical data but spent over ₹5,000 in Shopping/Other/Entertainment
                    if (currentSpend >= 5000m && (catName == "Shopping" || catName == "Entertainment" || catName == "Travel"))
                    {
                        alerts.Add(new AnomalyAlertDto
                        {
                            CategoryId = catId,
                            CategoryName = catName,
                            CurrentSpend = currentSpend,
                            HistoricalAverage = 0m,
                            StandardDeviation = 0m,
                            PercentageIncrease = 100m,
                            Severity = "Medium",
                            Message = $"Unusual initial high spending of ₹{currentSpend:N0} detected in {catName}."
                        });
                    }
                    continue;
                }

                var avg = monthlySpends.Average();
                double sumOfSquares = monthlySpends.Sum(s => Math.Pow((double)(s - avg), 2));
                var stdDev = (decimal)Math.Sqrt(sumOfSquares / monthlySpends.Count);

                // Trigger anomaly if current spend exceeds (Average + 1.5 * StdDev) and exceeds ₹1,000 variance
                var threshold = avg + (1.5m * stdDev);
                if (currentSpend > threshold && (currentSpend - avg) >= 1000m)
                {
                    var percentIncrease = avg > 0 ? ((currentSpend - avg) / avg) * 100m : 100m;
                    var severity = percentIncrease >= 100m ? "High" : "Medium";

                    alerts.Add(new AnomalyAlertDto
                    {
                        CategoryId = catId,
                        CategoryName = catName,
                        CurrentSpend = currentSpend,
                        HistoricalAverage = Math.Round(avg, 2),
                        StandardDeviation = Math.Round(stdDev, 2),
                        PercentageIncrease = Math.Round(percentIncrease, 1),
                        Severity = severity,
                        Message = $"⚠️ Unusual {catName} spending of ₹{currentSpend:N0} (Historical Avg: ₹{avg:N0}, +{percentIncrease:F0}% increase)."
                    });
                }
            }

            return alerts.OrderByDescending(a => a.CurrentSpend).ToList();
        }

        public async Task<SpendingPredictionDto> GetSpendingPredictionAsync(string userId)
        {
            var now = DateTime.UtcNow;
            var currentMonthStart = new DateTime(now.Year, now.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            var daysElapsed = Math.Max(1, now.Day);

            var currentMonthSpent = await _context.Transactions
                .Where(t => t.UserId == userId && t.Type == "Expense" && t.TransactionDate >= currentMonthStart)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            var dailyBurnRate = currentMonthSpent / daysElapsed;
            var projectedTotal = Math.Round(dailyBurnRate * daysInMonth, 2);

            // Fetch overall budget for current month
            var activeBudget = await _context.Budgets
                .Where(b => b.UserId == userId && b.CategoryId == null && b.PeriodMonth == now.Month && b.PeriodYear == now.Year)
                .Select(b => b.Amount)
                .FirstOrDefaultAsync();

            if (activeBudget <= 0m)
            {
                // Fallback to active rule target or default 40000
                activeBudget = 40000m;
            }

            var variance = projectedTotal - activeBudget;
            var isOverRisk = projectedTotal > activeBudget;

            string message;
            if (isOverRisk)
            {
                message = $"⚠️ At your current daily spending rate of ₹{dailyBurnRate:N0}/day, your projected month-end spending is ₹{projectedTotal:N0}, exceeding your ₹{activeBudget:N0} budget by ~₹{variance:N0}.";
            }
            else
            {
                message = $"✓ On track! At your current daily rate of ₹{dailyBurnRate:N0}/day, projected month-end spending is ₹{projectedTotal:N0} (Within ₹{activeBudget:N0} limit).";
            }

            return new SpendingPredictionDto
            {
                DaysElapsed = daysElapsed,
                TotalDaysInMonth = daysInMonth,
                CurrentMonthExpense = currentMonthSpent,
                DailyBurnRate = Math.Round(dailyBurnRate, 2),
                ProjectedMonthEndExpense = projectedTotal,
                MonthlyBudgetLimit = activeBudget,
                ProjectedVariance = Math.Round(variance, 2),
                IsExceedingBudgetRisk = isOverRisk,
                Message = message
            };
        }

        public async Task<List<RecurringExpenseDto>> GetRecurringExpensesAsync(string userId)
        {
            // Fetch past 90 days expenses
            var startDate = DateTime.UtcNow.AddDays(-90);

            var txns = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Type == "Expense" && t.TransactionDate >= startDate)
                .AsNoTracking()
                .ToListAsync();

            var groups = txns
                .Where(t => !string.IsNullOrWhiteSpace(t.Description))
                .GroupBy(t => t.Description!.Trim().ToLowerInvariant())
                .Where(g => g.Count() >= 2)
                .ToList();

            var result = new List<RecurringExpenseDto>();
            long fakeId = 1;

            foreach (var g in groups)
            {
                var sample = g.First();
                var avgAmount = g.Average(t => t.Amount);
                var monthlyCost = Math.Round(avgAmount, 2);
                var annualCost = Math.Round(monthlyCost * 12m, 2);

                result.Add(new RecurringExpenseDto
                {
                    RecurringId = fakeId++,
                    Title = char.ToUpper(sample.Description![0]) + (sample.Description.Length > 1 ? sample.Description[1..] : ""),
                    CategoryId = sample.CategoryId,
                    CategoryName = sample.Category.Name,
                    MonthlyAmount = monthlyCost,
                    AnnualCost = annualCost,
                    Frequency = "Monthly",
                    LastObservedDate = g.Max(t => t.TransactionDate)
                });
            }

            return result.OrderByDescending(r => r.MonthlyAmount).ToList();
        }

        public async Task<FinancialHealthScoreDto> CalculateFinancialHealthScoreAsync(string userId)
        {
            var now = DateTime.UtcNow;
            var currentMonthStart = new DateTime(now.Year, now.Month, 1);

            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId && t.TransactionDate >= currentMonthStart)
                .AsNoTracking()
                .ToListAsync();

            var activeRule = await _context.UserActiveRules
                .FirstOrDefaultAsync(uar => uar.UserId == userId);

            decimal income = activeRule?.MonthlyIncome ?? 50000m;
            decimal totalIncomeTxns = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
            if (totalIncomeTxns > income) income = totalIncomeTxns;

            decimal totalExpenses = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            decimal totalSavings = Math.Max(0, income - totalExpenses);

            // 1. Budget Adherence (30 Points)
            var budgetLimit = await _context.Budgets
                .Where(b => b.UserId == userId && b.CategoryId == null && b.PeriodMonth == now.Month && b.PeriodYear == now.Year)
                .Select(b => b.Amount)
                .FirstOrDefaultAsync();
            if (budgetLimit <= 0) budgetLimit = income * 0.8m;

            int budgetScore;
            if (totalExpenses <= budgetLimit)
            {
                budgetScore = 30;
            }
            else
            {
                var overpercent = ((totalExpenses - budgetLimit) / budgetLimit) * 100m;
                budgetScore = Math.Max(0, (int)(30 - (overpercent * 0.6m)));
            }

            // 2. Savings Rate (25 Points)
            var savingsRate = income > 0 ? (totalSavings / income) * 100m : 0m;
            int savingsScore;
            if (savingsRate >= 30m) savingsScore = 25;
            else if (savingsRate >= 20m) savingsScore = 20;
            else if (savingsRate >= 10m) savingsScore = 15;
            else if (savingsRate >= 5m) savingsScore = 10;
            else savingsScore = 5;

            // 3. Spending Stability (20 Points)
            var anomalies = await GetSpendingAnomaliesAsync(userId);
            int stabilityScore = Math.Max(0, 20 - (anomalies.Count * 5));

            // 4. Emergency Fund Progress (15 Points)
            var emergencySavings = totalSavings * 3; // Estimated accumulated balance
            var emergencyTarget = income * 3;
            var emergencyRatio = emergencyTarget > 0 ? (emergencySavings / emergencyTarget) * 100m : 0m;
            int emergencyScore = Math.Min(15, (int)(emergencyRatio * 0.15m));

            // 5. Recurring Expenses Ratio (10 Points)
            var recurring = await GetRecurringExpensesAsync(userId);
            var monthlyRecurringTotal = recurring.Sum(r => r.MonthlyAmount);
            var recurringRatio = income > 0 ? (monthlyRecurringTotal / income) * 100m : 0m;
            int recurringScore = recurringRatio <= 30m ? 10 : Math.Max(0, 10 - (int)((recurringRatio - 30m) * 0.2m));

            int finalScore = budgetScore + savingsScore + stabilityScore + emergencyScore + recurringScore;
            finalScore = Math.Clamp(finalScore, 0, 100);

            string grade;
            if (finalScore >= 85) grade = "Excellent (A+)";
            else if (finalScore >= 70) grade = "Good (A)";
            else if (finalScore >= 55) grade = "Fair (B)";
            else grade = "Needs Attention (C)";

            var recs = new List<string>();
            if (budgetScore < 25) recs.Add("Review your category spending to stay within monthly budget limits.");
            if (savingsScore < 20) recs.Add("Aim to increase your monthly savings rate closer to 20% using the 50/30/20 strategy.");
            if (anomalies.Any()) recs.Add($"Address {anomalies.Count} unusual spending spike(s) detected this month.");
            if (recurringScore < 8) recs.Add("Review recurring fixed expenses to reduce unnecessary monthly commitments.");
            if (!recs.Any()) recs.Add("Outstanding financial health! Continue executing your active strategy.");

            return new FinancialHealthScoreDto
            {
                HealthScore = finalScore,
                RatingGrade = grade,
                Components = new List<ComponentScoreDto>
                {
                    new ComponentScoreDto { Name = "Budget Adherence", MaxPoints = 30, EarnedScore = budgetScore, RawMetricValue = totalExpenses, Description = $"Spent ₹{totalExpenses:N0} vs ₹{budgetLimit:N0} budget" },
                    new ComponentScoreDto { Name = "Savings Rate", MaxPoints = 25, EarnedScore = savingsScore, RawMetricValue = Math.Round(savingsRate, 1), Description = $"{savingsRate:F1}% of income saved" },
                    new ComponentScoreDto { Name = "Spending Stability", MaxPoints = 20, EarnedScore = stabilityScore, RawMetricValue = anomalies.Count, Description = $"{anomalies.Count} spending anomalies detected" },
                    new ComponentScoreDto { Name = "Emergency Fund Target", MaxPoints = 15, EarnedScore = emergencyScore, RawMetricValue = Math.Round(emergencyRatio, 1), Description = "3-Month emergency reserve allocation" },
                    new ComponentScoreDto { Name = "Recurring Expense Ratio", MaxPoints = 10, EarnedScore = recurringScore, RawMetricValue = Math.Round(recurringRatio, 1), Description = $"{recurringRatio:F1}% committed to recurring fixed costs" }
                },
                ActionableRecommendations = recs,
                CalculatedAt = DateTime.UtcNow
            };
        }

        public async Task<CompleteAnalyticsOverviewDto> GetCompleteAnalyticsAsync(string userId)
        {
            var health = await CalculateFinancialHealthScoreAsync(userId);
            var prediction = await GetSpendingPredictionAsync(userId);
            var anomalies = await GetSpendingAnomaliesAsync(userId);
            var recurring = await GetRecurringExpensesAsync(userId);

            return new CompleteAnalyticsOverviewDto
            {
                HealthScore = health,
                Prediction = prediction,
                Anomalies = anomalies,
                RecurringExpenses = recurring,
                TotalMonthlyRecurring = recurring.Sum(r => r.MonthlyAmount),
                TotalAnnualRecurring = recurring.Sum(r => r.AnnualCost)
            };
        }
    }
}
