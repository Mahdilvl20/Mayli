; Mayli - The Notch for Windows
; NSIS (Nullsoft Scriptable Install System) Setup Script

!include "MUI2.nsh"
!include "FileFunc.nsh"

Name "Mayli"
OutFile "..\dist\Mayli_NSIS_Setup.exe"
InstallDir "$LOCALAPPDATA\Programs\Mayli"
InstallDirRegKey HKCU "Software\Mayli" "InstallDir"
RequestExecutionLevel user

!define MUI_ABORTWARNING
!define MUI_ICON "..\src\Mayli\Resources\Vayli.Brand.tray-dark.ico"
!define MUI_UNICON "..\src\Mayli\Resources\Vayli.Brand.tray-dark.ico"

; Pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\Mayli.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Start Mayli now"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Section "Mayli Core" SecCore
  SetOutPath "$INSTDIR"
  File /r "..\src\Mayli\bin\Release\net10.0-windows10.0.19041.0\*.*"

  ; Create Shortcuts
  CreateDirectory "$SMPROGRAMS\Mayli"
  CreateShortcut "$SMPROGRAMS\Mayli\Mayli.lnk" "$INSTDIR\Mayli.exe"
  CreateShortcut "$SMPROGRAMS\Mayli\Uninstall Mayli.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut "$DESKTOP\Mayli.lnk" "$INSTDIR\Mayli.exe"

  ; Uninstaller
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  ; Registry Keys
  WriteRegStr HKCU "Software\Mayli" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Mayli" "DisplayName" "Mayli — The Notch for Windows"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Mayli" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Mayli" "DisplayIcon" "$INSTDIR\Mayli.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Mayli" "DisplayVersion" "1.1.59"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Mayli" "Publisher" "Modi Studio"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\Mayli.lnk"
  Delete "$SMPROGRAMS\Mayli\Mayli.lnk"
  Delete "$SMPROGRAMS\Mayli\Uninstall Mayli.lnk"
  RMDir "$SMPROGRAMS\Mayli"

  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Mayli"
  DeleteRegKey HKCU "Software\Mayli"
SectionEnd
