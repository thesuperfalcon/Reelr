# Security overview

This document describes Reelr's security mechanisms as implemented, and the risks observed while reviewing the code. It is a code review, not a penetration test: no destructive tests were run, and findings are labelled **Confirmed** (visible in the code) or **Needs verification** (depends on runtime behaviour, intent or environment that the code alone does not settle).

## Authentication

### Accounts

- ASP.NET Core Identity with `User : IdentityUser<int>` and EF Core stores (`backend/Program.cs`).
- Usernames: 3 to 50 characters from `a-z A-Z 0-9 - . _ +`. `@` is excluded so a username can never look like an email address, because login looks up the email first.
- Emails must be unique (`RequireUniqueEmail`) and confirmed before login succeeds.
- Passwords: Identity's default complexity rules (digit, lower case, upper case, non-alphanumeric); registration and reset DTOs require at least 8 characters.
- Lockout: 20 consecutive failures lock the account for 15 minutes, also for new users. Wrong passwords on account deletion and password change count towards the same lockout.
- `LoginThrottle` blocks one IP from trying one account after 5 failures in 15 minutes, and those attempts stop counting towards the lockout. This stops guessing from one address without letting an attacker lock out the owner easily (comment in `Program.cs`). A password reset clears the account's counts from every address, since the link proves the owner.

### Enumeration resistance

`backend/Features/Auth/AuthController.cs` deliberately gives the same answers whether or not an account exists:

- Register: password rules are checked before the duplicate-email check; a taken email returns the same 202 as a new account, hashes a password to keep timing similar, and emails the owner instead.
- Login: an unknown user takes as long as a wrong password (dummy hash); unknown names are throttled too; a locked account gets the same 401; "email not confirmed" is revealed only after a correct password.
- Resend confirmation and forgot password always return 202. Emails go through a background queue so timing does not leak either.

Usernames are public by design (profiles, search, register reports a taken username).

### Tokens

| Token | Form | Lifetime | Storage | Revocation |
|---|---|---|---|---|
| Access token | JWT, HMAC-SHA256, claims `sub`, `name`, `sstamp` (no email) | 15 min (`Jwt:ExpiryMinutes`), 30 s clock skew | Browser memory only (`frontend/src/lib/api.ts`) | Security stamp check on every request (`SecurityStamps`, 30 s cache, cleared immediately when this API changes the stamp) |
| Refresh token | 32 random bytes, Base64Url; only its SHA-256 hash is stored | 30 days (`Jwt:RefreshDays`) | Cookie `__Secure-reelr-refresh`: `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/auth` | Rotated on each use; reuse after a 20 s grace revokes the whole family; logout revokes the family; a security stamp change invalidates all |

Issuer, audience, lifetime and signing key are validated (`AddJwtBearer` in `Program.cs`). The key must be at least 32 bytes or the app will not start.

Security stamp changes happen on password change and reset, rename, "log out everywhere" and account deletion. Each ends every access token (within 30 seconds at most for any cached value) and every refresh token.

### CSRF

- API calls authenticate with a bearer header, which browsers do not attach automatically, so they are not CSRF-prone.
- The two cookie-based endpoints (`POST /auth/refresh`, `POST /auth/logout`) need `SameSite=Strict` to send the cookie at all and also require the custom `X-Reelr-Csrf` header (`RequireCsrfHeaderAttribute`). A cross-site page cannot add a custom header without a CORS preflight, and the API has no CORS policy.

### Email links

Confirmation and reset tokens come from Identity's Data Protection token provider, expire after 3 hours, and travel in the URL fragment (`#userId=...&token=...`) so they never reach server logs or `Referer` headers (`AccountEmails.cs`). Data Protection keys are stored in the database so links survive restarts; in Azure they are encrypted with a Key Vault key (S3).

## Authorization

| Mechanism | Where | Notes |
|---|---|---|
| `[Authorize]` | Controllers and actions | JWT required. Anonymous access is the default for public reads. |
| Admin policy | `AdminAccess.Policy`, registered in `Program.cs`; used on `AdminController` and `InvitesController` | Authenticated user whose id is in `Admin:UserIds`. Ids rather than names, so a rename cannot transfer rights. Read at start-up. |
| Ownership checks | Each write action filters by `UserId == caller` | Not-owned items usually return 404 rather than 403, so existence is not revealed. Comments return 403 when the comment exists but is not the caller's. |
| Visibility | `UserVisibility` (diary, watchlist), private lists (`MovieListController.IsOwner`), `DerivedActivityFeed` | Hidden diaries and watchlists answer 404. |
| Blocks | `BlockRules` | Enforced on follow, comment, like, review lists by film, comment lists and the community feed. |
| Demo accounts | `NotForDemoAccountsAttribute`, explicit checks in `SettingsController.UpdateSettings`, `AuthController.ForgotPassword` and `ResetPassword` | Prevents changing or deleting shared demo accounts. |

## Input validation and data handling

