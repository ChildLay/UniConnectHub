# UniConnect Hub API v1

This guide documents the HTTP routes currently implemented by the API and gives a role-based test plan. Swagger is available in Development at `https://localhost:7040/swagger`; its OpenAPI document is `/swagger/v1/swagger.json`.

## Before testing

Start SQL Server and the API in the `Development` environment. Development startup creates the database if absent and runs the repeatable development data seeder. The default SQL Server is `localhost`; change `ConnectionStrings:DefaultConnection` if SSMS uses a named instance.

Seeded accounts (local development only):

| Account | Email | Password | API role |
| --- | --- | --- | --- |
| Demo administrator | `admin@demo.uniconnecthub.local` | `DemoPass123!` | `Admin` |
| Demo student | `student@demo.uniconnecthub.local` | `DemoPass123!` | `User` |
| Demo alumnus | `alumni@demo.uniconnecthub.local` | `DemoPass123!` | `User` |

Get an access token without a frontend:

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "student@demo.uniconnecthub.local",
  "password": "DemoPass123!"
}
```

Copy `accessToken` from the response. Send it as `Authorization: Bearer <accessToken>` on protected requests. In Swagger, click **Authorize** and paste the access token. Do not send the refresh token as a bearer token. Access tokens last 15 minutes; use `POST /api/v1/auth/refresh` to rotate the refresh token and get another access token.

PowerShell example:

```powershell
$base = "https://localhost:7040"
$login = Invoke-RestMethod -Method Post -Uri "$base/api/v1/auth/login" `
  -ContentType "application/json" `
  -Body (@{ email = "student@demo.uniconnecthub.local"; password = "DemoPass123!" } | ConvertTo-Json)
$token = $login.accessToken
Invoke-RestMethod -Uri "$base/api/v1/profiles/me" `
  -Headers @{ Authorization = "Bearer $token" }
```

## Role model and authorization checks

`UserRole` values in JWTs are `User`, `Moderator`, and `Admin`. `JourneyStage` (`NewStudent`, `CurrentStudent`, `Graduate`, `Alumni`) is profile/user data, not an authorization role. Community membership roles (`Member`, `Moderator`, `Admin`) are separate from API roles.

| Caller | Expected access |
| --- | --- |
| No token | Public routes work. Every route marked **Bearer** below returns `401 Unauthorized`. |
| `User` | Bearer routes are available, subject to ownership, privacy, university, relationship, and membership rules. Moderation routes return `403 Forbidden`. |
| `Moderator` API role | Can view and manage reports and hide content. Only Admin can list accounts, suspend users, or change roles. |
| `Admin` | Bearer routes plus all moderation and user-access management actions. Resource ownership rules can still apply to ordinary feature routes. |

The first Admin is seeded for Development. Do not expose the demo credentials outside a local environment. A `403` means the token was accepted but lacks authorization; a `401` means the request did not authenticate (missing, expired, malformed, wrong issuer/audience, or wrong signing key).

## Endpoint reference

All routes below are under `/api/v1`. Unless otherwise stated, request and response bodies use JSON. Paginated results have `items`, `pageNumber`, `pageSize`, `totalCount`, and `totalPages`. Page size is capped at 100.

### Authentication — `/auth`

| Method and path | Auth | Request / result |
| --- | --- | --- |
| `POST /auth/register` | Public | `RegisterRequest`: `email`, `password`, `firstName`, `lastName`, `universityId`, `journeyStage`. Returns `201`. Password needs at least 12 characters and upper/lower/numeric/special characters. |
| `POST /auth/login` | Public | `LoginRequest`: `email`, `password`. Returns `200` with `accessToken`, `refreshToken`, `accessTokenExpiresAtUtc`. |
| `POST /auth/refresh` | Public | `{ "refreshToken": "..." }`. Rotates refresh token; returns a new token pair. |
| `POST /auth/logout` | Bearer | `{ "refreshToken": "..." }`. Revokes the caller-owned refresh token; returns `204`. |
| `POST /auth/verify-email` | Public | `{ "token": "..." }`. Returns `204`. |
| `POST /auth/resend-verification` | Public | `{ "email": "..." }`. Returns `202` without account enumeration. |
| `POST /auth/forgot-password` | Public | `{ "email": "..." }`. Returns `202` without account enumeration. |
| `POST /auth/reset-password` | Public | `{ "token": "...", "newPassword": "..." }`. Returns `204`. |

Sensitive auth routes have a per-IP rate limit. In Development, one-time verification/reset tokens are logged by the console email adapter. Email is not sent externally by this adapter.

### Public lookup and system

