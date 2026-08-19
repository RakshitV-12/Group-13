# Database Design Document

## Project: Group 13 — Expense Tracker with AI Insights
**Target Engine:** Microsoft SQL Server 2019 / 2022 / Azure SQL  
**ORM:** Entity Framework Core 8.0  

---

## 1. Entity-Relationship Overview & User Data Isolation (FR17)

The database enforces a strict tenant boundary at the user level. Every core financial entity (`Transactions`, `Categories`, `Budgets`, `AIInsights`, `Notifications`, `Reports`) carries a non-nullable or nullable foreign key constraint referencing `AspNetUsers(Id)`:

```
[ AspNetUsers ] (1) ────< (N) [ Transactions ]
[ AspNetUsers ] (1) ────< (N) [ Categories ]
[ AspNetUsers ] (1) ────< (N) [ Budgets ]
[ AspNetUsers ] (1) ────< (N) [ AIInsights ]
[ AspNetUsers ] (1) ────< (N) [ Notifications ]
[ AspNetUsers ] (1) ────< (N) [ Reports ]

[ Categories ]  (1) ────< (N) [ Transactions ]
[ Categories ]  (1) ────< (N) [ Budgets ]
```

---

## 2. Relational Schema & Data Dictionary

### 2.1 Table: `dbo.AspNetUsers` (ASP.NET Core Identity)
| Column Name | Data Type | Nullable | Description |
|---|---|---|---|
| `Id` | `NVARCHAR(450)` | NO | Primary Key (GUID string) |
| `FullName` | `NVARCHAR(150)` | NO | User's full display name |
| `Email` | `NVARCHAR(256)` | NO | Unique login email address |
| `UserName` | `NVARCHAR(256)` | NO | Username (normalized email) |
| `PasswordHash` | `NVARCHAR(MAX)` | NO | PBKDF2 hashed password |
| `CreatedAt` | `DATETIME2` | NO | User account creation UTC timestamp |

### 2.2 Table: `dbo.Categories`
| Column Name | Data Type | Nullable | Constraints & Description |
|---|---|---|---|
| `CategoryId` | `INT` | NO | Primary Key, `IDENTITY(1,1)` |
| `UserId` | `NVARCHAR(450)` | YES | Foreign Key -> `AspNetUsers(Id)` (`NULL` for global default categories) |
| `Name` | `NVARCHAR(100)` | NO | Category name (e.g., 'Food & Dining') |
| `Type` | `NVARCHAR(20)` | NO | CHECK (`Type IN ('Income', 'Expense')`) |
| `Icon` | `NVARCHAR(50)` | YES | Icon identifier / SVG / FontAwesome name |
| `ColorCode` | `NVARCHAR(10)` | YES | Hex color code (e.g. `#fd7e14`) |
| `IsDefault` | `BIT` | NO | Default `0`; `1` for system global defaults |
| `CreatedAt` | `DATETIME2` | NO | UTC timestamp |

### 2.3 Table: `dbo.Transactions`
| Column Name | Data Type | Nullable | Constraints & Description |
|---|---|---|---|
| `TransactionId` | `BIGINT` | NO | Primary Key, `IDENTITY(1,1)` |
| `UserId` | `NVARCHAR(450)` | NO | Foreign Key -> `AspNetUsers(Id)` ON DELETE CASCADE |
| `CategoryId` | `INT` | NO | Foreign Key -> `Categories(CategoryId)` ON DELETE NO ACTION |
| `Amount` | `DECIMAL(18,2)` | NO | CHECK (`Amount > 0`) |
| `Type` | `NVARCHAR(20)` | NO | CHECK (`Type IN ('Income', 'Expense')`) |
| `TransactionDate` | `DATETIME2` | NO | Date and time when transaction occurred |
| `PaymentMethod` | `NVARCHAR(50)` | NO | CHECK (`PaymentMethod IN ('Cash', 'Credit Card', 'Debit Card', 'UPI', 'Net Banking', 'Other')`) |
| `Description` | `NVARCHAR(500)` | YES | Optional user notes |
| `IsRecurring` | `BIT` | NO | Default `0` |
| `CreatedAt` | `DATETIME2` | NO | UTC audit timestamp |
| `UpdatedAt` | `DATETIME2` | YES | UTC audit update timestamp |

