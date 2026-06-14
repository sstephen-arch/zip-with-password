# Starkive — Claude Instructions

## Project layout
- `Starkive/` — WPF desktop app (C#, .NET 8)
- `starkive-web` — Next.js website (separate repo at `C:\Users\shem_\Claude\Projects\starkive-web`)
- `supabase/` — Edge functions and migrations
- `Installer/` — Inno Setup script
- `msix_staging/` — MSIX packaging files
- `packaging/` — Build output (installers, MSIX)

## Deploying the website
`starkive-web` deploys via **Cloudflare Pages**. To publish any change:
```
cd C:\Users\shem_\Claude\Projects\starkive-web
git add <files>
git commit -m "..."
git push
```
Cloudflare auto-builds from `master` on `https://github.com/sstephen-arch/starkive-web.git`. Live in ~2 minutes after push.

## Releasing the app
Version must be bumped in **4 places**:
1. `Directory.Build.props` → `StarkiveVersion`
2. `Starkive/AppConstants.cs` → `AppVersion`
3. `Installer/Starkive.iss` → `#define AppVersion`
4. `msix_staging/AppxManifest.xml` → `Version="x.y.z.0"`

Build commands (from repo root):
```powershell
# Publish
dotnet publish Starkive/Starkive.csproj -c Release -r win-x64 --self-contained true

# MSIX
robocopy publish\win-x64 msix_staging\app /MIR
& "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe" pack /d msix_staging /p packaging\Starkive-1.x.x.msix /o
```

**Distribution is Microsoft Store only.** No .exe installer on the website — SmartScreen blocks unsigned installers. User uploads MSIX to Partner Center (app ID `9P3CM2XTXP61`).

## Security constraints — NEVER violate these
- **`Starkive/Secrets.cs` is gitignored** — never commit it. All OAuth credentials live here only.
- **No IP addresses in the database** — EULA §3a. Never insert `ip_address` into `audit_events` or any table.
- **`FROM_EMAIL` must stay `notifications@starkive.app`** — do not change the domain.
- **Admin panel** locked to: `shem_stephen@outlook.com` and `sstephen@depaloconsultingllc.com` only.
- **Never add .exe download links** back to starkive-web under any circumstances.

## Pro status architecture
- Checkout: app calls `POST https://starkive.app/api/checkout` with `{ email }` → opens Stripe URL → polls `get_pro_status()` every 5s for 3 min
- Supabase trigger `sync_pro_from_subscription` syncs `subscriptions.status` → `user_profiles.is_pro` on every webhook
- `past_due` subscriptions keep Pro access (grace period)

## Cloudflare edge runtime gotchas (starkive-web)
- Stripe: use `constructEventAsync` and `httpClient: Stripe.createFetchHttpClient()`
- Supabase: server fetches need a custom `User-Agent` or secret keys get rejected
- Cloudflare secrets set from PowerShell can get BOM-corrupted — write BOM-free file and pipe via `cmd /c type`
- Subscriptions matched to users **by email** — checkout email must equal sign-in email