| Method and path | Auth | Query / result |
| --- | --- | --- |
| `GET /` | Public | API version/module overview. |
| `GET /system/info` | Public | Basic application/version/status information. |
| `GET /universities` | Public | Optional `search`, `pageNumber`, `pageSize`. |
| `GET /universities/{id}` | Public | University by GUID. |
| `GET /health` | Public | Process health. |
| `GET /health/ready` | Public | Database readiness; returns unhealthy when the DB is unreachable. |

### Profiles — `/profiles` (Bearer)

| Method and path | Request / result |
| --- | --- |
| `GET /profiles/me` | Current caller's profile, including `profileImageUrl` and `coverImageUrl`. |
| `PUT /profiles/me` | Update profile: `headline`, `bio`, `programme`, `studyArea`, `graduationYear`, `industryExperience`, `careerGoals`, `volunteeringInterests`, `hobbies`, `languages`, `profileImageUrl`, `visibility`, `allowConnectionRequests`. |
| `POST /profiles/me/media/profile` | Authenticated multipart upload with form field `file`; JPEG, PNG or WebP, up to 5 MB. Returns updated profile with `profileImageUrl`. |
| `DELETE /profiles/me/media/profile` | Remove own profile photo; returns updated profile. |
| `POST /profiles/me/media/cover` | Authenticated multipart upload with form field `file`; JPEG, PNG or WebP, up to 5 MB. Returns updated profile with `coverImageUrl`. |
| `DELETE /profiles/me/media/cover` | Remove own cover photo; returns updated profile. |
| `GET /profiles/{userId}/media/{kind}/{fileName}` | Authenticated image response. Profile visibility and block rules apply. |
| `GET /profiles/{userId}` | Public-to-authorized-viewer profile; visibility, block, and connection rules apply. |
| `PUT /profiles/me/skills` | `{ "values": ["C#", "SQL Server"] }`. |
| `PUT /profiles/me/interests` | `{ "values": ["Mentoring", "Open source"] }`. |

Visibility values: `UniversityCommunity`, `ConnectionsOnly`, `Private`.

### Discovery — `/discovery` (Bearer)

| Method and path | Query parameters |
| --- | --- |
| `GET /discovery/people` | Optional `universityId`, `programme`, `studyArea`, `skill`, `interest`, `industry`, `careerGoal`, `journeyStage`, `pageNumber`, `pageSize`. Results respect profile visibility, blocks, and connections, and include the visible member's `profileImageUrl`. Matching is rule-based; `MatchScore` counts shared skills/interests. |

### Connections — `/connections` (Bearer)

| Method and path | Request / result |
| --- | --- |
| `POST /connections/requests` | `{ "recipientId": "<user-guid>" }`; creates a request. |
| `GET /connections/requests/incoming` | `pageNumber`, `pageSize`. |
| `GET /connections/requests/outgoing` | `pageNumber`, `pageSize`. |
| `DELETE /connections/requests/{id}` | Sender withdraws their own pending request; `204`. |
| `POST /connections/requests/{id}/accept` | Recipient accepts; `204`. |
| `POST /connections/requests/{id}/decline` | Recipient declines; `204`. |
| `GET /connections` | List caller's connections; `pageNumber`, `pageSize`. |
| `DELETE /connections/{userId}` | Remove a connection; `204`. |
| `POST /connections/block/{userId}` | Block user; `204`. |
| `DELETE /connections/block/{userId}` | Unblock user; `204`. |
| `GET /connections/blocked` | List blocked user IDs; `pageNumber`, `pageSize`. |

The recipient must allow connection requests. Blocking prevents requests and removes an existing connection/pending requests.

### Communities — `/communities` (Bearer)

| Method and path | Request / result |
| --- | --- |
| `POST /communities` | `{ "name": "AI Society", "description": "...", "universityId": "<guid>", "visibility": "Public" }`; creator becomes community Admin. |
| `GET /communities` | `pageNumber`, `pageSize`; lists visible communities at caller's university. |
| `GET /communities/{id}` | Community details if visible to caller. |
| `PUT /communities/{id}` | Same shape as create. Community admin only; university cannot be changed. |
| `DELETE /communities/{id}` | Deactivate community; community admin only; `204`. |
| `POST /communities/{id}/join` | Join visible non-private community; `204`. |
| `POST /communities/{id}/leave` | Leave community; owner/admin cannot leave first; `204`. |
| `GET /communities/{id}/members` | `pageNumber`, `pageSize`; returns member user IDs. |

Visibility values: `Public`, `UniversityOnly`, `Private`. A private community requires membership/invitation; invitation management is not currently exposed as an API route.

### Posts, comments, and feed (Bearer)

