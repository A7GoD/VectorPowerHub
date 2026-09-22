# Parametric Power Benchmark Suite (<=200 lines)
$SubProc = "54533251-82be-4824-96c1-47b60b740d00"
$Guids = @{
    EPP0        = "36687f9e-e3a5-4dbf-b1dc-15eb381c6863"
    EPP1        = "36687f9e-e3a5-4dbf-b1dc-15eb381c6864"
    Boost       = "be337238-0d82-4146-a960-4f3749d470c7"
    ParkMin     = "0cc5b647-c1df-4637-891a-dec35c318583"
    ParkMax     = "ea062031-0e34-4ff1-9b6d-eb1059334028"
    Autonomous  = "8baa4a8a-14c6-4451-8e8b-14bdbd197537"
}

function Set-DCParam([string]$Key, [int]$Val) {
    if ($Key -eq "EPP") {
        powercfg /setdcvalueindex SCHEME_CURRENT $SubProc $Guids.EPP0 $Val | Out-Null
        powercfg /setdcvalueindex SCHEME_CURRENT $SubProc $Guids.EPP1 $Val | Out-Null
    } else {
        powercfg /setdcvalueindex SCHEME_CURRENT $SubProc $Guids[$Key] $Val | Out-Null
    }
}

function Apply-CurrentConfig([int]$epp, [int]$boost, [int]$parkMin, [int]$auton) {
    Set-DCParam "EPP" $epp
    Set-DCParam "Boost" $boost
    Set-DCParam "ParkMin" $parkMin
    Set-DCParam "ParkMax" 100
    Set-DCParam "Autonomous" $auton
    powercfg /setactive SCHEME_CURRENT | Out-Null
}

function Sample-Metrics([int]$seconds = 6) {
    # Warmup 1.5s for power transition settling
    Start-Sleep -Milliseconds 1500
    $pkgPowers = @()
    $c3Residencies = @()
    $utilizations = @()
    $frequencies = @()

    for ($i = 0; $i -lt $seconds; $i++) {
        Start-Sleep -Seconds 1
        $em = (Get-Counter "\Energy Meter(*)\Power" -ErrorAction SilentlyContinue).CounterSamples
        $pkgSample = ($em | Where-Object { $_.InstanceName -eq "rapl_package0_pkg" }).CookedValue
        if (-not $pkgSample) { $pkgSample = ($em | Where-Object { $_.Path -like "*rapl_package0_pkg*" }).CookedValue }
        
        $c3Samples = (Get-Counter "\Processor Information(*)\% C3 Time" -ErrorAction SilentlyContinue).CounterSamples |
                     Where-Object { $_.InstanceName -ne "_Total" } |
                     Measure-Object -Property CookedValue -Average
        
        $utilSample = (Get-Counter "\Processor Information(_Total)\% Processor Utility" -ErrorAction SilentlyContinue).CounterSamples.CookedValue
        $freqSample = (Get-Counter "\Processor Information(_Total)\Processor Frequency" -ErrorAction SilentlyContinue).CounterSamples.CookedValue

        if ($pkgSample) { $pkgPowers += ($pkgSample / 1000.0) }
        if ($c3Samples.Average -ne $null) { $c3Residencies += $c3Samples.Average }
        if ($utilSample -ne $null) { $utilizations += $utilSample }
        if ($freqSample -ne $null) { $frequencies += $freqSample }
    }

    $avgPkg = if ($pkgPowers.Count -gt 0) { ($pkgPowers | Measure-Object -Average).Average } else { 0 }
    $avgC3 = if ($c3Residencies.Count -gt 0) { ($c3Residencies | Measure-Object -Average).Average } else { 0 }
    $avgUtil = if ($utilizations.Count -gt 0) { ($utilizations | Measure-Object -Average).Average } else { 0 }
    $avgFreq = if ($frequencies.Count -gt 0) { ($frequencies | Measure-Object -Average).Average } else { 0 }

    return [PSCustomObject]@{
        PkgPowerW  = [math]::Round($avgPkg, 2)
        C3Pct      = [math]::Round($avgC3, 2)
        ProcUtilPct= [math]::Round($avgUtil, 2)
        AvgFreqMHz = [math]::Round($avgFreq, 0)
    }
}

$results = @()

