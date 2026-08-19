# Software Requirements Specification (SRS)

## Project: Group 13 — Expense Tracker with AI Insights
**Document Version:** 1.0.0  
**Target Platform:** ASP.NET Core (.NET 8) Web API / C#, Entity Framework Core, Microsoft SQL Server  
**Reference Document:** Expense_Tracker_Requirement_Analysis  

---

## 1. Executive Summary & Vision

The **Expense Tracker with AI Insights** is an enterprise-grade personal finance and budgeting platform engineered to provide users with full transparency, granular control, and automated intelligence over their financial life.

Modern financial management demands more than basic transaction ledgers; users require proactive budgeting, automated limit alerts, statistical outlier / anomaly detection on spending behavior, trend forecasting, and actionable optimization recommendations. The Web API module serves as the secure, high-throughput backend backbone driving all transaction processing, user isolation boundaries, mathematical computations, and intelligence analytics.

---

## 2. Functional Requirements (FR Matrix)

### Module 1: User Identity, Security & Isolation (FR1 – FR2, FR17)
* **FR1: User Registration:** The system must allow new users to register with full name, unique email, and strong password. Passwords must be hashed using secure algorithms (PBKDF2 / ASP.NET Core Identity standard).
* **FR2: JWT Authentication:** The system must authenticate users via credentials and issue signed JSON Web Tokens (JWT) containing user identifier claims.
* **FR17: Multi-Tenant User Data Isolation:** The system must guarantee strict user data isolation across all endpoints and queries. A user must never be able to access, query, modify, or delete another user's categories, transactions, budgets, insights, notifications, or reports.

### Module 2: Category Management (FR3)
* **FR3.1: Global Default Categories:** The system must provide predefined standard categories for income (e.g., Salary, Freelance & Investments) and expenses (e.g., Food & Dining, Groceries, Rent & Housing, Utilities, Transportation, Entertainment, Healthcare).
* **FR3.2: Custom User Categories:** Users must be able to create custom categories with custom icons, color hex codes, and income/expense classification. Custom categories are tied exclusively to the creating user.

### Module 3: Transaction Management (FR4 – FR7)
* **FR4: Transaction Ingestion:** Users must be able to log income and expense transactions specifying amount ($> 0$), category, date/time, payment method (`Cash`, `Credit Card`, `Debit Card`, `UPI`, `Net Banking`, `Other`), and optional description.
* **FR5: Transaction Listing & Filtering:** Users must be able to query transactions filtered by start date, end date, category, transaction type, and payment method with server-side pagination.
* **FR6: Transaction Modification:** Users must be able to update any attribute of an existing transaction they own.
* **FR7: Transaction Deletion:** Users must be able to permanently delete transactions they own.

### Module 4: Budget Allocation & Utilization Tracking (FR8 – FR10, FR15)
* **FR8: Monthly Budgeting:** Users must be able to configure monthly budget spending caps for specific categories or an overall monthly budget cap for a given calendar month and year.
* **FR9: Budget Utilization Calculation:** The system must calculate real-time spending vs. budget caps, computing the exact utilization percentage:
  $$\text{Utilization \%} = \left( \frac{\sum \text{Expenses in Period}}{\text{Budget Amount}} \right) \times 100$$
* **FR10: Budget Alert Threshold:** When utilization reaches or exceeds the configured threshold (default $80.00\%$) or $100.00\%$, the system must flag the budget as threshold-exceeded or over-budget.
* **FR15: In-App Notifications:** The system must generate notification records when budgets reach thresholds or when unusual anomalies are flagged.

### Module 5: Financial Dashboard & Aggregated KPIs (FR10)
* **FR10.1: Summary KPIs:** The system must compute total income, total expenses, net savings ($\text{Income} - \text{Expenses}$), and savings rate ($\frac{\text{Net Savings}}{\text{Total Income}} \times 100$).
* **FR10.2: Category Breakdown:** The system must aggregate spending totals and percentage distributions per expense category for any requested date range.
* **FR10.3: Recent Activity:** The system must retrieve the most recent transactions for rapid dashboard rendering.

### Module 6: AI Insights & Intelligence Engine (FR11 – FR14)
* **FR11: Spending Anomaly Detection:** The system must analyze historical spending by category and identify anomalous transactions that exceed historical category averages by more than two standard deviations ($> \mu + 2\sigma$) or surge significantly above usual trends.
* **FR12: Month-End Expenditure Prediction:** The system must compute average daily burn rates and project end-of-month spending:
  $$\text{Projected Total} = \text{Current Spend} + (\text{Daily Average Spend} \times \text{Remaining Days in Month})$$
* **FR13: Actionable Recommendations:** The system must generate actionable, context-aware suggestions (e.g., highlighting categories where spending surged $>25\%$ compared to the previous period).
* **FR14: Spending Patterns:** The system must detect recurring high-velocity spending patterns and summarize actionable insights with confidence scores.

### Module 7: Periodic Reporting (FR16)
* **FR16: Financial Reports:** Users must be able to generate and query structured periodic financial reports (Monthly, Quarterly, Annual, Custom) containing aggregated summaries, category breakdowns, and key performance metrics.

---

## 3. Non-Functional Requirements (NFR Matrix)

* **NFR1: Security & Compliance:** All protected endpoints must require JWT Bearer authentication. Password complexity must be enforced. SQL injection protection must be guaranteed via parameterized queries / EF Core ORM.
* **NFR2: Performance & Scalability:** API response times for standard CRUD operations must be $< 100\text{ ms}$ under normal loads. Database indexes must be strategically placed on foreign keys and compound query filters `(UserId, TransactionDate)`.
* **NFR3: Reliability & Data Integrity:** Relational constraints (foreign keys, check constraints, unique constraints per user/category/period) must prevent orphan records or duplicate budgets.
* **NFR4: Maintainability & Clean Architecture:** The application must adhere to Clean Architecture separation of concerns: Core, Infrastructure, Application, WebApi, and Unit/Integration Tests.
* **NFR5: Comprehensive Test Coverage:** Automated unit and integration tests must validate all critical business logic, calculations, and user isolation boundaries.
