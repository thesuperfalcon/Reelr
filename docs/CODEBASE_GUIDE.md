# Codebase guide

How the repository is laid out and where to start for common changes. For why it is built this way, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Top level

```
backend/              ASP.NET Core API (one project)
  Data/               DbContext, design-time factory, DB-level exception handling
  Features/           Feature slices: controller + entities + DTOs (+ services)
  Migrations/         EF Core migrations (22 so far) and the model snapshot
  Program.cs          All service registration and the HTTP pipeline
tests/backend.Tests/  xUnit integration tests, folders mirror Features/
frontend/             React SPA (Vite) plus nginx config for the container
e2e/                  Playwright end-to-end tests and a fake TMDB server
infra/                Azure Bicep templates and deploy scripts
.github/              CI, Deploy and Dependabot configuration
.claude/              Shared Claude Code settings and a lint hook for frontend edits
docker-compose.yml    Local stack: SQL Server, migrate, backend, Mailpit, frontend
docker-compose.e2e.yml  Overlay that adds the fake TMDB for end-to-end runs
Reelr.postman_*.json  Postman collection and environment (partial, see TECHNICAL_DEBT.md)
```

## Backend: `backend/`

### `Program.cs`

The composition root. Read it first. It:

- checks required configuration at start-up (`Jwt:Key` of at least 32 bytes, `TMDB_READ_ACCESS_TOKEN`, `Email:Host`, `Email:FromAddress`, `App:BaseUrl`) and refuses to start without them,
- registers every service (see the DI table in [ARCHITECTURE.md](ARCHITECTURE.md#dependency-injection)),
- configures Identity (lockout, username characters), Data Protection (keys in the database, wrapped with a Key Vault key when `DataProtection:KeyVaultKeyUri` is set), JWT validation with the security stamp check, rate limits, forwarded headers, the TMDB HttpClient with resilience, health checks,
- builds the middleware pipeline in a deliberate order.

### `Data/`

| File | Role |
|---|---|
| `ReelrContext.cs` | The only DbContext. Extends `IdentityDbContext<User, IdentityRole<int>, int>` and implements `IDataProtectionKeyContext`. All keys, indexes, check constraints, delete behaviours and the computed `IsBackdated` column are configured in `OnModelCreating`. |
| `ReelrContextFactory.cs` | Used by `dotnet ef` and the migrations bundle, so they do not start the app (whose start-up checks need secrets a migration does not). |
| `OpenRetryInterceptor.cs` | Retries opening a SQL connection on transient Azure SQL errors. |
| `UniqueConflictExceptionHandler.cs` | Turns SQL duplicate-key errors (2601, 2627) into 409 ProblemDetails. |

### `Features/`

Every folder follows the same shape where it applies: `XController.cs`, entity classes, a `DTOs/` folder, and sometimes a service. The table lists each folder with its most important files.

| Folder | Responsibility | Key files |
|---|---|---|
| `Activity` | Following and Community feeds, "New from friends" shelf | `IActivityFeed.cs`, `DerivedActivityFeed.cs` (all feed visibility rules), `ActivityController.cs`, `ActivityCursor.cs`, `ActivityTypes.cs`, `ActivityOptions.cs` |
| `Auth` | Registration, email confirmation, password reset, login, refresh, logout, rate limits, security logging | `AuthController.cs`, `TokenService.cs`, `RefreshTokens.cs`, `RefreshToken.cs`, `SecurityStamps.cs`, `LoginThrottle.cs`, `RateLimitPolicies.cs`, `RequireCsrfHeaderAttribute.cs`, `AccountEmails.cs`, `SecurityLog.cs`, `ClaimsPrincipalExtensions.cs` |
| `Companies` | Studio search (TMDB proxy) | `CompanyController.cs` |
| `Diary` | Diary entries and the journal rules | `JournalService.cs`, `DiaryController.cs`, `DiaryEntry.cs` |
| `Email` | Background email sending | `EmailQueue.cs`, `EmailDispatcher.cs`, `IEmailSender.cs`, `SmtpEmailSender.cs`, `EmailOptions.cs` |
| `Invites` | Invite-only registration | `InviteCodes.cs`, `InvitesController.cs`, `Invite.cs` |
| `Moderation` | Reports and admin moderation | `AdminAccess.cs` (admin policy), `AdminController.cs`, `ReportController.cs`, `Report.cs` |
| `MovieLists` | Member lists of films | `MovieListController.cs`, `MovieList.cs`, `MovieListItem.cs` |
| `Movies` | TMDB client, film storage, refresh job, TMDB error mapping, TMDB DTOs | `TmdbService.cs`, `TmdbCache.cs`, `MovieCatalog.cs`, `MovieRefresher.cs`, `TmdbExceptionHandler.cs`, `MovieController.cs`, `Movie.cs` |
| `Observability` | Logging, correlation ids, health checks | `Logging.cs`, `CorrelationId.cs`, `HealthChecks.cs` |
| `Paging` | Cursor paging contract | `PageQuery.cs`, `PageCursor.cs`, `PageDto.cs` |
| `People` | Person details and search (TMDB proxy) | `PersonController.cs`, `PersonCredits.cs` |
| `Ratings` | Current rating per film | `RatingController.cs`, `Rating.cs`, `HalfStarsAttribute.cs` |
| `Reviews` | Reviews, comments, likes | `ReviewController.cs`, `ReviewCommentController.cs`, `ReviewLikeController.cs`, entities |
| `Settings` | The caller's own account: settings, password, avatar, export | `SettingsController.cs`, `AvatarImage.cs`, `DataExport.cs`, `UserAvatar.cs` |
| `Users` | Profiles, search, follow, block, delete account, visibility and block rules, demo accounts | `User.cs`, `UserController.cs`, `UserVisibility.cs`, `BlockRules.cs`, `DemoAccounts.cs`, `Follow.cs`, `Block.cs` |
| `WatchedMovies` | Watched status per film | `StatusController.cs`, `WatchedMovie.cs` |
| `WatchlistItems` | Watchlist | `WatchlistController.cs`, `WatchlistItem.cs` |

Naming note: folder names do not always match route names. `WatchedMovies` serves `/api/movies/{tmdbId}/status`; `Diary` serves `/api/watched`, `/api/diary/...` and `/api/movies/{tmdbId}/diary`. [API_REFERENCE.md](API_REFERENCE.md) maps routes to controllers.

### `Migrations/`

Timestamped EF Core migrations from `20260907141742_InitialCreate` to `20261012142931_AddInvites`, plus `ReelrContextModelSnapshot.cs`. Never edit an applied migration; add a new one. See [DATA_MODEL.md](DATA_MODEL.md#migrations).

### Other backend files

- `appsettings.json`: non-secret defaults (Serilog levels, email port and security, JWT issuer, audience and lifetimes, activity feed tuning).
- `appsettings.Development.json`: local SMTP (Mailpit on port 1025), open registration, local SQL Express connection string with Windows authentication.
- `Properties/launchSettings.json`: `http` (port 5084) and `https` (7160 and 5084) profiles.
- `dotnet-tools.json`: local tool manifest pinning `dotnet-ef` 10.0.12.
- `Dockerfile`: `build`, `bundle`, `migrations` and `runtime` stages.

## Tests: `tests/backend.Tests/`

Folders mirror `backend/Features/`. `Infrastructure/` holds the shared harness:

| File | Role |
|---|---|
| `ReelrApiFactory.cs` | `WebApplicationFactory<Program>` with a throwaway SQL Server database per test class, fake TMDB, fake email sender, lifted rate limits and overridable virtual properties. |
| `FakeTmdbHandler.cs` | HttpMessageHandler that answers TMDB paths from in-memory data, can delay or fail requests and records them. |
| `FakeEmailSender.cs` | Keeps sent emails, waits for them, and parses the confirmation or reset link. |
| `TestUsers.cs` | Registers, confirms and logs in a unique user and returns an authenticated `HttpClient`. |
| `PageExtensions.cs`, `TestImages.cs`, `TestLogs.cs` | Helpers for paged responses, generated images and captured logs. |

See [TESTING.md](TESTING.md).

## Frontend: `frontend/`

```
src/
  main.tsx        Router (lazy-loaded pages), QueryClient defaults, providers
  index.css       Tailwind and the five themes
  auth/           AuthProvider, auth-context (useAuth), RequireAuth
  lib/            API client, query hooks, types and small helpers
  pages/          One file per route (some files export several pages)
  components/     Shared UI: dialogs, feed items, stars, posters, layout
  test/           Vitest setup, fakes and accessibility tests
public/theme.js   Applies the saved theme before first paint (CSP forbids inline scripts)
nginx.conf.template, security-headers.conf, Dockerfile   Container serving and proxying
```

### `src/lib/`

| File | Role |
|---|---|
| `api.ts` | The only fetch layer. In-memory access token, refresh through the cookie (shared promise per tab, Web Lock across tabs), retry once on 401, `ApiError` with flattened validation messages. |
| `queries.ts` | Every TanStack Query hook (over 50), including cache keys and invalidation after mutations. |
| `types.ts` | TypeScript mirrors of the API DTOs. Keep it in step with `backend/Features/*/DTOs`. |
| `account.ts`, `invites.ts`, `moderation.ts` | Small API wrappers and types for account emails, invites and reports. |
| `activity.ts` | Feed helpers; skips activity kinds the client does not know. |
| `avatar.ts` | Crops and scales a picture in the browser before upload. |
| `drafts.ts` | Keeps unsaved review text in `sessionStorage`. |
| `focus.ts`, `tabs.ts`, `title.ts` | Accessibility helpers: return focus after inline steps, ARIA tab keyboard handling, page titles. |
| `theme.ts` | Theme names; must match `public/theme.js` and `index.css`. |
| `dates.ts`, `tmdb.ts` | Local calendar days and every date formatter (moments in the viewer's time zone, calendar days in UTC); TMDB image URLs, years and runtimes. |

### `src/pages/`

`HomePage`, `ActivityPage`, `MoviePage`, `PersonPage`, `ListPage`, `ReviewPage`, `SearchPage`, `ProfilePage` (exports `UserPage` for `/user/:id` and `ProfilePage` for `/profile`), `AuthPages` (login, register, confirm email, forgot and reset password), `LegalPages` (privacy, terms, accessibility statement), `SettingsPage`, `AdminPage`, `NotFoundPage`. Routes are in `src/main.tsx`; `/profile`, `/settings` and `/admin` are wrapped in `RequireAuth`.

### `src/components/`

Notable ones: `Layout.tsx` (header, footer, session-expired notice, focus on navigation), `RatingDialog.tsx` (the main logging dialog), `DiaryEntryDialog.tsx`, `ListFormDialog.tsx`, `AddToListDialog.tsx`, `ReviewComments.tsx`, `ReviewReactions.tsx`, `ReportButton.tsx`, `ActivityFeed.tsx` and `ActivityItem.tsx`, `HomeActivity.tsx`, `MarkdownEditor.tsx`, `ReviewText.tsx` (safe Markdown rendering), `SpoilerGate.tsx`, `Stars.tsx`, `LoadMore.tsx`.

## End-to-end: `e2e/`

`e2e/playwright.config.ts`, `e2e/tests/journey.spec.ts` (one full member journey), `e2e/tests/accessibility.spec.ts` (axe checks in every theme), `e2e/tests/accounts.ts` (Mailpit helpers), and `e2e/fake-tmdb/server.mjs` (two fixed films).

## Infrastructure: `infra/`

`main.bicep` (subscription scope, resource group), `resources.bicep` (everything inside it), `main.bicepparam` (reads values from environment variables), `deploy.ps1` (first-time and manual deploys), `setup-github.ps1` (OIDC identity for the Deploy workflow), `sql-users.cs` (creates database users for the managed identities).

## Where to start

### Adding a feature

1. Create `backend/Features/<Feature>/` with the entity, `DTOs/` and controller.
2. Add a `DbSet` and its configuration to `backend/Data/ReelrContext.cs`, then add a migration (see below).
3. If the feature shares rules with existing endpoints, put them in the existing service (`JournalService`, `UserVisibility`, `BlockRules`) or a new one registered in `backend/Program.cs`.
4. Add `tests/backend.Tests/<Feature>/` using `ReelrApiFactory` and `TestUsers`.
5. Add types to `frontend/src/lib/types.ts`, hooks to `frontend/src/lib/queries.ts`, and UI in `pages/` or `components/`.
6. If members create the content, decide whether it belongs in the feed (`DerivedActivityFeed`), the data export (`DataExport`), account deletion (`UserController.DeleteUser`), reports (`ReportTargetType`) and block rules.

### Creating an API endpoint

- Put it in the feature's controller with `[ApiController]`, an `[Http*]` route and `[EndpointSummary]` (used by OpenAPI).
- Add `[Authorize]` or `[Authorize(Policy = AdminAccess.Policy)]`; anonymous is the default.
- Use `User.GetUserId()` behind `[Authorize]`, `User.FindUserId()` on public endpoints.
- For lists, take `[FromQuery] PageQuery page` and return `PageDto<T>` via `PageDto.From(...)`; sort by a date or name, then by an id.
- For TMDB-backed search, add `[EnableRateLimiting(RateLimitPolicies.Search)]`.
- Prefer `Problem(...)`, `ValidationProblem(...)`, `NotFound()` or `Conflict()` without a string so errors stay ProblemDetails (see [TECHNICAL_DEBT.md](TECHNICAL_DEBT.md)).
- Endpoints that read the refresh cookie need `[RequireCsrfHeader]`. Account-changing endpoints may need `[NotForDemoAccounts]`.

### Changing a database entity

1. Change the entity and its configuration in `ReelrContext.OnModelCreating` (lengths, indexes, check constraints, delete behaviour).
2. Run `dotnet ef migrations add <Name> --project backend/backend.csproj` and review the generated file.
3. Update DTOs, `frontend/src/lib/types.ts` and, if relevant, `DataExport`.
4. CI checks `dotnet ef migrations has-pending-model-changes` and applies all migrations to an empty database. Tests use `EnsureCreated`, so they do not exercise migrations.

Mind SQL Server's multiple-cascade-path rule: links from users to likes, comments, follows and blocks are `Restrict`, and `UserController.DeleteUser` deletes those rows in code.

### Adding business logic

- Diary, rating, watched status, review linkage or watchlist: `backend/Features/Diary/JournalService.cs`.
- Who can see what: `backend/Features/Users/UserVisibility.cs`; blocking: `BlockRules.cs`; feed visibility: `DerivedActivityFeed.cs`.
- Storing a film: always call `MovieCatalog.GetOrCreateAsync` before other changes (it saves immediately).

### Integrating an external API

Follow `TmdbService`: a typed HttpClient registered in `Program.cs` with `AddStandardResilienceHandler`, one private method that maps every failure to a single exception type, and an `IExceptionHandler` that maps that exception to ProblemDetails. Add a fake `HttpMessageHandler` to `ReelrApiFactory`. Check secrets at start-up like `TMDB_READ_ACCESS_TOKEN`.

### Modifying the frontend

- Data access only through `api()` in `src/lib/api.ts` and hooks in `src/lib/queries.ts`; invalidate the right query keys after mutations.
- New routes go in `src/main.tsx` as lazy routes.
- Keep accessibility behaviour: focus return (`lib/focus.ts`), tab keyboard handling (`lib/tabs.ts`), page titles (`lib/title.ts`). `src/test/a11y.test.tsx` checks some of it.
- New external resources (scripts, styles, image hosts) also need a change to the Content-Security-Policy in `frontend/security-headers.conf`.
- Run `npm run lint`, `npm test` and `npm run build` (which type-checks).

### Changing authentication or authorization

- Token creation: `TokenService.cs`; validation and revocation: `Program.cs` (`AddJwtBearer`, `OnTokenValidated`) and `SecurityStamps.cs`.
- Refresh cookie and rotation: `RefreshTokens.cs`; client side: `frontend/src/lib/api.ts` and `src/auth/AuthProvider.tsx`.
- Login, lockout and throttling: `AuthController.cs`, Identity options in `Program.cs`, `LoginThrottle.cs`, `RateLimitPolicies.cs`.
- Admin access: `AdminAccess.cs` and the policy in `Program.cs`. Demo restrictions: `DemoAccounts.cs`.
- Run `tests/backend.Tests/Auth/` and the frontend tests; read [SECURITY.md](SECURITY.md) first.

### Fixing a bug

1. Every error response carries `correlationId`; search the logs for it (`CorrelationId` property; nginx logs it as `request_id`).
2. Reproduce with an integration test in the matching `tests/backend.Tests/<Feature>/` folder.
3. For TMDB issues, use `FakeTmdbHandler` to script the failing answer.
4. For UI issues, check the hook in `queries.ts` (cache keys and invalidation) before the component.
