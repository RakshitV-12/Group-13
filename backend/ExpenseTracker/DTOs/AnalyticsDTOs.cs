using System;
using System.Collections.Generic;

namespace ExpenseTracker.DTOs
{
    public class AnomalyAlertDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal CurrentSpend { get; set; }
        public decimal HistoricalAverage { get; set; }
        public decimal StandardDeviation { get; set; }
        public decimal PercentageIncrease { get; set; }
        public string Severity { get; set; } = "Medium"; // Low, Medium, High
        public string Message { get; set; } = string.Empty;
    }

    public class SpendingPredictionDto
    {
        public int DaysElapsed { get; set; }
        public int TotalDaysInMonth { get; set; }
        public decimal CurrentMonthExpense { get; set; }
        public decimal DailyBurnRate { get; set; }
        public decimal ProjectedMonthEndExpense { get; set; }
        public decimal MonthlyBudgetLimit { get; set; }
        public decimal ProjectedVariance { get; set; }
        public bool IsExceedingBudgetRisk { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class RecurringExpenseDto
    {
        public long RecurringId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal MonthlyAmount { get; set; }
        public decimal AnnualCost { get; set; }
        public string Frequency { get; set; } = "Monthly";
        public DateTime LastObservedDate { get; set; }
    }

    public class ComponentScoreDto
    {
        public string Name { get; set; } = string.Empty;
        public int MaxPoints { get; set; }
        public int EarnedScore { get; set; }
        public decimal RawMetricValue { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class FinancialHealthScoreDto
    {
        public int HealthScore { get; set; } // 0 - 100
        public string RatingGrade { get; set; } = "Good"; // Excellent, Good, Fair, Needs Attention
        public List<ComponentScoreDto> Components { get; set; } = new();
        public List<string> ActionableRecommendations { get; set; } = new();
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CompleteAnalyticsOverviewDto
    {
        public FinancialHealthScoreDto HealthScore { get; set; } = new();
        public SpendingPredictionDto Prediction { get; set; } = new();
        public List<AnomalyAlertDto> Anomalies { get; set; } = new();
        public List<RecurringExpenseDto> RecurringExpenses { get; set; } = new();
        public decimal TotalMonthlyRecurring { get; set; }
        public decimal TotalAnnualRecurring { get; set; }
    }
}
