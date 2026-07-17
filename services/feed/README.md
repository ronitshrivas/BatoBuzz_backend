# BatoBuzz Feed Service

Phase 2 of the backend: the feed, shared by the **user** and **merchant** apps.
Posts, likes, comments, replies, views, reports and cities.

Built to match the existing Identity service — same `net7.0`, same pinned
package versions, same `ApiResponse<T>` envelope, same `AppException` →
middleware error mapping, same `Database.Migrate()` on startup.

---

## Running it

```bash
# 1. Create the database
createdb -O batobuzz batobuzz_feed

# 2. Point it at your Postgres + set the SAME Jwt block as Identity
#    (services/feed/BatoBuzz.Feed/appsettings.json)

# 3. Run — migrations apply automatically on startup
dotnet run --project services/feed/BatoBuzz.Feed
```

Swagger: `http://localhost:5178/swagger` — the **Authorize** button takes an
access token from Identity (paste it raw, no `Bearer ` prefix).

> **The `Jwt` section must be byte-identical to Identity's.** The Feed service
> validates tokens locally against the shared signing key rather than calling
> Identity on every request. A mismatched key produces 401s on every
> authenticated route.

Ports: Identity `5168`, **Feed `5178`**, Gateway `5189`.

---

## Gateway

`gateway/BatoBuzz.Gateway` was a `Hello World` stub; it is now a working YARP
reverse proxy. Routes live in `appsettings.json`, so adding a service or moving
a port is a config change, not a redeploy:

| Path | → Service |
|---|---|
| `/api/user/auth/**` | Identity `5168` |
| `/api/merchant/auth/**` | Identity `5168` |
| `/api/feed/**` | Feed `5178` |
| `/api/merchant/posts/**` | Feed `5178` |

The apps should talk to the gateway (`5189`) only.

---

## API

Every response is wrapped in the existing envelope:

```json
{ "success": true, "message": null, "data": { } }
```

### Feed — `/api/feed` (both apps)

| Method | Route | Auth | Purpose |
|---|---|---|---|
| GET | `/posts` | anonymous ok | The feed. Filters + sort + paging |
| GET | `/posts/{id}` | anonymous ok | One post |
| GET | `/cities` | anonymous ok | City list for the filter dropdown |
| POST | `/posts/{id}/like` | any signed-in | **Toggles** like, returns new state |
| POST | `/posts/{id}/view` | any signed-in | Records a view (idempotent) |
| POST | `/posts/{id}/report` | any signed-in | Reports a post |
| GET | `/posts/{id}/comments` | anonymous ok | Comments, or replies via `?parentId=` |
| POST | `/posts/{id}/comments` | any signed-in | Comment, or reply via `parentId` |
| PUT | `/posts/{id}/comments/{cid}` | author | Edit |
| DELETE | `/posts/{id}/comments/{cid}` | author or post owner | Delete |

`GET /posts` query params — all optional:

`cityId`, `postType` (`ads`\|`reels`\|`job`\|`event`), `adsCategory` (only
applied when `postType=ads`), `sortBy` (`latest`\|`oldest`\|`mostViewed`\|
`forYou`, default `latest`), `search`, `merchantId`, `cursor`, `pageSize`
(1–50, default 10).

### Authoring — `/api/merchant/posts` (merchant app only)

| Method | Route | Purpose |
|---|---|---|
| GET | `/` | The merchant's own posts |
| POST | `/` | Publish |
| PUT | `/{id}` | Edit (full object, not a delta) |
| DELETE | `/{id}` | Delete (soft) |

All four require the **`ApprovedMerchant`** policy — your existing gate. A
Pending or Rejected merchant can still log in and read their status screen, but
cannot publish (`403`).

---

## Paging

Firestore returned a `DocumentSnapshot` cursor. Over HTTP that becomes an opaque
string — treat it as a token, don't parse it:

```
GET /api/feed/posts?cityId=ktm&pageSize=10
  → { items: [...], nextCursor: "eyJjcmVh...", hasMore: true }

GET /api/feed/posts?cityId=ktm&pageSize=10&cursor=eyJjcmVh...
  → { items: [...], nextCursor: null, hasMore: false }
```

This is **keyset** pagination, not `OFFSET`: page 50 costs the same as page 1,
and posts published mid-scroll can't cause skips or duplicates. The cursor
encodes `(sortKey, id)`, and `id` is the tiebreaker in every sort — without it,
posts sharing a timestamp would shuffle between pages. There's a test for
exactly that case (25 posts, one timestamp).

---

## Two things the apps must change

### 1. `likedBy` / `viewedBy` / `reportedBy` are gone

Firestore stored these as arrays on the post document, and the apps did
`likedBy.contains(uid)`. Those arrays are **not returned**, deliberately: they
grow without bound and they ship every actor's id to every reader.

