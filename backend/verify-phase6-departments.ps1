# ============================================================
# PHASE 6 — Department Management API Full Verification
# ============================================================

$base = "http://localhost:5000"

# ---- Helper to test expected HTTP status ----
function Test-Endpoint {
    param($label, $expected, $actual, $body = $null)
    $status = if ($actual -eq $expected) { "PASS" } else { "FAIL" }
    Write-Host "[$status] $label (expected $expected, got $actual)"
    if ($body) { Write-Host "       $body" }
}

# ============================================================
# 1 — SETUP: Register TWO businesses (tenant isolation test)
# ============================================================

$ts = [DateTime]::UtcNow.ToString("yyyyMMddHHmmssfff")

$b1 = Invoke-RestMethod -Uri "$base/api/auth/staff/register" -Method Post `
    -ContentType "application/json; charset=utf-8" `
    -Body (@{
        email="p6-biz1-$ts@test.local"; password="password123"
        fullName="Biz One Owner"; businessName="Hotel Alpha"
        businessType="hotel"; timezone="UTC"
    } | ConvertTo-Json)

$b2 = Invoke-RestMethod -Uri "$base/api/auth/staff/register" -Method Post `
    -ContentType "application/json; charset=utf-8" `
    -Body (@{
        email="p6-biz2-$ts@test.local"; password="password123"
        fullName="Biz Two Owner"; businessName="Hotel Beta"
        businessType="hotel"; timezone="UTC"
    } | ConvertTo-Json)

$h1 = @{ Authorization = "Bearer $($b1.accessToken)" }
$h2 = @{ Authorization = "Bearer $($b2.accessToken)" }

Write-Host ""
Write-Host "=== BUSINESS 1: $($b1.businessId) ==="
Write-Host "=== BUSINESS 2: $($b2.businessId) ==="
Write-Host ""

# ============================================================
# 2 — SECURITY: No token should return 401
# ============================================================

Write-Host "--- SECURITY ---"

try {
    Invoke-RestMethod -Uri "$base/api/departments"
    Test-Endpoint "No token -> 401" 401 200
} catch {
    $code = [int]$_.Exception.Response.StatusCode
    Test-Endpoint "No token -> 401" 401 $code
}

