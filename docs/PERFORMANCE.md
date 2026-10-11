# Response-time improvements

## What changed

- Dashboard/block financial reads project only the fields used in calculations. Latest export income is summed by MongoDB; the dashboard no longer downloads a full export for one number.
- Property block labels, recruitment reviewers, and audit entity labels use batch lookups instead of one database query per row. Booking counts are grouped by MongoDB.
- Agent analytics applies date ranges in MongoDB, projects only activity fields, groups activity by agent once, and shares each source read between the summary and individual detail. Detail property names are fetched by referenced IDs only. History renders 50 rows per section initially, with explicit Show more controls; totals use the complete data.
- Eviction queue reads the current export first, exits early when empty, batches properties, and requests only relevant historical record fields. Paid/overdue/empty boundaries, legacy address matching, 24-hour waiting periods, and manual holds are retained.
- Sync History requests `snapshots?summaryOnly=true`, which returns counts and metadata with empty record lists. The existing full-history endpoint remains the default for notice views.
- The scoped user repository shares actor lookups within one request only. Permissions are checked afresh on every new request; user writes invalidate the request-local lookup.
- Concurrent identical frontend GET requests are coalesced. Responses are not cached after completion, failure, or cancellation; mutations separate new reads from old in-flight reads.
- Notice pages request only their relevant data. Hidden tabs do not poll for booking counts or sync status. Booking-count polling does not overlap. Analytics filter changes cancel superseded requests.
- Property lists start their initial request immediately, retain the typing debounce, and allow retrying an identical failed query. The dashboard has loading placeholders and retry UI; the home page distinguishes loading/errors from genuinely empty data.
- JSON responses are compressed; fingerprinted JS/CSS can be cached for a year. HTML revalidates, while API responses are explicitly `no-store`.
- Additional indexes cover common list ordering, analytics dates, recent status history, and current rent snapshots. Existing indexes remain untouched.

## Check after deploying both services

Run from the repository root in PowerShell:

```powershell
./scripts/Measure-ApiLatency.ps1 -Samples 5
```

This makes public read-only requests and needs no auth cookie. Keep the first sample separate from subsequent samples: it can include wake-up/connection overhead. Compare status, headers time, total time, decoded body size, and `ServerTiming` using the same client and data before/after deployment. It is a spot check, not a load test or a latency guarantee.

For authenticated pages, use browser DevTools Network while signed in. Do not paste cookies or JWTs into logs. Compare dashboard, property list, commissions, agent summary/detail, sync history, notices, and eviction queue. Look for:

- one data request on the queue/history/notice page instead of unrelated parallel loads;
- `Server-Timing: app;dur=...`, measuring backend time until response headers are sent;
- `Content-Encoding: br` or `gzip` and `Vary: Accept-Encoding` for sufficiently large JSON responses;
- fresh status/amount data after updates and unchanged authorization/hold behavior;
- lower transferred size for dashboard, sync history, and filtered analytics.

The timing header excludes Render wake-up, DNS/TLS/proxy transit, most body transfer, and browser rendering. Compare it with Network timings and Serilog's request elapsed time to locate the remaining delay. Check MongoDB query execution plans and database/network latency on real production data; query-translation tests do not establish actual database execution speed. Index creation runs at startup and may make the first deployment slower while indexes build.

## Hosting and remaining limits

Render's [Free web service documentation](https://render.com/docs/free#spinning-down-on-idle) says Free services sleep after 15 minutes without inbound traffic and can take about a minute to wake. If either frontend or backend is on Free, source optimizations cannot eliminate that cold start. Check both services' compute plans before changing hosting; no plan changes are made by this patch. Place frontend, backend, and database close together where possible.

Current spot check of the deployed, pre-patch app (2026-10-11): backend `/health` about 617 ms total; proxied featured properties about 966 ms total. These are single unauthenticated samples, not before/after proof for private endpoints.

All-time analytics and full notice history can still grow large. If timings show those remaining endpoints dominate at production scale, the next step is database-side metric aggregation and server-paginated activity/history endpoints, with explicit pagination in the UI. Google Sheets publishing still waits for the external operation so existing sync success/failure semantics are preserved; it should be moved to a durable background job if it dominates sync time. No Redis cache, stale financial/permission cache, fire-and-forget sync, data deletion, or hosting purchase is introduced.

## Local verification

Backend regression tests include query translation for every date-bound combination, lean portfolio/history projections, one-source-read agent details, and a 100-property eviction queue requiring one property batch lookup. Frontend unit tests cover request sharing/cancellation, immediate initial loads, debounce/retry behavior, and per-notice-page request budgets. Focused Playwright tests mock all required APIs and check queue request count, summary-only Sync History, dashboard placeholders while a response is pending, and public property browsing/enquiries.

The older owner/property-management Playwright suite has unmocked access-management/announcement requests and expectations for retired UI labels; that broad suite is not a clean pass. The focused suite is separate so it can verify the changed performance paths without requiring a local MongoDB/backend. Docker's local engine was unavailable, so Nginx configuration was reviewed but not validated with `nginx -t` locally.
