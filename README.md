# UniConnect Hub

UniConnect Hub is a student and alumni community platform. This repository contains a Clean Architecture backend foundation and a Version 1 ASP.NET Core Web API for authentication, universities, profiles, discovery, connections, communities, posts/feed, events, mentoring, opportunities, notifications, reporting, and moderation.

## Prerequisites

- .NET 9 SDK (the installed SDK; .NET 10 runtime is present but its SDK is not installed)

## Build and run

```powershell
dotnet build UniConnectHub.sln
dotnet run --project src/UniConnect.Api
```

In Development, Swagger UI is available at `https://localhost:7040/swagger` and its v1 OpenAPI document is at `/swagger/v1/swagger.json`. API endpoints are versioned under `/api/v1`; `/api/v1/system/info` returns service metadata and `/api/v1/auth` exposes the initial authentication workflow. `/health` reports application health; `/health/ready` checks database connectivity.

See [docs/api-v1.md](docs/api-v1.md) for the complete endpoint reference, request examples, JWT usage, and authorization testing matrix for User, Moderator, and Admin roles.

Profile and cover photo uploads are documented in [docs/profile-media.md](docs/profile-media.md). For existing UAT or production SQL Server databases, apply [docs/cover-photo-schema.sql](docs/cover-photo-schema.sql) before deploying the cover-photo field.

## Tests

```powershell
dotnet test UniConnectHub.sln
```

## Architecture status

The solution contains `src/UniConnect.Domain`, `src/UniConnect.Application`, `src/UniConnect.Infrastructure`, `src/UniConnect.Api`, and Domain, Application, API, and integration test projects. The Version 1 React web client is in `clients/react-web`. See [the frontend setup guide](clients/react-web/README.md), [frontend architecture](docs/frontend-architecture.md), and [design system](docs/frontend-design-system.md). Database migrations and full end-to-end verification remain to be completed.

The Development database connection targets SQL Server at `localhost` using Windows integrated authentication and the `UniConnectHub` database.
## Email delivery

Email verification and password reset use the `IEmailService` abstraction. The default `Email:Provider` is `Console`, which queues messages to the development log. To send real email through Gmail SMTP, enable 2-Step Verification on the sender’s Google Account, create an App Password, and configure the API’s User Secrets (never commit the App Password):

```powershell
dotnet user-secrets init --project src/UniConnect.Api
dotnet user-secrets set "Email:Provider" "Gmail" --project src/UniConnect.Api
dotnet user-secrets set "Email:Gmail:Username" "your-address@gmail.com" --project src/UniConnect.Api
dotnet user-secrets set "Email:Gmail:AppPassword" "your-16-character-app-password" --project src/UniConnect.Api
dotnet user-secrets set "Email:Gmail:FromEmail" "your-address@gmail.com" --project src/UniConnect.Api
```

The Gmail sender uses `smtp.gmail.com` on port `587` with TLS. The sender and username can be a Gmail address; use a Google App Password rather than your normal Google password. Gmail is suitable for local testing and low-volume use, but its daily sending limits apply. For a production community, use a dedicated transactional email provider for better delivery monitoring and sender reputation. Install and start a local SQL Server instance before starting the API. Override `ConnectionStrings:DefaultConnection` using .NET user secrets or environment variables when needed; keep production credentials out of source control.

## Frontend checks

From `clients/react-web`, run:

```bash
npm ci
npm run lint
npm run typecheck
npm run test
npm run build
```


Brevo is also supported as a transactional SMTP provider. On Brevo's SMTP page, activate transactional sending, verify a sender, and copy the SMTP login and SMTP key (not the API key). Set the following User Secrets, then restart the API:

```powershell
dotnet user-secrets set "Email:Provider" "Brevo" --project src/UniConnect.Api
dotnet user-secrets set "Email:Brevo:Username" "your-brevo-smtp-login" --project src/UniConnect.Api
dotnet user-secrets set "Email:Brevo:SmtpKey" "your-brevo-smtp-key" --project src/UniConnect.Api
dotnet user-secrets set "Email:Brevo:FromEmail" "your-verified-sender" --project src/UniConnect.Api
```

Brevo's Free plan currently supports up to 300 sends per day, including transactional email. Free-provider addresses such as `@gmail.com` cannot be domain-authenticated and may be rewritten by Brevo, so use a verified sender address from a domain you control for production.