- DTO validation attributes on every write (see [DATA_MODEL.md](DATA_MODEL.md#validation-and-persistence-workflow)); `[ApiController]` returns 400 automatically.
- Search inputs are limited to 100 characters (50 for user search) and pages to 1 to 500. Search terms are escaped with `Uri.EscapeDataString` before being sent to TMDB.
- All database access is LINQ through EF Core, so queries are parameterised. No raw SQL (`FromSql`, `ExecuteSql`, `SqlQuery`) exists outside migrations.
- Entities are never bound from request bodies; DTOs are, which prevents over-posting.
- **Avatar uploads** (`AvatarImage.cs`): limited to 1 MB by the action (`RequestSizeLimit`) and to 2 MB by nginx, checked by magic bytes (JPEG, PNG, WebP), refused above 8000 px per side, decoded and re-encoded as WebP at most 512 px. Re-encoding drops EXIF and anything hidden in the file. Served with `X-Content-Type-Options: nosniff`.
- **Markdown reviews** (`frontend/src/components/ReviewText.tsx`): `react-markdown` without raw HTML, an allow-list of elements, links opened with `rel="noopener noreferrer nofollow"`. Comments are plain text.
- **Correlation ids** from clients are accepted only if short and alphanumeric (`CorrelationId.Read`); nginx replaces any client value.

## Transport and browser security

Set by nginx (`frontend/security-headers.conf`, included in every location):

- `Content-Security-Policy`: `default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' https://image.tmdb.org blob: data:; font-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'`. No inline scripts, which is why the theme loader is a separate file (`frontend/public/theme.js`).
- `Strict-Transport-Security: max-age=31536000`, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, a restrictive `Permissions-Policy`, `server_tokens off`.

The backend adds HSTS and HTTPS redirection outside Development. Fonts are self-hosted (`@fontsource/*`), so visitors do not contact Google.

Forwarded headers are trusted only from configured networks: the Compose subnet locally, private ranges in Azure with `ForwardLimit` 3 (`Program.cs`, `infra/resources.bicep`). `tests/backend.Tests/Auth/ForwardedHeadersTests.cs` checks that a client cannot pick its own IP.

## Rate limiting and abuse controls

Global read and write limits per user or IP, stricter per-IP limits on login, registration and account emails, a search limit, the per-account login throttle, and at most 3 account emails per address per hour. Details in [API_REFERENCE.md](API_REFERENCE.md#rate-limits). Reports and blocks give members their own controls; admins resolve reports.

## Secrets and configuration

- No secrets are committed. A scan of tracked files found only placeholders in the README and compose comments, a test-only JWT key in `tests/backend.Tests/Infrastructure/ReelrApiFactory.cs`, and an empty `password` variable in `Reelr.postman_environment.json`.
- Required secrets are checked at start-up (`Program.cs`).
- Local: .NET user secrets or a git-ignored `.env`. CI: no secrets; the SQL Server password and the end-to-end JWT key are generated per run and masked in logs. Azure: Key Vault secrets read by the backend's managed identity; deploys sign in to Azure with GitHub OIDC; the database uses managed identities and Entra-only authentication, with no SQL passwords.
- Least privilege in the database: the backend identity can read and write rows but not change the schema; only the migration job is `db_owner` (`infra/sql-users.cs`).
- Containers run as non-root users (`USER app` in `backend/Dockerfile`, `nginx-unprivileged` for the frontend). In Azure the backend has internal ingress only.

## External API security

- The TMDB token is sent only to the configured TMDB base address, over HTTPS by default. It is never sent to the browser.
- TMDB error bodies are logged but not returned to clients; logged paths omit query strings.
- TMDB responses are deserialised into typed DTOs and trimmed before storage. Image paths longer than 300 characters are dropped. Images are loaded by the browser from `image.tmdb.org` only (CSP).

## Logging and privacy

- Account and admin events go through `SecurityLog` (`backend.Features.Auth.SecurityLog` category) with user ids only; `SecurityLogTests` checks that no log line contains an email address.
- Request logs record method, path without query string, status, duration and user id. Email failures log only the recipient's domain.
- IP addresses appear in nginx access logs and are kept in memory for rate limiting. Azure Log Analytics keeps logs for 30 days, matching the privacy page.
- Members can export their data (`GET /api/settings/export`) and delete their account.

## Security-sensitive operations

| Operation | Protection |
|---|---|
| Login | Rate limit, per-account throttle, lockout, uniform answers |
| Password reset | Email link, 3 h expiry, rate limit, stamp change signs out everywhere; not for demo accounts |
| Password change | Current password with lockout counting, stamp change, new token for this device |
| Log out everywhere | Stamp change |
| Account deletion | Current password with lockout counting, transaction, stamp cache cleared |
| Rename | Stamp change, new token and cookie |
| Admin resolve and invites | Admin policy, logged in `SecurityLog` |
| Refresh | Cookie flags, CSRF header, atomic rotation, reuse detection |

## Observed risks and missing safeguards

| # | Finding | Status | Severity | Evidence |
|---|---|---|---|---|
| S1 | **Rating and watch date leak through reviews when the diary is private.** `DiaryVisibility` is documented as covering "how they rated it", but `ReviewDto.Score` (the author's current rating) and `ReviewDto.WatchedAt` are returned on public review endpoints, and feed `reviewed` items include the diary entry's rating and watch date, regardless of diary visibility. Only for films the member reviewed. | Confirmed in code; whether it is intended needs verification | Medium (privacy) | `ReviewController.ToDtos`, `DerivedActivityFeed.Reviewed`, `DiaryVisibility.cs` comment |
| S2 | **Password change allowed shorter passwords than sign-up.** `ChangePasswordDto.NewPassword` had only `[Required]`, so Identity's default minimum of 6 characters applied. Now `options.Password.RequiredLength = 8` in `Program.cs` covers every path, and the DTO has `[MinLength(8)]`. | Fixed | Low | `Program.cs`, `Settings/DTOs/ChangePasswordDto.cs` |
| S3 | **Data Protection keys were stored unencrypted in the database.** Anyone who could read the `DataProtectionKeys` table could forge email confirmation or password reset tokens for any account. Now, when `DataProtection:KeyVaultKeyUri` is set (in Azure, by `infra/resources.bicep`), `Program.cs` wraps every new key with the Key Vault key `data-protection` (`ProtectKeysWithAzureKeyVault`), signing in as the backend's user-assigned identity (`AZURE_CLIENT_ID`). Keys written before the change stay readable until they expire. If Key Vault cannot be reached, creating or reading a wrapped key fails; it never falls back to unencrypted keys. Local development, Docker and tests do not set the value and keep unencrypted keys. | Fixed | Medium | `Program.cs` `ProtectKeysWithAzureKeyVault`, `infra/resources.bicep` |
| S4 | **In-memory security state resets on restart and is per instance.** Login throttle, rate limits and per-address email counters reset whenever the container restarts, which in Azure happens on every restart or deploy, and on every cold start when `backendMinReplicas = 0`. A second replica would also halve their effect. The template caps replicas at 1. | Confirmed | Low | `LoginThrottle.cs`, `RateLimitPolicies.cs`, `AccountEmails.cs`, `infra/resources.bicep` |
| S5 | **Block rules are not applied everywhere.** Reviews by a blocked member still appear on their profile list (`GET /api/users/{id}/reviews`), on direct review links and in the review likes list, and their public lists stay visible. The film review list, comments and community feed do filter them. | Confirmed; intent needs verification | Low | `ReviewController.GetReviewsByUser`, `GetReview`, `ReviewLikeController.GetLikes` |
| S6 | **Email uniqueness relies on application checks.** The `NormalizedEmail` index is not unique (Identity's default), so two concurrent registrations with the same email could both pass `RequireUniqueEmail`. | Needs verification (race window is small) | Low | `ReelrContextModelSnapshot.cs` `EmailIndex` |
| S7 | **Refresh reuse grace window.** A copied refresh token used within 20 seconds of the legitimate use is treated as a parallel tab and gets its own new token instead of ending the login. This is a deliberate trade-off for multiple tabs. | Confirmed, by design | Low | `RefreshTokens.ReuseGrace` |
| S8 | **No JWT key rotation.** One symmetric key without a key id or rollover; rotating it ends every access token at once (refresh cookies still work, so users get new tokens). | Confirmed | Low | `Program.cs`, `TokenService.cs` |
| S9 | **Azure SQL accepts connections from any Azure service.** `AllowAllWindowsAzureIps` with public network access. Entra-only authentication still applies. | Confirmed, documented trade-off | Low | `infra/resources.bicep` |
| S10 | **Access token readable by injected script while the page is open.** The design limits theft to the token's 15-minute life, and the CSP blocks inline and third-party scripts, so exploitation needs an XSS bug first. | Confirmed, by design | Informational | `frontend/src/lib/api.ts` |
| S11 | **Local Docker stack runs in Development** (developer exception page, Swagger). Ports are bound to 127.0.0.1 and the file says "Local development only". | Confirmed | Informational | `docker-compose.yml` |
| S12 | **Legal text not reviewed.** The README states the privacy policy and terms have not been reviewed by a lawyer. An accessibility statement (`/accessibility`) was added on 13 October 2026. | Confirmed | Informational (compliance) | Root README, `LegalPages.tsx` |
| S13 | **Data export omitted some personal data**: reports the member filed (with their notes) and the members they blocked were stored but not exported. Now the export includes `reports` (only what the member filed) and `blocked` (usernames). | Fixed | Low (compliance) | `Settings/DataExport.cs` |

Not found: SQL injection paths, raw HTML rendering, committed secrets, unauthenticated write endpoints (other than the `/auth` forms), or endpoints that return email addresses of other members.

## Areas that need further verification

- Whether `MapInboundClaims` maps `sub` to `ClaimTypes.NameIdentifier` as `ClaimsPrincipalExtensions.GetUserId` expects. The integration tests pass through authenticated endpoints, which indicates it does, but it relies on a framework default.
- The behaviour of `X-Forwarded-Proto` and HTTPS redirection behind Azure Container Apps' proxies (configuration looks consistent; not tested here).
- Whether the TMDB circuit breaker defaults are suitable under real TMDB rate limiting.
- Dependency vulnerability status: Dependabot is configured for version updates; no vulnerability scan output is in the repository.
