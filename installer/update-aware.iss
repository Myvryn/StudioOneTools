; Shared code: makes an installer present itself as an "Update" when a previous
; install of the same AppId is already on the machine -- even if the payload is
; byte-identical -- and gives the user the choice to uninstall instead of
; update/reinstall. #include this at the end of a script that has defined
; MyAppName / MyAppVersion / MyAppGuid (the AppId GUID, WITHOUT braces -- see
; below) and set  DisableReadyPage=no .
;
; Fresh install    : no extra page -> double-click, UAC, done.
; Existing install : a page asks "Update / reinstall" or "Uninstall" before
;                     the Ready page. Choosing Uninstall runs the existing
;                     uninstaller and exits Setup -- it never falls through
;                     into installing a fresh copy right after.
;
; MyAppGuid: found the hard way -- SetupSetting("AppId") returns the AppId
; directive's ini text exactly as written, e.g. "{{81B07EBF-...}". The doubled
; leading brace is Inno's OWN escape (a bare "{" would start a {param}
; reference) which Inno's compiler undoes when it builds its OWN uninstall
; key, but nothing undoes it for a plain Pascal string built from that text --
; so a naive 'Uninstall\' + SetupSetting("AppId") + '_is1' never matches the
; real key. Every prior install then looked like a fresh install: no
; Update/Uninstall page, Ready page skipped, no "already installed" detection
; at all. Sidestepped entirely by having the including script #define
; MyAppGuid to the bare GUID once, and building AppId= and this key from that
; same constant so they can't drift apart.

[Code]
{ Abort (the documented "abort Setup" procedure) does NOT close the wizard
  when called from a page event like NextButtonClick -- verified with an
  instrumented no-admin test build: the log showed the uninstall branch ran
  to completion, Exec succeeded, "about to Abort" fired, and the window was
  STILL sitting on the same page afterward, Next/Cancel still live. Abort
  only cancels that one event; it does not terminate the process. A direct
  kernel32 call does, verified the same way. }
procedure ExitProcess(uExitCode: UINT);
  external 'ExitProcess@kernel32.dll stdcall';

var
  gPriorVersion:        String;
  gIsUpgrade:           Boolean;
  gUninstallChoicePage: TInputOptionWizardPage;

function VerField(const S: String; Idx: Integer): Integer;
var
  t: String;
  n, p: Integer;
begin
  t := S;
  n := 0;
  while (n < Idx) and (Pos('.', t) > 0) do
  begin
    Delete(t, 1, Pos('.', t));
    Inc(n);
  end;
  p := Pos('.', t);
  if p > 0 then
    t := Copy(t, 1, p - 1);
  Result := StrToIntDef(Trim(t), 0);
end;

{ >0 if A newer than B, <0 if older, 0 if equal (compares up to 4 dotted fields) }
function CmpVer(const A, B: String): Integer;
var
  i, x, y: Integer;
begin
  Result := 0;
  for i := 0 to 3 do
  begin
    x := VerField(A, i);
    y := VerField(B, i);
    if x > y then begin Result := 1; Exit; end;
    if x < y then begin Result := -1; Exit; end;
  end;
end;