| Method and path | Request / result |
| --- | --- |
| `POST /posts` | `{ "content": "...", "audience": "UniversityCommunity", "communityId": null, "allowComments": true, "selectedUserIds": [] }`. |
| `GET /posts` | `pageNumber`, `pageSize`; returns visible posts, newest first. |
| `GET /posts/{id}` | Get post if caller can view it. |
| `PUT /posts/{id}` | `{ "content": "...", "allowComments": true }`; author only. |
| `DELETE /posts/{id}` | Soft-delete own post; `204`. |
| `GET /posts/{id}/comments` | `pageNumber`, `pageSize`; paginated comments newest first, only when caller can view the post. Hidden and deleted comments are omitted. |
| `POST /posts/{id}/comments` | `{ "content": "..." }`; adds comment if comments are enabled. |
| `PUT /comments/{id}` | `{ "content": "Edited comment" }`; comment author only. |
| `DELETE /comments/{id}` | Soft-delete own comment; `204`. |
| `POST /posts/{id}/reactions` | `{ "type": "Celebrate" }`; replaces caller's reaction. |
| `DELETE /posts/{id}/reactions` | Remove caller's reaction; `204`. |
| `POST /posts/{id}/bookmark` | Bookmark; `204`. |
| `DELETE /posts/{id}/bookmark` | Remove bookmark; `204`. |
| `GET /posts/bookmarks` | `pageNumber`, `pageSize`; visible bookmarks. |
| `GET /feed` | `pageNumber`, `pageSize`; personalized visible-post list. |

Post audience values: `UniversityCommunity`, `StudentsOnly`, `GraduatesAndAlumni`, `ConnectionsOnly`, `CommunityMembers`, `SelectedUsers`. For `CommunityMembers`, supply `communityId`; for `SelectedUsers`, supply recipient GUIDs. Content visibility also respects blocking and profile privacy.

### Events — `/events` (Bearer)

| Method and path | Request / result |
| --- | --- |
| `POST /events` | `{ "title": "Meetup", "description": "...", "communityId": null, "startUtc": "2026-12-01T18:00:00Z", "endUtc": "2026-12-01T20:00:00Z", "capacity": 40, "type": "Online" }`. |
| `GET /events` | Optional `fromUtc`, `toUtc`, `pageNumber`, `pageSize`. |
| `GET /events/{id}` | Get published event or own non-published event. |
| `PUT /events/{id}` | `{ "title": "...", "description": "...", "startUtc": "...Z", "endUtc": "...Z", "capacity": 40, "type": "Hybrid" }`; host only. |
| `POST /events/{id}/register` | Register caller; `204`. |
| `DELETE /events/{id}/register` | Unregister caller; `204`. |
| `POST /events/{id}/cancel` | Host cancels; `204`. |
| `GET /events/{id}/attendees` | `pageNumber`, `pageSize`; host only. |

Types: `Physical`, `Online`, `Hybrid`. Capacity and duplicate registration are checked.

### Mentoring — `/mentoring` (Bearer)

| Method and path | Request / result |
| --- | --- |
| `PUT /mentoring/profile` | `{ "bio": "...", "capacity": 3, "isAvailable": true }`; opt in/update mentor profile. |
| `GET /mentoring/mentors` | `pageNumber`, `pageSize`. |
| `GET /mentoring/mentors/{userId}` | Mentor profile by user ID. |
| `POST /mentoring/requests` | `{ "mentorId": "<guid>", "message": "..." }`. |
| `GET /mentoring/requests` | Caller-related requests; `pageNumber`, `pageSize`. |
| `POST /mentoring/requests/{id}/accept` | Mentor only; `204`. |
| `POST /mentoring/requests/{id}/decline` | Mentor only; `204`. |
| `GET /mentoring/relationships` | Accepted caller relationships; `pageNumber`, `pageSize`. |
| `POST /mentoring/{id}/complete` | A party to accepted relationship; `204`. |
| `POST /mentoring/{id}/cancel` | A party to relationship; `204`. |

### Opportunities — `/opportunities` (Bearer)

| Method and path | Request / result |
| --- | --- |
| `POST /opportunities` | `{ "title": "Graduate developer", "description": "...", "type": "GraduateRole", "closingDateUtc": "2026-12-31T00:00:00Z", "applicationUrl": "https://example.com/apply" }`. |
| `GET /opportunities` | Optional `type`, `pageNumber`, `pageSize`; active opportunities for caller's university. |
| `GET /opportunities/{id}` | Get active opportunity for caller's university. |
| `PUT /opportunities/{id}` | Same body as create; creator only. |
| `DELETE /opportunities/{id}` | Close own opportunity; `204`. |
| `POST /opportunities/{id}/save` | Save; `204`. |
| `DELETE /opportunities/{id}/save` | Unsave; `204`. |
| `GET /opportunities/saved` | `pageNumber`, `pageSize`. |

Types currently accepted: `Internship`, `GraduateRole`, `Job`, `Volunteering`, `Workshop`, `Networking`, `Scholarship`, `ProfessionalDevelopment`, `CommunityActivity`, `Competition`, `Other`.

### Notifications — `/notifications` (Bearer)

