# Agile Sprint Planning Document (Web API Module)

## Project: Group 13 — Expense Tracker with AI Insights
**Methodology:** Agile Scrum  
**Sprint Cadence:** 2 Weeks per Sprint (5 Sprints Total)  
**Target Architecture:** ASP.NET Core (.NET 8) Web API  

---

## 1. High-Level Sprint Breakdown

```
+---------------------------------------------------------------------------------------+
| SPRINT 1: Setup, Identity & Database Architecture (Story Points: 21)                  |
| Focus: Database migrations, Identity/JWT, User Auth endpoints (FR1, FR2, FR17)        |
+---------------------------------------------------------------------------------------+
| SPRINT 2: Core Transactions & Categories Web API (Story Points: 26)                   |
| Focus: CRUD Transactions, Filtering/Paging, Categories API (FR3, FR4, FR5, FR6, FR7)   |
+---------------------------------------------------------------------------------------+
| SPRINT 3: Budgeting Engine, Alerts & Dashboard API (Story Points: 23)                 |
| Focus: Budget calculations, Utilization tracking, Summary metrics (FR8, FR9, FR10, FR15)|
+---------------------------------------------------------------------------------------+
| SPRINT 4: AI Insights, Reports & Intelligence Engine (Story Points: 26)               |
| Focus: Anomaly detection, Predictions, Recommendations, Reports (FR11, FR12, FR13, 14, 16)|
+---------------------------------------------------------------------------------------+
| SPRINT 5: Integration Testing, Security Hardening & CI/CD (Story Points: 18)           |
| Focus: Test automation, Performance audits, Swagger documentation, Repo deployment    |
+---------------------------------------------------------------------------------------+
```

---

## 2. Detailed User Stories & Acceptance Criteria Matrix

| User Story ID | Target Feature | User Story Format | Estimation (SP) | Acceptance Criteria (Given-When-Then) |
|---|---|---|---|---|
| **US-API-01** | User Registration & Auth | *As a user, I want to securely register and log in via API so that I can access my private financial records.* | 5 | **Given** valid registration details (`Email`, `Password`, `FullName`), **When** `POST /api/auth/register` is invoked, **Then** return HTTP `201 Created` and hash password securely. |
| **US-API-02** | Add Transaction | *As a user, I want to record income and expenses via API so that my transaction history remains accurate.* | 5 | **Given** an authenticated user and valid payload (`Amount > 0`, `CategoryId`, `Type`, `Date`), **When** `POST /api/transactions` is sent, **Then** return `201 Created` with generated `TransactionId`. |
| **US-API-03** | Filter & List Transactions | *As a user, I want to query transactions with pagination and date range filters to analyze my spending.* | 5 | **Given** an authenticated user, **When** `GET /api/transactions?startDate=X&endDate=Y&page=1` is called, **Then** return `200 OK` with paginated records isolated strictly to the user. |
| **US-API-04** | Budget Creation & Thresholds | *As a user, I want to set monthly category budgets so that I can monitor utilization limits.* | 5 | **Given** budget amount, category, month, and year, **When** `POST /api/budgets` is invoked, **Then** save budget and enforce unique constraint per category/period. |
| **US-API-05** | Budget Monitoring & Alert Calculation | *As a system/user, I want real-time budget utilization percentages calculated to trigger limit warnings.* | 5 | **Given** active transactions for current month, **When** `GET /api/budgets/status` is executed, **Then** compute spending vs threshold and return alerts if spent $\ge 80\%$. |
| **US-API-06** | Financial Dashboard Summary | *As a user, I want aggregated financial KPIs (Income, Expense, Net Savings) for quick visualization.* | 5 | **Given** authenticated user, **When** `GET /api/dashboard/summary` is called, **Then** return total income, total expense, savings rate, and category distribution. |
| **US-API-07** | AI Anomaly Detection & Insights | *As a user, I want to query AI-detected spending anomalies and recommendations.* | 8 | **Given** historical transaction records, **When** `GET /api/ai/insights` is triggered, **Then** return detected spending outliers and actionable cost-saving tips. |

---

## 3. Sprint Task Backlog & Execution Details

### Sprint 1: Project Scaffolding, Identity & Security (21 SP)
- **TASK-101:** Initialize Clean Architecture project structure (.NET 8 solution).
- **TASK-102:** Configure Entity Framework Core `ExpenseTrackerDbContext` with SQL Server provider.
- **TASK-103:** Implement ASP.NET Core Identity with custom `ApplicationUser` entity.
- **TASK-104:** Implement JWT token service with symmetric signature and claims generation.
- **TASK-105:** Implement `AuthController` (`/api/auth/register`, `/api/auth/login`, `/api/auth/me`).

### Sprint 2: Core Transactions & Categories (26 SP)
- **TASK-201:** Implement `Category` entity, seed standard default categories.
- **TASK-202:** Implement `CategoriesController` for listing global + user categories and creating custom categories.
- **TASK-203:** Implement `Transaction` entity with validation rules (Amount > 0, PaymentMethod check).
- **TASK-204:** Implement `TransactionService` with user data isolation (FR17) and pagination.
- **TASK-205:** Implement `TransactionsController` (GET, POST, GET/{id}, PUT/{id}, DELETE/{id}).

### Sprint 3: Budgeting Engine, Alerts & Dashboard (23 SP)
- **TASK-301:** Implement `Budget` entity with composite unique constraint `(UserId, CategoryId, PeriodMonth, PeriodYear)`.
- **TASK-302:** Implement `BudgetService` with real-time utilization calculation against active monthly transactions.
- **TASK-303:** Implement automated threshold alerts ($\ge 80\%$ utilization and $\ge 100\%$ over-budget).
- **TASK-304:** Implement `DashboardService` with income, expense, net savings, savings rate, and category percentage breakdown.
- **TASK-305:** Implement `DashboardController` and `BudgetsController`.

### Sprint 4: AI Insights, Reports & Intelligence Engine (26 SP)
- **TASK-401:** Implement `AIInsight` and `Report` entities.
- **TASK-402:** Implement statistical outlier algorithm ($> \mu + 2\sigma$) to flag anomalous spending spikes.
- **TASK-403:** Implement predictive burn-rate formula to forecast month-end expenditure.
- **TASK-404:** Implement rule-based recommendation generator for high-velocity categories.
- **TASK-405:** Implement `AIInsightsController` and `ReportsController`.

### Sprint 5: Testing, Security Hardening & GitHub Deployment (18 SP)
- **TASK-501:** Implement comprehensive unit test suite (`ExpenseTracker.UnitTests`) covering services and algorithms.
- **TASK-502:** Implement integration test suite (`ExpenseTracker.IntegrationTests`) covering end-to-end API workflows.
- **TASK-503:** Configure Swagger UI with Bearer Token authorization.
- **TASK-504:** Configure global exception handling middleware.
- **TASK-505:** Author comprehensive `README.md` and initialize Git repository.
