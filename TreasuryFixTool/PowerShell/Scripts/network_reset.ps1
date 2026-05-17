@"
# Network Reset Script for TreasuryFixTool
# This script resets the TCP/IP stack, renews IP address, and flushes DNS.

# Reset TCP/IP stack
netsh int ip reset

# Reset Winsock
netsh winsock reset

# Flush DNS Resolver Cache
ipconfig /flushdns

# Release and renew IP address (for all adapters)
ipconfig /release
ipconfig /renew

# Optional: Reset firewall (if needed)
# netsh advfirewall reset
"@