; ============================================================
;  Zip with Password — Inno Setup Script
;  Produces: ZipWithPassword-Setup.exe
;  Installs to Program Files, registers in Apps & Features,
;  adds Explorer context menu, creates Start Menu shortcut.
; ============================================================

#define AppName      "Zip with Password"
#define AppVersion   "1.0.0"
#define AppPublisher "Shem Stephen"
#define AppExeName   "ZipWithPassword.exe"
#define AppId        "{{A3F2C1D4-8B7E-4F5A-9C6D-2E1B0A3D4F5C}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com
AppSupportURL=https://github.com
AppUpdatesURL=https://github.com

; Where it installs
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; Output
OutputDir=dist
OutputBaseFilename=ZipWithPassword-Setup
SetupIconFile=

; Compression
Compression=lzma2/ultra64
SolidCompression=yes

; Appearance
WizardStyle=modern
WizardSizePercent=100
DisableDirPage=no
DisableReadyPage=no

; Require admin so we can write to Program Files and HKLM
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Uninstall info shown in Apps & Features
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
CreateUninstallRegKey=yes

; Min Windows version: Windows 10
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";   Description: "Create a &Desktop shortcut";    GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "contextmenu";   Description: "Add ""Zip with Password..."" to the right-click menu (files && folders)"; GroupDescription: "Shell integration:"; Flags: checkedonce

[Files]
; Main executable (built by Build-Exe.bat → PyInstaller)
Source: "dist\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Start Menu
Name: "{group}\{#AppName}";    Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

; Desktop (optional task)
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; ── Context menu: any file ────────────────────────────────────────────────
Root: HKLM; Subkey: "SOFTWARE\Classes\*\shell\ZipWithPassword";                         ValueType: string; ValueName: "";      ValueData: "Zip with Password...";            Flags: uninsdeletekey;   Tasks: contextmenu
Root: HKLM; Subkey: "SOFTWARE\Classes\*\shell\ZipWithPassword";                         ValueType: string; ValueName: "Icon";  ValueData: """{app}\{#AppExeName}""";         Tasks: contextmenu
Root: HKLM; Subkey: "SOFTWARE\Classes\*\shell\ZipWithPassword\command";                 ValueType: string; ValueName: "";      ValueData: """{app}\{#AppExeName}"" ""%1""";  Tasks: contextmenu

; ── Context menu: folder ──────────────────────────────────────────────────
Root: HKLM; Subkey: "SOFTWARE\Classes\Directory\shell\ZipWithPassword";                 ValueType: string; ValueName: "";      ValueData: "Zip with Password...";            Flags: uninsdeletekey;   Tasks: contextmenu
Root: HKLM; Subkey: "SOFTWARE\Classes\Directory\shell\ZipWithPassword";                 ValueType: string; ValueName: "Icon";  ValueData: """{app}\{#AppExeName}""";         Tasks: contextmenu
Root: HKLM; Subkey: "SOFTWARE\Classes\Directory\shell\ZipWithPassword\command";         ValueType: string; ValueName: "";      ValueData: """{app}\{#AppExeName}"" ""%1""";  Tasks: contextmenu

; ── Context menu: folder background (right-click inside a folder) ─────────
Root: HKLM; Subkey: "SOFTWARE\Classes\Directory\Background\shell\ZipWithPassword";      ValueType: string; ValueName: "";      ValueData: "Zip with Password...";            Flags: uninsdeletekey;   Tasks: contextmenu
Root: HKLM; Subkey: "SOFTWARE\Classes\Directory\Background\shell\ZipWithPassword";      ValueType: string; ValueName: "Icon";  ValueData: """{app}\{#AppExeName}""";         Tasks: contextmenu
Root: HKLM; Subkey: "SOFTWARE\Classes\Directory\Background\shell\ZipWithPassword\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}""";           Tasks: contextmenu

[Run]
; Offer to launch the app after install
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Nothing extra needed — registry keys are cleaned up automatically
; because we used Flags: uninsdeletekey on all Registry entries above

[Code]
// Optional: show a friendly finish message
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Nothing extra needed
  end;
end;
