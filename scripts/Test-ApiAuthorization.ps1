param(
    [string]$BaseUrl = "https://localhost:7040",
    [string]$DemoPassword = "DemoPass123!"
)

$ErrorActionPreference = "Stop"
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.ServerCertificateCustomValidationCallback = { $true }
$apiClient = [System.Net.Http.HttpClient]::new($handler)
$apiClient.Timeout = [TimeSpan]::FromSeconds(8)

function Invoke-ApiStatus {
    param([string]$Method, [string]$Path, [string]$Token = "")

    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), "$BaseUrl$Path")
    if ($Token) { $null = $request.Headers.TryAddWithoutValidation("Authorization", "Bearer $Token") }
    if ($Method -in @("POST", "PUT", "PATCH")) {
        $request.Content = [System.Net.Http.StringContent]::new("", [System.Text.Encoding]::UTF8, "application/json")
    }
    try {
        $response = $apiClient.SendAsync($request).GetAwaiter().GetResult()
        return [int]$response.StatusCode
    }
    catch {
        return -1
    }
    finally { $request.Dispose(); if ($response) { $response.Dispose() } }
}

function Get-DemoToken {
    param([string]$Email)

    $body = @{ email = $Email; password = $DemoPassword } | ConvertTo-Json
    $response = Invoke-RestMethod -SkipCertificateCheck -Method Post -Uri "$BaseUrl/api/v1/auth/login" `
        -ContentType "application/json" -Body $body
    return $response.AccessToken
}

function New-Route {
    param([string]$Method, [string]$Path)
    return @{ Method = $Method; Path = $Path }
}

$tokens = @{
    Admin = Get-DemoToken "admin@demo.uniconnecthub.local"
    User = Get-DemoToken "student@demo.uniconnecthub.local"
}
$moderatorToken = Get-DemoToken "alumni@demo.uniconnecthub.local"
$adminProfile = Invoke-RestMethod -SkipCertificateCheck -Uri "$BaseUrl/api/v1/profiles/me" `
    -Headers @{ Authorization = "Bearer $($tokens.Admin)" }
$moderatorProfile = Invoke-RestMethod -SkipCertificateCheck -Uri "$BaseUrl/api/v1/profiles/me" `
    -Headers @{ Authorization = "Bearer $moderatorToken" }

# Promote the alumni demo account using Admin authorization, then obtain a fresh JWT with its Moderator role claim.
$null = Invoke-RestMethod -SkipCertificateCheck -Method Post `
    -Uri "$BaseUrl/api/v1/moderation/users/$($moderatorProfile.UserId)/role" `
    -Headers @{ Authorization = "Bearer $($tokens.Admin)" } -ContentType "application/json" -Body "2"
$tokens.Moderator = Get-DemoToken "alumni@demo.uniconnecthub.local"

$communityResult = Invoke-RestMethod -SkipCertificateCheck -Uri "$BaseUrl/api/v1/communities?pageSize=1" `
    -Headers @{ Authorization = "Bearer $($tokens.Admin)" }
$postResult = Invoke-RestMethod -SkipCertificateCheck -Uri "$BaseUrl/api/v1/posts?pageSize=1" `
    -Headers @{ Authorization = "Bearer $($tokens.Admin)" }
$eventResult = Invoke-RestMethod -SkipCertificateCheck -Uri "$BaseUrl/api/v1/events?pageSize=1" `
    -Headers @{ Authorization = "Bearer $($tokens.Admin)" }
$opportunityResult = Invoke-RestMethod -SkipCertificateCheck -Uri "$BaseUrl/api/v1/opportunities?pageSize=1" `
    -Headers @{ Authorization = "Bearer $($tokens.Admin)" }
$reportResult = Invoke-RestMethod -SkipCertificateCheck -Uri "$BaseUrl/api/v1/moderation/reports?pageSize=1" `
    -Headers @{ Authorization = "Bearer $($tokens.Admin)" }

$communityId = if ($communityResult.Items.Count) { $communityResult.Items[0].Id } else { [guid]::NewGuid() }
$postId = if ($postResult.Items.Count) { $postResult.Items[0].Id } else { [guid]::NewGuid() }
$eventId = if ($eventResult.Items.Count) { $eventResult.Items[0].Id } else { [guid]::NewGuid() }
$opportunityId = if ($opportunityResult.Items.Count) { $opportunityResult.Items[0].Id } else { [guid]::NewGuid() }
$reportId = if ($reportResult.Items.Count) { $reportResult.Items[0].Id } else { [guid]::NewGuid() }
$unknownId = [guid]::NewGuid()

$publicGetRoutes = @(
    (New-Route GET "/api/v1"),
    (New-Route GET "/api/v1/system/info"),
    (New-Route GET "/api/v1/universities"),
    (New-Route GET "/api/v1/universities/$($adminProfile.UniversityId)"),
    (New-Route GET "/health"),
    (New-Route GET "/health/ready"),
    (New-Route GET "/swagger/v1/swagger.json")
)

$publicAuthRoutes = @(
    (New-Route POST "/api/v1/auth/register"),
    (New-Route POST "/api/v1/auth/login"),
    (New-Route POST "/api/v1/auth/refresh"),
    (New-Route POST "/api/v1/auth/verify-email"),
    (New-Route POST "/api/v1/auth/resend-verification"),
    (New-Route POST "/api/v1/auth/forgot-password"),
    (New-Route POST "/api/v1/auth/reset-password")
)

$protectedRoutes = @(
    (New-Route POST "/api/v1/auth/logout"),
    (New-Route GET "/api/v1/profiles/me"),
    (New-Route PUT "/api/v1/profiles/me"),
    (New-Route GET "/api/v1/profiles/$($adminProfile.UserId)"),
    (New-Route PUT "/api/v1/profiles/me/skills"),
    (New-Route PUT "/api/v1/profiles/me/interests"),
    (New-Route GET "/api/v1/discovery/people"),
    (New-Route POST "/api/v1/connections/requests"),
    (New-Route GET "/api/v1/connections/requests/incoming"),
    (New-Route GET "/api/v1/connections/requests/outgoing"),
    (New-Route POST "/api/v1/connections/requests/$unknownId/accept"),
    (New-Route POST "/api/v1/connections/requests/$unknownId/decline"),
    (New-Route GET "/api/v1/connections"),
    (New-Route DELETE "/api/v1/connections/$unknownId"),
    (New-Route POST "/api/v1/connections/block/$unknownId"),
    (New-Route DELETE "/api/v1/connections/block/$unknownId"),
    (New-Route GET "/api/v1/connections/blocked"),
    (New-Route POST "/api/v1/communities"),
    (New-Route GET "/api/v1/communities"),
    (New-Route GET "/api/v1/communities/$communityId"),
    (New-Route PUT "/api/v1/communities/$communityId"),
    (New-Route DELETE "/api/v1/communities/$unknownId"),
    (New-Route POST "/api/v1/communities/$unknownId/join"),
    (New-Route POST "/api/v1/communities/$unknownId/leave"),
    (New-Route GET "/api/v1/communities/$communityId/members"),
    (New-Route POST "/api/v1/posts"),
    (New-Route GET "/api/v1/posts"),
    (New-Route GET "/api/v1/posts/$postId"),
    (New-Route PUT "/api/v1/posts/$postId"),
    (New-Route DELETE "/api/v1/posts/$unknownId"),
    (New-Route POST "/api/v1/posts/$postId/comments"),
    (New-Route POST "/api/v1/posts/$unknownId/reactions"),
    (New-Route DELETE "/api/v1/posts/$unknownId/reactions"),
    (New-Route POST "/api/v1/posts/$unknownId/bookmark"),
    (New-Route DELETE "/api/v1/posts/$unknownId/bookmark"),
    (New-Route GET "/api/v1/posts/bookmarks"),
    (New-Route PUT "/api/v1/comments/$unknownId"),
    (New-Route DELETE "/api/v1/comments/$unknownId"),
    (New-Route GET "/api/v1/feed"),
    (New-Route POST "/api/v1/events"),
    (New-Route GET "/api/v1/events"),
    (New-Route GET "/api/v1/events/$eventId"),
    (New-Route PUT "/api/v1/events/$eventId"),
    (New-Route POST "/api/v1/events/$unknownId/register"),
    (New-Route DELETE "/api/v1/events/$unknownId/register"),
    (New-Route POST "/api/v1/events/$unknownId/cancel"),
    (New-Route GET "/api/v1/events/$eventId/attendees"),
    (New-Route PUT "/api/v1/mentoring/profile"),
    (New-Route GET "/api/v1/mentoring/mentors"),
    (New-Route GET "/api/v1/mentoring/mentors/$($moderatorProfile.UserId)"),
    (New-Route POST "/api/v1/mentoring/requests"),
    (New-Route GET "/api/v1/mentoring/requests"),
    (New-Route POST "/api/v1/mentoring/requests/$unknownId/accept"),
    (New-Route POST "/api/v1/mentoring/requests/$unknownId/decline"),
    (New-Route GET "/api/v1/mentoring/relationships"),
    (New-Route POST "/api/v1/mentoring/$unknownId/complete"),
    (New-Route POST "/api/v1/mentoring/$unknownId/cancel"),
    (New-Route POST "/api/v1/opportunities"),
    (New-Route GET "/api/v1/opportunities"),
    (New-Route GET "/api/v1/opportunities/$opportunityId"),
    (New-Route PUT "/api/v1/opportunities/$opportunityId"),
    (New-Route DELETE "/api/v1/opportunities/$unknownId"),
    (New-Route POST "/api/v1/opportunities/$unknownId/save"),
    (New-Route DELETE "/api/v1/opportunities/$unknownId/save"),
    (New-Route GET "/api/v1/opportunities/saved"),
    (New-Route GET "/api/v1/notifications"),
    (New-Route POST "/api/v1/notifications/$unknownId/read"),
    (New-Route POST "/api/v1/notifications/read-all"),
    (New-Route POST "/api/v1/reports"),
    (New-Route GET "/api/v1/moderation/reports"),
    (New-Route GET "/api/v1/moderation/users?pageSize=1"),
    (New-Route GET "/api/v1/moderation/reports/$reportId"),
    (New-Route POST "/api/v1/moderation/reports/$reportId/resolve"),
    (New-Route POST "/api/v1/moderation/content/$unknownId/hide?type=post"),
    (New-Route POST "/api/v1/moderation/users/$unknownId/suspend"),
    (New-Route POST "/api/v1/moderation/users/$unknownId/role")
)

$failures = [System.Collections.Generic.List[string]]::new()
$roleProbeCount = @{ User = 0; Moderator = 0; Admin = 0 }
foreach ($route in $publicGetRoutes) {
    $status = Invoke-ApiStatus $route.Method $route.Path
    if ($status -ne 200) { $failures.Add("Public $($route.Method) $($route.Path): expected 200, got $status") }
}
Write-Host "Checked $($publicGetRoutes.Count) public GET routes."
foreach ($route in $publicAuthRoutes) {
    $status = Invoke-ApiStatus $route.Method $route.Path
    if ($status -ne 400) { $failures.Add("Public auth $($route.Method) $($route.Path): expected body-validation 400, got $status") }
}
Write-Host "Checked $($publicAuthRoutes.Count) public auth routes without request bodies."
foreach ($route in $protectedRoutes) {
    $anonymousStatus = Invoke-ApiStatus $route.Method $route.Path
    if ($anonymousStatus -ne 401) { $failures.Add("Anonymous $($route.Method) $($route.Path): expected 401, got $anonymousStatus") }

    foreach ($role in @("User", "Moderator", "Admin")) {
        $status = Invoke-ApiStatus $route.Method $route.Path $tokens[$role]
        $roleProbeCount[$role]++
        $adminOnlyRoute = $route.Path -match '^/api/v1/moderation/users(?:$|[/?])'
        $moderatorRoute = $route.Path -match '^/api/v1/moderation/(?:reports(?:/|$)|content/)'
        if ($adminOnlyRoute -and $role -ne "Admin" -and $status -ne 403) {
            $failures.Add("$role $($route.Method) $($route.Path): expected Admin-only 403, got $status")
        }
        elseif ($moderatorRoute -and $role -eq "User" -and $status -ne 403) {
            $failures.Add("$role $($route.Method) $($route.Path): expected Moderator-only 403, got $status")
        }
        elseif ($moderatorRoute -and $role -ne "User" -and $status -eq 403) {
            $failures.Add("$role $($route.Method) $($route.Path): moderator/admin should be allowed, got 403")
        }
        elseif ($status -eq 401) {
            $failures.Add("$role $($route.Method) $($route.Path): valid token was rejected with 401")
        }
    }
}
Write-Host "Checked $($protectedRoutes.Count) protected routes anonymously and under all three roles."

"Protected routes checked: $($protectedRoutes.Count)"
"Public GET routes checked: $($publicGetRoutes.Count)"
"Public auth routes checked: $($publicAuthRoutes.Count)"
"Role probes: User=$($roleProbeCount.User), Moderator=$($roleProbeCount.Moderator), Admin=$($roleProbeCount.Admin)"
if ($failures.Count -eq 0) {
    "RESULT: PASS - no authorization failures found. Resource-level outcomes such as 400, 403, 404, and 409 are retained by the API."
}
else {
    "RESULT: FAIL - $($failures.Count) checks need review."
    $failures
    exit 1
}
$apiClient.Dispose()
$handler.Dispose()
