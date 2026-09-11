# ==============================================================================
# VectorPowerHub MSI Builder
# Uses native WindowsInstaller COM automation and makecab.exe (Zero dependencies)
# ==============================================================================
param(
    [string]$Version = "1.0.0",
    [string]$OutputDir = "bin",
    [string]$ProductCode = "{B95C3C8F-7B83-49F1-99A8-1E92A1A88301}",
    [string]$UpgradeCode = "{2B94A9A0-394E-40DC-BE4F-E0DC47FB5412}"
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $ProjectRoot

$BinDir = Join-Path $ProjectRoot $OutputDir
if (-not (Test-Path $BinDir)) {
    New-Item -ItemType Directory -Path $BinDir -Force | Out-Null
}

$ExePath = Join-Path $BinDir "VectorPowerHub.exe"
$IcoPath = Join-Path $ProjectRoot "app.ico"
$ReadmePath = Join-Path $ProjectRoot "README.md"
$MsiPath = Join-Path $BinDir "VectorPowerHub.msi"

# 1. Compile VectorPowerHub.exe if not present
if (-not (Test-Path $ExePath)) {
    Write-Host "[BUILD] Compiling VectorPowerHub.exe..."
    $csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    $cscArgs = @(
        "/target:winexe",
        "/unsafe",
        "/win32manifest:app.manifest",
        "/win32icon:app.ico",
        "/r:System.Windows.Forms.dll",
        "/r:System.Drawing.dll",
        "/r:System.dll",
        "/r:System.Core.dll",
        "/out:$ExePath",
        "src\*.cs"
    )
    & $csc $cscArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Compilation failed with exit code $LASTEXITCODE"
    }
}

if (-not (Test-Path $IcoPath)) {
    throw "Icon file not found at $IcoPath"
}
if (-not (Test-Path $ReadmePath)) {
    throw "README file not found at $ReadmePath"
}

Write-Host "[MSI] Staging MSI payload files..."
$ExeFileInfo = Get-Item $ExePath
$IcoFileInfo = Get-Item $IcoPath
$ReadmeFileInfo = Get-Item $ReadmePath