### 2.4 Table: `dbo.Budgets`
| Column Name | Data Type | Nullable | Constraints & Description |
|---|---|---|---|
| `BudgetId` | `INT` | NO | Primary Key, `IDENTITY(1,1)` |
| `UserId` | `NVARCHAR(450)` | NO | Foreign Key -> `AspNetUsers(Id)` ON DELETE CASCADE |
| `CategoryId` | `INT` | YES | Foreign Key -> `Categories(CategoryId)` ON DELETE SET NULL (`NULL` = overall budget) |
| `Amount` | `DECIMAL(18,2)` | NO | Spending cap, CHECK (`Amount > 0`) |
| `PeriodMonth` | `INT` | NO | CHECK (`PeriodMonth BETWEEN 1 AND 12`) |
| `PeriodYear` | `INT` | NO | CHECK (`PeriodYear >= 2020`) |
| `ThresholdPercent` | `DECIMAL(5,2)` | NO | Default `80.00` |
| `CreatedAt` | `DATETIME2` | NO | UTC timestamp |
| *Constraint* | `UQ_User_Category_Period` | NO | UNIQUE (`UserId`, `CategoryId`, `PeriodMonth`, `PeriodYear`) |

### 2.5 Table: `dbo.AIInsights`
| Column Name | Data Type | Nullable | Constraints & Description |
|---|---|---|---|
| `InsightId` | `BIGINT` | NO | Primary Key, `IDENTITY(1,1)` |
| `UserId` | `NVARCHAR(450)` | NO | Foreign Key -> `AspNetUsers(Id)` ON DELETE CASCADE |
| `InsightType` | `NVARCHAR(50)` | NO | CHECK (`InsightType IN ('Anomaly', 'Prediction', 'Recommendation', 'Pattern')`) |
| `Title` | `NVARCHAR(200)` | NO | Summary headline of the insight |
| `Content` | `NVARCHAR(MAX)` | NO | Detailed analytical markdown / text description |
| `ConfidenceScore` | `DECIMAL(5,4)` | YES | Value between `0.0000` and `1.0000` |
| `IsActionable` | `BIT` | NO | Default `1` |
| `IsRead` | `BIT` | NO | Default `0` |
| `GeneratedAt` | `DATETIME2` | NO | UTC timestamp |

### 2.6 Table: `dbo.Notifications`
| Column Name | Data Type | Nullable | Constraints & Description |
|---|---|---|---|
| `NotificationId` | `BIGINT` | NO | Primary Key, `IDENTITY(1,1)` |
| `UserId` | `NVARCHAR(450)` | NO | Foreign Key -> `AspNetUsers(Id)` ON DELETE CASCADE |
| `Type` | `NVARCHAR(50)` | NO | Notification type (`BudgetAlert`, `Anomaly`, `System`) |
| `Title` | `NVARCHAR(150)` | NO | Short notification title |
| `Message` | `NVARCHAR(1000)` | NO | Notification payload body |
| `IsRead` | `BIT` | NO | Default `0` |
| `CreatedAt` | `DATETIME2` | NO | UTC timestamp |

### 2.7 Table: `dbo.Reports`
| Column Name | Data Type | Nullable | Constraints & Description |
|---|---|---|---|
| `ReportId` | `BIGINT` | NO | Primary Key, `IDENTITY(1,1)` |
| `UserId` | `NVARCHAR(450)` | NO | Foreign Key -> `AspNetUsers(Id)` ON DELETE CASCADE |
| `ReportType` | `NVARCHAR(50)` | NO | CHECK (`ReportType IN ('Monthly', 'Quarterly', 'Annual', 'Custom')`) |
| `StartDate` | `DATE` | NO | Start date of reporting period |
| `EndDate` | `DATE` | NO | End date of reporting period |
| `SummaryJson` | `NVARCHAR(MAX)` | NO | JSON payload of aggregated metrics & charts |
| `GeneratedAt` | `DATETIME2` | NO | UTC timestamp |

---

## 3. Database Indexes

To guarantee optimal performance ($< 100\text{ ms}$) on high-volume queries:
1. `IX_Categories_UserId`: Fast lookup of user-defined categories alongside global defaults.
2. `IX_Transactions_UserId_Date` (`UserId`, `TransactionDate DESC`): Optimized compound index for historical pagination, date filtering, and KPI aggregations.
3. `IX_Transactions_CategoryId`: Facilitates category breakdown aggregations and budget joins.
4. `IX_Budgets_UserId_Period` (`UserId`, `PeriodYear`, `PeriodMonth`): Fast retrieval of active budget caps.
5. `IX_AIInsights_UserId_GeneratedAt`: Reverse-chronological retrieval of AI recommendations.
6. `IX_Notifications_UserId_IsRead`: Rapid count and retrieval of unread notifications.
7. `IX_Reports_UserId`: Querying historical user report archives.
