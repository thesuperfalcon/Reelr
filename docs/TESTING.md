# Testing

Reelr has three test suites: backend integration tests (the large majority), frontend unit and component tests, and a Playwright suite with one member journey plus axe accessibility checks. There are almost no pure unit tests of backend classes; behaviour is tested through HTTP against a real database.

| Suite | Location | Framework | Size (counted October 2026) | Runs in CI |
|---|---|---|---|---|
| Backend integration | `tests/backend.Tests/` | xUnit 2.9, `Microsoft.AspNetCore.Mvc.Testing` | 38 test files, 377 `[Fact]`/`[Theory]` methods | Yes, job `backend` |
| Frontend | `frontend/src/**/*.test.ts(x)` | Vitest 5, Testing Library, jsdom, axe-core | 4 files, 26 test cases | Yes, job `frontend` |
| End-to-end | `e2e/tests/` | Playwright 1.64 (Chromium), `@axe-core/playwright` | 2 spec files, 7 tests | Yes, job `e2e` |

## Backend integration tests

### How they work

`tests/backend.Tests/Infrastructure/ReelrApiFactory.cs` extends `WebApplicationFactory<Program>`:

- Each test class (via `IClassFixture<ReelrApiFactory>`) gets a **new SQL Server database** named `ReelrTests_<guid>`, created with `EnsureCreated` before the host starts and deleted on dispose.
- The real `Program.cs` pipeline runs, including authentication, validation, exception handlers, rate limiting and logging, with environment `Testing`.
- TMDB is replaced by `FakeTmdbHandler`, plugged in as the primary handler of the `TmdbService` HttpClient (the resilience handler stays in place).
- Email is replaced by `FakeEmailSender`, registered as `IEmailSender`; the real `EmailQueue` and `EmailDispatcher` still run.
- Logs are captured by `TestLogs` so tests can assert on log lines (for example that the security log never contains emails).
- Settings are fixed for determinism: rate limits lifted to 100 000, TMDB cache off, TMDB resilience made fast (no retry delay, short timeouts, circuit breaker effectively off), `MovieRefresh:Enabled=false`, community feed cache off, invite-only off.

Subclasses override virtual properties to test the defaults: `LowAuthLimitApiFactory`, `LowReadLimitApiFactory`, `LowWriteLimitApiFactory`, `LowSearchLimitApiFactory`, `LowLoginThrottleApiFactory`, `InviteOnlyApiFactory`, `ModerationApiFactory`, `DemoAccountApiFactory`, `TmdbCacheApiFactory`, `TmdbBaseUrlWithoutSlashApiFactory`, `ProductionDefaultsApiFactory`.

SQLite or the EF in-memory provider are not used, because some queries (a `Take` inside a projection) need SQL `APPLY` (comment in `ReelrApiFactory`).

### Helpers

| Helper | Use |
|---|---|
| `TestUsers.CreateAuthenticatedAsync()` | Registers a unique user (with an invite when the factory requires one), confirms the email through `FakeEmailSender`, logs in and returns `TestUser(Id, Username, Client)` with the bearer token set. |
| `FakeTmdbHandler` | `AddMovie(...)`, `SetResponse(path, body, status)`, `SetDelay`, failure counts, `Requests` and `RequestsForMovie` to check what was sent. Unknown paths return 404. |
| `FakeEmailSender` | `Sent`, `SentTo`, `WaitForAsync(address, subject)` (polls up to 5 s, since emails go through the background queue), `ReadLink` to extract the user id and token. |
| `ReelrApiFactory.WithContextAsync` | Direct database access for setup and assertions. |
| `PageExtensions`, `TestImages`, `TestLogs` | Paged response reading, generated test images (including a JPEG with EXIF data, to check it is stripped), log capture. |

### Running

```bash
dotnet test backend/backend.slnx
```

```bash
dotnet test backend/backend.slnx --filter "FullyQualifiedName~RefreshTokenTests"
```

By default the factory connects to `.\SQLEXPRESS` with Windows authentication. Set `REELR_TEST_SQLSERVER` to another server, without `Database=` (the factory appends it). For example, from the root README:

```powershell
$env:REELR_TEST_SQLSERVER = "Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;"
```

CI starts SQL Server 2022 in a container with a password generated for the run, sets `REELR_TEST_SQLSERVER` from it, runs the tests in Release with a TRX logger and `XPlat Code Coverage` (coverlet), and uploads the results as an artifact. Coverage is collected but no threshold is enforced and no report is published.

### What is covered

Test methods per area (from `[Fact]`/`[Theory]` counts):

