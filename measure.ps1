
Add-Type -AssemblyName System.Drawing
\ = New-Object System.Drawing.Bitmap(1, 1)
\ = [System.Drawing.Graphics]::FromImage(\)
\ = New-Object System.Drawing.Font('Segoe UI', 9.5, [System.Drawing.FontStyle]::Bold)
\ = 'P-Core Turbo Limit: Unbounded (Up to 5.5 GHz)'
\ = \.MeasureString(\, \)
\ = 'EPP Policy: 30% (Eager GPU Wattage Release)'
\ = \.MeasureString(\, \)
Write-Output ('S1 Width: ' + \.Width)
Write-Output ('S2 Width: ' + \.Width)
