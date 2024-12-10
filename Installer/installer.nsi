;--------------------------------
;Plugins
;https://nsis.sourceforge.io/ApplicationID_plug-in
;https://nsis.sourceforge.io/ShellExecAsUser_plug-in
;https://nsis.sourceforge.io/NsProcess_plugin
;https://nsis.sourceforge.io/Inetc_plug-in

;--------------------------------
;Version

    !define PRODUCT_VERSION "1.0.0.0"
    !define VERSION "1.0.0.0"
    VIProductVersion "${PRODUCT_VERSION}"
    VIFileVersion "${VERSION}"
    VIAddVersionKey "FileVersion" "${VERSION}"
    VIAddVersionKey "ProductName" "OpenShock Desktop"
    VIAddVersionKey "ProductVersion" "${PRODUCT_VERSION}"
    VIAddVersionKey "LegalCopyright" "Copyright OpenShock"
    VIAddVersionKey "FileDescription" ""

;--------------------------------
;Include Modern UI

    !include "MUI2.nsh"
    !include "FileFunc.nsh"
    !include "LogicLib.nsh"

;--------------------------------
;General

    Unicode True
    Name "OpenShock Desktop"
    OutFile "OpenShock_Desktop_Setup.exe"
    InstallDir "$LocalAppdata\OpenShock\Desktop"
    InstallDirRegKey HKLM "Software\OpenShockDesktop" "InstallDir"
    RequestExecutionLevel admin
    ShowInstDetails show

;--------------------------------
;Variables

    VAR upgradeInstallation

;--------------------------------
;Interface Settings

    !define MUI_ABORTWARNING

;--------------------------------
;Icons

    !define MUI_ICON "..\publish\Resources\openshock-icon.ico"
    !define MUI_UNICON "..\publish\Resources\openshock-icon.ico"

;--------------------------------
;Pages

    !define MUI_PAGE_CUSTOMFUNCTION_PRE SkipIfUpgrade
    !insertmacro MUI_PAGE_LICENSE "..\LICENSE"

    !define MUI_PAGE_CUSTOMFUNCTION_PRE SkipIfUpgrade
    !insertmacro MUI_PAGE_DIRECTORY

    !insertmacro MUI_PAGE_INSTFILES

    ;------------------------------
    ; Finish Page

    ; Checkbox to launch OpenShock Desktop.
    !define MUI_FINISHPAGE_RUN
    !define MUI_FINISHPAGE_RUN_TEXT "Launch OpenShock Desktop"
    !define MUI_FINISHPAGE_RUN_FUNCTION launchOpenShockDesktop

    ; Checkbox to create desktop shortcut.
    !define MUI_FINISHPAGE_SHOWREADME
    !define MUI_FINISHPAGE_SHOWREADME_TEXT "Create desktop shortcut"
    !define MUI_FINISHPAGE_SHOWREADME_FUNCTION createDesktopShortcut

    !define MUI_PAGE_CUSTOMFUNCTION_PRE SkipIfUpgrade
    !insertmacro MUI_PAGE_FINISH

    !insertmacro MUI_UNPAGE_CONFIRM
    !insertmacro MUI_UNPAGE_INSTFILES
    !insertmacro MUI_UNPAGE_FINISH

;--------------------------------
;Languages

    !insertmacro MUI_LANGUAGE "English"

;--------------------------------
;Macros

;--------------------------------
;Functions

Function SkipIfUpgrade
    StrCmp $upgradeInstallation 0 noUpgrade
        Abort
    noUpgrade:
FunctionEnd

Function .onInit
    StrCpy $upgradeInstallation 0

    ReadRegStr $R0 HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenShockDesktop" "UninstallString"
    StrCmp $R0 "" notInstalled
        StrCpy $upgradeInstallation 1
    notInstalled:

    ; If OpenShock Desktop is already running, display a warning message
    loop:
    StrCpy $1 "OpenShock.Desktop.exe"
    nsProcess::_FindProcess "$1"
    Pop $R1
    ${If} $R1 = 0
        MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION "OpenShock Desktop is still running. $\n$\nClick `OK` to kill the running process or `Cancel` to cancel this installer." /SD IDOK IDCANCEL cancel
            nsExec::ExecToStack "taskkill /IM OpenShock.Desktop.exe"
    ${Else}
        Goto done
    ${EndIf}
    Sleep 1000
    Goto loop

    cancel:
        Abort
    done:
FunctionEnd

Function .onInstSuccess
    ${If} $upgradeInstallation = 1
        Call launchOpenShockDesktop
    ${EndIf}
FunctionEnd

Function createDesktopShortcut
    CreateShortcut "$DESKTOP\OpenShock.lnk" "$INSTDIR\OpenShock.Desktop.exe"
FunctionEnd

Function launchOpenShockDesktop
    SetOutPath $INSTDIR
    ShellExecAsUser::ShellExecAsUser "" "$INSTDIR\OpenShock.Desktop.exe" ""
FunctionEnd

;--------------------------------
;Installer Sections

Section "Install" SecInstall

    StrCmp $upgradeInstallation 0 noUpgrade
        DetailPrint "Uninstall previous version..."
        ExecWait '"$INSTDIR\Uninstall.exe" /S _?=$INSTDIR'
        Delete $INSTDIR\Uninstall.exe
        Goto afterUpgrade
    noUpgrade:
    
    inetc::get "https://aka.ms/vs/17/release/vc_redist.x64.exe" $TEMP\vcredist_x64.exe
    ExecWait "$TEMP\vcredist_x64.exe /install /quiet /norestart"
    Delete "$TEMP\vcredist_x64.exe"

    afterUpgrade:

    SetOutPath "$INSTDIR"

    File /r /x *.log /x *.pdb /x *.mui "..\publish\*.*"

    WriteRegStr HKLM "Software\OpenShockDesktop" "InstallDir" $INSTDIR
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenShockDesktop" "DisplayName" "OpenShock Desktop"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenShockDesktop" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenShockDesktop" "DisplayIcon" "$\"$INSTDIR\Resources\openshock-icon.ico$\""

    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    IntFmt $0 "0x%08X" $0
    WriteRegDWORD HKLM  "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenShockDesktop" "EstimatedSize" "$0"

    CreateShortCut "$SMPROGRAMS\OpenShock.lnk" "$INSTDIR\OpenShock.Desktop.exe"
    ApplicationID::Set "$SMPROGRAMS\OpenShock.lnk" "OpenShock"

    WriteRegStr HKCU "Software\Classes\OpenShockDesktop" "" "URL:OpenShock"
    WriteRegStr HKCU "Software\Classes\OpenShockDesktop" "FriendlyTypeName" "OpenShock"
    WriteRegStr HKCU "Software\Classes\OpenShockDesktop" "URL Protocol" ""
    WriteRegExpandStr HKCU "Software\Classes\OpenShockDesktop\DefaultIcon" "" "$INSTDIR\Resources\openshock-icon.ico"
    WriteRegStr HKCU "Software\Classes\OpenShockDesktop\shell" "" "open"
    WriteRegStr HKCU "Software\Classes\OpenShockDesktop\shell\open" "FriendlyAppName" "OpenShock Desktop"
    WriteRegStr HKCU "Software\Classes\OpenShockDesktop\shell\open\command" "" '"$INSTDIR\OpenShock.Desktop.exe" --uri="%1"'
SectionEnd

;--------------------------------
;Uninstaller Section

Section "Uninstall"
    ; If OpenShock Desktop is already running, display a warning message and exit
    StrCpy $1 "OpenShock.Desktop.exe"
    nsProcess::_FindProcess "$1"
    Pop $R1
    ${If} $R1 = 0
        MessageBox MB_OK|MB_ICONEXCLAMATION "OpenShock Desktop is still running. Cannot uninstall this software.$\nPlease close OpenShock Desktop and try again." /SD IDOK
        Abort
    ${EndIf}

    RMDir /r "$INSTDIR"

    DeleteRegKey HKLM "Software\OpenShockDesktop"
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OpenShockDesktop"
    DeleteRegKey HKCU "Software\Classes\OpenShockDesktop"

    ${IfNot} ${Silent}
        Delete "$SMPROGRAMS\OpenShock.lnk"
        Delete "$DESKTOP\OpenShock.lnk"
    ${EndIf}
SectionEnd