$TempDir = Join-Path $env:TEMP ("vph_msi_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

try {
    # 2. Build Cabinet file using makecab.exe
    $CabPath = Join-Path $TempDir "VectorPowerHub.cab"
    $DdfPath = Join-Path $TempDir "files.ddf"

    $ddfContent = @"
.Set CabinetNameTemplate=$CabPath
.Set DiskDirectoryTemplate=
.Set MaxDiskSize=CDROM
.Set CompressionType=MSZIP
.Set UniqueFiles="OFF"
.Set Cabinet=ON
"$ExePath" "VectorPowerHub.exe"
"$IcoPath" "app.ico"
"$ReadmePath" "README.md"
"@
    Set-Content -Path $DdfPath -Value $ddfContent -Encoding ASCII
    $cabOut = & makecab.exe /F $DdfPath 2>&1
    if ($LASTEXITCODE -ne 0 -or (-not (Test-Path $CabPath))) {
        throw "makecab failed: $cabOut"
    }
    Write-Host "[MSI] Cabinet created: $((Get-Item $CabPath).Length) bytes"

    # 3. Create IDT files for Windows Installer tables
    # Directory.idt
    $dirIdt = @"
Directory`tDirectory_Parent`tDefaultDir
s72`tS72`tl255
Directory`tDirectory
TARGETDIR`t`tSourceDir
ProgramFiles64Folder`tTARGETDIR`t.
INSTALLDIR`tProgramFiles64Folder`tVECT~1|VectorPowerHub
ProgramMenuFolder`tTARGETDIR`t.
DesktopFolder`tTARGETDIR`t.
SystemFolder`tTARGETDIR`t.
"@
    Set-Content -Path (Join-Path $TempDir "Directory.idt") -Value $dirIdt -Encoding ASCII

    # Component.idt (64-bit components with attribute 256)
    $cmpIdt = @"
Component`tComponentId`tDirectory_`tAttributes`tCondition`tKeyPath
s72`tS38`ts72`ti2`tS255`tS72
Component`tComponent
cmp_VectorPowerHub_exe`t{E1805562-F7B6-4EB6-9285-DC92FEA89D61}`tINSTALLDIR`t256`t`tVectorPowerHub.exe
cmp_app_ico`t{D372863A-9E9C-4CD8-AE5F-CF23E954E572}`tINSTALLDIR`t256`t`tapp.ico
cmp_README_md`t{F4C82A2E-8924-4458-BB68-8A3CD1BAA9E3}`tINSTALLDIR`t256`t`tREADME.md
"@
    Set-Content -Path (Join-Path $TempDir "Component.idt") -Value $cmpIdt -Encoding ASCII

    # File.idt
    $fileIdt = @"
File`tComponent_`tFileName`tFileSize`tVersion`tLanguage`tAttributes`tSequence
s72`ts72`tl255`ti4`tS72`tS20`tI2`ti4
File`tFile
VectorPowerHub.exe`tcmp_VectorPowerHub_exe`tVECT~1.EXE|VectorPowerHub.exe`t$($ExeFileInfo.Length)`t$Version.0`t1033`t512`t1
app.ico`tcmp_app_ico`tapp.ico`t$($IcoFileInfo.Length)`t`t`t512`t2
README.md`tcmp_README_md`tREADME.md`t$($ReadmeFileInfo.Length)`t`t`t512`t3
"@
    Set-Content -Path (Join-Path $TempDir "File.idt") -Value $fileIdt -Encoding ASCII

    # Media.idt
    $mediaIdt = @"
DiskId`tLastSequence`tDiskPrompt`tCabinet`tVolumeLabel`tSource
i2`ti4`tL64`tS255`tS32`tS72
Media`tDiskId
1`t3`t1`t#VectorPowerHub.cab`tDISK1`t
"@
    Set-Content -Path (Join-Path $TempDir "Media.idt") -Value $mediaIdt -Encoding ASCII

    # Feature.idt
    $featIdt = @"
Feature`tFeature_Parent`tTitle`tDescription`tDisplay`tLevel`tDirectory_`tAttributes
s38`tS38`tL64`tL255`tI2`ti2`tS72`ti2
Feature`tFeature
Complete`t`tVector Power Hub`tInstalls all VectorPowerHub platform binaries and documentation.`t1`t1`tINSTALLDIR`t2
"@
    Set-Content -Path (Join-Path $TempDir "Feature.idt") -Value $featIdt -Encoding ASCII

    # FeatureComponents.idt
    $featCmpIdt = @"
Feature_`tComponent_
s38`ts72
FeatureComponents`tFeature_`tComponent_
Complete`tcmp_VectorPowerHub_exe
Complete`tcmp_app_ico
Complete`tcmp_README_md
"@
    Set-Content -Path (Join-Path $TempDir "FeatureComponents.idt") -Value $featCmpIdt -Encoding ASCII

    # Shortcut.idt
    $shortcutIdt = @"
Shortcut`tDirectory_`tName`tComponent_`tTarget`tArguments`tDescription`tHotkey`tIcon_`tIconIndex`tShowCmd`tWkDir
s72`ts72`tl128`ts72`ts72`tS255`tL255`tI2`tS72`tI2`tI2`tS72
Shortcut`tShortcut
sc_StartMenu`tProgramMenuFolder`tVECT~1|Vector Power Hub`tcmp_VectorPowerHub_exe`t[#VectorPowerHub.exe]`t`tMSI Vector GP68HX Real-Time Telemetry and Dynamic Wattage Management Platform`t`tAppIcon.ico`t0`t1`tINSTALLDIR
sc_Desktop`tDesktopFolder`tVECT~1|Vector Power Hub`tcmp_VectorPowerHub_exe`t[#VectorPowerHub.exe]`t`tMSI Vector GP68HX Real-Time Telemetry and Dynamic Wattage Management Platform`t`tAppIcon.ico`t0`t1`tINSTALLDIR
"@
    Set-Content -Path (Join-Path $TempDir "Shortcut.idt") -Value $shortcutIdt -Encoding ASCII

    # Registry.idt (Ensures DisplayIcon and InstallLocation are explicitly registered in ARP)
    $regIdt = @"
Registry`tRoot`tKey`tName`tValue`tComponent_
s72`ti2`tl255`tL255`tL0`ts72
Registry`tRegistry
reg_DisplayIcon`t2`tSoftware\Microsoft\Windows\CurrentVersion\Uninstall\[ProductCode]`tDisplayIcon`t[#VectorPowerHub.exe]`tcmp_VectorPowerHub_exe
reg_InstallLocation`t2`tSoftware\Microsoft\Windows\CurrentVersion\Uninstall\[ProductCode]`tInstallLocation`t[INSTALLDIR]`tcmp_VectorPowerHub_exe
"@
    Set-Content -Path (Join-Path $TempDir "Registry.idt") -Value $regIdt -Encoding ASCII

    # CustomAction.idt (Graceful process termination on uninstall/upgrade & set ARPINSTALLLOCATION)
    $caIdt = @"
Action`tType`tSource`tTarget`tExtendedType
s72`ti2`tS72`tS255`tI4
CustomAction`tAction
SetARPINSTALLLOCATION`t51`tARPINSTALLLOCATION`t[INSTALLDIR]`t
CloseRunningApp`t98`tSystemFolder`ttaskkill.exe /f /im VectorPowerHub.exe`t
"@
    Set-Content -Path (Join-Path $TempDir "CustomAction.idt") -Value $caIdt -Encoding ASCII

    # LaunchCondition.idt
    $lcIdt = @"
Condition`tDescription
s255`tl255
LaunchCondition`tCondition
NOT NEWERVERSIONDETECTED`tA newer version of Vector Power Hub is already installed.
"@
    Set-Content -Path (Join-Path $TempDir "LaunchCondition.idt") -Value $lcIdt -Encoding ASCII

    # Property.idt
    $propIdt = @"
Property`tValue
s72`tl0
Property`tProperty
ALLUSERS`t1
ARPCOMMENTS`tMSI Vector GP68HX Real-Time Telemetry and Dynamic Wattage Management Platform
ARPCONTACT`tMSI Vector Hub
ARPHELPLINK`thttps://github.com/a7god/VectorPowerHub
ARPNOMODIFY`t1
ARPPRODUCTICON`tAppIcon.ico
DiskPrompt`t[1]
Manufacturer`tMSI Vector Hub
ProductCode`t$ProductCode
ProductID`tnone
ProductLanguage`t1033
ProductName`tVector Power Hub
ProductVersion`t$Version
SecureCustomProperties`tOLDERVERSIONBEINGUPGRADED;NEWERVERSIONDETECTED
UpgradeCode`t$UpgradeCode
"@
    Set-Content -Path (Join-Path $TempDir "Property.idt") -Value $propIdt -Encoding ASCII

    # Upgrade.idt
    $upgIdt = @"
UpgradeCode`tVersionMin`tVersionMax`tLanguage`tAttributes`tRemove`tActionProperty
s38`tS32`tS32`tS32`ti4`tS255`ts72
Upgrade`tUpgradeCode`tVersionMin`tVersionMax`tLanguage`tAttributes
$UpgradeCode`t0.0.1`t$Version`t`t257`t`tOLDERVERSIONBEINGUPGRADED
$UpgradeCode`t$Version`t`t`t2`t`tNEWERVERSIONDETECTED
"@
    Set-Content -Path (Join-Path $TempDir "Upgrade.idt") -Value $upgIdt -Encoding ASCII

    # InstallExecuteSequence.idt
    $iesIdt = @"
Action`tCondition`tSequence
s72`tS255`tI2
InstallExecuteSequence`tAction
FindRelatedProducts`t`t25
LaunchConditions`t`t100
ValidateProductID`t`t700
CostInitialize`t`t800
FileCost`t`t900
CostFinalize`t`t1000
SetARPINSTALLLOCATION`t`t1001
MigrateFeatureStates`t`t1200
CloseRunningApp`t(REMOVE="ALL" OR OLDERVERSIONBEINGUPGRADED)`t1390
InstallValidate`t`t1400
InstallInitialize`t`t1500
RemoveExistingProducts`tOLDERVERSIONBEINGUPGRADED`t1525
ProcessComponents`t`t1600
UnpublishFeatures`t`t1800
RemoveRegistryValues`t`t2600
RemoveShortcuts`t`t3200
RemoveFiles`t`t3500
InstallFiles`t`t4000
CreateShortcuts`t`t4500
WriteRegistryValues`t`t5000
RegisterUser`t`t6000
RegisterProduct`t`t6100
PublishFeatures`t`t6300
PublishProduct`t`t6400
InstallFinalize`t`t6600
"@
    Set-Content -Path (Join-Path $TempDir "InstallExecuteSequence.idt") -Value $iesIdt -Encoding ASCII

    # InstallUISequence.idt
    $iusIdt = @"
Action`tCondition`tSequence
s72`tS255`tI2
InstallUISequence`tAction
FindRelatedProducts`t`t25
LaunchConditions`t`t100
ValidateProductID`t`t700
CostInitialize`t`t800
FileCost`t`t900
CostFinalize`t`t1000
ExecuteAction`t`t1300
"@
    Set-Content -Path (Join-Path $TempDir "InstallUISequence.idt") -Value $iusIdt -Encoding ASCII

    # AdminExecuteSequence.idt
    $aesIdt = @"
Action`tCondition`tSequence
s72`tS255`tI2
AdminExecuteSequence`tAction
CostInitialize`t`t800
FileCost`t`t900
CostFinalize`t`t1000
InstallInitialize`t`t1500
InstallAdminPackage`t`t3900
InstallFiles`t`t4000
InstallFinalize`t`t6600
"@
    Set-Content -Path (Join-Path $TempDir "AdminExecuteSequence.idt") -Value $aesIdt -Encoding ASCII

    # AdvtExecuteSequence.idt
    $advIdt = @"
Action`tCondition`tSequence
s72`tS255`tI2
AdvtExecuteSequence`tAction
CostInitialize`t`t800
CostFinalize`t`t1000
InstallInitialize`t`t1500
CreateShortcuts`t`t4500
PublishFeatures`t`t6300
PublishProduct`t`t6400
InstallFinalize`t`t6600
"@
    Set-Content -Path (Join-Path $TempDir "AdvtExecuteSequence.idt") -Value $advIdt -Encoding ASCII

    # 4. Create MSI Database using WindowsInstaller.Installer COM
    if (Test-Path $MsiPath) {
        Remove-Item $MsiPath -Force
    }

    Write-Host "[MSI] Initializing WindowsInstaller database..."
    $msiInstaller = New-Object -ComObject WindowsInstaller.Installer
    # 4 = msiOpenDatabaseModeCreateDirect
    $db = $msiInstaller.OpenDatabase($MsiPath, 4)

    $tablesToImport = @(
        "Directory.idt",
        "Component.idt",
        "File.idt",
        "Media.idt",
        "Feature.idt",
        "FeatureComponents.idt",
        "Shortcut.idt",
        "Registry.idt",
        "CustomAction.idt",
        "LaunchCondition.idt",
        "Property.idt",
        "Upgrade.idt",
        "InstallExecuteSequence.idt",
        "InstallUISequence.idt",
        "AdminExecuteSequence.idt",
        "AdvtExecuteSequence.idt"
    )

    foreach ($tbl in $tablesToImport) {
        Write-Host "Importing $tbl..."
        $db.Import($TempDir, $tbl)
    }

    # 5. Create Icon table and embed AppIcon.ico
    Write-Host "[MSI] Embedding application icon..."
    $iconView = $db.OpenView("CREATE TABLE Icon (Name CHAR(72) NOT NULL, Data OBJECT NOT NULL PRIMARY KEY Name)")
    $iconView.Execute()
    $iconView.Close()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($iconView) | Out-Null

    $insertIconView = $db.OpenView("INSERT INTO Icon (Name, Data) VALUES ('AppIcon.ico', ?)")
    $iconRec = $msiInstaller.CreateRecord(1)
    $iconRec.SetStream(1, $IcoPath)
    $insertIconView.Execute($iconRec)
    $insertIconView.Close()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($insertIconView) | Out-Null
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($iconRec) | Out-Null

    # 6. Embed Cabinet stream into _Streams table
    Write-Host "[MSI] Embedding compressed cabinet payload..."
    $cabView = $db.OpenView("INSERT INTO `_Streams` (`Name`, `Data`) VALUES ('VectorPowerHub.cab', ?)")
    $cabRec = $msiInstaller.CreateRecord(1)
    $cabRec.SetStream(1, $CabPath)
    $cabView.Execute($cabRec)
    $cabView.Close()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($cabView) | Out-Null
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($cabRec) | Out-Null

    # 7. Write Summary Information
    Write-Host "[MSI] Writing Summary Information Stream..."
    $sumInfo = $db.SummaryInformation(20)
    $sumInfo.Property(2) = "Installation Database"                          # PID_TITLE
    $sumInfo.Property(3) = "Vector Power Hub Setup"                          # PID_SUBJECT
    $sumInfo.Property(4) = "MSI Vector Hub"                                  # PID_AUTHOR
    $sumInfo.Property(7) = "x64;1033"                                        # PID_TEMPLATE
    $sumInfo.Property(9) = [Guid]::NewGuid().ToString("B").ToUpper()         # PID_REVNUMBER (PackageCode)
    $sumInfo.Property(14) = 200                                              # PID_PAGECOUNT (Installer version 2.0+)
    $sumInfo.Property(15) = 2                                                # PID_WORDCOUNT (Compressed files)
    $sumInfo.Property(18) = "Windows Installer"                              # PID_APPNAME
    $sumInfo.Persist()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($sumInfo) | Out-Null

    $db.Commit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($db) | Out-Null
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($msiInstaller) | Out-Null

    $msiSize = (Get-Item $MsiPath).Length
    Write-Host "[MSI] Successfully generated $MsiPath ($msiSize bytes)!"
}
finally {
    if (Test-Path $TempDir) {
        Remove-Item -Recurse -Force $TempDir -ErrorAction SilentlyContinue
    }
}
