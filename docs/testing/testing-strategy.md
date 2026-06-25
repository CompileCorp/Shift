# Shift Testing Strategy

## Overview

Shift uses a layered strategy built on the testing pyramid: a broad base of fast unit
tests, narrowing through integration and end-to-end tests that run against a real SQL
Server. Two concerns run through every level — **data safety** (a migration must never
silently lose data) and **regression prevention** (generated output is pinned with
snapshot tests so any change is deliberate and reviewed).

Principles:

- **Test against real SQL Server** via Testcontainers, not in-memory substitutes.
- **Prefer fast, isolated unit tests** for logic; reserve database-backed tests for
  behaviour that genuinely depends on the engine.
- **Each database test is self-contained** — it creates and drops its own uniquely
  named database.

## Test Levels

| Level | Purpose | Tooling | Representative areas |
|-------|---------|---------|----------------------|
| **Unit** | Pure logic, no external dependencies | `UnitTestContext<T>` + AutoMocker | Migration planning, DMD parsing, model export, assembly loading |
| **Integration** | Component behaviour against real SQL Server | Testcontainers (`SqlServerContainerFixture`) | Schema loading, SQL execution, type/constraint handling |
| **Data safety** | Block destructive migrations before they run | Real SQL Server + seeded data | String / decimal / binary / char truncation detection |
| **End-to-end** | Full workflow: assembly → plan → apply → verify | Real SQL Server | Complete migrations, mixin application, safe shrink |
| **Snapshot** | Prevent regressions in generated output | Verify (`.verified.txt`) | DMD generation, parser and export output |

Unit tests carry most of the coverage; the database-backed levels target behaviour that
can only be validated against the engine and are correspondingly fewer.

## Test Projects

The suite is split across four projects under `src/test/`, all using **xUnit**:

- **`Shift.Tests`** — Unit, integration, data-safety, E2E, and snapshot tests for the core
  `Shift` library. Uses Testcontainers, Verify, FluentAssertions, and Moq.AutoMock.
- **`Shift.Cli.Tests`** — Tests for the `Shift.Cli` command-line tool. Includes a
  dependency-graph test that builds the real service provider and resolves every command
  handler, guarding against unregistered interface dependencies.
- **`Shift.Ef.Tests`** — Tests for the Entity Framework code generators in `Shift.Ef`
  (entity, entity-map, DbContext, interface, and type-mapping generation).
- **`Shift.Test.Framework`** — Shared test infrastructure (`UnitTestContext<T>`,
  `VnumTestingHelper`, etc.) referenced by the other projects.

## Infrastructure

- **`SqlServerContainerFixture`** — shared SQL Server container; tests opt in with
  `[Collection("SqlServer")]` to reuse it and share the fixture.
- **`SqlServerTestHelper`** — database create/drop utilities and connection-string
  construction.
- **`DatabaseModelBuilder`** — fluent builder for test models; **`TestModels`** provides
  prebuilt scenarios.
- **`UnitTestContext<T>`** — base class exposing a mocked `Sut` via AutoMocker.

Containers are managed automatically (start, readiness check, cleanup) with dynamic port
binding.

## Conventions

**Naming** — `MethodName_Scenario_ExpectedResult`:

```
GeneratePlan_WithNewTables_ShouldCreateTableSteps
LoadDatabaseAsync_WithInvalidConnectionString_ShouldThrowException
IsAlterColumnPotentiallyUnsafe_WithStringTruncation_ShouldReturnTrue
```

**Unit test** — arrange via the mocked `Sut`, assert with FluentAssertions:

```csharp
public class MigrationPlannerTests : UnitTestContext<MigrationPlanner>
{
    [Fact]
    public void GeneratePlan_WithNewTables_ShouldCreateTableSteps()
    {
        var plan = Sut.GeneratePlan(CreateTargetModelWithTables(), new DatabaseModel());

        plan.Steps.Should().Contain(s => s.Action == MigrationAction.CreateTable);
    }
}
```

**Database test** — each test owns a uniquely named database and drops it in `finally`,
keeping tests fully isolated:

```csharp
[Collection("SqlServer")]
public class SqlServerLoaderTests
{
    private readonly SqlServerContainerFixture _fixture;

    [Fact]
    public async Task LoadDatabaseAsync_ShouldLoadTablesFromDatabase()
    {
        var dbName = SqlServerTestHelper.GenerateDatabaseName();
        var connectionString = SqlServerTestHelper.BuildDbConnectionString(
            _fixture.ConnectionStringMaster, dbName);
        await SqlServerTestHelper.CreateDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        try
        {
            var result = await new SqlServerLoader(connectionString).LoadDatabaseAsync();

            result.Tables.Should().NotBeEmpty();
        }
        finally
        {
            await SqlServerTestHelper.DropDatabaseAsync(_fixture.ConnectionStringMaster, dbName);
        }
    }
}
```

**Snapshot test** — assert generated output against a reviewed baseline:

```csharp
var dmd = Sut.GenerateDmdContent(table, model.Mixins.Values.ToList());
await Verify(dmd).UseTextForParameters($"{table.Name}.dmd");
```

To update a baseline after an intentional change, run the test, review the generated
`.received.txt`, and promote it to `.verified.txt`.

## Coverage

Coverage is collected on every PR and enforced by CI:

- **Collection** — `dotnet test --settings src/coverlet.runsettings --collect:"XPlat Code Coverage"` (Cobertura output).
- **Scope** — `src/coverlet.runsettings` is the single source of truth. Only product
  assemblies are measured (`[Shift]*`, `[Shift.Cli]*`, `[Shift.Ef]*`); test projects,
  `Examples/` sample code, auto-properties, and `[ExcludeFromCodeCoverage]` members
  (e.g. the CLI composition root) are excluded.
- **Gate** — ReportGenerator publishes an HTML artifact and a job-summary table; the build
  fails below `COVERAGE_THRESHOLD` (currently **99%** line coverage). See
  [CI/CD Pipeline](../ci-cd/pipeline.md).

To inspect coverage locally, run the collection command above and feed the resulting
`coverage.cobertura.xml` to [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
for an HTML report; CI remains authoritative for scope and threshold.

## Running Tests

**Prerequisites**: .NET 9.0 SDK and a running Docker Desktop (for the database-backed levels).

```bash
dotnet test                                   # everything
dotnet test --filter "MigrationPlanner"       # one class
dotnet test --filter "SqlServer"              # database-backed tests
dotnet test src/test/Shift.Tests              # one project
```

Unit tests complete in seconds; database-backed tests add the cost of container startup.
