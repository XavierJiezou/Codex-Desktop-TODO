Unicode true
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 8

!define APP_NAME "Codex Desktop TODO"
!define APP_EXE "CodexDesktopTODO.exe"
!define APP_VERSION "0.2.0"

Name "${APP_NAME}"
OutFile "..\dist\CodexDesktopTODO-Setup-${APP_VERSION}.exe"
InstallDir "$LOCALAPPDATA\CodexDesktopTODO"
InstallDirRegKey HKCU "Software\CodexDesktopTODO" "InstallDir"

Page directory
Page instfiles

Section "Install"
  SetOutPath "$INSTDIR"
  File "..\build\${APP_EXE}"
  WriteRegStr HKCU "Software\CodexDesktopTODO" "InstallDir" "$INSTDIR"
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  Delete "$INSTDIR\${APP_EXE}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "Software\CodexDesktopTODO"
SectionEnd
