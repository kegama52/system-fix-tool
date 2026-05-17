@"
# Winsock Reset Script for TreasuryFixTool
# This script resets the Winsock catalog to a clean state.

# Reset Winsock
netsh winsock reset

# Optional: Restart the computer to fully apply changes (commented out for automation)
# shutdown /r /t 0
"@