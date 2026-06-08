# Starkive — Microsoft Store Submission Guide

## Prerequisites

- Windows 10/11 dev machine with Node.js 18+ and npm
- Microsoft Partner Center account: https://partner.microsoft.com
- Code signing certificate (EV cert recommended for Store)
- electron-builder installed: `npm install -D electron-builder`

---

## 1. Partner Center Setup

1. Sign in to https://partner.microsoft.com/dashboard
2. **Apps and games → New product → MSIX or PWA app**
3. Reserve the app name **Starkive**
4. Note the **Package identity values** generated:
   - `identityName` (e.g. `YourPublisher.Starkive`)
   - `publisher` (CN=... from your Partner Center account)
   - `publisherDisplayName`

Update `starkive-app/package.json` → `build.appx`:

```json
"appx": {
  "applicationId": "Starkive",
  "identityName": "YourPublisher.Starkive",
  "publisher": "CN=YourPublisherCN",
  "publisherDisplayName": "Your Display Name",
  "backgroundColor": "#111318",
  "displayName": "Starkive",
  "languages": ["en-US"]
}
```

---

## 2. App Icons

Generate icons at these exact sizes and save to `starkive-app/assets/`:

| File | Size |
|------|------|
| `icon.png` | 256×256 (electron-builder base) |
| `StoreLogo.png` | 50×50 |
| `Square44x44Logo.png` | 44×44 |
| `Square150x150Logo.png` | 150×150 |
| `Wide310x150Logo.png` | 310×150 |

electron-builder auto-generates the required scale variants from `icon.png`.

---

## 3. Build the .appx

```bash
cd starkive-app
npm install
npm run build
```

This runs `electron-builder --win appx` (configured in `package.json` → `scripts.build`).

Output: `dist/Starkive-Setup.appx` (or `.msix`)

---

## 4. Test locally (sideload)

```powershell
# Enable developer mode
Add-AppxPackage -Register ".\dist\Starkive-Setup.appx" -ForceApplicationShutdown
```

Or: Settings → Developer Mode → Install .appx directly.

Verify:
- Deep link `starkive://auth?token=test` opens the app
- `starkive://upgrade` opens the app
- Zip + AES-256 works on a test file

---

## 5. Microsoft Store In-App Purchase ($1/month)

### Partner Center — subscription add-on

1. In Partner Center → your Starkive app → **Add-ons → New add-on**
2. Product type: **Subscription**
3. Product ID: choose a slug, e.g. `starkive_pro_monthly`
4. Set price: **$0.99** or **$1.00** (tier 1)
5. Billing period: **Monthly**
6. Free trial: optional (recommend 7 days)
7. Submit add-on for review

Copy the **Store ID** (e.g. `9NBLGGH4R3N6`) → set as `MS_STORE_PRODUCT_ID` in backend `.env`.

### In-app purchase flow (renderer)

The upgrade button in Starkive calls `starkive://upgrade` which can be wired in `main.js` to open the Microsoft Store purchase dialog via:

```javascript
const { WindowsStoreUtils } = require('electron-windows-store-utils'); // if available
// or shell.openExternal(`ms-windows-store://pdp/?productid=${STORE_ID}`)
```

After purchase, query entitlement from backend → update user plan to `pro` in Supabase.

---

## 6. Submit to Store

1. Partner Center → Starkive → **Start submission**
2. Upload the `.appx` / `.msix` file
3. Fill in: description, screenshots (1366×768 or 2560×1440), age rating
4. Privacy policy URL (required) — host a simple page
5. Submit → certification typically takes 3–5 business days

---

## 7. Environment for production backend

Deploy `starkive-backend` to a server (Fly.io, Railway, or VPS):

```bash
cd starkive-backend
npm install
NODE_ENV=production node index.js
```

Update `starkive-app/main.js` line:
```javascript
await shell.openExternal('https://your-production-domain.com/auth/google');
```

And update `GOOGLE_CALLBACK_URL` in `.env` to your production domain.

---

## 8. Checklist before submission

- [ ] All 5 icon sizes present in `assets/`
- [ ] `identityName`, `publisher`, `publisherDisplayName` match Partner Center exactly
- [ ] Deep link protocol `starkive://` registered and tested
- [ ] Backend deployed and `/health` returns `{"status":"ok"}`
- [ ] Google OAuth callback URL updated to production
- [ ] `MS_STORE_PRODUCT_ID` set in backend env
- [ ] Privacy policy URL live
- [ ] App tested on clean Windows machine (not dev machine)