| Method and path | Request / result |
| --- | --- |
| `GET /notifications` | `pageNumber`, `pageSize`; only caller's notifications. |
| `POST /notifications/{id}/read` | Mark caller's notification read; `204`. |
| `POST /notifications/read-all` | Mark all caller notifications read; `204`. |

### Reports and moderation

| Method and path | Auth | Request / result |
| --- | --- | --- |
| `POST /reports` | Bearer | `{ "targetType": "Post", "targetId": "<guid>", "reason": "Spam", "details": "..." }`; target type is `User`, `Post`, `Comment`, or `Community`. |
| `GET /moderation/reports` | Admin, Moderator | Optional `status`, `pageNumber`, `pageSize`. |
| `GET /moderation/users` | Admin | Optional `search`, `active`, `pageNumber`, `pageSize`; returns account identity, journey stage, role, active state, and email verification state. |
| `GET /moderation/reports/{id}` | Admin, Moderator | Report details. |
| `POST /moderation/reports/{id}/resolve` | Admin, Moderator | `{ "status": "Resolved", "resolution": "Reviewed" }`; statuses accepted: `UnderReview`, `Resolved`, `Dismissed`. |
| `POST /moderation/content/{id}/hide?type=post` | Admin, Moderator | Hides `post`, `comment`, `community`, or `notification`; `204`. |
| `POST /moderation/users/{id}/suspend` | Admin | Deactivates the user; `204`. |
| `POST /moderation/users/{id}/role` | Admin | JSON enum body: `0`=`User`, `1`=`Admin`, `2`=`Moderator`; refresh tokens are revoked, so the user must sign in again after their current access token expires. |

Moderation writes audit records. `Moderator` can review and resolve reports and hide content. Only `Admin` can list managed accounts, suspend users, or change API roles. Role changes revoke the user's refresh tokens; an already-issued access token remains valid until it expires.

## Test cases by user level

Run these checks using separate browser/Swagger authorizations or API clients. Use the three seeded accounts above; a Moderator-role test account can be assigned by logging in as seeded Admin and calling the role endpoint with the user's ID and JSON body `2`.

| Test | No token | User token | Moderator token | Admin token |
| --- | --- | --- | --- | --- |
| `GET /api/v1/system/info` | `200` | `200` | `200` | `200` |
| `GET /api/v1/universities` | `200` if DB is available | `200` | `200` | `200` |
| `GET /api/v1/profiles/me` | `401` | `200` for active account | `200` for active account | `200` for active account |
| `GET /api/v1/communities` | `401` | Visible communities only | Visible communities only | Visible communities only |
| `GET /api/v1/discovery/people` | `401` | `200` with visible matches | `200` with visible matches | `200` with visible matches |
| `GET /api/v1/moderation/reports` | `401` | `403` | `200` if DB is available | `200` if DB is available |
| Edit another user's profile/post/community/opportunity | `401` | `403` or not found | `403` or not found | Still subject to owner/admin rules for that feature |
| Use expired/wrong-key/malformed JWT on protected route | `401` | `401` | `401` | `401` |

An automated route-level smoke script is available at `scripts/Test-ApiAuthorization.ps1`. Run it from the repository root while the Development API and database are responsive:

```powershell
.\scripts\Test-ApiAuthorization.ps1
```

The script logs in with the seeded demo accounts, assigns the alumni demo user the API `Moderator` role, and checks public routes, anonymous rejection, valid bearer acceptance, Moderator report access, and Admin-only user management. It sends empty request bodies or unknown resource IDs for write routes to avoid creating/editing seeded resources. The notifications `read-all` route does mark the student demo account's current notifications as read.

For broad coverage, call every path in **Endpoint reference** first with no token (expected `401` for Bearer/Admin routes), then with a User token (expected normal success/validation outcomes and `403` on restricted routes), then with a Moderator token for report and content-moderation routes, and an Admin token for all moderation routes. Repeat with another user's resource ID to confirm ownership checks. Community Admin membership can authorize community management independently of the API role. A successful bearer authentication does not bypass data-level privacy, ownership, or membership checks.

## Common HTTP results

| Status | Meaning |
| --- | --- |
| `200` | Read or update succeeded. |
| `201` | Resource created. |
| `202` | Auth request accepted, including non-existent account emails. |
| `204` | Action succeeded with no response body. |
| `400` | Invalid body, enum, filter, or parameters. |
| `401` | Missing or invalid JWT/credentials. |
| `403` | Authenticated caller lacks required role, ownership, membership, or visibility. |
| `404` | Resource not found or intentionally hidden from caller. |
| `409` | State conflict, duplicate action, or capacity/transition rule. |
| `429` | Sensitive authentication route rate-limited. |
| `500` | Unexpected server/database failure; see API logs. |

Errors use Problem Details. Never paste live access/refresh tokens or signing keys into documentation or source control.


