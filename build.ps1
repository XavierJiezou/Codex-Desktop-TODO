$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildDir = Join-Path $root 'build'
$distDir = Join-Path $root 'dist'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$makensis = 'C:\Program Files (x86)\NSIS\makensis.exe'

if (!(Test-Path $csc)) {
  throw "csc.exe not found: $csc"
}

if (!(Test-Path $makensis)) {
  throw "makensis.exe not found: $makensis"
}

New-Item -ItemType Directory -Force -Path $buildDir, $distDir | Out-Null

& $csc `
  /nologo `
  /target:winexe `
  /platform:x64 `
  /optimize+ `
  /codepage:65001 `
  /out:"$buildDir\CodexDesktopTODO.exe" `
  /reference:System.Windows.Forms.dll `
  /reference:System.Drawing.dll `
  "$root\src\CodexDesktopTodo.cs"

& $makensis "$root\installer\CodexDesktopTODO.nsi"

Get-ChildItem "$buildDir\CodexDesktopTODO.exe", "$distDir\CodexDesktopTODO-Setup-0.2.0.exe" |
  Select-Object FullName, Length, LastWriteTime
