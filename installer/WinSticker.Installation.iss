[Code]
const
  WinStickerProductAppId = '{79802744-9348-4104-8293-7008953D084E}';
  WinStickerLegacyExeName = 'WinSticker.exe';
  WinStickerUninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{79802744-9348-4104-8293-7008953D084E}_is1';
  WinStickerWowUninstallKey = 'Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{79802744-9348-4104-8293-7008953D084E}_is1';
  WinStickerInstallStateKey = 'Software\WinSticker\DirectInstall';

var
  DirectInstallUpgrade: Boolean;
  ExistingInstallPath: string;
  ExistingInstallCount: Integer;
  ExistingInstallCandidates: string;

function NormalizeDirPath(Path: string): string;
begin
  Result := RemoveBackslashUnlessRoot(ExpandConstant(Trim(Path)));
end;

function SameInstallPath(LeftPath: string; RightPath: string): Boolean;
begin
  Result := CompareText(NormalizeDirPath(LeftPath), NormalizeDirPath(RightPath)) = 0;
end;

function ExtractExecutablePath(CommandLine: string): string;
var
  RemainingText: string;
  EndPosition: Integer;
begin
  Result := '';
  CommandLine := Trim(CommandLine);
  if CommandLine = '' then
    Exit;

  if Copy(CommandLine, 1, 1) = '"' then
  begin
    RemainingText := Copy(CommandLine, 2, MaxInt);
    EndPosition := Pos('"', RemainingText);
    if EndPosition > 0 then
      Result := Copy(RemainingText, 1, EndPosition - 1);
    Exit;
  end;

  EndPosition := Pos(' ', CommandLine);
  if EndPosition > 0 then
    Result := Copy(CommandLine, 1, EndPosition - 1)
  else
    Result := CommandLine;
end;

function IsWinStickerInstallPath(Path: string): Boolean;
var
  NormalizedPath: string;
begin
  NormalizedPath := NormalizeDirPath(Path);
  Result :=
    (NormalizedPath <> '') and
    DirExists(NormalizedPath) and
    FileExists(AddBackslash(NormalizedPath) + WinStickerLegacyExeName);
end;

function IsRegisteredWinStickerInstallPath(Path: string): Boolean;
var
  NormalizedPath: string;
begin
  NormalizedPath := NormalizeDirPath(Path);
  Result :=
    (NormalizedPath <> '') and
    DirExists(NormalizedPath) and
    (IsWinStickerInstallPath(NormalizedPath) or
     FileExists(AddBackslash(NormalizedPath) + 'WinSticker.Updater.exe') or
     FileExists(AddBackslash(NormalizedPath) + 'WinSticker.runtimeconfig.json'));
end;

function InstallCandidateListContains(Path: string): Boolean;
var
  Needle: string;
  Haystack: string;
