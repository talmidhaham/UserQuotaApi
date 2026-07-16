<#
.SYNOPSIS
    Manual integration smoke-test for the UserQuotaApi.
    Runs a series of HTTP scenarios against a running instance and reports pass/fail.

.USAGE
    # Start the API first, then in another terminal:
    .\test-api.ps1

    # Override the base URL if needed:
    .\test-api.ps1 -BaseUrl "https://localhost:7000"
#>

param(
    [string] $BaseUrl = "http://localhost:5000"
)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

$pass  = 0
$fail  = 0
$total = 0

function Write-Header([string] $title) {
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkCyan
    Write-Host "  $title" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkCyan
}

function Write-Step([string] $verb, [string] $url, [string] $description) {
    Write-Host ""
    Write-Host "  ► $description" -ForegroundColor White
    Write-Host "    $verb $url" -ForegroundColor DarkGray
}

function Assert-Status {
    param(
        $Response,
        [int]    $Expected,
        [string] $Label
    )

    $script:total++
    $actual = [int] $Response.StatusCode

    if ($actual -eq $Expected) {
        Write-Host "    ✔  $Label — got $actual (expected $Expected)" -ForegroundColor Green
        $script:pass++
    } else {
        Write-Host "    ✘  $Label — got $actual (expected $Expected)" -ForegroundColor Red
        $script:fail++
        $body = $Response.Content
        if ($body) { Write-Host "       Body: $body" -ForegroundColor DarkRed }
    }
}

function Invoke-Api {
    param(
        [string] $Method,
        [string] $Path,
        [object] $Body = $null
    )

    $uri = "$BaseUrl$Path"
    $headers = @{ "Accept" = "application/json" }

    try {
        if ($Body) {
            $json = $Body | ConvertTo-Json -Compress
            $headers["Content-Type"] = "application/json"
            return Invoke-WebRequest -Uri $uri -Method $Method -Headers $headers `
                                     -Body $json -ErrorAction Stop -SkipHttpErrorCheck
        } else {
            return Invoke-WebRequest -Uri $uri -Method $Method -Headers $headers `
                                     -ErrorAction Stop -SkipHttpErrorCheck
        }
    } catch {
        Write-Host "    ✘  REQUEST FAILED: $_" -ForegroundColor Red
        $script:fail++
        $script:total++
        return $null
    }
}

function Get-JsonBody($Response) {
    try   { return $Response.Content | ConvertFrom-Json }
    catch { return $null }
}

# ---------------------------------------------------------------------------
# Connectivity check
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "  UserQuotaApi — API Smoke Tests" -ForegroundColor Cyan
Write-Host "  Target: $BaseUrl" -ForegroundColor DarkGray
Write-Host ""

try {
    $ping = Invoke-WebRequest -Uri "$BaseUrl/health" -UseBasicParsing -ErrorAction Stop -SkipHttpErrorCheck
    Write-Host "  ✔  API reachable (health: $([int]$ping.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "  ✘  Cannot reach $BaseUrl — is the API running?" -ForegroundColor Red
    Write-Host "     Start it with:  dotnet run --project src\UserQuotaApi.API" -ForegroundColor Yellow
    exit 1
}

# ---------------------------------------------------------------------------
# SCENARIO 1 — User CRUD
# ---------------------------------------------------------------------------

Write-Header "SCENARIO 1 · User CRUD"

# POST — create user
Write-Step "POST" "/api/users" "Create a new user (Alice)"
$r = Invoke-Api POST "/api/users" @{ name = "Alice"; email = "alice@example.com" }
if (-not $r) { exit 1 }
Assert-Status $r 201 "Create user returns 201 Created"

$alice = Get-JsonBody $r
Write-Host "    → Assigned Id: $($alice.id)  Location: $($r.Headers['Location'])" -ForegroundColor DarkGray

# GET — fetch by id
Write-Step "GET" "/api/users/$($alice.id)" "Fetch the created user by id"
$r = Invoke-Api GET "/api/users/$($alice.id)"
Assert-Status $r 200 "Get user returns 200 OK"
$fetched = Get-JsonBody $r
Write-Host "    → Name: $($fetched.name)  Email: $($fetched.email)" -ForegroundColor DarkGray

# GET — unknown id
Write-Step "GET" "/api/users/999999" "Fetch non-existent user"
$r = Invoke-Api GET "/api/users/999999"
Assert-Status $r 404 "Unknown user returns 404 Not Found"

# PUT — update user
Write-Step "PUT" "/api/users/$($alice.id)" "Update Alice's name"
$r = Invoke-Api PUT "/api/users/$($alice.id)" @{ name = "Alice Updated"; email = "alice@example.com" }
Assert-Status $r 200 "Update user returns 200 OK"
$updated = Get-JsonBody $r
Write-Host "    → Updated name: $($updated.name)" -ForegroundColor DarkGray

# PUT — unknown id
Write-Step "PUT" "/api/users/999999" "Update non-existent user"
$r = Invoke-Api PUT "/api/users/999999" @{ name = "Ghost"; email = "ghost@example.com" }
Assert-Status $r 404 "Update unknown user returns 404 Not Found"

