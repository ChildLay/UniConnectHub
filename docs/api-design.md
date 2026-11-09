# API design

- Application routes use the explicit URI prefix `/api/v1`.
- `/health`, `/health/ready` and `/swagger` are host/operations endpoints.
- Controllers accept request DTOs and return response DTOs; persistence entities are not returned.
- Errors use ProblemDetails through centralized handling.
- Pagination uses a shared request that clamps page size to 100.
- Swagger UI is available in Development at `/swagger` and documents v1 at `/swagger/v1/swagger.json`.
- `GET /api/v1` describes currently available and planned module areas.
- Current module workflows include auth, university catalog, profiles, connections/blocking, communities, posts/feed, events, and opportunities. Discovery, mentoring, notifications, reporting, and moderation routes are not yet implemented.
