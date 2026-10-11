# Response-time improvements

## What changed

- Dashboard/block financial reads project only the fields used in calculations. Latest export income is summed by MongoDB; the dashboard no longer downloads a full export for one number.
- Property block labels, recruitment reviewers, and audit entity labels use batch lookups instead of one database query per row. Booking counts are grouped by MongoDB.
- Agent analytics applies date ranges in MongoDB, projects only activity fields, groups activity by agent once, and shares each source read between the summary and individual detail. Detail property names are fetched by referenced IDs only. History renders 50 rows per section initially, with explicit Show more controls; totals use the complete data.
- Eviction queue reads only current evictable rows and lean property fields. It streams decisive historical rows in batches of four snapshots and stops once each candidate's current cycle is settled; repeated evictable rows are filtered out in MongoDB. Paid/overdue/empty boundaries, legacy address matching, 24-hour waiting periods, and manual holds are retained.
- Eviction/overdue notices use `/notices/feed` with 50 generated notices per page instead of downloading every historical export. `/notices/feed-summary` calculates full-history pending/sent counts separately, including the browser's local day boundaries; its slower history scan does not block displaying or using notices. Tenant mappings are fetched only for displayed rows, without tenant evidence/images. Overdue/active lists use `/notices/records` to filter and slice the latest export in MongoDB; their summary totals still describe the whole export. Both paged endpoints cap requested page size at 100.
- Notice pages provide loading, retry, and Load more controls. Queue rendering starts with 50 cards; Show more reveals additional properties. Notice pagination carries a server-generated `asOf` timestamp so new syncs do not shift older notice pages. A new sync detected between current-export list pages restarts pagination. Resolving a notice returns only that updated record through `/resolution-record`, while the existing full-snapshot resolution endpoint remains supported.
- Sync History requests `snapshots?summaryOnly=true`, which returns counts and metadata with empty record lists. The existing full-history endpoint remains the default for notice views.
- The scoped user repository shares actor lookups within one request only. Permissions are checked afresh on every new request; user writes invalidate the request-local lookup.
- Concurrent identical frontend GET requests are coalesced. A shared `DataModelService` now also keeps bounded, short-lived display models in browser memory. See the freshness rules below; server-only reads remain available for forms and permissions.
- Notice pages request only their relevant data. Hidden tabs do not poll for booking counts or sync status. Booking-count polling does not overlap. Analytics filter changes cancel superseded requests.
- Property lists start their initial request immediately, retain the typing debounce, and allow retrying an identical failed query. The dashboard has loading placeholders and retry UI; the home page distinguishes loading/errors from genuinely empty data.
- JSON responses are compressed; fingerprinted JS/CSS can be cached for a year. HTML revalidates, while API responses are explicitly `no-store`.
- Additional indexes cover common list ordering, analytics dates, recent status history, and current rent snapshots. Existing indexes remain untouched.

## Shared frontend data models

Pages already load their Angular code lazily through `loadComponent`. Display data now goes through `ApiService.read()` and `DataModelService`, covering public properties, portfolio lists, dashboards, analytics, commissions, bookings, tenants, users, recruitment, audit logs, and notice summaries/first pages. The domain services remain the typed interfaces for components.

- On a first visit, fetch the requested data only. Do not preload every endpoint or poll hidden pages.
- On a repeat visit within 10 seconds, reuse the saved model without another request.
- Between 10 seconds and two minutes, show the saved result immediately and update it in the background. A small site-wide indicator identifies the preview while it refreshes. Display loaders stop on the first result instead of hiding saved data until refresh completes.
- After two minutes, wait for a new response instead of showing the old model. A network/5xx refresh failure keeps an already-visible preview with an explicit warning and Reload button. Authorization and other client errors still propagate; 401/403 also purge saved models.
- POST/PUT/PATCH/DELETE and uploads invalidate models at the beginning and end, including failures/cancellation. A late pre-write response is re-read rather than cached or displayed. The read-only commission preview POST is exempt. Sign-in, logout and explicit auth refresh clear memory and cancel pending model reads.
- Store no response bodies or credentials in localStorage, IndexedDB or a service worker. The per-tab, per-page-lifetime store is limited to 80 entries and approximately 8 MiB of serialized JSON, with a 1 MiB per-entry limit and least-recently-used eviction. Consumers receive copies so form mutations cannot corrupt saved data.
- Login, access/team settings, recruitment settings, editable property/booking details, block management, inline enquiry editing, current sync data, and eviction eligibility bypass completed-response reuse. Opening a settlement editor also fetches the current ledger before populating the form. The backend remains authoritative for every mutation. No hold/release, payment or eviction-cycle rules changed.
- Notice and overdue first pages can be reused, but Load more and notice resolution wait for their refresh to finish. Continuation pages are fetched from the server using the refreshed feed cutoff; current-export lists restart when a sync changes the snapshot. Large exports are not background-prefetched or stored wholesale.

