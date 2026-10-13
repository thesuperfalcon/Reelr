# Project overview

This document describes what Reelr is for, the concepts it works with, how members use it and the rules it enforces. For how it is built, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Purpose

Reelr is a social film diary. A member keeps a personal record of the films they watch (when, how they rated it, whether they liked it, whether it was a rewatch), writes reviews, keeps a watchlist and lists, and follows other members to see their activity.

The repository root [README.md](../README.md) states the goal as "a social film diary, in the spirit of Letterboxd". The code and the legal pages show the current scope is a closed beta: registration needs an invite code from an admin, and shared demo accounts exist for presenting the app (see [Deployment context](#deployment-context)).

## Domain concepts

| Concept | Meaning in Reelr | Where it lives |
|---|---|---|
| Film (Movie) | A TMDB film. Reelr stores a local copy only once a member interacts with it. Every public URL and API route uses the TMDB id, never the local id. | `backend/Features/Movies/Movie.cs`, `MovieCatalog.cs` |
| Watched status | The member's current "I have seen this" state for a film: last watch date, liked, rewatched. One per member and film. | `backend/Features/WatchedMovies/WatchedMovie.cs` |
| Rating | The member's current score for a film, 0.5 to 5 in half steps. One per member and film. | `backend/Features/Ratings/Rating.cs` |
| Diary entry | One logged viewing. It copies the rating, liked and rewatched values at the moment of logging, so later changes add entries instead of rewriting history. | `backend/Features/Diary/DiaryEntry.cs` |
| Review | Markdown text about a film, at most 5000 characters, with an optional spoiler flag. One per member and film. It can belong to the diary entry that wrote it. | `backend/Features/Reviews/Review.cs` |
| Watchlist | Films a member wants to see. | `backend/Features/WatchlistItems/WatchlistItem.cs` |
| List | A named, public or private collection of films. | `backend/Features/MovieLists/` |
| Follow | A one-way subscription to another member's activity. | `backend/Features/Users/Follow.cs` |
| Block | One member blocking another. | `backend/Features/Users/Block.cs` |
| Activity feed | A computed timeline of logs, reviews, list changes and watchlist additions. | `backend/Features/Activity/` |
| Report | A member flagging a review, comment or list for an admin. | `backend/Features/Moderation/Report.cs` |
| Invite | A one-time registration code made by an admin. | `backend/Features/Invites/` |
| Admin, demo account | Roles assigned by user id in configuration, not in the database. | `AdminAccess.cs`, `DemoAccounts.cs` |

## User workflows

### Visitor (not signed in)

- Browse trending and popular films, search films, people, studios and members, open film, person, list, review and profile pages.
- Read public reviews, comments, public lists, the Community feed, and diaries and watchlists whose owners made them public.

### Joining

1. An admin creates an invite on the Admin page and sends the sign-up link (`/register?invite=<code>`).
2. The new member registers with username, email, password and the terms checkbox (they confirm they are at least 13).
3. Reelr emails a confirmation link. Login is refused with 403 until the address is confirmed.
4. After login the member stays signed in for up to 30 days through a refresh cookie (see [SECURITY.md](SECURITY.md)).

### Logging a film

The main entry point is the rating dialog on a film page (`frontend/src/components/RatingDialog.tsx`), which calls `POST /api/movies/{tmdbId}/diary`. One call can set the rating, mark the film watched, set liked and rewatched, pick a watch date and write or replace the review. The result is one diary entry.

Smaller actions exist too: mark watched (`POST .../status`), rate (`POST .../rating`), add to watchlist, add to list. Each goes through the same rules (below).

### Social use

- Follow members; see their activity in the Following feed and a "New from friends" shelf on the home page.
- Like and comment on reviews.
- Report content; block members.

### Account management

On the Settings page (`frontend/src/pages/SettingsPage.tsx`): change username, watchlist and diary visibility, feed preferences, avatar, password, theme; sign out everywhere; export all data as JSON; delete the account (requires the password).

### Admin

Admins see open reports grouped by item and either remove the item or dismiss the reports. They create, list and revoke invites (`frontend/src/pages/AdminPage.tsx`).

## Business rules

These rules are enforced in code. The file named is where each one lives.

### Journal rules (`backend/Features/Diary/JournalService.cs`)

- Every log, rating or "mark watched" adds a diary entry that snapshots the current rating, liked and rewatched values.
- A logged or rated film counts as watched and is removed from the watchlist. The member may add it again afterwards.
- Updating a rating (`PUT .../rating`) also adds a diary entry, because a rated film counts as watched.
- The watched status follows the newest diary entry. Editing or deleting entries recomputes it; deleting the last entry removes the watched status. The rating stays.
- A review written while logging belongs to that diary entry. Deleting the entry deletes the review. Removing the watched status deletes every diary entry for the film and their reviews.
- A back-dated log never moves the watched date backwards.
- Watch dates must be between 1888-01-01 and tomorrow (UTC), to allow for the furthest time zone. A picked day is stored at 12:00 UTC so it shows as the same calendar day from UTC-12 to UTC+12.

### Content limits

| Item | Limit | Enforced by |
|---|---|---|
| Rating | 0.5 to 5, half steps | `HalfStarsAttribute` and database check constraints |
| Review text | 5000 characters (Markdown counts) | DTO `MaxLength` and check constraint `CK_Reviews_Text` |
| Comment | 1000 characters, plain text | DTO and column length |
| List name, description | 100 and 1000 characters | DTO and column length |
| Report note | 500 characters | DTO and column length |
| Username | 3 to 50 characters, letters, digits and `-._+` only (no `@`) | `RegisterDto`, Identity options in `Program.cs` |
| Password | Identity default rules, minimum 8 characters | Identity, DTOs |
| Avatar | JPEG, PNG or WebP, at most 1 MB and 8000 px per side; stored as WebP at most 512 px | `AvatarImage.cs` |

### One-per-member rules

A member has at most one rating, one watched status, one review and one watchlist entry per film, one like per review, one follow per member and one open report per item. These are primary keys or unique indexes in the database; a duplicate request returns 409 (see [DATA_MODEL.md](DATA_MODEL.md)).

### Visibility (`backend/Features/Users/UserVisibility.cs`)

- Watchlist visibility: `Public`, `Followers` (default) or `Private`.
- Diary visibility: `Public` (default), `Followers` or `Private`. It covers what the member watched, when and how they rated it, and their logs in the feeds.
- Reviews are always public; the code comment calls a review "something the user chose to publish". See [SECURITY.md](SECURITY.md) for a consequence of this.
- A hidden diary or watchlist answers 404, as if the member did not exist.
- Private lists are visible only to their owner.

### Feeds (`backend/Features/Activity/DerivedActivityFeed.cs`)

- Following feed: activity of followed members within 365 days, respecting their diary and watchlist visibility and only their public lists. The member's own activity is included when the `ShowOwnActivity` setting is on.
- Community feed: activity of everyone within 30 days, but only public diaries, reviews and public lists that contain films. It excludes back-dated logs (logged more than 14 days after the watch date), the viewer's own activity and members the viewer blocked.
- Logs by the same person within 60 minutes are grouped into one item, as are films added to the same list.
- A review marked as containing spoilers shows no excerpt in the feed.

### Blocking (`backend/Features/Users/BlockRules.cs`, `UserController.cs`)

- Blocking ends follows in both directions; unblocking does not restore them.
- A blocked member cannot follow the blocker, or like or comment on the blocker's reviews.
- The blocker no longer sees the blocked member's reviews on film pages, their comments, or their Community activity.

### Moderation

- Members cannot report their own content, and can report only lists they can see.
- An admin resolves all open reports about an item at once: `Removed` deletes the item (likes and comments cascade with a review), `Dismissed` keeps it.

### Demo accounts (`backend/Features/Users/DemoAccounts.cs`)

A demo account can do everything a member does except change its username, password or avatar, sign out everywhere, reset its password or delete itself.

### Account deletion (`UserController.DeleteUser`)

Deletion requires the current password and removes the member's follows, blocks, likes, comments, ratings, diary, reviews, lists, watchlist, avatar, refresh tokens and reports they filed. Invites they used stay marked as used.

## Implemented, partial and planned

**Implemented and wired into the SPA:** everything listed above.

**Implemented in the API but not used by the SPA** (verified by searching `frontend/src` for each route):

- `GET /api/movies/search` with `castId`, `crewId`, `studioId`, `genreId` filters (TMDB discover).
- `GET /api/movies/{tmdbId}`, `/similar` and `/credits`, `GET /api/people/search`, `GET /api/companies/search`.
- `GET /api/reviews/{id}/likes`, `POST /api/movies/{tmdbId}/reviews` (the SPA writes reviews through the diary endpoint).

These remain useful for API clients such as the Postman collection.

**Designed for, not implemented:**

- A stored activity table. `IActivityFeed` and the `Activity:Implementation` setting exist so the feed can move from the current derived queries to a stored event table; only `Derived` exists (`backend/Program.cs`).
- Several instances. The README and code comments say caches, rate limits, login throttling and the security stamp cache are in-memory and would need a shared store such as Redis first. The Azure template caps the backend at one replica.

No TODO or FIXME comments exist in the code.

## Not supported

Verified by the absence of code or configuration:

- Third-party sign-in (OAuth or OIDC for members), two-factor authentication.
- Notifications (in-app, email or push) other than account emails.
- Direct messages, threaded comment replies, comment likes.
- Tags, genres or custom fields on diary entries or lists; ordering films within a list (lists sort by date added).
- Importing data from other services; the export is JSON only.
- Localisation: TMDB requests use `language=en-US` and the UI text is English.
- Role management in the database: Identity roles exist in the schema but are not used.
- Horizontal scaling (see above).

## Deployment context

- The production beta runs in Azure (Sweden Central) as described in `infra/` and the root README.
- The privacy page is written for that setup. The root README warns it has not been reviewed by a lawyer and must be kept in step with what the code stores.
- The apps stay warm, but the database pauses when idle, so the first request after a quiet hour can take up to a minute.

## Vision and implementation

The root README and code comments are the only statement of intent. They match the implementation closely; the README even documents operational steps (backups, alerts, first admin). The differences found are small:

- The README says "`UserVisibility` decides who can see a member's watchlist". It now also decides diary visibility.
- The README describes the Postman collection as the API's companion; the collection covers only part of the API (see [TECHNICAL_DEBT.md](TECHNICAL_DEBT.md)).
- `frontend/README.md` is the unchanged Vite template and says nothing about Reelr.
