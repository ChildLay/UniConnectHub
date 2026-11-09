# Testing strategy

Domain tests cover core invariants and Application tests cover shared paging behavior. Integration tests use `WebApplicationFactory` for health and system metadata. Relational SQL Server behavior should be verified with SQL Server/Testcontainers in CI when a Docker runner is available. This environment currently has restricted local socket access, so live integration tests may not run here.