# DELETE — remove user
Write-Step "DELETE" "/api/users/$($alice.id)" "Delete Alice"
$r = Invoke-Api DELETE "/api/users/$($alice.id)"
Assert-Status $r 204 "Delete user returns 204 No Content"

# GET after DELETE — should be gone
Write-Step "GET" "/api/users/$($alice.id)" "Fetch deleted user (expect 404)"
$r = Invoke-Api GET "/api/users/$($alice.id)"
Assert-Status $r 404 "Deleted user returns 404 Not Found"

# DELETE — unknown id
Write-Step "DELETE" "/api/users/999999" "Delete non-existent user"
$r = Invoke-Api DELETE "/api/users/999999"
Assert-Status $r 404 "Delete unknown user returns 404 Not Found"

# ---------------------------------------------------------------------------
# SCENARIO 2 — Quota: create user, consume within limit
# ---------------------------------------------------------------------------

Write-Header "SCENARIO 2 · Quota — consume within limit"

Write-Step "POST" "/api/users" "Create user Bob for quota tests"
$r = Invoke-Api POST "/api/users" @{ name = "Bob"; email = "bob_quota@example.com" }
Assert-Status $r 201 "Create user returns 201 Created"
$bob = Get-JsonBody $r
Write-Host "    → Bob Id: $($bob.id)" -ForegroundColor DarkGray

Write-Step "GET" "/api/quota" "List all quota records"
$r = Invoke-Api GET "/api/quota"
Assert-Status $r 200 "GET /api/quota returns 200 OK"

Write-Host ""
Write-Host "  Consuming 5 quota units (MaxRequests = 5)..." -ForegroundColor White
for ($i = 1; $i -le 5; $i++) {
    Write-Step "POST" "/api/quota/consume/$($bob.id)" "Consume unit $i / 5"
    $r = Invoke-Api POST "/api/quota/consume/$($bob.id)"
    Assert-Status $r 200 "Consume $i returns 200 OK"
    $msg = (Get-JsonBody $r).message
    Write-Host "    → $msg" -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# SCENARIO 3 — Quota: enforce limit (429)
# ---------------------------------------------------------------------------

Write-Header "SCENARIO 3 · Quota — enforce limit (429)"

Write-Step "POST" "/api/quota/consume/$($bob.id)" "Consume unit 6 — over limit"
$r = Invoke-Api POST "/api/quota/consume/$($bob.id)"
Assert-Status $r 429 "6th consume returns 429 Too Many Requests"
$msg = (Get-JsonBody $r).message
Write-Host "    → $msg" -ForegroundColor DarkGray

Write-Step "POST" "/api/quota/consume/$($bob.id)" "Consume unit 7 — still blocked"
$r = Invoke-Api POST "/api/quota/consume/$($bob.id)"
Assert-Status $r 429 "7th consume returns 429 Too Many Requests"

# ---------------------------------------------------------------------------
# SCENARIO 4 — Quota: independent counters per user
# ---------------------------------------------------------------------------

Write-Header "SCENARIO 4 · Quota — independent counters per user"

Write-Step "POST" "/api/users" "Create a second user (Carol) — fresh quota counter"
$r = Invoke-Api POST "/api/users" @{ name = "Carol"; email = "carol_quota@example.com" }
Assert-Status $r 201 "Create Carol returns 201 Created"
$carol = Get-JsonBody $r
Write-Host "    → Carol Id: $($carol.id)" -ForegroundColor DarkGray

Write-Step "POST" "/api/quota/consume/$($carol.id)" "Carol consumes her first unit (Bob's limit must not affect her)"
$r = Invoke-Api POST "/api/quota/consume/$($carol.id)"
Assert-Status $r 200 "Carol's first consume returns 200 OK (independent counter)"

# ---------------------------------------------------------------------------
# SCENARIO 5 — Edge cases
# ---------------------------------------------------------------------------

Write-Header "SCENARIO 5 · Edge cases"

Write-Step "POST" "/api/users" "Create user with minimal fields"
$r = Invoke-Api POST "/api/users" @{ name = "X"; email = "x@x.com" }
Assert-Status $r 201 "Minimal user returns 201 Created"

Write-Step "GET" "/api/quota" "Quota list includes all created users"
$r = Invoke-Api GET "/api/quota"
Assert-Status $r 200 "GET /api/quota returns 200 OK"
$quotas = Get-JsonBody $r
Write-Host "    → Total quota records: $($quotas.Count)" -ForegroundColor DarkGray

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkCyan
Write-Host "  RESULTS" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkCyan
Write-Host ""
Write-Host "  Total : $total" -ForegroundColor White
Write-Host "  Passed: $pass" -ForegroundColor Green
if ($fail -gt 0) {
    Write-Host "  Failed: $fail" -ForegroundColor Red
    Write-Host ""
    exit 1
} else {
    Write-Host "  Failed: $fail" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  All assertions passed." -ForegroundColor Green
    Write-Host ""
}
