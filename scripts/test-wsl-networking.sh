#!/bin/bash

echo "=== WSL Networking Test Script ==="
echo ""

# Get WSL IP
WSL_IP=$(hostname -I | awk '{print $1}')
echo "WSL IP Address: $WSL_IP"
echo ""

# Test local services
echo "Testing local service accessibility..."
echo "1. Testing Aspire Dashboard (localhost:15010):"
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 5 http://localhost:15010)
if [ "$HTTP_CODE" = "200" ]; then
    echo "   ✅ Aspire Dashboard accessible locally"
else
    echo "   ❌ Aspire Dashboard not accessible (HTTP: $HTTP_CODE)"
fi

echo ""
echo "2. Testing Web App (localhost:5001):"
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 5 -k https://localhost:5001)
if [ "$HTTP_CODE" = "200" ]; then
    echo "   ✅ Web App accessible locally"
else
    echo "   ❌ Web App not accessible (HTTP: $HTTP_CODE)"
fi

echo ""
echo "Testing WSL IP accessibility..."
echo "3. Testing Aspire Dashboard via WSL IP ($WSL_IP:15010):"
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 5 http://$WSL_IP:15010)
if [ "$HTTP_CODE" = "200" ]; then
    echo "   ✅ Aspire Dashboard accessible via WSL IP"
    echo "   📊 Windows can access: http://$WSL_IP:15010"
else
    echo "   ❌ Aspire Dashboard not accessible via WSL IP (HTTP: $HTTP_CODE)"
    echo "   💡 Use Windows port forwarding instead"
fi

echo ""
echo "4. Testing Web App via WSL IP ($WSL_IP:5001):"
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 5 -k https://$WSL_IP:5001)
if [ "$HTTP_CODE" = "200" ]; then
    echo "   ✅ Web App accessible via WSL IP"
    echo "   📱 Windows can access: https://$WSL_IP:5001"
else
    echo "   ❌ Web App not accessible via WSL IP (HTTP: $HTTP_CODE)"
    echo "   💡 Use Windows port forwarding instead"
fi

echo ""
echo "=== Port Forwarding Commands for Windows PowerShell (Admin) ==="
echo "netsh interface portproxy add v4tov4 listenport=15010 listenaddress=0.0.0.0 connectport=15010 connectaddress=$WSL_IP"
echo "netsh interface portproxy add v4tov4 listenport=5001 listenaddress=0.0.0.0 connectport=5001 connectaddress=$WSL_IP"
echo ""
echo "After running port forwarding, access from Windows:"
echo "Aspire Dashboard: http://localhost:15010"
echo "Web App: https://localhost:5001"
echo ""