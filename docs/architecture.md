# Architecture

```text
Domain (no project dependencies)
  ↑ Application
  ↑ Infrastructure (implements application ports; EF Core/SQL Server)
  ↑ API (HTTP host; references Application and Infrastructure)
```

Domain owns invariants; Application owns use-case contracts and workflows; Infrastructure owns SQL Server, password/token/email adapters; API owns HTTP contracts and middleware. The system is modular and intentionally not represented as fully complete: some aggregate models exist before their application workflows and endpoints.
