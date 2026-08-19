-- =========================================================================
-- DATABASE CREATION SCRIPT: ExpenseTrackerDb
-- Target Engine: Microsoft SQL Server 2019 / 2022 / Azure SQL
-- Project: Smart Expense Tracker with AI Insights (Academic College Project)
-- =========================================================================

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ExpenseTrackerDb')
BEGIN
    CREATE DATABASE ExpenseTrackerDb;
END
GO

USE ExpenseTrackerDb;
GO

-- 1. Users Table (ASP.NET Core Identity)
-- Standard AspNetUsers table created automatically by EF Core Identity migrations

-- 2. Categories Table
IF OBJECT_ID('dbo.Categories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories (
        CategoryId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId NVARCHAR(450) NULL, -- NULL for default system-wide categories
        Name NVARCHAR(100) NOT NULL,
        Type NVARCHAR(20) NOT NULL CHECK (Type IN ('Income', 'Expense')),
        Icon NVARCHAR(50) NULL,
        ColorCode NVARCHAR(10) NULL,
        IsDefault BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Categories_Users FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_Categories_UserId ON dbo.Categories(UserId);
END
GO

-- 3. Category Keywords Table (For Quick Entry e.g. "Suji 250" -> Food)
IF OBJECT_ID('dbo.CategoryKeywords', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CategoryKeywords (
        KeywordId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CategoryId INT NOT NULL,
        Keyword NVARCHAR(100) NOT NULL,
        CONSTRAINT FK_CategoryKeywords_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId) ON DELETE CASCADE
    );

    CREATE INDEX IX_CategoryKeywords_Keyword ON dbo.CategoryKeywords(Keyword);
END
GO

-- 4. Transactions Table (Manual & Quick Entries)
IF OBJECT_ID('dbo.Transactions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Transactions (
        TransactionId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId NVARCHAR(450) NOT NULL,
        CategoryId INT NOT NULL,
        Amount DECIMAL(18,2) NOT NULL CHECK (Amount > 0),
        Type NVARCHAR(20) NOT NULL CHECK (Type IN ('Income', 'Expense')),
        TransactionDate DATETIME2 NOT NULL,
        PaymentMethod NVARCHAR(50) NOT NULL CHECK (PaymentMethod IN ('Cash', 'Credit Card', 'Debit Card', 'UPI', 'Net Banking', 'Other')),
        Description NVARCHAR(500) NULL,
        IsRecurring BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Transactions_Users FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE,
        CONSTRAINT FK_Transactions_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId) ON DELETE NO ACTION
    );

    CREATE INDEX IX_Transactions_UserId_Date ON dbo.Transactions(UserId, TransactionDate DESC);
    CREATE INDEX IX_Transactions_CategoryId ON dbo.Transactions(CategoryId);
END
GO

-- 5. Budgets Table (Monthly Spending Limits & Thresholds)
IF OBJECT_ID('dbo.Budgets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Budgets (
        BudgetId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId NVARCHAR(450) NOT NULL,
        CategoryId INT NULL, -- NULL implies overall monthly budget
        Amount DECIMAL(18,2) NOT NULL CHECK (Amount > 0),
        PeriodMonth INT NOT NULL CHECK (PeriodMonth BETWEEN 1 AND 12),
        PeriodYear INT NOT NULL CHECK (PeriodYear >= 2020),
        ThresholdPercent DECIMAL(5,2) NOT NULL DEFAULT 80.00,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Budgets_Users FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE,
        CONSTRAINT FK_Budgets_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId) ON DELETE SET NULL,
        CONSTRAINT UQ_User_Category_Period UNIQUE (UserId, CategoryId, PeriodMonth, PeriodYear)
    );

    CREATE INDEX IX_Budgets_UserId_Period ON dbo.Budgets(UserId, PeriodYear, PeriodMonth);
END
GO

-- 6. AI Insights Table (Optional Chatbot & Suggestions)
IF OBJECT_ID('dbo.AIInsights', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AIInsights (
        InsightId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId NVARCHAR(450) NOT NULL,
        InsightType NVARCHAR(50) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        ConfidenceScore DECIMAL(5,4) NULL,
        IsRead BIT NOT NULL DEFAULT 0,
        GeneratedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_AIInsights_Users FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_AIInsights_UserId_GeneratedAt ON dbo.AIInsights(UserId, GeneratedAt DESC);
END
GO

-- 7. Seed Initial Default Categories
IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE IsDefault = 1)
BEGIN
    INSERT INTO dbo.Categories (UserId, Name, Type, Icon, ColorCode, IsDefault)
    VALUES 
    (NULL, 'Food', 'Expense', 'utensils', '#fd7e14', 1),
    (NULL, 'Shopping', 'Expense', 'shopping-bag', '#6610f2', 1),
    (NULL, 'Transport', 'Expense', 'truck', '#0d6efd', 1),
    (NULL, 'Bills', 'Expense', 'zap', '#20c997', 1),
    (NULL, 'Entertainment', 'Expense', 'film', '#e83e8c', 1),
    (NULL, 'Healthcare', 'Expense', 'heart', '#dc3545', 1),
    (NULL, 'Education', 'Expense', 'book', '#198754', 1),
    (NULL, 'Travel', 'Expense', 'compass', '#0dcaf0', 1),
    (NULL, 'Investment', 'Expense', 'trending-up', '#ffc107', 1),
    (NULL, 'Other', 'Expense', 'tag', '#6c757d', 1),
    (NULL, 'Salary', 'Income', 'briefcase', '#28a745', 1),
    (NULL, 'Freelance & Others', 'Income', 'dollar-sign', '#17a2b8', 1);
END
GO
