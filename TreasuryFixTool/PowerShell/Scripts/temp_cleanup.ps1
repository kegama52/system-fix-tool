@"
# Temp Cleanup Script for TreasuryFixTool
# This script clears temporary files from user and system temp folders.

# Define temp folders
$tempFolders = @(
    "$env:SystemRoot\Temp",
    "$env:USERPROFILE\AppData\Local\Temp"
)

foreach ($folder in $tempFolders) {
    if (Test-Path $folder) {
        try {
            Get-ChildItem -Path $folder -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
            Write-Output "Cleaned: $folder"
        } catch {
            Write-Output "Warning: Could not clean $folder. Error: $_"
        }
    }
}

# Also clear the recent files history (optional)
try {
    Clear-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs" -Name * -ErrorAction SilentlyContinue
    Write-Output "Cleared recent docs history."
} catch {
    Write-Output "Warning: Could not clear recent docs history. Error: $_"
}
"@