begin
  Needle := Uppercase(#13#10 + NormalizeDirPath(Path) + #13#10);
  Haystack := Uppercase(#13#10 + ExistingInstallCandidates + #13#10);
  Result := Pos(Needle, Haystack) > 0;
end;

procedure AddInstallCandidate(Path: string; Source: string; RequireExecutable: Boolean);
var
  NormalizedPath: string;
begin
  NormalizedPath := NormalizeDirPath(Path);
  if (NormalizedPath = '') or InstallCandidateListContains(NormalizedPath) then
    Exit;

  if RequireExecutable then
  begin
    if not IsWinStickerInstallPath(NormalizedPath) then
      Exit;
  end
  else if not IsRegisteredWinStickerInstallPath(NormalizedPath) then
    Exit;

  if ExistingInstallCandidates = '' then
    ExistingInstallCandidates := NormalizedPath
  else
    ExistingInstallCandidates := ExistingInstallCandidates + #13#10 + NormalizedPath;

  ExistingInstallCount := ExistingInstallCount + 1;
  if ExistingInstallPath = '' then
    ExistingInstallPath := NormalizedPath;

  Log('WinSticker install candidate detected from ' + Source + ': ' + NormalizedPath);
end;

procedure AddRegistryInstallCandidate(RootKey: Integer; KeyName: string; Source: string);
var
  InstallPath: string;
begin
  InstallPath := '';
  if RegQueryStringValue(RootKey, KeyName, 'InstallLocation', InstallPath) then
    AddInstallCandidate(InstallPath, Source, False);
end;

function TryReadShortcutTarget(ShortcutPath: string; var TargetPath: string): Boolean;
var
  ShellObject: Variant;
  ShortcutObject: Variant;
begin
  Result := False;
  TargetPath := '';
  if not FileExists(ShortcutPath) then
    Exit;

  try
    ShellObject := CreateOleObject('WScript.Shell');
    ShortcutObject := ShellObject.CreateShortcut(ShortcutPath);
    TargetPath := Trim(Format('%s', [ShortcutObject.TargetPath]));
    Result := TargetPath <> '';
  except
    Log('WinSticker could not inspect shortcut: ' + ShortcutPath);
  end;
end;

function ShortcutTargetsInstall(ShortcutPath: string; InstallPath: string): Boolean;
var
  TargetPath: string;
begin
  Result :=
    TryReadShortcutTarget(ShortcutPath, TargetPath) and
    SameInstallPath(ExtractFileDir(TargetPath), InstallPath) and
    (CompareText(ExtractFileName(TargetPath), WinStickerLegacyExeName) = 0);
end;

procedure AddShortcutInstallCandidate(ShortcutPath: string);
var
  TargetPath: string;
  TargetDirectory: string;
begin
  if not TryReadShortcutTarget(ShortcutPath, TargetPath) then
    Exit;

  if CompareText(ExtractFileName(TargetPath), WinStickerLegacyExeName) <> 0 then
    Exit;

  TargetDirectory := ExtractFileDir(TargetPath);
  AddInstallCandidate(TargetDirectory, 'shortcut ' + ShortcutPath, True);
end;

procedure CollectDirectInstallCandidates;
begin
  ExistingInstallPath := '';
  ExistingInstallCount := 0;
  ExistingInstallCandidates := '';

  AddRegistryInstallCandidate(HKEY_CURRENT_USER, WinStickerUninstallKey, 'HKCU uninstall');
  AddRegistryInstallCandidate(HKEY_CURRENT_USER, WinStickerWowUninstallKey, 'HKCU 32-bit uninstall');
  AddRegistryInstallCandidate(HKEY_LOCAL_MACHINE, WinStickerUninstallKey, 'HKLM uninstall');
  AddRegistryInstallCandidate(HKEY_LOCAL_MACHINE, WinStickerWowUninstallKey, 'HKLM 32-bit uninstall');
  AddRegistryInstallCandidate(HKEY_CURRENT_USER, WinStickerInstallStateKey, 'HKCU WinSticker install state');
  AddRegistryInstallCandidate(HKEY_LOCAL_MACHINE, WinStickerInstallStateKey, 'HKLM WinSticker install state');

  AddInstallCandidate(ExpandConstant('{localappdata}\Programs\WinSticker'), 'current default path', True);
  AddInstallCandidate(ExpandConstant('{localappdata}\WinSticker'), 'legacy user path', True);
  AddInstallCandidate(ExpandConstant('{commonpf}\WinSticker'), 'default Program Files path', True);
  AddInstallCandidate(ExpandConstant('{commonpf32}\WinSticker'), 'default Program Files (x86) path', True);

  AddShortcutInstallCandidate(ExpandConstant('{userprograms}\WinSticker.lnk'));
  AddShortcutInstallCandidate(ExpandConstant('{commonprograms}\WinSticker.lnk'));
  AddShortcutInstallCandidate(ExpandConstant('{userdesktop}\WinSticker.lnk'));
  AddShortcutInstallCandidate(ExpandConstant('{commondesktop}\WinSticker.lnk'));
  AddShortcutInstallCandidate(ExpandConstant('{userstartup}\WinSticker.lnk'));
  AddShortcutInstallCandidate(ExpandConstant('{commonstartup}\WinSticker.lnk'));
  AddShortcutInstallCandidate(ExpandConstant('{userappdata}\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\WinSticker.lnk'));
end;

function TryReadExplicitDirectory(var DirectoryPath: string): Boolean;
var
  Index: Integer;
  Parameter: string;
  Prefix: string;
begin
  Result := False;
  DirectoryPath := '';
  Prefix := '/DIR=';

  for Index := 1 to ParamCount do
  begin
    Parameter := ParamStr(Index);
    if CompareText(Copy(Parameter, 1, Length(Prefix)), Prefix) = 0 then
    begin
      DirectoryPath := Copy(Parameter, Length(Prefix) + 1, MaxInt);
      if (Length(DirectoryPath) >= 2) and
         (Copy(DirectoryPath, 1, 1) = '"') and
         (Copy(DirectoryPath, Length(DirectoryPath), 1) = '"') then
        DirectoryPath := Copy(DirectoryPath, 2, Length(DirectoryPath) - 2);

      DirectoryPath := Trim(DirectoryPath);
      Result := DirectoryPath <> '';
      Exit;
    end;
  end;
end;

function GetDefaultInstallDir(Param: string): string;
begin
  if DirectInstallUpgrade and (ExistingInstallPath <> '') then
    Result := ExistingInstallPath
  else
    Result := ExpandConstant('{autopf}\WinSticker');
end;

function GetInstallScopeName(Param: string): string;
begin
  if IsAdminInstallMode then
    Result := 'all-users'
  else
    Result := 'current-user';
end;

function BuildInstallCandidateList: string;
begin
  Result := ExistingInstallCandidates;
  StringChangeEx(Result, #13#10, #13#10 + '  ', True);
  if Result <> '' then
    Result := '  ' + Result;
end;

function ShouldSuppressDirectInstallMessages: Boolean;
var
  Index: Integer;
  Parameter: string;
begin
  Result := False;
  for Index := 1 to ParamCount do
  begin
    Parameter := Uppercase(ParamStr(Index));
    if (Parameter = '/VERYSILENT') or (Parameter = '/SUPPRESSMSGBOXES') then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function PrepareDirectInstallPlan: Boolean;
var
  ExplicitDirectory: string;
  MessageText: string;
begin
  Result := False;
  DirectInstallUpgrade := False;
  CollectDirectInstallCandidates;

  if TryReadExplicitDirectory(ExplicitDirectory) and IsWinStickerInstallPath(ExplicitDirectory) then
    AddInstallCandidate(ExplicitDirectory, 'explicit /DIR path', True);

  if ExistingInstallCount > 1 then
  begin
    MessageText :=
      ExpandConstant('{cm:MultipleInstallationsTitle}') + #13#10#13#10 +
      FmtMessage(ExpandConstant('{cm:MultipleInstallationsBody}'), [BuildInstallCandidateList]) + #13#10#13#10 +
      ExpandConstant('{cm:MultipleInstallationsFooter}');
    Log('WinSticker installation blocked because multiple installations were detected: ' + ExistingInstallCandidates);
    if not ShouldSuppressDirectInstallMessages then
      MsgBox(MessageText, mbError, MB_OK);
    Exit;
  end;

  if ExistingInstallCount = 1 then
  begin
    DirectInstallUpgrade := True;
    if TryReadExplicitDirectory(ExplicitDirectory) and
       (ExplicitDirectory <> '') and
       (not SameInstallPath(ExplicitDirectory, ExistingInstallPath)) then
    begin
      MessageText := FmtMessage(ExpandConstant('{cm:UpgradeDirectoryMismatch}'), [ExistingInstallPath, ExplicitDirectory]);
      Log('WinSticker installation blocked because /DIR does not match the existing install: ' + ExplicitDirectory);
      if not ShouldSuppressDirectInstallMessages then
        MsgBox(MessageText, mbError, MB_OK);
      DirectInstallUpgrade := False;
      Exit;
    end;

    Log('WinSticker upgrade locked to existing install directory: ' + ExistingInstallPath);
  end
  else
    Log('WinSticker installation plan: first install.');

  Result := True;
end;

function EscapePowerShellString(Value: string): string;
begin
  Result := Value;
  StringChangeEx(Result, '''', '''''', True);
end;

function StopWinStickerProcessesAtPath(InstallPath: string): Boolean;
var
  PowerShellPath: string;
  CommandLine: string;
  Parameters: string;
  ResultCode: Integer;
begin
  Result := True;
  InstallPath := NormalizeDirPath(InstallPath);
  if InstallPath = '' then
    Exit;

  PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  if not FileExists(PowerShellPath) then
  begin
    Log('WinSticker could not find Windows PowerShell for path-scoped process shutdown.');
    Result := False;
    Exit;
  end;

  CommandLine :=
    '$target = [System.IO.Path]::GetFullPath(''' + EscapePowerShellString(InstallPath) + ''').TrimEnd(''\''); ' +
    'Get-CimInstance Win32_Process | ' +
    'Where-Object { $_.Name -ieq ''WinSticker.exe'' -and $_.ExecutablePath -and ' +
    '([System.IO.Path]::GetDirectoryName($_.ExecutablePath)).TrimEnd(''\'') -ieq $target } | ' +
    'ForEach-Object { Stop-Process -Id $_.ProcessId -Force }';
  Parameters := '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "' + CommandLine + '"';

  Log('WinSticker stopping processes under: ' + InstallPath);
  if not Exec(PowerShellPath, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Log('WinSticker path-scoped process shutdown could not be started.');
    Result := False;
    Exit;
  end;

  Log('WinSticker path-scoped process shutdown exit code: ' + IntToStr(ResultCode));
  Result := ResultCode = 0;
end;
