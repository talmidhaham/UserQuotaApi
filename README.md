# UserQuotaApi

A .NET 9 REST API that manages users and per-user request quotas.  
Quotas are backed by **EF Core + SQLite** during the day (09:00–16:59 UTC) and by an **in-memory store** at night — switched automatically via a time-based strategy pattern.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Clone & Restore](#2-clone--restore)
3. [Project Structure](#3-project-structure)
4. [Running the API](#4-running-the-api)
   - [Direct (dotnet run)](#41-direct-dotnet-run)
   - [Aspire Dashboard](#42-aspire-dashboard)
5. [Swagger UI](#5-swagger-ui)
6. [API Reference](#6-api-reference)
7. [Testing](#7-testing)
   - [Unit Tests](#71-unit-tests)
   - [Integration Tests](#72-integration-tests)
   - [Smoke Test Script](#73-smoke-test-script-powershell)
8. [Inspecting the Database](#8-inspecting-the-database)
9. [Configuration](#9-configuration)
10. [Architecture Notes](#10-architecture-notes)

---

## 1. Prerequisites

| Tool | Minimum Version | Check |
|------|----------------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download) | **9.0** | `dotnet --version` |
| Git | any | `git --version` |
| PowerShell | 7+ (for smoke test) | `pwsh --version` |

> The `global.json` at the repo root pins the SDK to `9.0.100` with `latestFeature` roll-forward.  
> No database server, Docker, or cloud account is required — SQLite runs in-process.

---

## 2. Clone & Restore

```bash
git clone <repo-url>
cd UserQuotaApi

# Restore all NuGet packages
dotnet restore
```

---

## 3. Project Structure

```
UserQuotaApi/
├── src/
│   ├── UserQuotaApi.API/          # Web API — controllers, repositories, EF Core
│   ├── UserQuotaApi.AppHost/      # .NET Aspire orchestration host
│   └── UserQuotaApi.ServiceDefaults/  # Shared OpenTelemetry / health-check config
├── tests/
│   ├── UserQuotaApi.UnitTests/        # Fast in-process unit tests (xUnit)
│   └── UserQuotaApi.IntegrationTests/ # Full HTTP integration tests (WebApplicationFactory)
├── tools/
│   └── DbQuery/                   # CLI tool for inspecting quota.db
├── test-api.ps1                   # PowerShell smoke-test script
└── README.md
```

**Key source files:**

| File | Purpose |
|------|---------|
| `Controllers/UsersController.cs` | CRUD for users |
| `Controllers/QuotaController.cs` | Consume quota / list quotas |
| `Repositories/TimeBasedQuotaRepository.cs` | Strategy router (EF ↔ InMemory) |
| `Services/UtcTimeDataSourceSelector.cs` | Day/night decision (09:00–16:59 UTC) |
| `Infrastructure/AppDbContext.cs` | EF Core DbContext + Unit of Work |
| `Extensions/DatabaseExtensions.cs` | SQLite registration |
| `Extensions/RepositoryExtensions.cs` | DI wiring for all repositories |

---

## 4. Running the API

### Recommended evaluator quick start

From the repository root, restore dependencies, run all automated tests, and
start the API:

```powershell
dotnet restore UserQuotaApi.sln
dotnet test UserQuotaApi.sln --no-restore
dotnet run --project src\UserQuotaApi.API --no-restore
```

Then open `http://localhost:5000/swagger`.

This is the simplest evaluation path. It does not require Docker, a database
server, the Aspire workload, or an HTTPS development certificate. The SQLite
schema and `quota.db` file are created automatically.

### 4.1 Direct (dotnet run)

```powershell
dotnet run --project src\UserQuotaApi.API
```

The API starts on **`http://localhost:5000`**.

```
Now listening on: http://localhost:5000
Application started. Press Ctrl+C to shut down.
```

SQLite creates `quota.db` automatically on first run — no migrations needed.

---

### 4.2 Aspire Dashboard

The optional `.NET Aspire` AppHost provides a live telemetry dashboard (traces,
logs, and metrics). It is not required to run or test the API.

The default AppHost profile uses HTTPS and requires a valid ASP.NET Core
development certificate. Create and trust it once before starting that profile:

```powershell
dotnet dev-certs https
dotnet dev-certs https --trust
```

Verify that a valid certificate is available:

```powershell
dotnet dev-certs https --check
```

If the existing certificate is expired or invalid, recreate it:

```powershell
dotnet dev-certs https --clean
dotnet dev-certs https
dotnet dev-certs https --trust
```

Then run the AppHost:

```powershell
dotnet run --project src\UserQuotaApi.AppHost
```

Alternatively, run the existing HTTP profile without creating a certificate:

```powershell
dotnet run --project src\UserQuotaApi.AppHost --launch-profile http
```

Aspire prints two URLs when it starts:

```
Login to the dashboard at: https://localhost:17136/login?t=<token>
quota-api: http://localhost:<port>
```

Open the dashboard URL in your browser to see:

- **Resources** — live status of the API process
- **Console logs** — structured logs from every request
- **Traces** — distributed trace spans per HTTP request
- **Metrics** — request rates, durations, error counts

> The development certificate is required for the Aspire dashboard's HTTPS
> endpoint. It is not required when running the API directly over
> `http://localhost:5000` as described in section 4.1. Production deployments
> should use a proper production certificate, not the development certificate.

> This project references the Aspire 9 hosting packages directly, so no
> separate Aspire workload installation is required.

---

## 5. Swagger UI

When the API runs in **Development** mode (the default for `dotnet run`), Swagger UI is available at:

```
http://localhost:5000/swagger
```

You can explore and try every endpoint directly from the browser.

Additional health endpoints:

| URL | Description |
|-----|-------------|
| `http://localhost:5000/health` | Overall health (Healthy / Unhealthy) |
| `http://localhost:5000/alive` | Liveness probe |

---

## 6. API Reference

### Users

| Method | Endpoint | Body | Success |
|--------|----------|------|---------|
| `POST` | `/api/users` | `{ "name": "Alice", "email": "alice@example.com" }` | `201 Created` |
| `GET` | `/api/users/{id}` | — | `200 OK` |
| `PUT` | `/api/users/{id}` | `{ "name": "...", "email": "..." }` | `200 OK` |
| `DELETE` | `/api/users/{id}` | — | `204 No Content` |

Creating a user also initialises a quota record for that user automatically.

### Quota

| Method | Endpoint | Body | Success | Failure |
|--------|----------|------|---------|---------|
| `GET` | `/api/quota` | — | `200 OK` — array of all quota records | — |
| `POST` | `/api/quota/consume/{id}` | — | `200 OK` | `429 Too Many Requests` when limit reached |

**Example — create a user and consume quota:**

```powershell
# Create user
$r = Invoke-WebRequest http://localhost:5000/api/users `
     -Method POST -ContentType "application/json" `
     -Body '{"name":"Alice","email":"alice@example.com"}' `
     -SkipHttpErrorCheck
$user = $r.Content | ConvertFrom-Json

# Consume quota
Invoke-WebRequest "http://localhost:5000/api/quota/consume/$($user.id)" `
     -Method POST -SkipHttpErrorCheck
```

---

## 7. Testing

### 7.1 Unit Tests

Fast, in-process tests — no running API or database required.

```powershell
dotnet test tests\UserQuotaApi.UnitTests
```

**Coverage:**

| Test class | What it tests |
|------------|--------------|
| `InMemoryQuotaRepositoryTests` | Within-limit, over-limit, exact-limit, concurrency, get-all, get-by-id |
| `DataSourceSelectorTests` | UTC hour → daytime/nighttime routing (09:00–16:59 = day) |

Expected output:

```
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14
```

---

### 7.2 Integration Tests

Full HTTP stack tests using `WebApplicationFactory<Program>`.  
Each test class spins up a real in-process server with an isolated **SQLite in-memory database** — no file I/O, no shared state between classes.

```powershell
dotnet test tests\UserQuotaApi.IntegrationTests
```

**Two factory fixtures pin the strategy branch under test:**

| Fixture | `IsDaytime()` | Quota store |
|---------|--------------|-------------|
| `DaytimeFactory` | `true` | EF Core → SQLite in-memory |
| `NighttimeFactory` | `false` | `InMemoryQuotaRepository` |

**Test classes:**

| Class | Factory | Scenarios |
|-------|---------|-----------|
| `UsersControllerTests` | Daytime | POST, GET, PUT, DELETE — happy paths and 404s |
| `QuotaControllerDaytimeTests` | Daytime | GetAll, consume within limit, enforce 429 (EF path) |
| `QuotaControllerNighttimeTests` | Nighttime | Consume, enforce 429, **30 concurrent requests** (InMemory path) |

Expected output:

```
Passed!  - Failed: 0, Passed: 17, Skipped: 0, Total: 17
```

Run both suites together:

```powershell
dotnet test
```

Expected totals across both test projects:

```
Unit tests:        Failed: 0, Passed: 14, Skipped: 0, Total: 14
Integration tests: Failed: 0, Passed: 17, Skipped: 0, Total: 17
Overall:           Failed: 0, Passed: 31, Skipped: 0, Total: 31
```

---

### 7.3 Smoke Test Script (PowerShell)

An end-to-end script that hits the **live running API** and asserts status codes with colour-coded output.

> Requires the API to be running first (`dotnet run --project src\UserQuotaApi.API`).

```powershell
.\test-api.ps1                              # default http://localhost:5000
.\test-api.ps1 -BaseUrl "http://localhost:5000"
```

**Scenarios covered:**

| # | Scenario | Assertions |
|---|----------|-----------|
| 1 | User CRUD | Create → GET → PUT → DELETE → 404 checks (8 assertions) |
| 2 | Quota — within limit | Create user, consume all 5 units → all 200 OK (7 assertions) |
| 3 | Quota — limit enforced | 6th and 7th consume → 429 with body message (2 assertions) |
| 4 | Independent counters | Second user's quota unaffected by first user's exhaustion (2 assertions) |
| 5 | Edge cases | Minimal-field user, quota list count (2 assertions) |

Expected output:

```
  Total : 21
  Passed: 21
  Failed: 0

  All assertions passed.
```

The script exits with code `0` on success and `1` on any failure — safe to use in CI.

---

## 8. Inspecting the Database

The persistent SQLite file is written to:

```
src\UserQuotaApi.API\quota.db
```

Use the included `DbQuery` tool to print `Users` and `Quotas` tables in a formatted table:

```powershell
dotnet run --project tools\DbQuery
```

You can also pass a custom path:

```powershell
dotnet run --project tools\DbQuery -- C:\path\to\other.db
```

**Sample output:**

```
Connected to: ...\src\UserQuotaApi.API\quota.db

┌─ Users ────────────────────────────────────────────────┐
  +----+-------+---------------------+-----------------------------+
  | Id | Name  | Email               | CreatedAt                   |
  +----+-------+---------------------+-----------------------------+
  |  1 | Alice | alice@example.com   | 2026-07-15 10:00:00.0000000 |
  |  2 | Bob   | bob@example.com     | 2026-07-15 10:01:00.0000000 |
  +----+-------+---------------------+-----------------------------+
  2 row(s)

┌─ Quotas ───────────────────────────────────────────────┐
  +----+--------+---------------+-------------+---------+
  | Id | UserId | ConsumedCount | MaxRequests | Version |
  +----+--------+---------------+-------------+---------+
  |  1 |      1 |             3 |           5 |       3 |
  |  2 |      2 |             5 |           5 |       5 |
  +----+--------+---------------+-------------+---------+
  2 row(s)
```

The `Version` column is the optimistic concurrency token — it increments on every successful `TryConsumeAsync` call.

---

## 9. Configuration

All settings live in `src/UserQuotaApi.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "QuotaDb": "Data Source=quota.db"
  },
  "Quota": {
    "MaxRequests": 5
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionStrings:QuotaDb` | `Data Source=quota.db` | SQLite file path (relative to the API working directory) |
| `Quota:MaxRequests` | `5` | Maximum requests per user before 429 is returned |

Override for local development in `appsettings.Development.json` or via environment variables:

```powershell
$env:Quota__MaxRequests = "10"
dotnet run --project src\UserQuotaApi.API
```

---

## 10. Architecture Notes

### Strategy pattern for quota storage

```
IQuotaRepository
    └── TimeBasedQuotaRepository        ← registered as IQuotaRepository
            ├── EfQuotaRepository       ← active 09:00–16:59 UTC (SQLite)
            └── InMemoryQuotaRepository ← active 17:00–08:59 UTC (in-memory)

IUserRepository
    └── EfUserRepository                ← always EF Core (single source of truth)
```

### Concurrency

| Store | Mechanism |
|-------|-----------|
| EF Core | Optimistic locking via `Version` concurrency token + retry loop |
| InMemory | `lock(quota)` per record — prevents over-consumption under parallel requests |

### Unit of Work

`AppDbContext` implements `IUnitOfWork`.  
Controllers call `UnitOfWork.SaveChangesAsync()` explicitly — repositories only stage changes.  
`InMemoryQuotaRepository` returns a `NoOpUnitOfWork` (saves are immediate, no transaction concept).
