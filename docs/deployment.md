# Deployment

Deploy the API as an ASP.NET Core service with a managed SQL Server connection. Provide JWT signing, issuer, audience, and database credentials through the hosting environment's secret manager. Configure exact trusted CORS origins and HTTPS termination. `/health` is process health; `/health/ready` includes SQL connection readiness. The GitHub Actions workflow builds/tests pull requests and publishes an artifact on pushes to `main`. No cloud-specific deployment is configured.