function UninstKey(): String;
begin
  Result := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{{#MyAppGuid}}_is1';
end;

procedure DetectPrior();
var
  v: String;
begin
  gPriorVersion := '';
  gIsUpgrade := False;

  if RegQueryStringValue(HKLM64, UninstKey(), 'DisplayVersion', v) then
  begin gIsUpgrade := True; gPriorVersion := v; end
  else if RegQueryStringValue(HKLM32, UninstKey(), 'DisplayVersion', v) then
  begin gIsUpgrade := True; gPriorVersion := v; end
  else if RegQueryStringValue(HKCU, UninstKey(), 'DisplayVersion', v) then
  begin gIsUpgrade := True; gPriorVersion := v; end
  else if RegKeyExists(HKLM64, UninstKey())
       or RegKeyExists(HKLM32, UninstKey())
       or RegKeyExists(HKCU,   UninstKey()) then
    gIsUpgrade := True;
end;

function InitializeSetup(): Boolean;
begin
  DetectPrior();
  Result := True;

  if gIsUpgrade and (gPriorVersion <> '')
     and (CmpVer(gPriorVersion, '{#MyAppVersion}') > 0) then
    Result := MsgBox('{#MyAppName} ' + gPriorVersion + ' is already installed, and it is'
      + ' newer than this package (version {#MyAppVersion}).' + #13#10#13#10
      + 'Do you want to replace it with the older version?',
      mbConfirmation, MB_YESNO) = IDYES;
end;

procedure InitializeWizard();
var
  Desc: String;
begin
  if not gIsUpgrade then Exit;

  if gPriorVersion <> '' then
    Desc := '{#MyAppName} ' + gPriorVersion + ' is already installed on this computer.'
  else
    Desc := '{#MyAppName} is already installed on this computer.';

  gUninstallChoicePage := CreateInputOptionPage(wpSelectDir,
    '{#MyAppName} is already installed', 'What would you like to do?', Desc,
    True, False);
  gUninstallChoicePage.Add('&Update / reinstall to version {#MyAppVersion}');
  gUninstallChoicePage.Add('&Uninstall {#MyAppName}');
  gUninstallChoicePage.SelectedValueIndex := 0;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ResultCode: Integer;
  UninstPath: String;
  Started:    Boolean;
begin
  Result := True;
  if (gUninstallChoicePage <> nil) and (CurPageID = gUninstallChoicePage.ID)
     and (gUninstallChoicePage.SelectedValueIndex = 1) then
  begin
    // the uninstallexe constant is empty at this point in the wizard, even
    // though app itself is already correctly resolved (verified with a
    // self-driving no-admin test build -- it invokes NextButtonClick
    // directly, sidestepping the OS-level focus-stealing that makes
    // external keystroke automation unreliable for this kind of test).
    // Built manually instead -- Inno's uninstaller for a single-language
    // setup like ours is always named unins000.exe, directly in app.
    UninstPath := ExpandConstant('{app}') + '\unins000.exe';
    if not FileExists(UninstPath) then
    begin
      MsgBox('Could not find the existing uninstaller at:' + #13#10 + UninstPath
        + #13#10#13#10 + 'Nothing was uninstalled. Please uninstall {#MyAppName} '
        + 'from Windows Settings > Apps instead.', mbError, MB_OK);
      Result := False;
      Exit;
    end;

    Started := Exec(UninstPath, '', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
    if not Started then
    begin
      MsgBox('Could not start the uninstaller at:' + #13#10 + UninstPath
        + #13#10#13#10 + 'Windows error code: ' + IntToStr(ResultCode) + #13#10#13#10
        + 'Nothing was uninstalled. Please uninstall {#MyAppName} from Windows '
        + 'Settings > Apps instead.', mbError, MB_OK);
      Result := False;
      Exit;
    end;

    { Whether the user completed or cancelled that uninstall wizard, this
      Setup's job is done -- never fall through into installing a fresh copy.
      ExitProcess, not Abort -- see the comment on ExitProcess's declaration
      above for why. }
    ExitProcess(0);
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  { show the Ready page only when this is an update }
  Result := (PageID = wpReady) and (not gIsUpgrade);
end;

function SameVersion(): Boolean;
begin
  Result := gIsUpgrade and (gPriorVersion = '{#MyAppVersion}');
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpReady) and gIsUpgrade then
  begin
    if SameVersion() then
      WizardForm.NextButton.Caption := '&Reinstall'
    else
      WizardForm.NextButton.Caption := '&Update';
  end;

  if (CurPageID = wpFinished) and gIsUpgrade then
  begin
    if SameVersion() then
    begin
      WizardForm.FinishedHeadingLabel.Caption := '{#MyAppName} has been reinstalled';
      WizardForm.FinishedLabel.Caption :=
        'Version {#MyAppVersion} was reinstalled.';
    end
    else
    begin
      WizardForm.FinishedHeadingLabel.Caption := '{#MyAppName} has been updated';
      if gPriorVersion <> '' then
        WizardForm.FinishedLabel.Caption :=
          'Updated from version ' + gPriorVersion + ' to {#MyAppVersion}.'
      else
        WizardForm.FinishedLabel.Caption := '{#MyAppName} {#MyAppVersion} is now installed.';
    end;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  if SameVersion() then
    Result := '{#MyAppName} {#MyAppVersion} is already installed on this computer.' + NewLine
            + 'Setup will reinstall it (replace the current files).'
  else if gIsUpgrade and (gPriorVersion <> '') then
    Result := '{#MyAppName} ' + gPriorVersion + ' is installed on this computer.' + NewLine
            + 'Setup will update it to version {#MyAppVersion}.'
  else if gIsUpgrade then
    Result := '{#MyAppName} is already installed on this computer.' + NewLine
            + 'Setup will update it to version {#MyAppVersion}.'
  else
    Result := 'Setup is ready to install {#MyAppName} {#MyAppVersion}.';
end;
