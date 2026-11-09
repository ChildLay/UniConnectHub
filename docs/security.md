# Security

HTTPS redirection, JWT validation, ASP.NET Core password hashing, token hashing at rest, lockout, ProblemDetails, named origin-based CORS and configurable IP rate limiting are configured. Production startup requires Jwt issuer, audience and signing key; the key must be at least 32 bytes. Do not commit production secrets. The email adapter is development-only in behavior; provider integration, verified-email enforcement in authorization, role administration, and resource privacy checks remain outstanding.
