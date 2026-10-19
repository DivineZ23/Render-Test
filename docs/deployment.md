# Deployment

Build the Angular static bundle and ASP.NET API image using the supplied Dockerfiles. MongoDB should be a managed replica set in production. Place the frontend and API behind TLS and preferably the same origin.

Before release:

1. Supply all secrets through the deployment platform.
2. Register the exact production Discord callback URL.
3. Set the exact frontend CORS origin.
4. Use a 32+ byte random JWT signing key and rotate it deliberately.
5. Configure durable uploads or the site-specific Zipline contract.
6. Run backend build/tests, frontend build/tests, Playwright, `docker compose config`, image scanning, and dependency auditing.
7. Disable seed data and restrict Swagger.
8. Configure MongoDB backups, alerting, and log retention.

The API exposes `/health` for basic process health. Add authenticated dependency health checks at the platform level if your environment requires them.

## Google Sheets rent-data publishing

Data Sync accepts both the legacy eight-column export and the newer ten-column export ending in `Last Paid,Next Charge`. Payment dates use US month/day/year; `Unknown`, `N/A`, and blank dates are stored as missing. Last Paid and Next Charge are kept separately in sync history and shown in the record table; neither is treated as the legacy Paid-through date. The exported Status remains authoritative for sync status handling, with manual property Hold protection unchanged.

The bot-facing Google Sheets contract remains eight columns (`A:H`); the two new source date columns do not change the bot's input schema.

Data Sync can publish the normalized eight-column game export directly to the Google Sheet used by the Discord bot. Publishing happens immediately after a successful website sync; the Data Sync page checks the latest state every minute and exposes a manual retry when Google rejects a write.

1. Create a Google Cloud service account and enable the Google Sheets API for its project.
2. Create a JSON key for that service account. Keep the downloaded JSON outside the repository.
3. Share the target spreadsheet with the JSON file's `client_email` as an Editor.
4. Add these values to the VPS environment:

```dotenv
GOOGLE_SHEETS_SPREADSHEET_ID=1jOrbMx12x-WPGyamV0u4y6ATx7C10wpErGXEUrb74A4
GOOGLE_SHEETS_SHEET_ID=0
GOOGLE_SHEETS_CLIENT_EMAIL=service-account-name@project-id.iam.gserviceaccount.com
GOOGLE_SHEETS_PRIVATE_KEY=-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----\n
```

Use the `private_key` value from the JSON key and retain literal `\n` sequences when storing it as one environment-variable line. Never commit the JSON key, service-account private key, or a populated `.env` file.

The publisher resolves the tab name from `GOOGLE_SHEETS_SHEET_ID`, clears only columns `A:H` on that tab, and rewrites the following bot-facing contract:

```text
Status, Address, Interior, Renter CID, Renter Name, Phone, Income, Cost
```
