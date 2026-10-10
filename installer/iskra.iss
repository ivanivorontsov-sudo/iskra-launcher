; Inno Setup — установка Искры для текущего пользователя, без прав администратора.
#define AppName "Искра"
#define AppId "Iskra.Launcher"
#ifndef AppVersion
  #define AppVersion "1.2.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

[Setup]
AppId={{9F2A7C10-1B4E-4D33-9A77-ISKRA000001}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Иван Воронцов
DefaultDirName={localappdata}\Iskra
DefaultGroupName=Искра
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputBaseFilename=Iskra-Setup-{#AppVersion}
OutputDir=output
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\Iskra.exe
SetupIconFile=..\src\Iskra.App\Assets\iskra.ico
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

[Languages]
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "Запускать Искру при входе в Windows"; GroupDescription: "Автозапуск:"
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Ярлыки:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\Iskra.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Fonts\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\Искра"; Filename: "{app}\Iskra.exe"
Name: "{group}\Удалить Искру"; Filename: "{uninstallexe}"
Name: "{userdesktop}\Искра"; Filename: "{app}\Iskra.exe"; Tasks: desktopicon
Name: "{userstartup}\Искра"; Filename: "{app}\Iskra.exe"; Parameters: "--tray"; Tasks: autostart

[Run]
Filename: "{app}\Iskra.exe"; Description: "Запустить Искру"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Перед удалением вернуть оформление Windows как было (тихо, без прав администратора для HKCU).
Filename: "{app}\Iskra.exe"; Parameters: "--restore-silent"; Flags: runhidden skipifdoesntexist; RunOnceId: "IskraRestore"
