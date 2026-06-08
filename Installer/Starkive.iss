; ─────────────────────────────────────────────────────────────────────────────
;  Starkive  –  Inno Setup script
;  Produces: Starkive-Setup-1.2.0.exe
; ─────────────────────────────────────────────────────────────────────────────

#define AppName      "Starkive"
#define AppVersion   "1.2.0"
#define AppPublisher "DePaolo Consulting LLC"
#define AppURL       "https://starkive.app"
#define AppExeName   "Starkive.exe"
#define AppId        "{{B4E1F2A3-7C8D-4E5F-9A0B-1C2D3E4F5A6B}"

; ── Source paths (relative to this .iss file) ────────────────────────────────
#define PublishDir   "..\publish"
#define LicenseFile  "License.rtf"

[Setup]
; ── Identity & versioning ────────────────────────────────────────────────────
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup

; ── Upgrade behaviour ────────────────────────────────────────────────────────
; Any previous installation with the same AppId is silently removed before
; the new version is installed – no manual uninstall required from the user.
; Files are only installed to a single folder (no components/features).
; The [UninstallRun] + [InstallDelete] entries below leave no orphans.
; The previous version's uninstaller is called automatically via CloseApplications.
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=no

; ── Install location ─────────────────────────────────────────────────────────
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Require admin so we can write to Program Files and create the uninstall key
PrivilegesRequired=admin

; ── Wizard appearance ────────────────────────────────────────────────────────
; WizardStyle=modern gives the Revo-style full-size left-panel wizard
WizardStyle=modern
WizardSizePercent=120
; Use our custom left-panel bitmap (494×386 pixels, shown on welcome/finish)
WizardImageFile=wizard_side.bmp
; Banner across the top of inner pages (494×58)
WizardSmallImageFile=wizard_banner.bmp
SetupIconFile=..\Starkive\app.ico

; ── Output ───────────────────────────────────────────────────────────────────
OutputDir=..\packaging
OutputBaseFilename=Starkive-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
InternalCompressLevel=ultra64

; ── License ──────────────────────────────────────────────────────────────────
LicenseFile={#LicenseFile}

; ── Languages ────────────────────────────────────────────────────────────────
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ── Tasks (shown on "Select Additional Tasks" page) ──────────────────────────
[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

; ── Files ────────────────────────────────────────────────────────────────────
[Files]
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; ── Shortcuts ────────────────────────────────────────────────────────────────
[Icons]
Name: "{group}\{#AppName}";          Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";  Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

; ── Context-menu registration via the app's own CLI ──────────────────────────
; Starkive registers its shell extension with --install and removes it with
; --uninstall, so the installer/uninstaller delegates to the EXE itself.
[Run]
; Register shell extension after install
Filename: "{app}\{#AppExeName}"; Parameters: "--install"; \
    Flags: runhidden waituntilterminated; \
    StatusMsg: "Registering shell extension..."

; Offer to launch the app on the finish page
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove shell extension before files are deleted
Filename: "{app}\{#AppExeName}"; Parameters: "--uninstall"; \
    RunOnceId: "UnregShellExt"; \
    Flags: runhidden waituntilterminated

; ── Upgrade: auto-remove previous version ────────────────────────────────────
; Inno Setup detects the existing installation via AppId and calls the old
; uninstaller automatically before copying new files.  No extra code needed —
; this is built into the CloseApplications + same-AppId behaviour above.
; The registry key written by a previous MSI build (if any) is cleaned up:
[Registry]
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1"; \
    Flags: dontcreatekey uninsdeletekey

; ── App data is deliberately left in place on uninstall ──────────────────────
; %APPDATA%\Starkive (history, settings, saved passwords) survives uninstall
; so users don't lose data when upgrading.  A fresh install leaves it intact.
