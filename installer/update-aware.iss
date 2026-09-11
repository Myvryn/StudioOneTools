; Shared code: makes an installer present itself as an "Update" when a previous
; install of the same AppId is already on the machine -- even if the payload is
; byte-identical. #include this at the end of a script that has defined
; MyAppName / MyAppVersion and set  DisableReadyPage=no .
;
; Fresh install : Ready page stays hidden -> double-click, UAC, done.
; Existing install: a one-line Ready page appears, the button reads "Update",
;                   and the Finished page says "has been updated".

[Code]
var
  gPriorVersion: String;
  gIsUpgrade:    Boolean;

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
  Result := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1';
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
