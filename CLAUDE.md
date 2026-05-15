# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run API
dotnet run --project ./src/Kura.Api

# Tests
dotnet test
dotnet test --filter "FullyQualifiedName~ServiceName"   # single test class

# EF Core migrations (evidence only — Flyway manages the actual schema)
dotnet ef migrations add <MigrationName> --project src/Kura.Infrastructure --startup-project src/Kura.Api
# Never run `dotnet ef database update` against the shared Oracle instance

# Docker
docker-compose up
```

## Architecture

4-layer Clean Architecture: `Api → Application → Domain ← Infrastructure`

- **Domain** — entities and repository interfaces only; zero references to Infrastructure, EF Core, or ASP.NET
- **Application** — services (orchestration), DTOs, FluentValidation validators
- **Infrastructure** — EF Core `KuraDbContext`, repository implementations, Fluent API configurations in `Persistence/Configurations/`
- **Api** — thin controllers that only route HTTP; all logic lives in Application services

Reference layout:
```
src/
  Kura.Api/
  Kura.Application/
  Kura.Domain/
  Kura.Infrastructure/
tests/
  Kura.Application.Tests/
  Kura.Domain.Tests/
  Kura.Infrastructure.Tests/
```

No `.sln` file — run `dotnet` commands from repo root or target individual projects with `--project`.

## Key Patterns

- **Repository + Unit of Work** — `IRepository<T>` / `IUnitOfWork` defined in Domain, implemented in Infrastructure; call `IUnitOfWork.CommitAsync()` once per service operation
- **Soft delete** — all entities inherit `EntidadeBase` (`Id`, `StAtiva`, `DtCriacao`, `DtAtualizacao`); `StAtiva = false` via `Repository.SoftDelete()`; global `HasQueryFilter` on `StAtiva`
- **DTOs** — always separate from domain entities (`CreateDto`, `UpdateDto`, `ResponseDto`)
- **Validation** — FluentValidation only, not DataAnnotations; auto-validated via `AddFluentValidationAutoValidation()`
- **Error handling** — global `ExceptionHandlerMiddleware` returns RFC 7807 `application/problem+json`; logs via Serilog/ILogger to stdout. `LOG_ERRO` is a PL/SQL table — the .NET layer does **not** write to it
- **Exception → HTTP mapping:**
  - `EntidadeNaoEncontradaException` → 404
  - `RegraDeNegocioException` → 422
  - `ConflitoConcorrenciaException` → 409
  - `UnauthorizedAccessException` → 401
- **Multi-tenancy** — every query implicitly filters by `ID_CLINICA` from the JWT claim via `IClinicaContext` + `HasQueryFilter` in `KuraDbContext.ApplyTenantFilters()`
- **Bool columns** — all `bool`/`bool?` properties are globally mapped to `CHAR(1)` `'S'`/`'N'` by `BoolToSimNaoConverter`; never use `bit` or `NUMBER(1)` for booleans in Fluent API
- **Async** — every I/O call uses `await`; no `.Result` or `.Wait()`
- **Nullable reference types** — enabled; treat warnings as errors

## Database

Oracle 19c. Critical constraints:
- All table/column identifiers **UPPERCASE** in Fluent API configurations
- Explicit Oracle sequences for primary key generation (no `IDENTITY` or `ValueGeneratedOnAdd()`)
- camelCase in JSON responses, snake_case in Oracle column names
- **Read-only enforcement** — `ReadOnlyTablesInterceptor` throws `InvalidOperationException` on any `Add`/`Update`/`Delete` against `ContaTutor` or `Consentimento`; these are managed by the Java backend
- **Agendamento** is readable and writable (status updates only) with optimistic concurrency via `NrVersion` column
- `VW_TIMELINE_PET` is a database view mapped as a keyless entity (`TimelineItem`); never add a PK configuration to it
- Connection string lives in `appsettings.Development.json` (never committed); required config keys:
  - `ConnectionStrings:DefaultConnection`
  - `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`
  - `IoT:ApiKey` — API key for ESP32 devices hitting `IotController`
  - `Luna:ApiKey` — API key for the Python Luna chatbot integration

## Domain Model (core entities)

**Clinic domain** (written by .NET): `Clinica`, `Veterinario`, `Pet`, `Tutor` (N:N via `TutorPet`), `Especie`, `Raca`, `EventoClinico`, `Vacina`, `Prescricao`, `Exame`, `Consulta`, `Documento`, `Notificacao`, `Medicamento`, `DispositivoIot`, `LeituraTemperatura`, `AlertaTemperatura`, `TriagemLuna`, `InviteTutor`, `Alerta`

**External tables** (read-only, managed by Java): `ContaTutor`, `Consentimento`

**Shared table** (read-only, updated by .NET via status PATCH): `Agendamento`

`EventoClinico` is the base type; `Vacina`, `Prescricao`, `Exame`, and `Consulta` are subtypes — each POST creates both rows atomically inside a single `IUnitOfWork.CommitAsync()`.

## Authentication

- **JWT** — claims `clinicaId` and `veterinarioId`; all controllers use `[Authorize]` by default
- **API Key** (`X-Api-Key` header) — `ApiKeyAuthFilter` guards `IotController`; key from `IoT:ApiKey` config
- **Luna** — Python chatbot writes `TriagemLuna` rows; .NET reads them and exposes analytics via `LunaController`

## Testing

Stack: **xUnit + Moq + FluentAssertions**

- `Kura.Application.Tests` — unit tests for Application services; mock all repositories and `IUnitOfWork`
- `Kura.Domain.Tests` — pure domain logic tests
- `Kura.Infrastructure.Tests` — policy tests (migration file existence, `ReadOnlyTablesInterceptor` behaviour)

Migrations in `src/Kura.Infrastructure/Migrations/` are kept as FIAP academic evidence; `MigrationsPolicyTests` asserts the folder is non-empty.
