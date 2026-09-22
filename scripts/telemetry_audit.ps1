$counters = @(
    '\Energy Meter(rapl_package0_pkg)\Power',
    '\Energy Meter(rapl_package0_pp0)\Power',
    '\Energy Meter(rapl_package0_pp1)\Power',
    '\Processor Information(_Total)\% C3 Time',
    '\Processor Information(_Total)\% Processor Utility'
)

$samples = Get-Counter -Counter $counters -SampleInterval 1 -MaxSamples 5

$pkgVals = @()
$pp0Vals = @()
$pp1Vals = @()
$c3Vals = @()
$utilVals = @()

foreach ($s in $samples) {
    foreach ($cs in $s.CounterSamples) {
        if ($cs.Path -like '*rapl_package0_pkg*') { $pkgVals += $cs.CookedValue }
        elseif ($cs.Path -like '*rapl_package0_pp0*') { $pp0Vals += $cs.CookedValue }
        elseif ($cs.Path -like '*rapl_package0_pp1*') { $pp1Vals += $cs.CookedValue }
        elseif ($cs.Path -like '*C3 Time*') { $c3Vals += $cs.CookedValue }
        elseif ($cs.Path -like '*Processor Utility*') { $utilVals += $cs.CookedValue }
    }
}

$pkgAvg = ($pkgVals | Measure-Object -Average).Average / 1000.0
$pp0Avg = ($pp0Vals | Measure-Object -Average).Average / 1000.0
$pp1Avg = ($pp1Vals | Measure-Object -Average).Average / 1000.0
$c3Avg = ($c3Vals | Measure-Object -Average).Average
$utilAvg = ($utilVals | Measure-Object -Average).Average
$uncoreAvg = $pkgAvg - $pp0Avg - $pp1Avg

[PSCustomObject]@{
    PackagePower_W   = [math]::Round($pkgAvg, 3)
    CorePower_PP0_W  = [math]::Round($pp0Avg, 3)
    iGPU_PP1_W       = [math]::Round($pp1Avg, 3)
    UncorePower_W    = [math]::Round($uncoreAvg, 3)
    C3_Residency_Pct = [math]::Round($c3Avg, 2)
    Utility_Pct      = [math]::Round($utilAvg, 2)
} | Format-List

Write-Host "=== Top Active CPU Processes ==="
Get-Process | Sort-Object CPU -Descending | Select-Object -First 5 Id, ProcessName, CPU, @{N='WS_MB';E={[math]::Round($_.WorkingSet64/1MB,1)}} | Format-Table -AutoSize
