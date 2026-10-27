# FR-AUTH-003 production security settings

## Refresh token cookie

In Production, the Google login API sets `culinaryblog_refresh` as an
`HttpOnly`, `Secure`, `SameSite=None` cookie scoped to `/api/v1/auth` and omits
the token value from the JSON response. The Auth.js server callback reads the
server-to-server cookie response and keeps the value inside its encrypted,
`HttpOnly` session cookie. Its session callback does not expose the refresh
token to browser JavaScript. Development keeps the existing Google JSON
response contract for local testing.

This change is limited to Google OAuth. Email login, registration, and the
existing refresh flow keep their current response and request contracts.

## Client IP forwarding and Google login quota

Google login is limited to 10 requests per minute per client IP using an atomic
Redis counter shared by API instances. Redis is fail-closed: if it is
unavailable, Google login returns 503 rather than bypassing the quota.

Set `ForwardedHeaders:KnownProxies` to the exact IP addresses of the immediate
trusted proxy or Next.js service that connects to the API. Do not use wildcard,
CIDR-all, or public client addresses. The API processes at most one
`X-Forwarded-For` hop by default and ignores the header when the TCP peer is not
in this trusted list.

Set `AUTH_CLIENT_IP_HEADER` only if the trusted ingress overwrites that header
with the real client IP. For example, use `x-real-ip` only when the deployment
proxy replaces any client-provided value. Auth.js forwards that value to the
API as `X-Forwarded-For`; the API still accepts it only from a configured
trusted proxy.

For local development, `appsettings.Development.json` trusts loopback only.
Production proxy addresses and frontend origins must be supplied by deployment
configuration, not committed as broad defaults.