try {
    Invoke-RestMethod -Uri "$base/api/departments" `
        -Headers @{ Authorization = "Bearer invalidtoken.abc.def" }
    Test-Endpoint "Bad token -> 401" 401 200
} catch {
    $code = [int]$_.Exception.Response.StatusCode
    Test-Endpoint "Bad token -> 401" 401 $code
}

Write-Host ""

# ============================================================
# 3 — LIST: Default departments created at registration
# ============================================================

Write-Host "--- LIST (default departments from registration) ---"

$list1 = Invoke-RestMethod -Uri "$base/api/departments" -Headers $h1
Write-Host "Business 1 departments ($($list1.totalCount)):"
$list1.items | ForEach-Object { Write-Host "  - $($_.name) [sortOrder=$($_.sortOrder)]" }

# ============================================================
# 4 — CREATE: Valid department
# ============================================================

Write-Host ""
Write-Host "--- CREATE ---"

$dept = Invoke-RestMethod -Uri "$base/api/departments" -Method Post `
    -Headers $h1 -ContentType "application/json" `
    -Body (@{
        name = "Room Service"
        description = "Handles in-room dining requests"
        icon = "room_service"
        handlesCategories = @("food", "beverage")
        slaMinutes = 30
    } | ConvertTo-Json)

Test-Endpoint "Create -> has id" "truthy" ($null -ne $dept.id) $dept.id
Test-Endpoint "Create -> name correct" "Room Service" $dept.name
Test-Endpoint "Create -> slaMinutes" 30 $dept.slaMinutes
Test-Endpoint "Create -> activeIssueCount is 0" 0 $dept.activeIssueCount
Write-Host "Created department id: $($dept.id)"

# ============================================================
# 5 — CREATE: Validation — empty name should return 400
# ============================================================

Write-Host ""
Write-Host "--- VALIDATION ---"

try {
    Invoke-RestMethod -Uri "$base/api/departments" -Method Post `
        -Headers $h1 -ContentType "application/json" `
        -Body '{"name":"","description":"No name dept"}'
    Test-Endpoint "Empty name -> 400" 400 200
} catch {
    $code = [int]$_.Exception.Response.StatusCode
    Test-Endpoint "Empty name -> 400" 400 $code
}

try {
    Invoke-RestMethod -Uri "$base/api/departments" -Method Post `
        -Headers $h1 -ContentType "application/json" `
        -Body '{"name":"   ","description":"Whitespace name"}'
    Test-Endpoint "Whitespace name -> 400" 400 200
} catch {
    $code = [int]$_.Exception.Response.StatusCode
    Test-Endpoint "Whitespace name -> 400" 400 $code
}

# ============================================================
# 6 — DETAIL: Get the created department
# ============================================================

Write-Host ""
Write-Host "--- DETAIL ---"

$detail = Invoke-RestMethod `
    -Uri "$base/api/departments/$($dept.id)" -Headers $h1
Test-Endpoint "Detail -> correct id" $dept.id $detail.id
Test-Endpoint "Detail -> name" "Room Service" $detail.name
Test-Endpoint "Detail -> slaMinutes" 30 $detail.slaMinutes

# ============================================================
# 7 — DETAIL: Unknown ID should return 404
# ============================================================

try {
    Invoke-RestMethod `
        -Uri "$base/api/departments/00000000-0000-0000-0000-000000000000" `
        -Headers $h1
    Test-Endpoint "Unknown id -> 404" 404 200
} catch {
    $code = [int]$_.Exception.Response.StatusCode
    Test-Endpoint "Unknown id -> 404" 404 $code
}

# ============================================================
# 8 — UPDATE
# ============================================================

Write-Host ""
Write-Host "--- UPDATE ---"

$updated = Invoke-RestMethod `
    -Uri "$base/api/departments/$($dept.id)" -Method Put `
    -Headers $h1 -ContentType "application/json" `
    -Body (@{
        name = "Room Service Updated"
        description = "Now also handles minibar"
        icon = "room_service"
        handlesCategories = @("food", "beverage", "minibar")
        slaMinutes = 20
        sortOrder = $dept.sortOrder
        isActive = $true
    } | ConvertTo-Json)

Test-Endpoint "Update -> name changed" "Room Service Updated" $updated.name
Test-Endpoint "Update -> slaMinutes changed" 20 $updated.slaMinutes
Test-Endpoint "Update -> updatedAt advanced" $true ($updated.updatedAt -ne $dept.updatedAt)

# ============================================================
# 9 — TENANT ISOLATION: Business 2 cannot see Business 1 dept
# ============================================================

Write-Host ""
Write-Host "--- TENANT ISOLATION ---"

try {
    $crossTenant = Invoke-RestMethod `
        -Uri "$base/api/departments/$($dept.id)" -Headers $h2
    Test-Endpoint "Cross-tenant detail -> 404" 404 200
} catch {
    $code = [int]$_.Exception.Response.StatusCode
    Test-Endpoint "Cross-tenant detail -> 404" 404 $code
}

