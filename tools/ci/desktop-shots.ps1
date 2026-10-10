# Проверка на windows-latest: рабочий стол и Проводник до/после применения тем «Искры».
param([string]$Exe = "publish\Iskra.exe", [string]$Out = "screens", [string[]]$Themes = @('notebook','spotlight','neon','mica','tiles'))
$ErrorActionPreference = 'Continue'
trap { Write-Warning $_; continue }
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
New-Item -ItemType Directory -Force $Out | Out-Null
$report = Join-Path $env:LOCALAPPDATA 'Iskra\ci-report.txt'

function Shot($name) {
  $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
  $bmp.Save((Join-Path $Out "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
  Write-Host "screenshot $name"
}
function ShowDesktop { (New-Object -ComObject Shell.Application).MinimizeAll(); Start-Sleep 3 }
function ExplorerShot($name) {
  Start-Process explorer.exe "$env:USERPROFILE\Desktop"; Start-Sleep 5; Shot $name
  (New-Object -ComObject Shell.Application).Windows() | ForEach-Object { try { $_.Quit() } catch {} }
  Start-Sleep 1
}

# Тестовый рабочий стол: ярлыки, общий ярлык, папка, .url, обычные файлы.
$desk = [Environment]::GetFolderPath('Desktop'); $pub = [Environment]::GetFolderPath('CommonDesktopDirectory')
$wsh = New-Object -ComObject WScript.Shell
foreach ($p in @(@{n='Notepad';t="$env:WINDIR\System32\notepad.exe"}, @{n='Calculator';t="$env:WINDIR\System32\calc.exe"},
                 @{n='Paint';t="$env:WINDIR\System32\mspaint.exe"}, @{n='PowerShell';t="$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"},
                 @{n='Command Prompt';t="$env:WINDIR\System32\cmd.exe"})) {
  $l = $wsh.CreateShortcut((Join-Path $desk ($p.n + '.lnk'))); $l.TargetPath = $p.t; $l.Save() }
$l = $wsh.CreateShortcut((Join-Path $pub 'Explorer (Public).lnk')); $l.TargetPath = "$env:WINDIR\explorer.exe"; $l.Save()
New-Item -ItemType Directory -Force (Join-Path $desk 'Projects') | Out-Null
Set-Content (Join-Path $desk 'Website.url') "[InternetShortcut]`r`nURL=https://example.com"
Set-Content (Join-Path $desk 'Note.txt') 'текст'
New-Item -ItemType File -Force (Join-Path $desk 'Document.docx') | Out-Null

ShowDesktop; Shot 'desktop-before'
foreach ($t in $Themes) {
  Remove-Item $report -ErrorAction SilentlyContinue
  $p = Start-Process $Exe -ArgumentList '--ci-apply-theme', $t -PassThru -Wait
  if (Test-Path $report) { Get-Content $report | Write-Host }
  Write-Host "apply $t exit=$($p.ExitCode)"
  Start-Sleep 6
  ShowDesktop; Shot "desktop-$t"
  ExplorerShot "explorer-$t"
  $wm = Get-ItemProperty 'HKCU:\Control Panel\Desktop\WindowMetrics'
  Write-Host "grid $t : IconSpacing=$($wm.IconSpacing) IconVerticalSpacing=$($wm.IconVerticalSpacing)"
  Get-ChildItem $desk -Filter *.lnk | ForEach-Object { Write-Host ("  {0} -> {1}" -f $_.Name, $wsh.CreateShortcut($_.FullName).IconLocation) }
}
Remove-Item $report -ErrorAction SilentlyContinue
Start-Process $Exe -ArgumentList '--ci-repair' -Wait
if (Test-Path $report) { Get-Content $report | Write-Host }
Start-Sleep 6; ShowDesktop; Shot 'desktop-after-repair'
