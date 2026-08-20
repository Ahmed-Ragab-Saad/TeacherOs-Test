# TeacherOS Backend

TeacherOS is a multi-tenant SaaS platform for teachers and educational centers. This repository currently contains the production-oriented backend and identity/tenancy persistence foundation only; no student, attendance, finance, messaging, video, subscription, or other product feature is implemented yet.

## Technology

- ASP.NET Core and C# on .NET 10
- Entity Framework Core with SQL Server
- Clean Architecture in a modular monolith
- Generated OpenAPI, Problem Details, structured logging, correlation IDs, and health checks
- xUnit tests and executable architecture rules
- Central NuGet package management and Docker packaging

## Architecture

Dependencies point inward:

```text
TeacherOS.Domain
        ^
        |
TeacherOS.Application
        ^             ^
        |             |
TeacherOS.Infrastructure
        ^
        |
TeacherOS.Api
```

The concrete project references are:

```text
Domain         -> none
Application    -> Domain
Infrastructure -> Application, Domain
Api            -> Application, Infrastructure
```

- `TeacherOS.Domain` owns framework-independent domain behavior and primitives.
- `TeacherOS.Application` owns use cases, expected-result contracts, and ports such as `IUnitOfWork`, `ITenantContext`, and `ICorrelationContext`. It does not reference EF Core.
- `TeacherOS.Infrastructure` owns EF Core, SQL Server configuration, and technical adapters.
- `TeacherOS.Api` owns HTTP concerns only: middleware, error normalization, OpenAPI, and operational endpoints.

Future behavior belongs in vertical use-case folders such as `Features/Students/Commands/CreateStudent/`. Each use case gets a focused handler and explicit contracts. Public interfaces live in individual files under Application abstractions; implementations live at the outer boundary. Provider SDK types must never cross into Application or Domain.

Write use cases stage changes and commit once through `IUnitOfWork.SaveChangesAsync`. Repositories are introduced only for meaningful aggregate persistence needs; there is no generic repository. External side effects will occur after authoritative state commits through durable delivery patterns when those features exist.

Tenant-owned operations must require a trusted `ITenantContext`. The interface exists, but no implementation is registered because authentication and membership resolution have not been designed yet. This deliberately prevents an accidental untrusted or missing tenant fallback.

## Projects

```text
src/
  TeacherOS.Domain/
  TeacherOS.Application/
  TeacherOS.Infrastructure/
  TeacherOS.Api/
tests/
  TeacherOS.Domain.Tests/
  TeacherOS.Application.Tests/
  TeacherOS.IntegrationTests/
  TeacherOS.ArchitectureTests/
```

Architecture tests validate both compiled assembly dependencies and the exact production project-reference graph. They also protect the Application layer from EF Core and enforce public type/interface file layout.

## Local development

Prerequisites:

- .NET SDK 10.0.400 or a compatible patch selected by `global.json`
- SQL Server for database-backed work

Restore, build, and test:

```powershell
dotnet restore TeacherOS.slnx
dotnet build TeacherOS.slnx --no-restore
dotnet test TeacherOS.slnx --no-build
```

Every completed backend task must also verify formatting, EF tooling when persistence changes, an actual API startup, liveness and Development OpenAPI responses, readiness against the available infrastructure, and a clean API shutdown. Integration tests do not replace this runtime check.

The API requires `Database:ConnectionString`. Keep it outside tracked settings. For local development, use User Secrets:

```powershell
dotnet user-secrets set "Database:ConnectionString" "<local-development-connection-string>" --project src/TeacherOS.Api/TeacherOS.Api.csproj
dotnet run --project src/TeacherOS.Api/TeacherOS.Api.csproj
```

For deployed environments, set `Database__ConnectionString` through the platform's secret store. Startup validation fails when this critical value is absent.

Operational endpoints:

- `/health/live` checks only the process foundation.
- `/health/ready` checks the process and SQL Server connectivity.
- `/openapi/v1.json` is available in Development and reflects implemented HTTP endpoints.

## Database migrations

The DbContext and migrations assembly are in `TeacherOS.Infrastructure`. ASP.NET Core Identity users and TeacherOS tenancy share this context, while tenant membership remains the explicit user-to-tenant relation. The API never calls `EnsureCreated` and does not apply migrations automatically at startup.

Create later migrations with the pinned local EF tool:

```powershell
dotnet tool restore
dotnet ef migrations add <MigrationName> --project src/TeacherOS.Infrastructure/TeacherOS.Infrastructure.csproj --startup-project src/TeacherOS.Api/TeacherOS.Api.csproj --output-dir Persistence/Migrations
dotnet ef database update --project src/TeacherOS.Infrastructure/TeacherOS.Infrastructure.csproj --startup-project src/TeacherOS.Api/TeacherOS.Api.csproj
```

Create, review, and test migrations before applying them through a controlled deployment step.

## Docker

Build the API image from the repository root:

```powershell
docker build -t teacheros-api .
```

Supply `Database__ConnectionString` at runtime. The image does not contain credentials and does not mutate the database schema on startup.

## Engineering rules

- Domain remains free of ASP.NET Core, EF Core, configuration, and provider dependencies.
- Application remains free of Infrastructure, API, and EF Core dependencies.
- Every public interface and primary public type has its own matching file.
- I/O is asynchronous and cancellation tokens propagate to persistence boundaries.
- UTC time is authoritative; use the registered `TimeProvider` instead of static clock calls in business logic.
- Expected failures use stable `Error.Code` values and `Result`; unexpected failures become safe Problem Details responses.
- API contracts never expose EF entities.
- Tenant isolation fails closed and Platform Admin behavior must be explicit.
- Secrets, personal data, and provider payloads do not belong in logs or source control.
- SQL reads should project to read models and use no-tracking unless tracking is required.
- External calls do not run inside long database transactions.

Future integrations such as background jobs, Redis, realtime communication, messaging, secure video, and the Python AI service are intentionally postponed until a concrete use case requires them.
