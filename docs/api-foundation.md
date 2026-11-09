# API and architecture

## Project boundaries

`UniConnect.Domain` contains framework-independent entities and invariants. `UniConnect.Application` contains use-case contracts, authentication workflows and shared paging. `UniConnect.Infrastructure` implements SQL Server persistence and token/email services. `UniConnect.Api` hosts controllers, authentication configuration, Swagger and HTTP middleware. Dependencies flow inward: API → Application/Infrastructure, Infrastructure → Application/Domain, Application → Domain.

## Routing and OpenAPI

Application routes use `/api/v1/...`. Swagger UI is available in Development at `/swagger`, with the v1 document at `/swagger/v1/swagger.json`. Health routes `/health` and `/health/ready` are operational endpoints outside the application version path.

## Authentication and secrets

The initial auth endpoints implement registration, login, refresh-token rotation, logout, email verification and password reset. Passwords use ASP.NET Core's versioned `PasswordHasher<User>`. Only hashes of refresh and one-time tokens are persisted. JWT signing uses HMAC SHA-256 and must be supplied securely. For local development, set secrets with `dotnet user-secrets init --project src/UniConnect.Api` and `dotnet user-secrets set "Jwt:SigningKey" "<at-least-32-byte-local-secret>" --project src/UniConnect.Api`; use deployment environment variables such as `Jwt__SigningKey` in hosted environments. Never use the placeholder in production.

Email delivery currently uses a development logger adapter. Production email provider integration and its secrets belong in Infrastructure and environment configuration. Verification/reset secrets are not included in logs.

## Errors and logging

Unexpected exceptions are logged server-side and returned as safe ProblemDetails. Controllers stay thin and API responses use DTOs. Built-in `ILogger` providers emit structured application logs.

## CORS and rate limiting

The named `FrontendClients` policy reads exact origins from `Cors:AllowedOrigins`; production defaults to no browser origins. The sensitive endpoint rate limit is IP-partitioned and configurable under `RateLimiting`.

## Health

`/health` checks process health. `/health/ready` checks the configured SQL Server connection without disclosing connection details.

## Persistence and evolution

EF Core mappings live in `IEntityTypeConfiguration<T>` classes, with SQL Server configured in Infrastructure. Domain does not reference EF Core or ASP.NET. Add migrations with `dotnet ef migrations add InitialCreate --project src/UniConnect.Infrastructure --startup-project src/UniConnect.Api`. The EF CLI must be installed to run that command. Application and API should add feature workflows through use-case services; controllers should not access the DbContext directly.

The current schema models universities, accounts, profiles, skills/interests, connections/blocks, communities, posts, events, mentoring, opportunities, notifications, reports, and audit records. Not every model has complete user-facing CRUD workflows yet.
