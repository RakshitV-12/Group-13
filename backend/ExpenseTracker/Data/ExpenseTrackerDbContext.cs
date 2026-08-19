using ExpenseTracker.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Data
{
    public class ExpenseTrackerDbContext : IdentityDbContext<User>
    {
        public ExpenseTrackerDbContext(DbContextOptions<ExpenseTrackerDbContext> options)
            : base(options)
        {
        }

        public DbSet<Category> Categories => Set<Category>();
        public DbSet<CategoryKeyword> CategoryKeywords => Set<CategoryKeyword>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<Budget> Budgets => Set<Budget>();
        public DbSet<AIInsight> AIInsights => Set<AIInsight>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Categories
            builder.Entity<Category>(entity =>
            {
                entity.ToTable("Categories");
                entity.HasKey(c => c.CategoryId);
                entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
                entity.Property(c => c.Type).IsRequired().HasMaxLength(20);
                entity.Property(c => c.IsDefault).HasDefaultValue(false);
                entity.HasOne(c => c.User)
                      .WithMany(u => u.Categories)
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(c => c.UserId);
            });

            // Category Keywords
            builder.Entity<CategoryKeyword>(entity =>
            {
                entity.ToTable("CategoryKeywords");
                entity.HasKey(k => k.KeywordId);
                entity.Property(k => k.Keyword).IsRequired().HasMaxLength(100);
                entity.HasOne(k => k.Category)
                      .WithMany(c => c.Keywords)
                      .HasForeignKey(k => k.CategoryId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(k => k.Keyword);
            });

            // Transactions
            builder.Entity<Transaction>(entity =>
            {
                entity.ToTable("Transactions");
                entity.HasKey(t => t.TransactionId);
                entity.Property(t => t.Amount).HasPrecision(18, 2).IsRequired();
                entity.Property(t => t.Type).IsRequired().HasMaxLength(20);
                entity.Property(t => t.PaymentMethod).IsRequired().HasMaxLength(50);
                entity.Property(t => t.Description).HasMaxLength(500);

                entity.HasOne(t => t.User)
                      .WithMany(u => u.Transactions)
                      .HasForeignKey(t => t.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(t => t.Category)
                      .WithMany(c => c.Transactions)
                      .HasForeignKey(t => t.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(t => new { t.UserId, t.TransactionDate });
            });

            // Budgets
            builder.Entity<Budget>(entity =>
            {
                entity.ToTable("Budgets");
                entity.HasKey(b => b.BudgetId);
                entity.Property(b => b.Amount).HasPrecision(18, 2).IsRequired();
                entity.Property(b => b.ThresholdPercent).HasPrecision(5, 2).HasDefaultValue(80.00m);

                entity.HasOne(b => b.User)
                      .WithMany(u => u.Budgets)
                      .HasForeignKey(b => b.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(b => b.Category)
                      .WithMany(c => c.Budgets)
                      .HasForeignKey(b => b.CategoryId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(b => new { b.UserId, b.CategoryId, b.PeriodMonth, b.PeriodYear }).IsUnique();
            });

            // AI Insights
            builder.Entity<AIInsight>(entity =>
            {
                entity.ToTable("AIInsights");
                entity.HasKey(a => a.InsightId);
                entity.Property(a => a.Title).IsRequired().HasMaxLength(200);

                entity.HasOne(a => a.User)
                      .WithMany(u => u.AIInsights)
                      .HasForeignKey(a => a.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(a => new { a.UserId, a.GeneratedAt });
            });
        }
    }
}