| Area | Test classes (by file) | Methods | Highlights |
|---|---|---|---|
| Auth | `AuthControllerTests`, `RefreshTokenTests`, `TokenRevocationTests`, `TokenServiceTests`, `TokenNameClaimTests`, `LoginThrottleTests`, `AuthRateLimitTests`, `ForwardedHeadersTests.cs` (three classes), `SecurityLogTests` | 51 | Registration answers for taken emails, confirmation and reset links, lockout, per-account throttle, refresh rotation, concurrent refresh grace, reuse detection ending the login, CSRF header, revocation on password change and "log out everywhere", forwarded-header spoofing, no personal data in security logs |
| Diary and journal | `DiaryControllerTests`, `RatingControllerTests`, `HalfStarsAttributeTests`, `StatusControllerTests`, `WatchlistControllerTests` | 79 | Journal rules, watch date limits, watchlist removal, status recomputation |
| Reviews | `ReviewControllerTests`, `ReviewCommentControllerTests`, `ReviewLikeControllerTests` | 39 | Ownership, spoilers, block effects on likes and comments |
| Lists | `MovieListControllerTests` | 26 | Private list visibility, ownership, paging |
| Users and settings | `UserControllerTests`, `BlockTests`, `DiaryVisibilityTests`, `DemoAccountTests`, `SettingsControllerTests` | 62 | Visibility rules, block effects, account deletion, demo restrictions, avatar upload, export |
| Feed | `ActivityControllerTests` | 19 | Following and community rules, grouping, paging |
| Films and TMDB | `MovieControllerTests`, `PersonControllerTests`, `CompanyControllerTests`, `MovieRefreshTests`, `TmdbResilienceTests` (includes `TmdbCacheTests`), `TmdbBaseUrlTests` | 47 | TMDB failure mapping to 502 and 503, timeouts, caching, refresh behaviour |
| Moderation and invites | `ModerationTests`, `InviteTests` | 14 | Admin-only access, report rules, resolution, invite claim and revoke |
| Platform | `ConcurrencyTests`, `HealthCheckTests`, `LoggingTests`, `PlatformTests`, `RateLimitTests.cs` (read, write and search classes), `PagingTests` | 40 | Parallel duplicate requests produce one row and no 500, health endpoints, request logging without query strings, correlation ids in errors, start-up secret checks, global and search rate limits with `Retry-After`, cursor validation |

### Gaps observed

A test project existing does not mean a feature is tested. Searching the test code found no tests for:

- `EmailDispatcher` retry and give-up behaviour, and `EmailQueue` overflow (`FakeEmailSender` never fails).
- `SmtpEmailSender` itself (no SMTP test double).
- `OpenRetryInterceptor` (transient Azure SQL errors cannot be simulated against SQL Express).
- The migrations themselves: tests use `EnsureCreated`. CI compensates with `has-pending-model-changes` and by applying every migration to an empty database, but no test runs against a migrated schema or checks data migrations.
- `UniqueConflictExceptionHandler` is exercised indirectly through `ConcurrencyTests`, which assert on outcomes rather than on the 409 body.
- Multi-instance behaviour (not supported, see [ARCHITECTURE.md](ARCHITECTURE.md)).
- Performance or load of the derived activity feed.
- Most per-endpoint rate-limit policies beyond the representative cases in `RateLimitTests.cs` and `AuthRateLimitTests`.

## Frontend tests

Run with `cd frontend && npm test` (`vitest run`). Configuration: `frontend/vitest.config.ts` (jsdom, `src/test/setup.ts`, mocks restored between tests).

| File | What it checks |
|---|---|
| `src/lib/api.test.ts` | Headers and token, multipart uploads, 204 handling, ValidationProblem and plain-text error parsing, refresh and retry once on 401, sign-out when refresh fails, no refresh for anonymous visitors, shared refresh promise, signed-in marker without storing the token. |
| `src/auth/AuthProvider.test.tsx` | Restoring a session from the cookie, no refresh for a browser never signed in, expired cookie, login and logout. |
| `src/test/a11y.test.tsx` | axe checks on forms, the star rating slider's keyboard behaviour, focus management for delete confirmation and spoiler reveal, ARIA tab keyboard navigation, the Load more button. |
| `src/lib/theme.test.ts` | Default theme, applying and remembering a theme, resetting. |

`src/test/fakes.ts` provides a fake JWT and a route-based `fetch` mock.

Gaps: no tests for `queries.ts` cache invalidation, page components, routing, `RequireAuth`, the rating dialog's logging flow or the Markdown editor. There are no visual regression tests.

## End-to-end tests

`e2e/tests/accessibility.spec.ts` runs axe (WCAG 2.0 to 2.2 A and AA tags, including colour contrast, which jsdom cannot check) in a real browser on the public pages and on the signed-in pages and rating dialog, once for each of the five themes (6 tests). The `/accessibility` page says these checks exist and must stay in step with them.

`e2e/tests/journey.spec.ts` has one test: "sign up, log a film with a review, follow someone and delete the account". It drives Chromium against the full Docker stack (nginx, API, SQL Server, Mailpit) with the fake TMDB, and reads confirmation emails through Mailpit's API (`e2e/tests/accounts.ts`).

```bash
docker compose -p reelr-e2e -f docker-compose.yml -f docker-compose.e2e.yml up -d --build --wait
```

```bash
cd e2e && npm ci && npx playwright install chromium && npx playwright test
```

```bash
docker compose -p reelr-e2e -f docker-compose.yml -f docker-compose.e2e.yml down -v
```

`E2E_BASE_URL` (default http://localhost:8080) and `E2E_MAILPIT_URL` configure targets. `playwright.config.ts` uses a 120 s timeout, runs serially, retries once in CI and keeps traces and screenshots on failure. The e2e overlay raises the auth rate limits to 1000, because every test signs up from one address.

These are the only tests that exercise nginx (proxying, security headers, the refresh cookie through a real browser) and the migrations bundle running before the backend. Functionally they do not cover admin moderation, invites, lists, the feed beyond following, settings other than deletion, or mobile viewports.

## Testing risks

- Backend tests need a real SQL Server, which makes local setup heavier and test runs slower than in-memory alternatives, but keeps SQL behaviour real.
- One database per test class means tests in a class share data. Tests create unique users to avoid collisions; ordering assumptions inside a class would be fragile.
- `FakeEmailSender.WaitForAsync` and similar polling make some tests timing-dependent (5 s ceiling).
- `ReelrApiFactory` turns the TMDB circuit breaker and cache off by default; `ProductionDefaultsApiFactory` and `TmdbCacheApiFactory` exist to cover the production settings.
- The fake TMDB in `e2e/` and `FakeTmdbHandler` in the backend tests are separate implementations that can drift from TMDB's real responses.
