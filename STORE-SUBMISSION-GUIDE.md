# Zip with Password — Windows Store Submission Guide

This guide takes you from zero to a published Windows Store listing.
Follow the phases in order.

---

## Phase 1 — Set up GitHub (free, one-time)

This lets GitHub's servers build your MSI and MSIX automatically
so you never need .NET or WiX installed locally.

1. Create a free account at **github.com** if you don't have one.
2. Create a new **private** repository called `zip-with-password`.
3. Upload the entire `Zip+Password` folder to that repo.
   - Easiest: install **GitHub Desktop** (desktop.github.com), clone your repo,
     drag the files in, then click Commit → Push.
4. After pushing, click the **Actions** tab in your GitHub repo.
   - You'll see the "Build — MSI + MSIX" workflow run automatically.
   - Wait ~5 minutes for it to finish.
5. Click the completed run → scroll to **Artifacts** → download `ZipWithPassword-MSI`.
   - This is your installable `.msi` — double-click to test it on your machine.

---

## Phase 2 — Microsoft Partner Center (one-time, $19 USD)

This is the developer account you need to publish to the Windows Store.

1. Go to **partner.microsoft.com/dashboard** and sign in with a Microsoft account.
2. Click **Create account** → follow the prompts.
   - Individual account: $19 USD one-time fee (no annual renewal).
   - Use your real legal name — it appears on your Store listing.
3. After account creation, go to:
   **Account settings → Legal info → Publisher display name**
   Copy the **Publisher** value (looks like `CN=Your Name, O=Your Name, ...`).
4. Go to **Apps and Games → New product → App** and reserve your app name:
   - Name: `Zip with Password`
   - This gives you a **Package/Identity Name** (like `12345YourName.ZipWithPassword`).

---

## Phase 3 — Update the MSIX manifest

Open `packaging/AppxManifest.xml` and update two values:

```xml
<Identity
  Name="12345YourName.ZipWithPassword"    ← from Partner Center step 4 above
  Publisher="CN=Your Name, ..."            ← from Partner Center step 3 above
  Version="1.0.0.0"
  ProcessorArchitecture="x64" />

<Properties>
  <PublisherDisplayName>Your Name</PublisherDisplayName>   ← your display name
```

Commit and push this change to GitHub. The Actions workflow will rebuild.

---

## Phase 4 — Create your app icons

The MSIX needs 7 image files in `packaging/assets/`.
See `packaging/assets/README.txt` for exact sizes.

**Quickest path:** Go to **canva.com**, search "app icon", design a simple
padlock/zip logo, then export at each required size. Free account is enough.

After adding the images, commit and push to GitHub.

---

## Phase 5 — Build the MSIX

1. In your GitHub repo, click **Actions → Build — MSI + MSIX**.
2. Click **Run workflow** (top right) → set "Also build MSIX?" to **Yes** → Run.
3. Wait ~8 minutes.
4. Download the `ZipWithPassword-MSIX` artifact — this is your `.msix` file.

---

## Phase 6 — Submit to the Store

1. In Partner Center, open your `Zip with Password` app.
2. Click **Start your submission**.
3. Fill in:
   | Section | What to enter |
   |---|---|
   | Pricing | Free, or set a price (you keep 85% revenue) |
   | Age rating | Complete the questionnaire — this app rates as **Everyone** |
   | Properties | Category: **Utilities & tools** |
   | Store listing | Description, screenshots (take 2–3 from your sandbox), keywords |
   | Packages | Upload `ZipWithPassword.msix` |
4. Click **Submit to the Store**.
5. Microsoft reviews within **1–3 business days**.
   - `broadFileSystemAccess` capability requires a written justification.
   - In the submission form, explain: *"The app compresses user-selected files
     and folders into AES-256 password-protected ZIP archives. Broad file system
     access is required to read the source files and write the output ZIP."*
   - This is routinely approved for utility apps.

---

## Phase 7 — Code signing (recommended before launch)

Without a code signing certificate, users outside the Store who run your `.msi`
directly will see a SmartScreen warning. For Store submissions, Microsoft handles
signing — no cert needed.

If you want to distribute the `.msi` directly (outside the Store):

| Certificate type | Cost | Where to buy |
|---|---|---|
| Standard OV cert | ~$200/yr | DigiCert, Sectigo, Comodo |
| EV cert (best SmartScreen trust) | ~$400/yr | DigiCert, Sectigo |

Once you have a `.pfx` certificate file:
1. In your GitHub repo, go to **Settings → Secrets → Actions**.
2. Add secret `CERTIFICATE_PFX` = base64 of your .pfx file:
   ```powershell
   [Convert]::ToBase64String([IO.File]::ReadAllBytes("cert.pfx")) | clip
   ```
3. Add secret `CERTIFICATE_PASSWORD` = your pfx password.
4. The workflow signs the MSIX automatically on every build.

---

## Summary — what each file does

| File | Purpose |
|---|---|
| `ZipWithPassword/` | C# WPF source code |
| `.github/workflows/build.yml` | Automated cloud build (GitHub Actions) |
| `installer.wxs` | WiX source → produces the `.msi` installer |
| `packaging/AppxManifest.xml` | MSIX manifest → required for Windows Store |
| `packaging/assets/` | App icons for the Store listing |
| `license.rtf` | License shown during MSI install |

---

## Cost summary

| Item | Cost |
|---|---|
| GitHub account | Free |
| Microsoft Partner Center | $19 one-time |
| Code signing cert (optional, for direct .msi distribution) | $200–400/yr |
| Windows Store revenue cut | 15% (you keep 85%) |
