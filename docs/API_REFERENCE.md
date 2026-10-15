# API reference

The Reelr API is a JSON REST API served by `backend/`. Every endpoint below was taken from the controller attributes in `backend/Features/*/*Controller.cs` and checked against the action code. In Development the generated OpenAPI document is at `/openapi/v1.json` and Swagger UI at `/swagger` (`backend/Program.cs`); both are off in other environments.

## Conventions

### Base paths

- `/auth/...` for sign-in. The refresh cookie is scoped to this path.
- `/api/...` for everything else.
- `/health/live` and `/health/ready` for health checks.

In the browser both prefixes are same-origin: Vite (`frontend/vite.config.ts`) and nginx (`frontend/nginx.conf.template`) proxy them to the backend. There is no CORS configuration.

### Authentication

| Label | Meaning |
|---|---|
| Public | No token needed. A valid token may add per-caller fields (`likedByMe`, `isFollowing`, ...). |
| Member | `Authorization: Bearer <access token>` required. Missing or invalid token: 401. |
| Admin | Member whose user id is in `Admin:UserIds` (policy `AdminAccess.Policy`). Otherwise 403. |
| Cookie + CSRF | Uses the `__Secure-reelr-refresh` cookie and requires the `X-Reelr-Csrf` header (any value); without the header: 403. |
| Not demo | Returns 403 for accounts in `Demo:UserIds` (`NotForDemoAccountsAttribute` or an explicit check). |

Access tokens are HMAC-SHA256 JWTs valid for `Jwt:ExpiryMinutes` (15). Claims: `sub` (user id), `name` (username), `sstamp` (security stamp). A token is refused once the user's security stamp changes (password change or reset, rename, "log out everywhere", deletion). See [SECURITY.md](SECURITY.md).

### Paging

Endpoints marked "paged" take `?cursor=&limit=`:

- `limit`: 1 to 100, default 30. Out of range: 400.
- `cursor`: opaque string from the previous page's `nextCursor`. Unreadable: 400.
- Response: `{ "items": [...], "nextCursor": "..." | null }` (`backend/Features/Paging/PageDto.cs`).

The activity feed is different: `limit` defaults to 20 and is clamped to 1 to 50 (no 400), and it uses its own cursor format (`ActivityCursor`).

### Errors

- Most errors are `application/problem+json` (RFC 7807) with `status`, `title` and a `correlationId` extension matching the `X-Correlation-Id` response header.
- Model validation failures are 400 `ValidationProblem` with an `errors` object (field name to messages). Identity errors use the Identity error code as the key.
- Some 400 and 409 responses are a **plain JSON string** instead of ProblemDetails, where a controller calls `BadRequest("...")` or `Conflict("...")`. The SPA handles both (`readError` in `frontend/src/lib/api.ts`). They are marked "(string)" below.
- Unique-key races return 409 ProblemDetails with title "This already exists." (`UniqueConflictExceptionHandler`).
- TMDB failures return 502 (TMDB error or unreadable answer) or 503 (timeout or circuit open) with title "Film data is unavailable." (`TmdbExceptionHandler`).
- Rate limits return 429 with `Retry-After`.

### Rate limits

Defaults from `backend/Features/Auth/RateLimitPolicies.cs`; each can be overridden under `RateLimiting:<Name>:PermitLimit` and `WindowSeconds`.

| Policy | Applies to | Default | Partition |
|---|---|---|---|
| Read (global) | Every GET, HEAD, OPTIONS except `/health` | 300 per 60 s | user id, else IP |
| Write (global) | Every other method | 60 per 60 s | user id, else IP |
| `search` | Endpoints marked "search limit" | 30 per 60 s, on top of Read | user id, else IP |
| `auth-login` | login, confirm-email, reset-password | 10 per 60 s | IP |
| `auth-register` | register | 5 per 3600 s | IP |
| `auth-account-email` | resend-confirmation, forgot-password | 5 per 900 s | IP |
| `page-views` | `POST /api/analytics/page-views`, instead of Write | 120 per 60 s | user id, else IP |

Login is additionally throttled per account and IP by `LoginThrottle` (5 failures per 900 s), and a single email address receives at most 3 account emails per hour (`AccountEmails`).

