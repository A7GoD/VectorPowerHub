$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = (Resolve-Path "bin\VectorPowerHub.exe").Path
$psi.Arguments = "/test"
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
$p = [System.Diagnostics.Process]::Start($psi)
$p.WaitForExit(10000)
$out = $p.StandardOutput.ReadToEnd()
$err = $p.StandardError.ReadToEnd()
Write-Host "STDOUT:"
Write-Host $out
if ($err) {
    Write-Host "STDERR:"
    Write-Host $err
}
