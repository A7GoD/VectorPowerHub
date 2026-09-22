$devices = Get-PnpDevice -Class Keyboard, HIDClass, USB | Where-Object { $_.Status -eq 'OK' }
Write-Output "=== ACTIVE KEYBOARDS & HID ==="
$devices | Select-Object FriendlyName, InstanceId, Status, Class | Format-Table -AutoSize

Write-Output "`n=== REGISTRY POWER SETTINGS ==="
Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Enum\USB' -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.PSChildName -eq 'Device Parameters' } |
    ForEach-Object {
        $props = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
        if ($null -ne $props.EnhancedPowerManagementEnabled -or $null -ne $props.DeviceSelectiveSuspended -or $null -ne $props.SelectiveSuspendEnabled) {
            [PSCustomObject]@{
                Path = $_.PSPath.Replace('Microsoft.PowerShell.Core\Registry::HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum\USB\', '')
                EnhancedPower = $props.EnhancedPowerManagementEnabled
                DeviceSelectiveSuspended = $props.DeviceSelectiveSuspended
                SelectiveSuspendEnabled = $props.SelectiveSuspendEnabled
            }
        }
    } | Format-Table -AutoSize
