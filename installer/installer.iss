; SparkForge - Inno Setup Installer Script
; Build with: iscc installer.iss

#define MyAppName "SparkForge"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SMF Works"
#define MyAppURL "https://github.com/smfworks/smf-sparkforge"
#define MyAppExeName "SparkForge.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE.txt
OutputDir=..\installer
OutputBaseFilename=SparkForge-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardImageFile=installer-sidebar.bmp
WizardSmallImageFile=installer-small.bmp
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "startmenu"; Description: "Create a &Start Menu shortcut"; GroupDescription: "Additional shortcuts:"
Name: "autostart"; Description: "&Start with Windows (minimized to tray)"; GroupDescription: "Startup options:"

[Files]
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\*.pdb"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenu
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--minimized"; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\SparkForge"

[Code]
function GetOllamaInstalled: Boolean;
begin
  Result := RegKeyExists(HKEY_CURRENT_USER, 'Software\Ollama') or
            RegKeyExists(HKEY_LOCAL_MACHINE, 'Software\Ollama') or
            FileExists(ExpandConstant('{localappdata}\Programs\Ollama\ollama.exe')) or
            FileExists(ExpandConstant('{pf}\Ollama\ollama.exe'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if not GetOllamaInstalled then
    begin
      MsgBox(
        'Ollama is not installed on this system.' + #13#10 + #13#10 +
        'SparkForge requires Ollama to run AI models locally.' + #13#10 +
        'Please download and install Ollama from https://ollama.com' + #13#10 + #13#10 +
        'The app will guide you through setup on first run.',
        mbInformation, MB_OK
      );
    end;
  end;
end;
