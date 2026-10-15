# External integrations

Reelr depends on a small set of external systems. This document covers, for each one, why it is used, where the code is, how it authenticates, how failures are handled and what configuration it needs. No credential values appear here.

| System | Used for | Required to start? |
|---|---|---|
| [TMDB](#tmdb) | All film, person and studio data | Token required; TMDB itself may be down |
| [SMTP server](#smtp-email) | Account emails | Host and sender required; the server may be down |
| [SQL Server or Azure SQL](#sql-server-and-azure-sql) | All persistent state | Yes |
| [Azure platform](#azure-hosting) | Production hosting, secrets, logs, alerts | Production only |
| [GitHub](#github-actions-container-registry-and-dependabot) | CI, image registry, deploy identity, dependency updates | Delivery only |
| [TMDB image CDN](#tmdb-image-cdn) | Posters and photos in the browser | No |

## TMDB

### Purpose

TMDB (The Movie Database) is the source of truth for films, people and studios: details, credits, trailers, images, trending and popular lists, search and discovery. Reelr stores a slim copy of a film only when a member interacts with it (see [DATA_MODEL.md](DATA_MODEL.md)).

### Implementation

| File | Role |
|---|---|
| `backend/Features/Movies/TmdbService.cs` | Typed HttpClient with one method per TMDB call and a single private `GetFromTmdb<T>` that every call goes through. |
| `backend/Features/Movies/TmdbCache.cs` | In-memory cache with its own size limit, counted in characters of TMDB JSON. |
| `backend/Features/Movies/TmdbUnavailableException.cs`, `TmdbExceptionHandler.cs` | One exception type for every failure, mapped to 502 or 503 ProblemDetails. |
| `backend/Features/Movies/MovieCatalog.cs` | Fetches and stores a film on first use; refreshes stale films. |
| `backend/Features/Movies/MovieRefresher.cs` | Background service that calls `MovieCatalog.RefreshStaleAsync`. |
| `backend/Features/Observability/HealthChecks.cs` | `TmdbHealthCheck` pings TMDB's `configuration` endpoint for `/health/ready`. |
| `backend/Features/Movies/DTOs/Tmdb*.cs`, `backend/Features/People/DTOs/TmdbPersonDetailsDto.cs` | Deserialisation targets (case-insensitive JSON). |

TMDB paths used: `movie/{id}`, `movie/{id}/credits`, `/videos`, `/images`, `/recommendations`, `/similar`, `search/movie`, `search/person`, `search/company`, `discover/movie`, `trending/movie/week`, `movie/popular`, `person/{id}?append_to_response=movie_credits,external_ids`, and `configuration` (health). All except images and company search add `language=en-US`.

### Authentication

A TMDB API Read Access Token sent as `Authorization: Bearer <token>` on each request (`GetFromTmdb`, `PingAsync`). The token comes from configuration key `TMDB_READ_ACCESS_TOKEN`: user secrets locally, `.env` in Docker Compose, Key Vault secret `tmdb-token` in Azure. The app refuses to start without it (`backend/Program.cs`).

### Request and response handling

- Base address: `Tmdb:BaseUrl`, default `https://api.themoviedb.org/3/`. A trailing `/` is added if missing, because paths are relative (`TmdbBaseUrlTests` checks this).
- Successful answers are cached by path: lists 30 minutes, searches 10 minutes, details 6 hours. Cache size defaults to 50 MB (`Tmdb:CacheSizeMegabytes`, 0 disables it). 404 answers are not cached.
- Calls that may legitimately 404 (a film, person, credits, recommendations, similar) return null, which controllers turn into 404.
- `GetMovieDetails` runs credits, videos and images in parallel; if any of those fail it returns the film without them.
- `SearchAll` runs film, person and company searches in parallel, splits people into cast and crew by `known_for_department`, and moves an exact name match to the top of each group.
- Logged paths drop the query string so search text does not reach logs. TMDB's error body is logged, never returned to clients.

### Error handling and retries

`AddStandardResilienceHandler` in `backend/Program.cs` with overrides:

| Setting | Value |
|---|---|
| Attempt timeout | 4 s |
| Total request timeout | 10 s |
| Retries | 2 (library default backoff) |
| Circuit breaker | Library defaults |

Any section under `Tmdb:Resilience` overrides these (the tests use it). Failures map as follows:

| Failure | Result |
|---|---|
| Non-success status (other than an allowed 404) | `TmdbUnavailableException` 502 |
| `HttpRequestException` | 502 |
| Timeout or circuit breaker open (`TaskCanceledException`, `ExecutionRejectedException`) | 503 |
| Unreadable JSON | 502 |

`TmdbResilienceTests` covers these paths.

### Limitations and failure scenarios

- When TMDB is down, browsing films and searching fail with 502 or 503. Pages that read stored data (diary, reviews, lists, feeds, profiles) keep working, and `/health/ready` reports `Degraded` but stays 200.
- Logging, rating or listing a film that is not stored yet also fails, because `MovieCatalog` must fetch it first.
- `MovieRefresher` stops a run after three failures in a row and tries again next interval. A film TMDB no longer knows keeps its stored data.
- TMDB's own rate limits are not handled explicitly beyond retries and caching. The `search` rate-limit policy (30 per minute per client) exists because each search calls TMDB.
- The cache is per process.
- Attribution: the root README carries the required TMDB notice.

### Configuration

| Key | Default | Purpose |
|---|---|---|
| `TMDB_READ_ACCESS_TOKEN` | none (required) | Bearer token |
| `Tmdb:BaseUrl` | `https://api.themoviedb.org/3/` | Overridden to the fake TMDB in end-to-end runs |
| `Tmdb:CacheSizeMegabytes` | 50 | Cache size; 0 turns it off |
| `Tmdb:Resilience:*` | see above | Resilience overrides |
| `MovieRefresh:Enabled`, `IntervalHours`, `MaxAgeDays`, `BatchSize` | true, 6, 30, 100 | Refresh job |

### Test doubles

- `tests/backend.Tests/Infrastructure/FakeTmdbHandler.cs` replaces the HTTP handler in integration tests.
- `e2e/fake-tmdb/server.mjs` is a Node HTTP server with two fixed films for the Docker end-to-end stack, wired in through `Tmdb__BaseUrl` in `docker-compose.e2e.yml`.

## TMDB image CDN

The browser builds image URLs itself (`https://image.tmdb.org/t/p/<size><path>` in `frontend/src/lib/tmdb.ts`) from the poster and profile paths the API returns. The nginx Content-Security-Policy allows `img-src https://image.tmdb.org` (`frontend/security-headers.conf`). Visitors' browsers therefore contact TMDB's CDN directly; the backend does not proxy images.

## SMTP email

### Purpose

Account emails only: email confirmation, password reset, and "you already have an account" (`backend/Features/Auth/AccountEmails.cs`). There are no notification emails.

### Implementation

| File | Role |
|---|---|
| `AccountEmails.cs` | Composes plain-text messages. Links go to `App:BaseUrl` with the user id and token after `#`, so they do not reach server logs or `Referer` headers. At most 3 emails per address per hour (in-memory counter). |
| `backend/Features/Email/EmailQueue.cs` | Bounded in-memory `Channel` (capacity 1000, drops new messages when full). |
| `backend/Features/Email/EmailDispatcher.cs` | Hosted service that sends queued messages one at a time. |
| `backend/Features/Email/SmtpEmailSender.cs` | MailKit `SmtpClient`, 15 s timeout, optional authentication. |
| `backend/Features/Email/IEmailSender.cs` | Seam for tests (`FakeEmailSender`). |

### Authentication

Username and password from `Email:UserName` and `Email:Password` when a username is set; none for Mailpit. In Azure the password is Key Vault secret `email-password`, and `infra/main.bicep` defaults the host to `smtp-relay.brevo.com`.

### Error handling

- Requests enqueue and return immediately, so response time does not reveal whether an account exists and a slow mail server does not slow requests.
- Each message is tried up to four times (immediately, then after 2, 10 and 30 seconds). After that it is logged as an error (subject and recipient domain only) and dropped.
- Queued messages are lost if the process stops. The user can request the email again.

### Configuration

| Key | Default | Notes |
|---|---|---|
| `Email:Host` | none (required) | `localhost` in Development, `mailpit` in Compose |
| `Email:Port` | 587 (`appsettings.json`) | 1025 for Mailpit |
| `Email:Security` | `StartTls` | MailKit `SecureSocketOptions`; `None` for Mailpit |
| `Email:UserName`, `Email:Password` | empty | |
| `Email:FromAddress` | none (required) | |
| `Email:FromName` | `Reelr` | |
| `App:BaseUrl` | none (required) | Site address used in links |

Mailpit (`axllent/mailpit:v1.31.2`) catches mail in Docker Compose (web UI on port 8025). The end-to-end tests read confirmation emails through Mailpit's API (`e2e/tests/accounts.ts`).

## SQL Server and Azure SQL

### Purpose

All persistent state, including Identity, refresh tokens and the Data Protection key ring.

### Implementation and authentication

- EF Core SQL Server provider, connection string `ConnectionStrings:ReelrContext`.
- Local development: `.\SQLEXPRESS` with Windows authentication (`backend/appsettings.Development.json`). Docker Compose: SQL Server 2022 container with the `sa` login from `.env`.
- Azure: Entra-only authentication. The backend and the migration job connect with user-assigned managed identities (`Authentication=Active Directory Managed Identity` in the connection string built in `infra/resources.bicep`); no SQL passwords exist. `infra/sql-users.cs` gives the backend identity `db_datareader` and `db_datawriter` and the migration identity `db_owner`.

### Resilience

`backend/Data/OpenRetryInterceptor.cs` retries opening a connection up to 6 times with exponential backoff (2 to 20 seconds) for Azure transient errors such as 40613 (a paused serverless database waking up). It deliberately excludes error 4060 so EF can still detect a missing database. The Azure connection string uses a 90-second connect timeout.

### Keeping the paused database asleep

Every wake-up of the serverless database uses part of its monthly free amount (about 900 vCore seconds for a short visit, measured in October 2026), so the backend avoids waking it for work that can wait:

- `DatabaseActivityInterceptor` reports every connection opened or closed to `DatabaseActivity`.
- `IdleConnectionCloser` clears the SqlClient pools 60 seconds after the last use (`Database:CloseIdleConnectionsAfterSeconds`). Pooled connections are sessions, and the database only starts counting down to its pause once none are left; the pool alone would keep them open for 4 to 8 minutes. Just before closing, it runs the `IBeforeDatabaseIdle` work.
- `MovieRefresher` waits for the next database use, not for any request, so scanners asking for paths such as `/api/.env` do not wake the database.
- Logged-out visitors get the community feed from `AnonymousCommunityPages`, which stays valid until the database is used again and is read again before it goes idle (`AnonymousCommunityRefresh`).

### Limitations

- The Azure database is serverless with auto-pause after 60 minutes and the free offer, which pauses until next month if the free amount runs out (`infra/resources.bicep`). The first request after a pause can take up to a minute.
- The Azure SQL server allows connections from all Azure services (`AllowAllWindowsAzureIps`), because Container Apps have no fixed outbound address. Access still requires an Entra identity with a database user.
- Integration tests need a real SQL Server; see [TESTING.md](TESTING.md).

## Azure hosting

Defined in `infra/main.bicep` and `infra/resources.bicep`, deployed by `infra/deploy.ps1` or the Deploy workflow.

| Resource | Use |
|---|---|
| Resource group `rg-reelr`, Sweden Central | Holds everything |
| User-assigned identities `id-reelr-backend`, `id-reelr-migrate` | Database sign-in; backend also reads Key Vault |
| Key Vault `kv-reelr-<suffix>` | `jwt-key`, `tmdb-token`, `email-password`, referenced by the backend container app |
| Azure SQL server and database | See above |
| Log Analytics `log-reelr` | Container logs, 30-day retention |
| Container Apps environment `cae-reelr` | Hosts `reelr` (frontend, external), `backend` (internal) and job `reelr-migrate` |
| Managed certificate `mc-reelr-domain` (optional) | With `customDomain` set (`-CustomDomain` or variable `REELR_CUSTOM_DOMAIN`), binds the site's own domain to the frontend app with a free certificate Azure renews. `App:BaseUrl` then uses that domain. The root README names the beta domain and its Cloudflare DNS records. |
| Action group and metric alerts (optional) | Email when the backend returns more than 5 server errors in 15 minutes, restarts more than twice in 30 minutes, or the database has less than 20 % of its monthly free amount left |

Backend settings set by the template include `ASPNETCORE_ENVIRONMENT=Production`, `ForwardedHeaders__ForwardLimit=3`, private-range `ForwardedHeaders__KnownNetworks__*`, `Registration__InviteOnly`, `Admin__UserIds__*` and `Demo__UserIds__*`. Liveness and startup probes use `/health/live`, not `/health/ready`, so probes do not keep the database awake.

## GitHub Actions, Container Registry and Dependabot

- **CI** (`.github/workflows/ci.yml`): on pushes and pull requests to `main` and `develop`. Jobs: backend build, tests and migration checks against a throwaway SQL Server container started with `docker run`; frontend lint, test and build; end-to-end tests against the Docker stack. The SQL password is generated per run, so CI needs no repository secrets and also runs for Dependabot and fork pull requests. Actions are pinned to commit SHAs.
- **Deploy** (`.github/workflows/deploy.yml`): after a successful CI run on `main`, or manually. Builds and pushes `reelr-backend`, `reelr-migrate` and `reelr-frontend` to `ghcr.io/<owner>/` tagged with the commit SHA and `latest`, runs the migration job, then deploys `infra/main.bicep`. Signs in to Azure with OIDC (`azure/login` with `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` repository variables; no stored keys). `infra/setup-github.ps1` creates that identity. The root README notes the images must be public so Azure can pull them.
- **Dependabot** (`.github/dependabot.yml`): weekly updates against `develop` for GitHub Actions, NuGet (backend and tests), npm (frontend and e2e) and Docker base images. Major updates of `@types/node` (follows Node 24) and `typescript` (until typescript-eslint supports 7) are ignored.
