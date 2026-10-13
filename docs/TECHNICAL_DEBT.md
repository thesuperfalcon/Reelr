# Technical debt and improvement opportunities

Findings from reviewing the code at commit `4540153` on `develop`. Each one has evidence in the repository. The codebase is in good shape overall: it has no TODO or FIXME comments, the design decisions are written down in code comments, CI is thorough and tests are extensive. Most items below are small. None justifies a rewrite.

Severity: **High** (likely to cause user-visible bugs or security problems soon), **Medium** (real cost or risk, worth scheduling), **Low** (tidy-up or only matters at larger scale).

## Summary

| ID | Finding | Category | Severity |
|---|---|---|---|
| [TD-1](#td-1-error-responses-are-not-always-problemdetails) | Error responses are not always ProblemDetails | Error handling | Medium |
| [TD-2](#td-2-most-timestamps-are-serialised-without-a-utc-marker) | Most timestamps are serialised without a UTC marker | Correctness | Medium |
| [TD-3](#td-3-data-protection-keys-are-not-encrypted-at-rest) | Data Protection keys are not encrypted at rest | Security | Medium |
| [TD-4](#td-4-private-diary-data-appears-on-reviews) | Private diary data appears on reviews | Privacy | Medium |
| [TD-5](#td-5-single-instance-state) | Single-instance state blocks scaling out | Architecture | Medium |
| [TD-6](#td-6-testing-gaps) | Testing gaps | Testing | Medium |
| [TD-7](#td-7-block-rules-are-applied-in-some-places-only) | Block rules are applied in some places only | Consistency | Low |
| [TD-8](#td-8-password-length-differs-between-sign-up-and-password-change) | Password length differs between sign-up and password change (fixed) | Security | Low |
| [TD-9](#td-9-settings-update-is-not-atomic) | Settings update is not atomic | Correctness | Low |
| [TD-10](#td-10-data-export-is-incomplete) | Data export is incomplete | Compliance | Low |
| [TD-11](#td-11-repeated-query-code-in-controllers) | Repeated query code in controllers | Duplication | Low |
| [TD-12](#td-12-moviecatalog-swallows-every-save-error) | `MovieCatalog` swallows every save error | Error handling | Low |
| [TD-13](#td-13-stale-documentation-and-api-collection) | Stale documentation and API collection | Documentation | Low |
| [TD-14](#td-14-unbounded-or-unpaged-reads) | Unbounded or unpaged reads | Performance | Low |
| [TD-15](#td-15-refresh-tokens-of-inactive-users-are-never-cleaned-up) | Refresh tokens of inactive users are never cleaned up | Data hygiene | Low |
| [TD-16](#td-16-background-work-and-scale-to-zero) | Background work and scale-to-zero | Operations | Low |
| [TD-17](#td-17-floating-image-tags) | Floating image tags | Reproducibility | Low |
| [TD-18](#td-18-small-api-inconsistencies) | Small API inconsistencies | Consistency | Low |
| [TD-19](#td-19-api-surface-not-used-by-the-spa) | API surface not used by the SPA | Maintenance | Low |

## Findings

### TD-1. Error responses are not always ProblemDetails

- **Description:** The project convention (stated in `CLAUDE.md` and the root README) is that errors are `application/problem+json` with a `correlationId`. 27 call sites instead return a plain JSON string through `BadRequest("...")` or `Conflict("...")`. With `[ApiController]`, only results without a body are converted to ProblemDetails, so these responses have no `status`, `title` or `correlationId`.
- **Evidence:** `BadRequest("...")` in `MovieController`, `PersonController`, `CompanyController`, `UserController`, `ReviewLikeController`, `DiaryController`, `ActivityController`, `ReportController`, `AdminController`, `SettingsController`; `Conflict("...")` in `RatingController`, `StatusController`, `WatchlistController`, `ReviewController`, `MovieListController`, `UserController`. The SPA has a fallback for it: `readError` in `frontend/src/lib/api.ts` ("Plain text body, e.g. Conflict(\"...\") or BadRequest(\"...\")").
- **Impact:** Clients need two parsing paths. Users cannot quote a correlation id for these errors. The convention in the docs is not quite true.
- **Next step:** Replace with `Problem(statusCode: ..., title: "...")` or `ValidationProblem` (field errors), one controller at a time. The SPA keeps working because `readError` reads `title`. Add a test in `PlatformTests` that a typical 400 and 409 carry `correlationId`.

### TD-2. Most timestamps are serialised without a UTC marker

- **Description:** Timestamps are stored as UTC, but EF Core reads `datetime2` values with `DateTimeKind.Unspecified`, and `System.Text.Json` then writes them without `Z`. Browsers parse such strings as local time. Some code knows this and marks values as UTC; most does not.
- **Evidence:** `DateTime.SpecifyKind(..., DateTimeKind.Utc)` appears only in `InvitesController.AsUtc` (with the comment "SQL Server returns the stored UTC times without a kind; marking them makes the JSON end in 'Z'"), `AdminController`, and `DerivedActivityFeed`. Unmarked: `ReviewDto.CreatedAt` and `UpdatedAt`, `ReviewCommentDto`, `DiaryEntryDto.WatchedAt`, `StatusDto.WatchedAt`, `MovieListDto.CreatedAt` and `UpdatedAt`, `WatchlistEntryDto.AddedAt`. The SPA uses `new Date(value)` on them (for example `frontend/src/pages/MoviePage.tsx`, `ReviewComments.tsx`, `ListPage.tsx`).
- **Impact:** Displayed dates and times are offset by the viewer's UTC offset. Diary days are mostly safe because picked days are stored at 12:00 UTC, but "now" logs and review or comment dates near midnight can show the wrong day. This follows from framework defaults and the code; it was not reproduced at runtime.
- **Next step:** Add a convention in `ReelrContext.ConfigureConventions` that reads every `DateTime` as UTC (a value converter), then remove the scattered `SpecifyKind` calls. Add an integration test that a review's `createdAt` ends in `Z`.

### TD-3. Data Protection keys are not encrypted at rest

- **Description:** The key ring that signs email confirmation and password reset tokens is persisted to the `DataProtectionKeys` table without key encryption.
- **Evidence:** `backend/Program.cs`: `AddDataProtection().SetApplicationName("Reelr").PersistKeysToDbContext<ReelrContext>()`, no `ProtectKeysWith...`.
- **Impact:** Read access to the database (a leaked backup, an over-privileged identity) is enough to forge a password reset token for any account. See [SECURITY.md](SECURITY.md) S3.
- **Next step:** In Azure, protect keys with Key Vault (`ProtectKeysWithAzureKeyVault`, using the backend's managed identity, which already reads the vault). Keep local development unencrypted. Existing keys stay readable during the change.

### TD-4. Private diary data appears on reviews

- **Description:** Diary visibility hides "what they watched, when, and how they rated it", but review responses and `reviewed` feed items include the author's current rating and the linked diary entry's watch date for everyone.
- **Evidence:** `backend/Features/Users/DiaryVisibility.cs` (comment); `ReviewController.ToDtos` (`Score`, `WatchedAt`); `DerivedActivityFeed.Reviewed` (`Rating`, `WatchedAt` from `r.DiaryEntry`).
- **Impact:** A member with a private diary still discloses rating and watch date for each film they review. The review text itself is public by design.
- **Next step:** Confirm the intended behaviour. If the diary setting should win, null out `Score` and `WatchedAt` when `UserVisibility.CanSeeDiary` is false for the viewer (one extra join), and add cases to `DiaryVisibilityTests`.

### TD-5. Single-instance state

- **Description:** Several pieces of state live in process memory: the ASP.NET rate limiter, `LoginThrottle`, `TmdbCache`, `SecurityStamps`' cache (with immediate invalidation only on the instance that changed the stamp), `AccountEmails`' per-address counter, the community feed cache and `EmailQueue`.
- **Evidence:** Those classes; the root README ("One instance for now ... Running several instances would need Redis"); `maxReplicas: 1` in `infra/resources.bicep`.
- **Impact:** No problem today. A second replica would weaken rate limits, delay revocation by up to 30 seconds on other instances, duplicate refresh work and lose queued emails on scale-in. Restarts already reset the abuse counters (see TD-16).
- **Next step:** Only when scaling is needed: move rate limiting, throttling and the stamp cache to a distributed store (for example Redis), and email to a durable queue or outbox table. Until then keep `maxReplicas: 1`.

### TD-6. Testing gaps

- **Description:** Backend coverage of endpoints is strong, but some infrastructure and most of the frontend have no tests.
- **Evidence:** No test references `EmailDispatcher`, `SmtpEmailSender` or `OpenRetryInterceptor`. `ReelrApiFactory` uses `EnsureCreated`, so tests never run against the migrated schema (CI only checks that migrations apply). Frontend tests cover `api.ts`, `AuthProvider`, theme and some accessibility, but not `queries.ts` invalidation, pages or the rating dialog flow. Coverage is collected in CI with no threshold or report. End-to-end tests cover one member journey and axe checks of the main pages. Details in [TESTING.md](TESTING.md#gaps-observed).
- **Impact:** Regressions in email retries, cache invalidation (stale UI after a mutation) or schema drift between `EnsureCreated` and migrations would not be caught automatically.
- **Next step:** Add a unit test for `EmailDispatcher` with a failing fake sender; test the most important mutation hooks with a mocked `fetch`; consider running one backend test class against a migrated database; publish the coverage report.

### TD-7. Block rules are applied in some places only

- **Description:** `BlockRules.BlockedBy` filters the film review list, comment lists and the community feed, but not a member's review list, single reviews, the likes list or lists.
- **Evidence:** `ReviewController.GetReviews` (filtered) versus `GetReviewsByUser` and `GetReview` (not); `ReviewLikeController.GetLikes`; `MovieListController`. The block checks are written separately in four controllers and the feed.
- **Impact:** Blocking is less complete than the README's "you stop seeing them" suggests. Each new endpoint has to remember the rule.
- **Next step:** Decide the intended scope (hiding on profiles may be unnecessary since the viewer navigated there), document it in `BlockRules.cs`, and add tests for the chosen behaviour.

### TD-8. Password length differs between sign-up and password change

- **Description:** Registration and reset require 8 characters; password change only Identity's default of 6.
- **Evidence:** `[MinLength(8)]` in `RegisterDto` and `ResetPasswordDto`; only `[Required]` in `Settings/DTOs/ChangePasswordDto.cs`; `Program.cs` does not set `options.Password.RequiredLength`.
- **Impact:** A member can choose a weaker password than the sign-up form allows.
- **Status:** Fixed. `options.Password.RequiredLength = 8` is set in the Identity options, and `ChangePasswordDto.NewPassword` has `[MinLength(8)]` like the other DTOs.

### TD-9. Settings update is not atomic

- **Description:** `PATCH /api/settings` renames the user (saved immediately by `SetUserNameAsync`, plus a new refresh cookie) before it validates the visibility enums. An invalid visibility then returns 400 although the rename already happened.
- **Evidence:** `SettingsController.UpdateSettings`.
- **Impact:** Rare (the SPA sends valid values), but the response contradicts the stored state.
- **Next step:** Validate every field first, then apply changes.

### TD-10. Data export is incomplete

- **Description:** The export describes itself as "everything Reelr stores about one user" (GDPR articles 15 and 20) but leaves out reports the member filed (including their notes) and the members they blocked.
- **Evidence:** `backend/Features/Settings/DataExport.cs`; `Report` and `Block` entities.
- **Impact:** Compliance gap for a closed beta; easy to fix.
- **Next step:** Add `Reports` (type, id, reason, note, dates, outcome) and `Blocked` (usernames) to the export, and update `SettingsControllerTests`.

### TD-11. Repeated query code in controllers

- **Description:** Controllers use `ReelrContext` directly, which keeps them simple but repeats some query shapes.
- **Evidence:** The cursor-paging pattern (`if (page.After is PageCursor after) ... Take(page.Limit + 1) ... PageDto.From`) appears in about ten actions across `DiaryController`, `WatchlistController`, `ReviewController`, `ReviewCommentController`, `ReviewLikeController`, `UserController` and `MovieListController`. The "top 3 movies" query appears twice in `MovieListController` (`GetList`, `UpdateList`) besides `ToSummaries`.
- **Impact:** Small; changes to paging or list previews must be made in several places.
- **Next step:** Extract the list detail query in `MovieListController` into one private method. Leave the paging pattern unless it starts to diverge; each instance sorts by different columns, and a generic helper would add indirection.

### TD-12. `MovieCatalog` swallows every save error

- **Description:** `GetOrCreateAsync` catches any `DbUpdateException` and assumes another request stored the film first.
- **Evidence:** `backend/Features/Movies/MovieCatalog.cs` (`catch (DbUpdateException)`).
- **Impact:** A different database error (for example a truncation) is turned into a re-read and then `InvalidOperationException("Could not store movie ...")`, hiding the original cause in logs. Still a 500, so not user-visible beyond that.
- **Next step:** Catch only SQL errors 2601 and 2627, as `UniqueConflictExceptionHandler` does, and log the original exception otherwise.

### TD-13. Stale documentation and API collection

- **Description:** Several documents no longer match the code.
- **Evidence:**
  - `frontend/README.md` is the unchanged Vite template.
  - `Reelr.postman_collection.json` covers about half the API: no diary, lists, feed, settings, reports, admin, invites, blocks, refresh or logout. Its login stores a bearer token, but sessions now depend on the refresh cookie.
  - Root README: "`UserVisibility` decides who can see a member's watchlist" (it also handles diaries).
  - `CLAUDE.md`: "Paged endpoints take `?cursor=&limit=` (default 30, max 100)"; the activity feed uses default 20, maximum 50, clamping instead of 400, and its own cursor.
- **Impact:** Misleads new developers and API users.
- **Next step:** Replace `frontend/README.md` with a pointer to these docs; regenerate the Postman collection from `/openapi/v1.json` or remove it in favour of Swagger; fix the two sentences.

### TD-14. Unbounded or unpaged reads

- **Description:** A few reads load everything that matches.
- **Evidence:** `AdminController.GetOpenReports` loads all open reports into memory and groups them in C#. `SettingsController.GetBlocked` and `InvitesController.GetInvites` are unpaged. `UserController.SearchUsers` uses `NormalizedUserName.Contains(...)`, which cannot use the index. `ReviewController.GetReviews` counts all reviews of the film on every page request. The shared `IMemoryCache` (`AddMemoryCache()` without a size limit) holds community feed pages keyed by viewer, limit and types.
- **Impact:** None at closed-beta scale; grows linearly with data.
- **Next step:** Revisit when the member count grows: page the admin list, return `totalCount` only on the first page, and set a size limit on the shared cache.

### TD-15. Refresh tokens of inactive users are never cleaned up

- **Description:** Expired refresh tokens are deleted only when the same user gets a new token.
- **Evidence:** `RefreshTokens.AddAsync` (`ExecuteDeleteAsync` scoped to `user.Id`).
- **Impact:** Rows for members who stop visiting stay forever. The privacy page says login records are kept "for at most 30 days".
- **Next step:** Delete expired tokens for everyone in a periodic job (for example inside `MovieRefresher`'s loop or a small new hosted service).

### TD-16. Background work and scale-to-zero

- **Description:** In Azure the backend keeps one replica warm by default, but `backendMinReplicas = 0` lets it scale to zero. Background services only run while a replica is up.
- **Evidence:** `backendMinReplicas` in `infra/main.bicep`; `MovieRefresher` runs at the first visitor after start-up (plus a 2-minute delay) and then at the first visitor at least 6 hours later; `EmailQueue` is in memory with retries up to about 42 seconds.
- **Impact:** An email still retrying when the replica scales in is lost. Rate-limit and throttle counters reset at each cold start (see [SECURITY.md](SECURITY.md) S4).
- **Next step:** Acceptable for the beta. If it matters, move refresh and email to a scheduled Container Apps job.

### TD-17. Floating image tags

- **Description:** Some container images use moving tags.
- **Evidence:** `mcr.microsoft.com/mssql/server:2022-latest` in `docker-compose.yml` and CI; `mcr.microsoft.com/dotnet/sdk:10.0` and `aspnet:10.0` in `backend/Dockerfile`; `node:24-alpine` in `frontend/Dockerfile` and `docker-compose.e2e.yml`; `nginxinc/nginx-unprivileged:1.29-alpine`. Dependabot's `docker` entry covers `/backend` and `/frontend` only, not the compose files. (Mailpit is pinned to `v1.31.2`; GitHub Actions are pinned to SHAs.)
- **Impact:** Builds are not fully reproducible; a base image change can alter behaviour without a commit.
- **Next step:** Optional: pin digests in Dockerfiles and let Dependabot update them.

### TD-18. Small API inconsistencies

- **Description:** Minor differences between similar endpoints.
- **Evidence:**
  - `POST /api/watchlist/{tmdbId}` returns 200 with an empty body; comparable adds return 204 (`POST /api/lists/{id}/movies`, follow).
  - `POST /api/movies/{tmdbId}/reviews` stores the text as sent, while the diary path trims it (`JournalService.LogAsync`).
  - Not-owned items return 404 in most controllers but 403 for comments (`ReviewCommentController`).
  - Watched status and rating updates (`PUT`) always add a diary entry, which may surprise API clients; it is documented in `JournalService` but not in the endpoint summaries.
- **Impact:** Small client confusion.
- **Next step:** Align when touching those endpoints; mention the diary side effect in `EndpointSummary`.

### TD-19. API surface not used by the SPA

- **Description:** Several endpoints have no caller in `frontend/src`.
- **Evidence:** `GET /api/movies/search` (discover filters), `GET /api/movies/{tmdbId}`, `/similar`, `/credits`, `GET /api/people/search`, `GET /api/companies/search`, `GET /api/reviews/{id}/likes`, `POST /api/movies/{tmdbId}/reviews`. Most are tested and in the Postman collection.
- **Impact:** Extra surface to maintain and rate-limit; each TMDB-backed one consumes TMDB quota if abused (the search limit applies).
- **Next step:** Keep them if API clients matter (the Postman collection suggests so); otherwise remove unused ones. Identity's role, claim and external-login tables are also unused but harmless.

## Outdated dependencies

Not verifiable offline. The declared versions are recent for October 2026 (.NET 10.0.11/10.0.12 packages, React 19.3, Vite 8, TypeScript 6, Playwright 1.64), and Dependabot opens weekly update pull requests for NuGet, npm, Docker and GitHub Actions (`.github/dependabot.yml`; major updates of `@types/node` and `typescript` are held back on purpose). No vulnerability audit output is stored in the repository; running `npm audit` and `dotnet list package --vulnerable` periodically, or enabling Dependabot security alerts, would close that gap.

## Things that are not debt

Some choices look unusual but are deliberate and documented in code comments: the real SQL Server in tests (`ReelrApiFactory`), `Restrict` delete behaviours with manual deletion (`ReelrContext`), the separate migration identity and bundle (`backend/Dockerfile`, `infra/sql-users.cs`), uniform answers on account forms (`AuthController`), the derived activity feed behind an interface (`IActivityFeed`), and admins by user id (`AdminAccess`). Changing them would need a reason beyond style.
