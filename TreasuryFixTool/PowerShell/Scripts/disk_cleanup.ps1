@"
# Disk Cleanup Script for TreasuryFixTool
# This script runs the built-in Disk Cleanup utility to clean temporary files, recycle bin, etc.

# Clean drive C: with low disk space options (cleans common temporary files)
cleanmgr /lowdisk /d C:

# Additionally, we can clean up temporary files manually as a fallback
try {
    $tempPaths = @("$env:SystemRoot\Temp", "$env:USERPROFILE\AppData\Local\Temp")
    foreach ($path in $tempPaths) {
        if (Test-Path $path) {
            Get-ChildItem -Path $path -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
} catch {
    Write-Output "Warning: Some temporary files could not be removed. Error: $_"
}
"@