try {
    Invoke-RestMethod -Uri "$base/api/departments/$($dept.id)" `
        -Method Delete -Headers $h2
    Test-Endpoint "Cross-tenant delete -> 404" 404 200
} catch {
    $code = [int]$_.Exception.Response.StatusCode
    Test-Endpoint "Cross-tenant delete -> 404" 404 $code
}

try {
    Invoke-RestMethod -Uri "$base/api/departments/$($dept.id)" `
        -Method Put -Headers $h2 -ContentType "application/json" `
        -Body '{"name":"Hacked","description":"","sortOrder":1,"isActive":true}'
    Test-Endpoint "Cross-tenant update -> 404" 404 200
} catch {
    $code = [int]$_.Exception.Response.StatusCode
    Test-Endpoint "Cross-tenant update -> 404" 404 $code
}

$list2 = Invoke-RestMethod -Uri "$base/api/departments" -Headers $h2
$crossVisible = $list2.items | Where-Object { $_.id -eq $dept.id }
Test-Endpoint "Cross-tenant list isolation" `
    $true ($null -eq $crossVisible)

# ============================================================
# 10 — DELETE: Should succeed (no active issues)
# ============================================================

Write-Host ""
Write-Host "--- DELETE (deactivation) ---"

try {
    Invoke-RestMethod -Uri "$base/api/departments/$($dept.id)" `
        -Method Delete -Headers $h1
    Test-Endpoint "Delete -> 204" 204 204
} catch {
    $code = [int]$_.Exception.Response.StatusCode
    Test-Endpoint "Delete -> 204" 204 $code
}

# ============================================================
# 11 — POST DELETE: Deactivated dept gone from list
# ============================================================

$listAfter = Invoke-RestMethod -Uri "$base/api/departments" -Headers $h1
$stillThere = $listAfter.items | Where-Object { $_.id -eq $dept.id }
Test-Endpoint "Deactivated dept hidden from list" `
    $true ($null -eq $stillThere)

# ============================================================
# 12 — DELETE: Already deactivated -> 404
# ============================================================

try {
    Invoke-RestMethod -Uri "$base/api/departments/$($dept.id)" `
        -Method Delete -Headers $h1
    Test-Endpoint "Re-delete -> 404" 404 200
} catch {
    $code = [int]$_.Exception.Response.StatusCode
    Test-Endpoint "Re-delete -> 404" 404 $code
}

# ============================================================
# 13 — DELETE: Dept with active issues -> 409
# ============================================================

Write-Host ""
Write-Host "--- DELETE SAFETY (active issues block) ---"

# Create a dept, create a room, submit feedback to generate an issue,
# then try to delete the dept — expect 409

$safeDept = Invoke-RestMethod -Uri "$base/api/departments" -Method Post `
    -Headers $h1 -ContentType "application/json" `
    -Body '{"name":"Safety Test Dept","description":"Will have issues"}'

# Create a room and assign it to get a session
$room = Invoke-RestMethod -Uri "$base/api/locations" -Method Post `
    -Headers $h1 -ContentType "application/json" `
    -Body '{"name":"Safety Room","label":"Standard","type":"room"}'

# Scan QR -> get session cookie
$session = Invoke-WebRequest `
    -Uri "$base/r/$($room.shortCode)" -SessionVariable sv `
    -UseBasicParsing -TimeoutSec 120
$sessionCookies = $sv

# Submit negative feedback (rating 2 -> creates issue)
$feedbackBody = @{
    rating = 2
    comment = "Active issue test for dept delete safety check"
    website = ""
} | ConvertTo-Json

$feedbackResp = Invoke-RestMethod -Uri "$base/api/feedback" `
    -Method Post -WebSession $sv -ContentType "application/json" `
    -Body $feedbackBody

Write-Host "Feedback submitted, issueId: $($feedbackResp.issueId)"

# Assign the issue to safeDept
if ($feedbackResp.issueId) {
    Invoke-RestMethod `
        -Uri "$base/api/issues/$($feedbackResp.issueId)/assign" `
        -Method Patch -Headers $h1 -ContentType "application/json" `
        -Body (@{ departmentId = $safeDept.id } | ConvertTo-Json)

    # Now try to delete — should get 409
    try {
        Invoke-RestMethod -Uri "$base/api/departments/$($safeDept.id)" `
            -Method Delete -Headers $h1
        Test-Endpoint "Delete with active issues -> 409" 409 200
    } catch {
        $code = [int]$_.Exception.Response.StatusCode
        Test-Endpoint "Delete with active issues -> 409" 409 $code
    }

    # Resolve the issue then delete should succeed
    Invoke-RestMethod `
        -Uri "$base/api/issues/$($feedbackResp.issueId)/resolve" `
        -Method Post -Headers $h1
    
    try {
        Invoke-RestMethod -Uri "$base/api/departments/$($safeDept.id)" `
            -Method Delete -Headers $h1
        Test-Endpoint "Delete after resolve -> 204" 204 204
    } catch {
        $code = [int]$_.Exception.Response.StatusCode
        Test-Endpoint "Delete after resolve -> 204" 204 $code
    }
} else {
    Write-Host "[SKIP] No issueId returned - check feedback routing"
}

# ============================================================
# SUMMARY
# ============================================================

Write-Host ""
Write-Host "=== Verification complete. All PASS = Phase 6 ready to commit ==="