Write-Host "`n>>> [1/5] Baseline State (EPP 50%, Boost 3, Park 0%, Autonomous 1) ..." -ForegroundColor Cyan
Apply-CurrentConfig 50 3 0 1
$m = Sample-Metrics 5
$results += [PSCustomObject]@{ Category="Baseline"; Setting="EPP 50%, Boost 3, Park 0%, Auto 1"; PowerW=$m.PkgPowerW; C3Pct=$m.C3Pct; Util=$m.ProcUtilPct; FreqMHz=$m.AvgFreqMHz }

# Test 1: EPP Variations (Hold Boost 3, Park 0, Auto 1)
$eppLevels = @(25, 50, 80, 100)
foreach ($epp in $eppLevels) {
    Write-Host ">>> [Test 1] EPP $epp% (Boost 3, Park 0%, Auto 1) ..." -ForegroundColor Cyan
    Apply-CurrentConfig $epp 3 0 1
    $m = Sample-Metrics 5
    $results += [PSCustomObject]@{ Category="EPP Sweep"; Setting="EPP $epp%"; PowerW=$m.PkgPowerW; C3Pct=$m.C3Pct; Util=$m.ProcUtilPct; FreqMHz=$m.AvgFreqMHz }
}

# Test 2: Boost Mode Variations (Hold EPP 80%, Park 0, Auto 1)
$boostModes = @(
    @{ Val=0; Name="Boost 0 (Disabled - Base clock)" },
    @{ Val=3; Name="Boost 3 (Efficient Enabled)" },
    @{ Val=4; Name="Boost 4 (Efficient Aggressive)" },
    @{ Val=6; Name="Boost 6 (Efficient Guaranteed)" }
)
foreach ($b in $boostModes) {
    Write-Host ">>> [Test 2] $($b.Name) (EPP 80%, Park 0%, Auto 1) ..." -ForegroundColor Cyan
    Apply-CurrentConfig 80 $b.Val 0 1
    $m = Sample-Metrics 5
    $results += [PSCustomObject]@{ Category="Boost Sweep"; Setting=$b.Name; PowerW=$m.PkgPowerW; C3Pct=$m.C3Pct; Util=$m.ProcUtilPct; FreqMHz=$m.AvgFreqMHz }
}

# Test 3: Core Parking Variations (Hold EPP 80%, Boost 3, Auto 1)
$parkLevels = @(
    @{ Val=0; Name="Park 0% (Aggressive dynamic parking)" },
    @{ Val=10; Name="Park 10% (Light unparking floor)" },
    @{ Val=50; Name="Park 50% (Half unparked)" },
    @{ Val=100; Name="Park 100% (All unparked - Race to Sleep)" }
)
foreach ($p in $parkLevels) {
    Write-Host ">>> [Test 3] $($p.Name) (EPP 80%, Boost 3, Auto 1) ..." -ForegroundColor Cyan
    Apply-CurrentConfig 80 3 $p.Val 1
    $m = Sample-Metrics 5
    $results += [PSCustomObject]@{ Category="Core Parking"; Setting=$p.Name; PowerW=$m.PkgPowerW; C3Pct=$m.C3Pct; Util=$m.ProcUtilPct; FreqMHz=$m.AvgFreqMHz }
}

# Test 4: Autonomous Mode Variations (Hold EPP 80%, Boost 3, Park 0)
$autoModes = @(
    @{ Val=0; Name="Autonomous 0 (OS-guided scheduler)" },
    @{ Val=1; Name="Autonomous 1 (HW CPPC HWPv2 autonomous)" }
)
foreach ($a in $autoModes) {
    Write-Host ">>> [Test 4] $($a.Name) (EPP 80%, Boost 3, Park 0) ..." -ForegroundColor Cyan
    Apply-CurrentConfig 80 3 0 $a.Val
    $m = Sample-Metrics 5
    $results += [PSCustomObject]@{ Category="Autonomous Mode"; Setting=$a.Name; PowerW=$m.PkgPowerW; C3Pct=$m.C3Pct; Util=$m.ProcUtilPct; FreqMHz=$m.AvgFreqMHz }
}

# Restore Optimal Setting (EPP 80%, Boost 3, Park 0%, Auto 1)
Write-Host "`n>>> Restoring Optimal Profile (EPP 80%, Boost 3, Park 0%, Auto 1) ..." -ForegroundColor Green
Apply-CurrentConfig 80 3 0 1

Write-Host "`n=== BENCHMARK RESULTS TABLE ===" -ForegroundColor Yellow
$results | Format-Table -AutoSize
$results | Export-Clixml -Path "C:\Users\a7god\Coding\VectorPowerHub\benchmark_results.xml"
