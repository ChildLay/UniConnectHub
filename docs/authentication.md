# Authentication

The current authentication module includes registration, login, access JWT issuance, one-time refresh rotation, logout, email verification and password reset. Passwords use ASP.NET Core Identity's `PasswordHasher<User>`. The database stores one-way SHA-256 digests for refresh, verification and reset tokens, with expiry and one-time-use state. Login applies a five-attempt, fifteen-minute lockout. Forgot-password and resend-verification return the same accepted response whether or not an account exists.

Email transport is a development adapter and currently logs that a message was queued; it does not deliver mail. Production delivery must be provided through Infrastructure. Do not enable real user signup until email delivery and delivery of one-time links are configured.

Set local secrets:

```powershell
dotnet user-secrets init --project src/UniConnect.Api
dotnet user-secrets set "Jwt:SigningKey" "<at-least-32-byte-random-secret>" --project src/UniConnect.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your SQL Server connection string>" --project src/UniConnect.Api
```

The email adapter currently receives a raw one-time token to make local flow development possible; use a real HTTPS frontend URL and single-use link once an email provider is added.
