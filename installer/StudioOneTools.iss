; Tools for Studio One -- Inno Setup script
;
; The real installer. Replaces shipping the raw framework-dependent publish
; renamed to .exe: this gets the app into Program Files, adds Start Menu +
; (optional) Desktop shortcuts, writes a proper Add/Remove Programs entry,
; and -- because the app is published framework-dependent, not self-contained
; -- makes sure the .NET 10 Desktop Runtime is actually on the machine before
; handing control to it.
;
; One script, two flavours, selected with /DFlavor=<name>:
;
;   Web      (default) small download; if the runtime is missing, fetches the
;            official installer at setup time and runs it silently. Needs
;            internet during install.
;   Offline  bundles the runtime installer inside this .exe. No internet
;            needed, ~55-60 MB larger.
;
; Build with:  build-installer.ps1
;   or direct: ISCC.exe /DMyAppVersion=1.3.0 /DFlavor=Offline StudioOneTools.iss
; Requires Inno Setup 6+: https://jrsoftware.org/isdl.php
;
; Expects the framework-dependent publish at:
;   ..\src\StudioOneTools.App\bin\Release\net10.0-windows\win-x64\publish\
; (dotnet publish -c Release; SelfContained=false is set in the .csproj, so
; this is already the small, no-runtime-bundled output -- nothing special to
; pass here.)
;
; Deliberately minimal UI, matching the other Six Walls installers: no
; welcome page, no folder picker, no component tree. Double-click -> one UAC
; prompt -> (first run only) a short runtime-fetch step -> done.

#ifndef MyAppVersion
  #define MyAppVersion "1.3.0"
#endif
#ifndef Flavor
  #define Flavor "Web"
#endif
#if (Flavor != "Web") && (Flavor != "Offline")
  #error Flavor must be Web or Offline
#endif

#define MyAppName       "Tools for Studio One"
#define MyPublisher     "Six Walls"
#define MyExeName       "StudioOneTools.App.exe"
#define PublishDir      "..\src\StudioOneTools.App\bin\Release\net10.0-windows\win-x64\publish"
; aka.ms/dotnet/<channel>/windowsdesktop-runtime-win-x64.exe always resolves
; to the latest patch on that channel -- the same stable-link pattern used by
; other vendors' runtime bootstrappers, so this URL does not need updating
; every .NET 10 servicing release.
#define DotNetChannel   "10.0"
#define DotNetBootstrapUrl "https://aka.ms/dotnet/" + DotNetChannel + "/windowsdesktop-runtime-win-x64.exe"

[Setup]
AppId={{81B07EBF-0A6F-4F60-B5A7-9C7066D64BFC}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyPublisher}
DefaultDirName={autopf}\{#MyPublisher}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableWelcomePage=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
; Ready page is hidden for a fresh install and shown (button = "Update") for
; an upgrade -- see update-aware.iss.
DisableReadyPage=no
DisableFinishedPage=no
UninstallDisplayName={#MyAppName} {#MyAppVersion}
UninstallDisplayIcon={app}\{#MyExeName}
OutputDir=dist
OutputBaseFilename=Tools-for-Studio-One-{#MyAppVersion}-Windows-{#Flavor}
SetupIconFile=six-walls.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce

[Files]
Source: "{#PublishDir}\{#MyExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
#if Flavor == "Offline"
; Fetched by build-installer.ps1 into installer\vendor\ (gitignored) before
; ISCC runs -- not committed, and not present at all for a Web-flavour build.
Source: "vendor\windowsdesktop-runtime-win-x64.exe"; DestDir: "{tmp}"; Flags: dontcopy
#endif

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyExeName}"; Tasks: desktopicon

[Registry]
Root: HKLM; Subkey: "Software\{#MyPublisher}\{#MyAppName}"; ValueType: string; \
    ValueName: "Version"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletekey

[Run]
#if Flavor == "Offline"
; /passive (not /quiet) so the runtime installer shows its own progress bar --
; with /quiet there is nothing on screen for the ~15-20s this step takes, and
; that reads as a hang.
Filename: "{tmp}\windowsdesktop-runtime-win-x64.exe"; Parameters: "/install /passive /norestart"; \
    StatusMsg: "Installing the .NET Desktop Runtime (one-time)..."; \
    Check: NeedsDotNetDesktopRuntimeOffline; Flags: waituntilterminated
#else
; $ProgressPreference = 'SilentlyContinue' matters, not just style: PowerShell's
; default Invoke-WebRequest progress-bar rendering is a well-known perf bug that
; can make a download 10-100x slower, which is exactly what read as a lock-up
; here. /passive (not /quiet) on the runtime install itself for the same reason
; as the Offline branch above -- something visibly happening beats silence.
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
    Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command ""$ProgressPreference = 'SilentlyContinue'; $ErrorActionPreference = 'Stop'; $u = '{#DotNetBootstrapUrl}'; $p = Join-Path $env:TEMP 'sixwalls-dotnet-desktop-runtime.exe'; Invoke-WebRequest -Uri $u -OutFile $p -UseBasicParsing; Start-Process -FilePath $p -ArgumentList '/install','/passive','/norestart' -Wait"""; \
    StatusMsg: "Getting the .NET Desktop Runtime (one-time, ~55 MB -- this can take a minute)..."; \
    Check: NeedsDotNetDesktopRuntime; Flags: waituntilterminated
#endif
Filename: "{app}\{#MyExeName}"; Description: "Launch {#MyAppName}"; Flags: postinstall skipifsilent nowait

#include "update-aware.iss"

[Code]
{ True when no installed .NET Desktop Runtime is on the 10.x line -- our
  net10.0-windows / RollForward=LatestMinor build needs exactly major 10;
  a 10.x of any patch/minor satisfies it, an only-.NET-11 machine does not. }
function NeedsDotNetDesktopRuntime(): Boolean;
var
  Names: TArrayOfString;
  I, Major, DotPos: Integer;
begin
  Result := True;
  if RegGetValueNames(HKLM64,
       'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App',
       Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      DotPos := Pos('.', Names[I]);
      if DotPos > 0 then
        Major := StrToIntDef(Copy(Names[I], 1, DotPos - 1), 0)
      else
        Major := StrToIntDef(Names[I], 0);
      if Major = 10 then
      begin
        Result := False;
        Exit;
      end;
    end;
  end;
end;

#if Flavor == "Offline"
{ Same check, but also extracts the bundled runtime installer out of the
  setup .exe first -- "dontcopy" files are never extracted automatically,
  only on demand via ExtractTemporaryFile, so this has to happen before the
  Run entry above tries to launch it. Only called once, right before that
  entry's Check decides whether to run it. }
function NeedsDotNetDesktopRuntimeOffline(): Boolean;
begin
  Result := NeedsDotNetDesktopRuntime();
  if Result then
    ExtractTemporaryFile('windowsdesktop-runtime-win-x64.exe');
end;
#endif
