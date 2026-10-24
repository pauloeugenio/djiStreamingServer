Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"

Name "Drone Streaming Server"
OutFile "../install-windows.exe"
InstallDir "$LOCALAPPDATA\Programs\DroneStreamingServer"
InstallDirRegKey HKCU "Software\DroneStreamingServer" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "Drone Streaming Server · Paulo Eugenio"
VIProductVersion "1.0.0.0"
VIAddVersionKey /LANG=1046 "ProductName" "Drone Streaming Server"
VIAddVersionKey /LANG=1046 "FileDescription" "Instalador do Drone Streaming Server para Windows x64"
VIAddVersionKey /LANG=1046 "FileVersion" "1.0.0.0"
VIAddVersionKey /LANG=1046 "CompanyName" "Paulo Eugenio"
VIAddVersionKey /LANG=1046 "LegalCopyright" "Paulo Eugenio"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "Seu drone. Sua rede. Sua transmissão."
!define MUI_WELCOMEPAGE_TEXT "Este assistente instala o Drone Streaming Server para Windows, com player integrado.$\r$\n$\r$\nNão precisa de Docker nem Python.$\r$\n$\r$\nSe necessário, será instalado o Microsoft Edge WebView2 Runtime (requer internet).$\r$\n$\r$\nO servidor só será iniciado quando você abrir o aplicativo e clicar em Iniciar servidor."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TEXT "Instalação concluída.$\r$\n$\r$\nAbra Drone Streaming Server pelo atalho na área de trabalho ou no menu Iniciar.$\r$\n$\r$\nA transmissão só será iniciada pelo botão Iniciar servidor dentro do aplicativo."
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "PortugueseBR"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "Este instalador requer Windows 10 ou 11 de 64 bits (x64)."
    Abort
  ${EndIf}
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_ICONSTOP "Este aplicativo requer Windows 10 ou 11."
    Abort
  ${EndIf}
  SetRegView 64
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $0 < 461808
    MessageBox MB_ICONSTOP "Instale o .NET Framework 4.7.2 ou mais recente pelo Windows Update antes de continuar."
    Abort
  ${EndIf}
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\DroneStreamingServer.Desktop") p.r0'
  ${If} $0 != 0
    System::Call 'kernel32::CloseHandle(p r0)'
    MessageBox MB_ICONSTOP "Feche o Drone Streaming Server antes de instalar ou atualizar."
    Abort
  ${EndIf}
FunctionEnd

Function DetectWebView
  StrCpy $2 ""
  ReadRegStr $0 HKLM "SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
  ReadRegStr $1 HKCU "SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
  ${If} $0 != ""
  ${AndIf} $0 != "0.0.0.0"
    StrCpy $2 "installed"
  ${EndIf}
  ${If} $1 != ""
  ${AndIf} $1 != "0.0.0.0"
    StrCpy $2 "installed"
  ${EndIf}
FunctionEnd

Section "Instalar aplicativo" Main
  SetShellVarContext current
  SetRegView 64
  Call DetectWebView
  ${If} $2 == ""
    InitPluginsDir
    SetOutPath "$PLUGINSDIR"
    File "build/MicrosoftEdgeWebview2Setup.exe"
    DetailPrint "Preparando o player integrado (Microsoft WebView2)..."
    ExecWait '"$PLUGINSDIR\MicrosoftEdgeWebview2Setup.exe" /silent /install' $0
    Call DetectWebView
    ${If} $2 == ""
      MessageBox MB_ICONSTOP "O WebView2 Runtime não foi detectado após a instalação. Verifique a internet e instale o Microsoft Edge WebView2 Runtime antes de tentar novamente."
      SetErrorLevel 1
      Abort
    ${EndIf}
  ${EndIf}
  SetOutPath "$INSTDIR"
  File "build/DroneStreamingServer.exe"
  File "build/DroneStreamingServer.exe.config"
  File "build/mediamtx.exe"
  File "build/Microsoft.Web.WebView2.Core.dll"
  File "build/Microsoft.Web.WebView2.WinForms.dll"
  File "build/WebView2Loader.dll"
  File "build/LICENSE-MediaMTX.txt"
  File "build/LICENSE-WebView2.txt"
  File "build/README.md"
  SetOverwrite off
  File "build/about.json"
  SetOverwrite on
  WriteUninstaller "$INSTDIR\uninstall.exe"
  CreateDirectory "$SMPROGRAMS\Drone Streaming Server"
  CreateShortcut "$SMPROGRAMS\Drone Streaming Server\Drone Streaming Server.lnk" "$INSTDIR\DroneStreamingServer.exe"
  CreateShortcut "$SMPROGRAMS\Drone Streaming Server\Desinstalar.lnk" "$INSTDIR\uninstall.exe"
  CreateShortcut "$DESKTOP\Drone Streaming Server.lnk" "$INSTDIR\DroneStreamingServer.exe"
  WriteRegStr HKCU "Software\DroneStreamingServer" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DroneStreamingServer" "DisplayName" "Drone Streaming Server"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DroneStreamingServer" "DisplayVersion" "1.0.0"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DroneStreamingServer" "Publisher" "Paulo Eugenio"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DroneStreamingServer" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DroneStreamingServer" "DisplayIcon" "$INSTDIR\DroneStreamingServer.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DroneStreamingServer" "UninstallString" '$\"$INSTDIR\uninstall.exe$\"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DroneStreamingServer" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DroneStreamingServer" "NoRepair" 1
SectionEnd

Function un.onInit
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\DroneStreamingServer.Desktop") p.r0'
  ${If} $0 != 0
    System::Call 'kernel32::CloseHandle(p r0)'
    MessageBox MB_ICONSTOP "Feche o Drone Streaming Server antes de desinstalar."
    Abort
  ${EndIf}
FunctionEnd

Section "Uninstall"
  SetShellVarContext current
  SetRegView 64
  Delete "$DESKTOP\Drone Streaming Server.lnk"
  Delete "$SMPROGRAMS\Drone Streaming Server\Drone Streaming Server.lnk"
  Delete "$SMPROGRAMS\Drone Streaming Server\Desinstalar.lnk"
  RMDir "$SMPROGRAMS\Drone Streaming Server"
  Delete "$INSTDIR\DroneStreamingServer.exe"
  Delete "$INSTDIR\DroneStreamingServer.exe.config"
  Delete "$INSTDIR\mediamtx.exe"
  Delete "$INSTDIR\Microsoft.Web.WebView2.Core.dll"
  Delete "$INSTDIR\Microsoft.Web.WebView2.WinForms.dll"
  Delete "$INSTDIR\WebView2Loader.dll"
  Delete "$INSTDIR\LICENSE-MediaMTX.txt"
  Delete "$INSTDIR\LICENSE-WebView2.txt"
  Delete "$INSTDIR\README.md"
  Delete "$INSTDIR\about.json"
  Delete "$INSTDIR\uninstall.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "Software\DroneStreamingServer"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\DroneStreamingServer"
  ; Logs e preferências em LocalAppData são preservados; o runtime é compartilhado.
SectionEnd
