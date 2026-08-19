# Agile Sprint Planning Document

## Project: Smart Expense Tracker with AI Insights
**Academic Level:** College Final Year / Major Project & Viva Ready  
**Methodology:** Agile Scrum (4 Sprints)  
**Tech Stack:** ASP.NET Core (.NET 8) Web API, Entity Framework Core, SQL Server, Vanilla HTML/CSS/JS  

---

## 1. High-Level Sprint Breakdown

```
+---------------------------------------------------------------------------------------+
| SPRINT 1: Database Setup, Identity & User Authentication                              |
| - SQL Server schema & EF Core migrations                                              |
| - JWT authentication (Register, Login, Token generation)                              |
| - User data isolation rules                                                          |
+---------------------------------------------------------------------------------------+
| SPRINT 2: Core Expense Entry (Manual & Quick Entry) & Categories                      |
| - 10 standard categories & keywords seed                                              |
| - Manual transaction entry (Amount, Description, Category, Date, Payment Method)      |
| - Quick entry engine (e.g. "Suji 250" -> Food, "Uber 180" -> Transport)             |
| - Transaction CRUD, search, and category filtering                                    |
+---------------------------------------------------------------------------------------+
| SPRINT 3: Dashboard KPIs, 50/30/20 Financial Rule & Budgeting Engine                  |
| - Total income, total expenses, savings rate calculations                             |
| - 50/30/20 Financial rule status (50% Needs, 30% Wants, 20% Savings)                  |
| - Monthly & Category budget limits with real-time threshold warnings (80% / 100%)     |
+---------------------------------------------------------------------------------------+
| SPRINT 4: Optional AI Financial Chatbot, Testing & Frontend UI Polish                 |
| - Database-backed AI Chatbot (answers spending queries without hallucination)        |
| - xUnit automated unit & integration testing                                          |
| - Responsive HTML/CSS/JS frontend interface & Swagger UI verification                 |
+---------------------------------------------------------------------------------------+
```

---

## 2. Core User Stories & Acceptance Criteria

| ID | Feature | User Story Format | Acceptance Criteria |
|---|---|---|---|
| **US-01** | User Auth | *As a student/user, I want to register and log in securely so that my financial records remain private.* | **Given** valid email and password, **When** login is called, **Then** return JWT token and isolate all data to this user. |
| **US-02** | Manual Expense | *As a user, I want to record income/expenses with amount, date, category, and payment method.* | **Given** valid transaction fields, **When** `POST /api/transactions` is submitted, **Then** save record in database. |
| **US-03** | Quick Entry | *As a busy user, I want to type "Suji 250" and have the system auto-detect the category.* | **Given** `"Suji 250"`, **When** `POST /api/transactions/quick` is called, **Then** recognize `Food`, `₹250`, `Suji` and persist it. |
| **US-04** | 50/30/20 Rule | *As a user, I want to see if my spending follows healthy financial rules.* | **Given** monthly income & expenses, **When** dashboard is loaded, **Then** calculate Needs %, Wants %, and Savings % against 50/30/20 targets. |
| **US-05** | Budget Alerts | *As a user, I want a warning when I reach 80% of my monthly budget.* | **Given** active spending, **When** total spent reaches 80% of budget cap, **Then** show warning badge and progress meter. |
| **US-06** | AI Chatbot | *As a user, I want to ask questions like "How much did I spend on food?".* | **Given** a query, **When** sent to chatbot, **Then** compute exact total from database transactions and return answer. |