This improves repeat navigation and avoids duplicate calls; it does not make an uncached API request or Render cold start instantaneous. Changes from other users become visible on the next server read, not by push notification. Refresh/reload when an immediately current report is required. The audit Refresh button explicitly invalidates saved models.

## Check after deploying both services

Run from the repository root in PowerShell:

```powershell
./scripts/Measure-ApiLatency.ps1 -Samples 5
```

This makes public read-only requests and needs no auth cookie. Keep the first sample separate from subsequent samples: it can include wake-up/connection overhead. Compare status, headers time, total time, decoded body size, and `ServerTiming` using the same client and data before/after deployment. It is a spot check, not a load test or a latency guarantee.

For authenticated pages, use browser DevTools Network while signed in. Do not paste cookies or JWTs into logs. Compare dashboard, property list, commissions, agent summary/detail, sync history, notices, and eviction queue. Look for:

- one data request on the queue/history page; notice pages request a bounded feed and independent totals, with no full-export history request;
- a first notice response with at most 50 `items`, and overdue-list responses with at most 50 `snapshot.records`; more requests occur only on Load more;
- `Server-Timing: app;dur=...`, measuring backend time until response headers are sent;
- `Content-Encoding: br` or `gzip` and `Vary: Accept-Encoding` for sufficiently large JSON responses;
- fresh status/amount data after updates and unchanged authorization/hold behavior;
- lower transferred size for dashboard, sync history, and filtered analytics.

The timing header excludes Render wake-up, DNS/TLS/proxy transit, most body transfer, and browser rendering. Compare it with Network timings and Serilog's request elapsed time to locate the remaining delay. Check MongoDB query execution plans and database/network latency on real production data; query-translation tests do not establish actual database execution speed. Index creation runs at startup and may make the first deployment slower while indexes build.

## Hosting and remaining limits

Render's [Free web service documentation](https://render.com/docs/free#spinning-down-on-idle) says Free services sleep after 15 minutes without inbound traffic and can take about a minute to wake. If either frontend or backend is on Free, source optimizations cannot eliminate that cold start. Check both services' compute plans before changing hosting; no plan changes are made by this patch. Place frontend, backend, and database close together where possible.

Current spot check of the deployed, pre-patch app (2026-10-11): backend `/health` about 617 ms total; proxied featured properties about 966 ms total. These are single unauthenticated samples, not before/after proof for private endpoints.

All-time analytics, completed eviction history, and the legacy full-export history endpoint can still grow large. Notice counters still scan historical rows inside MongoDB, although those rows are no longer transferred to the app. A queue candidate without a generated notice or a status boundary may still require scanning older history. If database timing remains dominant at production scale, persisted notice-cycle summaries with migration/invalidation would be the next step. Google Sheets publishing still waits for the external operation so existing sync success/failure semantics are preserved; it should be moved to a durable background job if it dominates sync time. No Redis cache, permission cache, fire-and-forget sync, data deletion, or hosting purchase is introduced. Financial display models follow the short-lived preview rules above; they are never used as authoritative mutation inputs.

## Local verification

Backend regression tests include query translation for every date-bound combination, lean portfolio/history projections, one-source-read agent details, and a 100-property eviction queue requiring one property batch lookup. Frontend unit tests cover request sharing/cancellation, immediate initial loads, debounce/retry behavior, and per-notice-page request budgets. Focused Playwright tests mock all required APIs and check queue request count, summary-only Sync History, dashboard placeholders while a response is pending, and public property browsing/enquiries.

Notice performance regressions also verify that a 100-property queue stops after one snapshot despite 200 older exports, legacy paid/overdue/empty boundaries close an eviction cycle, paged feed totals include undisplayed notices, and MongoDB translates filtering/paging/count projections. Browser checks cover deferred initial notice loading, loading a second page, updating a single resolved notice, and retrying a failed overdue request. These checks establish query shape and behavior, not real production database latency.

Data-model regressions cover query isolation, immediate saved values followed by background updates, expiry, mutation/session invalidation, late-response races, cloned values, authorization failures, bounded retention, and server-only form/eligibility reads. Browser checks verify instant dashboard revisits with one request, a delayed refresh updating the visible amount without a blocking loader, and failure/reload recovery. Notice tests prevent continuation paging against a still-refreshing cutoff.

The older owner/property-management Playwright suite has unmocked access-management/announcement requests and expectations for retired UI labels; that broad suite is not a clean pass. The focused suite is separate so it can verify the changed performance paths without requiring a local MongoDB/backend. Docker's local engine was unavailable, so Nginx configuration was reviewed but not validated with `nginx -t` locally.