The DTO carries the only bit the UI needs:

| Was | Now |
|---|---|
| `post.isLikedBy(uid)` | `post.isLiked` |
| `post.likedBy.length` | `post.likeCount` |
| `post.isViewedBy(uid)` | `post.isViewed` |
| `post.isReportedBy(uid)` | `post.isReported` |

`POST /posts/{id}/like` toggles and returns `{ postId, isLiked, likeCount }` —
use it to reconcile optimistic UI rather than recomputing locally.

### 2. Comment authors are neutral

`PostCommentModel` reads `merchantId`/`merchantName`/`merchantPhoto` with a
fallback to `userId`/`userName`/`userPhoto`. Both apps comment, so the wire uses
one author with a type tag:

```
authorId, authorType ("user" | "merchant"), authorName, authorPhoto
```

`canEdit` is computed server-side — use it to show the edit/delete affordance
instead of comparing ids client-side.

Everything else keeps the field names your `MerchantPostModel` already parses:
`postId`, `post`, `postType`, `imageUrls`, `merchantName`, `likeCount`,
`commentCount`, `viewCount`, the job/event/reel blocks, and so on.

---

## Notable behaviour

**Likes are a table, not an array.** A unique index on `(PostId, ActorId)` makes
liking idempotent at the database level — a double-tap or a retried request
can't produce two likes. The counter moves in the same transaction as the row,
so they can't drift apart the way `arrayUnion` + `increment` could.

**Replies are two levels, permanently.** Replying to a reply attaches to the
root comment instead of nesting deeper, matching the app's UI. Replies roll into
their parent's `replyCount`; only top-level comments move the post's
`commentCount`. Deleting a root comment hides its replies too.

**Reporters stop seeing the post.** Filtered in SQL, not client-side — a
reported post can't reappear through pagination.

**Deletes are soft.** `IsDeleted` keeps comment/like history intact and gives a
clean `404` to anyone holding the id.

**Merchants don't inflate their own view counts.** Same rule as the old
Firestore transaction.

**Type-specific validation.** A job with no title, an event with no date, a reel
with no video, or an empty ad are all rejected at the edge (`400`) instead of
rendering as broken cards.

---

## Changes to existing code

Four files outside the Feed service were touched:

**`shared/BatoBuzz.Shared/Auth/TokenClaims.cs`** — added `display_name` and
`photo_url`. The feed denormalizes author name/photo onto each post and comment
(exactly as the Firestore docs did), so carrying them in the token avoids an
HTTP hop to Identity on every write.

**`UserAuthService.cs`, `MerchantAuthService.cs`, `RefreshService.cs`** — emit
those two claims. `RefreshService` matters: without it, names would vanish the
first time a token rotated.

> Existing tokens don't carry the new claims. They keep working — author name
> just comes through empty until the next login or refresh.

**`Identity/Middleware/ExceptionMiddleware.cs`** — a real bug, found by the HTTP
tests. `JsonSerializer` defaults to PascalCase while MVC serializes camelCase,
so **errors** came back as `{"Success":false}` while every success was
`{"success":true}`. Anything parsing `success` would miss the error path
entirely. Both middlewares now pin camelCase.

---

## Test coverage

The service was verified against a real PostgreSQL 16 instance, not mocks.

**68 integration tests** — creation, per-type validation, ownership rules, like
toggling and counter integrity, a **concurrent double-tap race**, view
idempotency, comment/reply threading and counts, cascade delete, moderation,
filters, all four sort orders, **pagination across identical timestamps**,
cursor tampering, report-hiding, soft delete, and the `DateTime.Kind` round-trip
that has bitten this stack before.

**31 HTTP tests** — real Kestrel, real JWTs, real policies: the approval gate
(pending merchant → 403), envelope shape, query-string binding, cursor
round-tripping through a URL, `pageSize` bounds, tampered tokens, and a check
that `likedBy`/`viewedBy`/`reportedBy` never appear in a response.

`dotnet ef` can't run here (nuget.org unreachable), so the migration was
generated by invoking EF Core's own scaffolder API in-process — the same code
path the CLI uses. It is a genuine EF migration with Designer and snapshot
files, not hand-written SQL.

---

## Not included

Notifications on like/comment. The old `FeedService` wrote straight into a
`notifications` collection; that belongs in a Notification service alongside
your FCM v1 sender rather than inline in the feed. The hooks are obvious —
`PostService.ToggleLikeAsync` and `CommentService.AddAsync` — and both already
know the actor and the post owner.

The Flutter apps are also untouched: the feed screens are wired through
Firestore streams and `DocumentSnapshot` cursors across ~86 files, which is its
own piece of work.