### Types

Dates such as `watchedOn` are `YYYY-MM-DD`. Ratings are decimals from 0.5 to 5 in steps of 0.5. Enums are strings: `WatchlistVisibility` and `DiaryVisibility` are `Public`, `Followers`, `Private`; report types, reasons and outcomes are listed under [Moderation](#moderation).

## Auth (`AuthController`, route `auth`)

| Method and path | Auth | Request | Success | Errors |
|---|---|---|---|---|
| `POST /auth/register` | Public, `auth-register` | `{ username (3-50), email, password (min 8, Identity rules), inviteCode? (max 64), acceptTerms: true }` | 202 `{ message }` | 400 validation (password rules, taken username, invite invalid or used) |
| `POST /auth/confirm-email` | Public, `auth-login` | `{ userId, token }` | 204 | 400 "This link is invalid or has expired." |
| `POST /auth/resend-confirmation` | Public, `auth-account-email` | `{ email }` | 202 `{ message }` (always) | 400 validation |
| `POST /auth/forgot-password` | Public, `auth-account-email` | `{ email }` | 202 `{ message }` (always; no email for demo accounts) | 400 validation |
| `POST /auth/reset-password` | Public, `auth-login` | `{ userId, token, newPassword }` | 204; also confirms the email and clears lockout | 400 invalid link, or Identity password errors |
| `POST /auth/login` | Public, `auth-login` | `{ userInput (email or username), password }` | 200 `{ token }` and sets the refresh cookie | 401 wrong or unknown credentials or locked out (same answer); 403 `type: "email-not-confirmed"`; 429 throttled |
| `POST /auth/refresh` | Cookie + CSRF | none | 200 `{ token }`, rotated cookie | 401 missing, expired, revoked or reused cookie (cookie cleared); 403 missing header |
| `POST /auth/logout` | Cookie + CSRF | none | 204, cookie cleared, that login's token family revoked | 403 missing header |

Notes:

- Registration answers 202 with the same message whether the email is new or taken; a taken email gets an "already registered" email instead (`AccountEmails.SendAlreadyRegistered`).
- When `Registration:InviteOnly` is true (default outside Development), `inviteCode` must be an unused, unrevoked code; the claim and account creation share one transaction.
- Confirmation and reset links point to the SPA (`App:BaseUrl`) with `#userId=...&token=...`; tokens expire after 3 hours.
- The refresh cookie is `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/auth`, valid `Jwt:RefreshDays` (30) days. Reusing a cookie that was already rotated more than 20 seconds ago revokes the whole login.

## Films, people and studios (TMDB proxies)

These endpoints are public and read from TMDB through `TmdbService` (cached in memory). Responses are TMDB-shaped DTOs (`backend/Features/Movies/DTOs`, `backend/Features/People/DTOs`).

| Method and path | Auth | Parameters | Success | Errors |
|---|---|---|---|---|
| `GET /api/movies/{tmdbId}` | Public | | 200 `TmdbMovieDto` | 404, 502/503 |
| `GET /api/movies/{tmdbId}/details` | Public | | 200 `MovieDetailsDto`: film plus top 10 cast, selected crew jobs, YouTube trailers and teasers, up to 20 images | 400 (string) if id <= 0, 404, 502/503 |
| `GET /api/movies/{tmdbId}/credits` | Public | | 200 `TmdbCreditsDto` | 404, 502/503 |
| `GET /api/movies/{tmdbId}/recommended` | Public | | 200 `TmdbSearchResultDto` | 400 (string), 404, 502/503 |
| `GET /api/movies/{tmdbId}/similar` | Public | | 200 `TmdbSearchResultDto` | 400 (string), 404, 502/503 |
| `GET /api/movies/trending` | Public | | 200 `TmdbSearchResultDto` (TMDB weekly trending) | 404, 502/503 |
| `GET /api/movies/popular` | Public | | 200 `TmdbSearchResultDto` | 404, 502/503 |
| `GET /api/movies/search` | Public, search limit | `query` (max 100) or any of `castId`, `crewId`, `studioId`, `genreId`; `page` 1-500 | 200 `TmdbSearchResultDto` (title search, else TMDB discover) | 400 (string) if no filter |
| `GET /api/movies/search/all` | Public, search limit | `query` (required, max 100) | 200 `MovieSearchResultDto` `{ page, movies, cast, crew, studios }`, exact matches first | 400 (string) |
| `GET /api/people/{personId}` | Public | | 200 `PersonDetailsDto` with merged credits and up to 8 "known for" films | 400 (string), 404, 502/503 |
| `GET /api/people/search` | Public, search limit | `query` (required, max 100) | 200 `TmdbPersonSearchResultDto` | 400 (string) |
| `GET /api/companies/search` | Public, search limit | `query` (required, max 100) | 200 `TmdbCompanySearchResultDto` | 400 (string) |

`MovieDetailsDto` degrades gracefully: if credits, videos or images fail, those parts are empty; only the main film call failing returns 502/503.

## Journal: diary, watched status, rating, watchlist

All write endpoints here store the film locally first (`MovieCatalog`); an unknown TMDB id returns 404. The journal rules in [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md#journal-rules-backendfeaturesdiaryjournalservicecs) apply.

### Diary (`DiaryController`)

| Method and path | Auth | Request | Success | Errors |
|---|---|---|---|---|
| `GET /api/watched` | Member, paged | | 200 page of `DiaryEntryDto` (newest watch date first) | |
| `GET /api/watched/films` | Member | | 200 `int[]` of watched TMDB ids | |
| `GET /api/users/{userId}/diary` | Public, paged | | 200 page of `DiaryEntryDto` | 404 if the user is missing or their diary visibility hides it |
| `POST /api/movies/{tmdbId}/diary` | Member | `LogDiaryEntryDto { score?, liked?, rewatched, watchedOn?, review? (max 5000), containsSpoilers? }` | 200 `DiaryEntryDto` | 400 (string) watch date in the future or before 1888; 400 validation; 404 |
| `PUT /api/diary/{entryId}` | Member (owner) | `UpdateDiaryEntryDto { watchedOn (required), rating?, liked?, rewatched }` | 200 `DiaryEntryDto` | 400; 404 if not the caller's entry |
| `DELETE /api/diary/{entryId}` | Member (owner) | | 204 (its review is deleted; last entry removes watched status) | 404 |

`DiaryEntryDto`: `{ id, tmdbId, title, posterUrl, rating, liked, rewatched, hasReview, watchedAt }`.

### Watched status (`StatusController`, route `api/movies/{tmdbId}/status`)

| Method | Auth | Request | Success | Errors |
|---|---|---|---|---|
| `GET` | Member | | 200 `StatusDto { tmdbId, liked, rewatched, watchedAt }` | 404 if not watched |
| `POST` | Member | `{ liked?, rewatched }` | 200 `StatusDto`; logs a diary entry, removes from watchlist | 409 (string) already watched; 404 |
| `PUT` | Member | `{ liked?, rewatched }` | 200 `StatusDto`; logs a diary entry | 404 |
| `DELETE` | Member | | 204; deletes every diary entry for the film and their reviews (rating stays) | 404 |

### Rating (`RatingController`, route `api/movies/{tmdbId}/rating`)

| Method | Auth | Request | Success | Errors |
|---|---|---|---|---|
| `GET` | Member | | 200 `RatingDto { tmdbId, score }` | 404 |
| `POST` | Member | `{ score }` | 200 `RatingDto`; marks watched, logs a diary entry, removes from watchlist | 400 validation; 409 (string) already rated; 404 |
| `PUT` | Member | `{ score }` | 200 `RatingDto`; also logs a diary entry | 400; 404 if not rated yet |
| `DELETE` | Member | | 204 (diary entries keep their snapshot rating) | 404 |

### Watchlist (`WatchlistController`, route `api/watchlist`)

| Method and path | Auth | Success | Errors |
|---|---|---|---|
| `GET /api/watchlist` | Member, paged | 200 page of `WatchlistEntryDto { tmdbId, title, posterUrl, addedAt }` | |
| `GET /api/watchlist/{tmdbId}` | Member | 200 `{ onWatchlist }` | |
| `POST /api/watchlist/{tmdbId}` | Member | 200 (empty body) | 409 (string) already on watchlist; 404 |
| `DELETE /api/watchlist/{tmdbId}` | Member | 204 | 404 |
| `GET /api/users/{userId}/watchlist` | Public, paged | 200 page of `WatchlistEntryDto` | 404 if missing or hidden by visibility |

## Reviews, comments and likes

### Reviews (`ReviewController`)

| Method and path | Auth | Request | Success | Errors |
|---|---|---|---|---|
| `GET /api/movies/{tmdbId}/reviews` | Public, paged | | 200 `MovieReviewPageDto { items, nextCursor, totalCount }`, newest first; excludes authors the caller blocked | |
| `GET /api/movies/{tmdbId}/reviews/mine` | Member | | 200 `ReviewDto`, or 204 if none | |
| `GET /api/users/{userId}/reviews` | Public, paged | | 200 page of `ReviewDto` | |
| `GET /api/reviews/{id}` | Public | | 200 `ReviewDto` | 404 |
| `POST /api/movies/{tmdbId}/reviews` | Member | `{ text (1-5000), containsSpoilers }` | 200 `ReviewDto` (standalone review, no diary entry) | 409 (string) already reviewed; 404 |
| `PUT /api/reviews/{id}` | Member (author) | `{ text?, containsSpoilers? }` (null fields unchanged) | 200 `ReviewDto` | 400 blank text; 404 if not the author's |
| `DELETE /api/reviews/{id}` | Member (author) | | 204 (likes and comments cascade) | 404 |

`ReviewDto`: `{ id, userId, username, profileImageUrl, tmdbId, title, posterUrl, text, containsSpoilers, likeCount, commentCount, likedByMe, score, watchedAt, createdAt, updatedAt }`. `score` is the author's current rating of the film; `watchedAt` comes from the diary entry that wrote the review, if any.

### Comments (`ReviewCommentController`)

| Method and path | Auth | Request | Success | Errors |
|---|---|---|---|---|
| `GET /api/reviews/{reviewId}/comments` | Public, paged | | 200 page of `ReviewCommentDto`, oldest first; excludes authors the caller blocked | 404 review missing |
| `POST /api/reviews/{reviewId}/comments` | Member | `{ text (1-1000) }` | 200 `ReviewCommentDto` | 403 if the review author blocked the caller; 404 |
| `PUT /api/comments/{id}` | Member (comment author) | `{ text }` | 200 `ReviewCommentDto` | 403 not the author; 404 |
| `DELETE /api/comments/{id}` | Member (comment or review author) | | 204 | 403; 404 |

### Likes (`ReviewLikeController`, route `api/reviews/{reviewId}`)

| Method and path | Auth | Success | Errors |
|---|---|---|---|
| `GET .../likes` | Public, paged | 200 page of `UserSummaryDto { id, userName, profileImageUrl }`, newest first | 404 |
| `PUT .../like` | Member | 200 `{ likeCount, likedByMe: true }` (idempotent) | 400 (string) own review; 403 author blocked caller; 404 |
| `DELETE .../like` | Member | 200 `{ likeCount, likedByMe: false }` (idempotent) | 404 |

## Lists (`MovieListController`, route `api/lists`)

| Method and path | Auth | Request | Success | Errors |
|---|---|---|---|---|
| `GET /api/lists` | Member, paged | | 200 page of `MovieListSummaryDto` (caller's lists, newest first, with up to 3 newest films) | |
| `GET /api/users/{userId}/lists` | Public, paged | | 200 page of `MovieListSummaryDto` (private lists only for the owner) | 404 user missing |
| `GET /api/lists/containing/{tmdbId}` | Member | | 200 `int[]` of the caller's list ids containing the film | |
| `GET /api/lists/{id}` | Public | | 200 `MovieListDto` with `movieCount`, `topMovies`, and `watchedCount` for a signed-in caller | 404 missing or private and not owner |
| `GET /api/lists/{id}/movies` | Public, paged | | 200 page of `MovieListItemDto` (newest addition first) | 404 |
| `POST /api/lists` | Member | `{ name (1-100), description? (max 1000), isPublic (default true) }` | 201 `MovieListDto`, `Location` header | 400 |
| `PUT /api/lists/{id}` | Member (owner) | `{ name?, description?, isPublic? }` | 200 `MovieListDto` | 400; 404 |
| `DELETE /api/lists/{id}` | Member (owner) | | 204 | 404 |
| `POST /api/lists/{id}/movies` | Member (owner) | `{ tmdbId }` | 204 | 409 (string) already in list; 404 |
| `DELETE /api/lists/{id}/movies/{tmdbId}` | Member (owner) | | 204 | 404 |

## Users, follows and blocks (`UserController`, route `api/users`)

| Method and path | Auth | Request | Success | Errors |
|---|---|---|---|---|
| `GET /api/users/{id}` | Public | | 200 `UserProfileDto { id, userName, profileImageUrl, followerCount, followingCount, isFollowing, isBlocked, watchlistVisibility, canSeeWatchlist, diaryVisibility, canSeeDiary }` | 404 |
| `GET /api/users/{id}/avatar` | Public | | 200 image bytes (`image/webp`), `Cache-Control: public, max-age=31536000, immutable` | 404 |
| `GET /api/users/search` | Public, search limit | `query` (required, max 50) | 200 up to 20 `UserSummaryDto`, prefix matches first | 400 (string) |
| `GET /api/users/{id}/followers` | Public, paged | | 200 page of `UserSummaryDto` by username | 404 |
| `GET /api/users/{id}/following` | Public, paged | | 200 page of `UserSummaryDto` by username | 404 |
| `POST /api/users/{id}/follow` | Member | | 204 | 400 (string) self; 403 blocked either way; 404; 409 (string) already following |
| `DELETE /api/users/{id}/follow` | Member | | 204 | 404 not following |
| `PUT /api/users/{id}/block` | Member | | 204 (idempotent; ends follows both ways) | 400 (string) self; 404 |
| `DELETE /api/users/{id}/block` | Member | | 204 (idempotent) | |
| `DELETE /api/users/{id}` | Member, not demo, own id only | `{ password }` in the body | 204 | 400 wrong password (counts towards lockout); 403 other user or demo |

## Settings (`SettingsController`, route `api/settings`)

All endpoints act on the caller and need a Member token. If the token belongs to a deleted user, they return 401.

| Method and path | Extra auth | Request | Success | Errors |
|---|---|---|---|---|
| `GET /api/settings` | | | 200 `UserSettingsDto { userName, email, profileImageUrl, watchlistVisibility, diaryVisibility, showFriendReviews, showOwnActivity, isAdmin, isDemo }` | |
| `PATCH /api/settings` | Rename not demo | `{ userName?, watchlistVisibility?, diaryVisibility?, showFriendReviews?, showOwnActivity? }` | 200 `UpdatedSettingsDto` (adds `token` after a rename, plus a new refresh cookie) | 400 validation or taken name; 403 demo rename |
| `POST /api/settings/password` | Not demo | `{ currentPassword, newPassword }` | 200 `{ token }` and a new refresh cookie; other devices are signed out | 400 wrong current password (counts towards lockout) or weak new password |
| `GET /api/settings/export` | | | 200 JSON file download `reelr-<username>-<date>.json` | |
| `GET /api/settings/blocked` | | | 200 `UserSummaryDto[]` | |
| `POST /api/settings/logout-everywhere` | Not demo | | 204; every token and refresh cookie stops working | |
| `PUT /api/settings/avatar` | Not demo | `multipart/form-data` field `file` (JPEG, PNG or WebP, at most 1 MB) | 200 `UserSettingsDto` | 400 (string) too large, wrong type or unreadable |
| `DELETE /api/settings/avatar` | Not demo | | 200 `UserSettingsDto` | |

## Activity feed (`ActivityController`, route `api/feed`)

| Method and path | Auth | Parameters | Success | Errors |
|---|---|---|---|---|
| `GET /api/feed/following` | Member | `cursor`, `limit` (default 20, clamped 1-50), `types` (comma list), `includeOwn` (defaults to the `ShowOwnActivity` setting) | 200 `ActivityPageDto { items, nextCursor }` | 400 (string) invalid cursor |
| `GET /api/feed/community` | Public | `cursor`, `limit`, `types` | 200 `ActivityPageDto`; first page cached 60 s per viewer, and for logged-out visitors until the database is next used | 400 (string) invalid cursor |
| `GET /api/feed/following/films` | Member | `limit` (default 6, clamped 1-24) | 200 `FollowingFilmDto[]` | |

`types` values: `watched`, `reviewed`, `listCreated`, `listAdded`, `watchlistAdded`; unknown values are ignored. The Community feed only produces `watched`, `reviewed` and `listCreated`. `ActivityItemDto` fields: `id` (for example `diary:123`, `listitem:4:56`), `type`, `occurredAt`, `actor`, `movie?`, `list?`, `review?` (excerpt up to 280 characters, empty for spoilers), `rating`, `liked`, `rewatched`, `watchedAt`, `groupCount`, `groupMovies`.

## Moderation

### Reports (`ReportController`)

| Method and path | Auth | Request | Success | Errors |
|---|---|---|---|---|
| `POST /api/reports` | Member | `{ targetType: "Review" \| "Comment" \| "List", targetId, reason: "Spam" \| "Harassment" \| "UnmarkedSpoilers" \| "Inappropriate" \| "Other", note? (max 500) }` | 204 | 400 (string) own content; 400 validation; 404 item missing or a private list; 409 already reported (open) |

### Admin (`AdminController`, route `api/admin`)

| Method and path | Auth | Request | Success | Errors |
|---|---|---|---|---|
| `GET /api/admin/reports` | Admin | | 200 `ReportedItemDto[]`: each item with open reports, grouped, most reported first, with author, text preview (600 characters), reasons and notes | 403 |
| `POST /api/admin/reports/resolve` | Admin | `{ targetType, targetId, outcome: "Dismissed" \| "Removed" }` | 204; `Removed` deletes the item | 400; 403; 404 no open reports |

### Invites (`InvitesController`, route `api/admin/invites`)

| Method and path | Auth | Request | Success | Errors |
|---|---|---|---|---|
| `GET /api/admin/invites` | Admin | | 200 `InviteDto[] { id, note, createdAt, usedAt, usedBy, revokedAt }`, newest first | 403 |
| `POST /api/admin/invites` | Admin | `{ note? (max 100) }` | 201 `CreatedInviteDto` with the one-time `code` and a `link` to `/register?invite=...` | 403 |
| `DELETE /api/admin/invites/{id}` | Admin | | 204 (also when already revoked) | 403; 404; 409 already used |

## Page views (`AnalyticsController`)

Counted per route and UTC day, with nothing that identifies the visitor. The SPA sends one for each page it shows (`frontend/src/lib/analytics.ts`), except when the browser sends Do Not Track or Global Privacy Control.

| Method and path | Auth | Request | Success | Errors |
|---|---|---|---|---|
| `POST /api/analytics/page-views` | Anonymous | `{ route }`, an SPA route such as `/movie/:tmdbId` (`PageViewRoutes`; others are ignored) | 204 | 400 no route; 429 (`page-views`) |
| `GET /api/admin/page-views?days=30` | Admin | `days` 1 to 365 | 200 `PageViewStatsDto { days, totalViews, perDay: [{ day, views }], perRoute: [{ route, views }] }` | 400; 403 |

## Health

| Path | Response |
|---|---|
| `GET /health/live` | 200 `{ status, checks: {} }` while the process runs. No rate limit. |
| `GET /health/ready` | `{ status, checks: { database, tmdb } }`. 503 when the database check fails; TMDB down is `Degraded` with 200. The TMDB result is cached for 1 minute. |

## External API dependency

Every endpoint in [Films, people and studios](#films-people-and-studios-tmdb-proxies), and every journal, review or list write for a film that is not stored yet, depends on TMDB. See [INTEGRATIONS.md](INTEGRATIONS.md#tmdb).

## Other clients

`Reelr.postman_collection.json` and `Reelr.postman_environment.json` in the repository root cover auth (register, login), users, films, watchlist, status, rating, reviews, comments, likes, companies and people. They do not cover diary, lists, feed, settings, reports, admin, invites, blocks or refresh and logout.
