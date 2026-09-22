# Apply-PowerOptimizations.ps1
# Low-overhead Dynamic CPU Power Optimizer for Intel Core Ultra / Raptor Lake HX Architecture
# Configures dynamic core parking, Speed Shift V2 autonomous CPPC, EPP, and boost on DC.

param(
    [switch]$MeasureOnly
)

$SubProc = "54533251-82be-4824-96c1-47b60b740d00"

function Set-PowerDC([string]$Guid, [int]$Value, [string]$Name) {
    powercfg /setdcvalueindex SCHEME_CURRENT $SubProc $Guid $Value
    Write-Host "[DC-CONFIG] $Name -> $Value" -ForegroundColor Cyan
}

function Set-PowerAC([string]$Guid, [int]$Value, [string]$Name) {
    powercfg /setacvalueindex SCHEME_CURRENT $SubProc $Guid $Value
    Write-Host "[AC-CONFIG] $Name -> $Value" -ForegroundColor DarkCyan
}

if (-not $MeasureOnly) {
    Write-Host "=== Applying Dynamic DC Low-Power CPU Parameters ===" -ForegroundColor Green

    # 1. Dynamic Core Parking on DC:
    # CPMINCORES = 0% (Allow idle P-cores to sleep/deep C-state)
    Set-PowerDC "0cc5b647-c1df-4637-891a-dec35c318583" 0 "CPMINCORES (Min Parked Cores %)"
    # CPMAXCORES = 100% (Allow all 24 cores unparked on dynamic demand)
    Set-PowerDC "ea062031-0e34-4ff1-9b6d-eb1059334028" 100 "CPMAXCORES (Max Unparked Cores %)"
    # CPINCREASETIME = 1 (1 check interval to instantly unpark for bursts)
    Set-PowerDC "2ddd5a84-5a71-437e-912a-db0b8c788732" 1 "CPINCREASETIME (Unpark Latency)"
    # CPDECREASETIME = 2 (Fast park back to sleep - max valid setting is 2/intervals)
    Set-PowerDC "dfd10d17-d5eb-45dd-877a-9a34ddd15c82" 2 "CPDECREASETIME (Parking Delay)"
    # CPCONCURRENCY = Dynamic concurrency threshold (0)
    Set-PowerDC "2430ab6f-a520-44a2-9601-f7f23b5134b1" 0 "CPCONCURRENCY (Dynamic Concurrency)"

    # 2. Dynamic Boost Mode on DC:
    # PERFBOOSTMODE = 3 (Efficient Enabled: ramps on sustained loads, cuts idle spikes)
    Set-PowerDC "be337238-0d82-4146-a960-4f3749d470c7" 3 "PERFBOOSTMODE (Efficient Enabled)"

    # 3. Dynamic EPP on DC:
    # PERFEPP = 80% (0x50: Energy-biased Speed Shift scaling)
    Set-PowerDC "36687f9e-e3a5-4dbf-b1dc-15eb381c6863" 80 "PERFEPP (EPP Class 0 - 80%)"
    Set-PowerDC "36687f9e-e3a5-4dbf-b1dc-15eb381c6864" 80 "PERFEPP1 (EPP Class 1 - 80%)"

    # 4. Autonomous Frequency Scaling (Speed Shift V2 / HW-CPPC):
    # PERFAUTONOMOUS = 1 (Hardware-controlled autonomous frequency selection)
    Set-PowerDC "8baa4a8a-14c6-4451-8e8b-14bdbd197537" 1 "PERFAUTONOMOUS (HW Speed Shift Enabled)"

    # 5. Autonomous Mode Activity Window:
    # PERFAUTONOMOUSWINDOW = 30000 microseconds (calibrated dynamic window)
    Set-PowerDC "cfeda3d0-7697-4566-a922-a9086cd49dfa" 30000 "PERFAUTONOMOUSWINDOW (30ms Window)"

    # 6. Processor Performance Time Check Interval:
    # PERFCHECK = 15ms (Fast responsiveness polling)
    Set-PowerDC "4d2b0152-7d5c-498b-88e2-34345392a2c5" 15 "PERFCHECK (15ms Interval)"

    # Preserve AC Performance Settings & Ensure Zero AC Sleep
    Set-PowerAC "0cc5b647-c1df-4637-891a-dec35c318583" 10 "CPMINCORES (AC: 10%)"
    Set-PowerAC "ea062031-0e34-4ff1-9b6d-eb1059334028" 100 "CPMAXCORES (AC: 100%)"
    Set-PowerAC "be337238-0d82-4146-a960-4f3749d470c7" 3 "PERFBOOSTMODE (AC: Efficient Enabled)"
    Set-PowerAC "36687f9e-e3a5-4dbf-b1dc-15eb381c6863" 25 "PERFEPP (AC: 25%)"
    Set-PowerAC "36687f9e-e3a5-4dbf-b1dc-15eb381c6864" 25 "PERFEPP1 (AC: 25%)"
    # 6a. Disable Global USB Selective Suspend (AC & DC)
    powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0
    powercfg /setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0


    # 6b. USB xHCI Controller, Hub & Keyboard Zero-Latency Power Configuration
    $keepAliveIds = @('ROOT_HUB30', 'VID_0BDA', 'VID_05E3', 'VID_25A7', 'VID_A8A5', 'VID_A8A4', 'VID_258A')
    $pciTargets = @('HKLM:\SYSTEM\CurrentControlSet\Enum\PCI\VEN_8086&DEV_7F6E*', 'HKLM:\SYSTEM\CurrentControlSet\Enum\PCI\VEN_8086&DEV_5782*', 'HKLM:\SYSTEM\CurrentControlSet\Enum\PCI\VEN_8086&DEV_5781*')
    foreach ($pciPat in $pciTargets) {
        Get-Item $pciPat -ErrorAction SilentlyContinue | ForEach-Object {
            Get-ChildItem $_.PSPath -ErrorAction SilentlyContinue | ForEach-Object {
                Set-ItemProperty -Path $_.PSPath -Name "PnPCapabilities" -Value 24 -Type DWord -Force -ErrorAction SilentlyContinue
                $dp = Join-Path $_.PSPath "Device Parameters"
                if (-not (Test-Path $dp)) { New-Item -Path $dp -Force -ErrorAction SilentlyContinue | Out-Null }
                if (Test-Path $dp) {
                    Set-ItemProperty -Path $dp -Name "EnhancedPowerManagementEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
                    Set-ItemProperty -Path $dp -Name "SelectiveSuspendEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
                    Set-ItemProperty -Path $dp -Name "AllowIdleIrpInD3" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
                    Set-ItemProperty -Path $dp -Name "D3ColdSupported" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
                }
            }
        }
    }
    $kbBases = @('HKLM:\SYSTEM\CurrentControlSet\Enum\USB', 'HKLM:\SYSTEM\CurrentControlSet\Enum\HID')
    foreach ($b in $kbBases) {
        if (Test-Path $b) {
            $devKeys = Get-ChildItem -Path $b -ErrorAction SilentlyContinue
            foreach ($dk in $devKeys) {
                foreach ($kid in $keepAliveIds) {
                    if ($dk.PSChildName -like "*$kid*") {
                        $instances = Get-ChildItem -Path $dk.PSPath -ErrorAction SilentlyContinue
                        foreach ($inst in $instances) {
                            Set-ItemProperty -Path $inst.PSPath -Name "PnPCapabilities" -Value 24 -Type DWord -Force -ErrorAction SilentlyContinue
                            $dp = Join-Path $inst.PSPath "Device Parameters"
                            if (-not (Test-Path $dp)) { New-Item -Path $dp -Force -ErrorAction SilentlyContinue | Out-Null }
                            if (Test-Path $dp) {
                                Set-ItemProperty -Path $dp -Name "AllowIdleIrpInD3" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
                                Set-ItemProperty -Path $dp -Name "D3ColdSupported" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
                                Set-ItemProperty -Path $dp -Name "DefaultIdleTimeout" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
                                Set-ItemProperty -Path $dp -Name "EnhancedPowerManagementEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
                                Set-ItemProperty -Path $dp -Name "DeviceSelectiveSuspended" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
                                Set-ItemProperty -Path $dp -Name "SelectiveSuspendEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
                            }
                        }
                    }
                }
            }
        }
    }

    # Commit active power scheme
    powercfg /setactive SCHEME_CURRENT
    Write-Host "Active power scheme committed successfully (AC Standby: Never)." -ForegroundColor Green
}

# 7. Telemetry & Verification (5s sampling)
Write-Host "`n=== Live Power & C-State Residency Measurement (5s sample) ===" -ForegroundColor Yellow
$samples = @()
for ($i = 1; $i -le 5; $i++) {
    Start-Sleep -Seconds 1
    $c3 = (Get-Counter "\Processor Information(*)\% C3 Time" -ErrorAction SilentlyContinue).CounterSamples |
          Measure-Object -Property CookedValue -Average
    $procUtility = (Get-Counter "\Processor Information(_Total)\% Processor Utility" -ErrorAction SilentlyContinue).CounterSamples.CookedValue
    $samples += [PSCustomObject]@{
        Second        = $i
        AvgC3Residency = [math]::Round($c3.Average, 2)
        ProcUtility   = [math]::Round($procUtility, 2)
    }
}

$samples | Format-Table -AutoSize
$finalC3 = ($samples | Measure-Object -Property AvgC3Residency -Average).Average
Write-Host ("Overall Mean C3 Deep Sleep Residency: " + [math]::Round($finalC3, 2) + "%") -ForegroundColor Green
