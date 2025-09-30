# Known Issues for Kubernetes Deployment

## SignalR Client URL Resolution Issue

### Problem
SignalR connections in `Microsoft.Greenlight.Web.DocGen.Client/Services/SignalRConnectionService.cs` use service discovery URLs that are internal to the Kubernetes cluster (e.g., `http://api-main:8080/hubs/notification-hub`). These URLs are not accessible from browser clients since they are cluster-internal service names.

### Current Behavior
- SignalR gets API address via `AdminHelper.GetApiServiceUrl()` (after our fixes)
- In production, this returns `http://api-main:8080` (cluster-internal URL)
- Browser clients cannot connect to these internal URLs
- Connection will fail when the browser tries to establish SignalR connection

### Root Cause
Service discovery URLs are designed for server-to-server communication within the cluster, not for client-to-server communication from browsers outside the cluster.

### Potential Solutions (for future implementation)

#### Option 1: Request URL-based API Address Construction
- Extract the current request URL from the web-docgen service
- Replace the service name portion (e.g., `web-docgen` → `api-main`)
- Preserve the host, port, and scheme from the browser's perspective
- Handle hostname overrides properly

#### Option 2: Public Ingress-based URLs
- Configure Kubernetes Ingresses for public access
- Use publicly accessible URLs for SignalR connections
- Require DNS configuration and certificates

#### Option 3: Server-Side SignalR Hub Proxy
- Keep SignalR connections internal to the cluster
- Implement a WebSocket proxy on the web-docgen service
- Relay SignalR messages through the web service

### Files Affected
- `src/Microsoft.Greenlight.Web.DocGen.Client/Services/SignalRConnectionService.cs` (line 174)
- `src/Microsoft.Greenlight.Web.DocGen.Client/Services/SignalRSubscriptionManager.cs` (if present)
- Any other client-side code that uses service discovery URLs for browser connections

### Temporary Workaround
Until ingresses are configured, SignalR functionality will not work in Kubernetes deployments. Local development (with HTTPS URLs) should continue to work normally.

### Notes
- This issue only affects Kubernetes deployments
- Local development uses HTTPS URLs that are accessible to browsers
- The issue will need to be resolved when implementing public ingresses
- Consider hostname override configuration complexity in the solution

---
*Generated during service discovery fixes - January 2025*