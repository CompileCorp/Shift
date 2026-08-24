# Proposal: DMD-driven EF codegen with a cross-module DbContext base class

## Overview

Shift already generates EF Core entities, entity maps, a `DbContext` and a `DbContext` interface directly from DMD, without EF Core's reverse-engineering scaffolder. `EfCodeGenerator.GenerateEfCodeAsync` ([`src/Shift.Ef/EfCodeGenerator.cs:29-49`](../../src/Shift.Ef/EfCodeGenerator.cs)) orchestrates four plain `StringBuilder` emitters — `EntityGenerator`, `EntityMapGenerator`, `DbContextInterfaceGenerator`, `DbContextGenerator` — and nothing in the repository calls `dotnet ef dbcontext scaffold`, implements `IDesignTimeDbContextFactory`, or touches `IReverseEngineerScaffolder`. `Microsoft.EntityFrameworkCore.Design` is referenced in [`src/Shift.Ef/Shift.Ef.csproj:13`](../../src/Shift.Ef/Shift.Ef.csproj) but no source file uses it.

So this proposal is **not** "replace the EF scaffolder" starting from zero. That decision was made long ago and the DMD-driven generator already exists. What this proposal asks for is two increments on top of it:

1. Make the DMD-driven path **first-class per module** — namespace, context name, interface name and output location configured per logical module rather than as one global value for the whole run.
2. Add **cross-module `DbContext` inheritance**, so (for example) a Forestry module's generated context derives from a Core module's generated context, sharing Core's entity set without duplicating it.

> **The short version:** the generator is good; the *configuration surface* and the *absence of a module concept* are what block the feature we actually want.

## Current state and the gaps

### What exists today

| Concern | Where | Behaviour |
|---|---|---|
| Config surface | [`EfCodeGenerationOptions.cs:5-8`](../../src/Shift.Ef/EfCodeGenerationOptions.cs) | Exactly four settings: `NamespaceName` (default `"Generated"`), `ContextClassName` (`"GeneratedDbContext"`), `InterfaceName` (`"IGeneratedDbContext"`), `BaseClassName?` |
| Entity emission | [`EntityGenerator.cs:36`](../../src/Shift.Ef/EntityGenerator.cs) | `public partial class {Table}Entity` — always, with **no base type and no interface** |
| Context emission | [`DbContextGenerator.cs:27-28`](../../src/Shift.Ef/DbContextGenerator.cs) | `public partial class {Context} : {baseClass}, {Interface}` where `baseClass` falls back to `DbContext` |
| Mapping application | [`DbContextGenerator.cs:51-60`](../../src/Shift.Ef/DbContextGenerator.cs) | `base.OnModelCreating(modelBuilder)` followed by one explicit `modelBuilder.ApplyConfiguration(new {Table}EntityMap())` per table |
| Output layout | [`EfCodeGenerator.cs:63-99`](../../src/Shift.Ef/EfCodeGenerator.cs) | Flat: every `{Table}Entity.g.cs`, `{Table}EntityMap.g.cs`, `{Interface}.g.cs`, `{Context}.g.cs` written straight into one `outputPath` |

Two things in that table are worth calling out as *good news*, because they mean less work than it first appears:

- `BaseClassName` already exists and is already wired into the context's declaration.
- `OnModelCreating` already uses **explicit per-table `ApplyConfiguration` calls**, not `ApplyConfigurationsFromAssembly`. That is exactly the shape module-scoped mapping needs (see [Design 2](#design-2--per-module-generation-and-the-base-class-feature)).

### Gap 1 — you cannot generate a customised EF context from DMD files

`BaseClassName` is applied **only** to the `DbContext`. And the only CLI surface that lets a caller set any of these options is `ef sql-custom`, which parses `--namespace`, `--context`, `--interface`, `--base-class` and `--schema` ([`src/Shift.Cli/CommandHelper.cs:206-266`](../../src/Shift.Cli/CommandHelper.cs), option switch at lines 230-244).

That command loads the model **from a live database**:

```csharp
// src/Shift.Cli/Commands/EfFromSqlCustomCommand.cs:31
var model = await _shift.LoadFromSqlAsync(request.ConnectionString, request.Schema);
```

The DMD-file path, `ef files`, takes only input paths and an output path ([`CommandHelper.cs:182-204`](../../src/Shift.Cli/CommandHelper.cs)) and calls the convenience overload, so it silently accepts the hardcoded defaults:

```csharp
// src/Shift.Cli/Commands/EfFromFilesCommand.cs:29-31
var model = await _shift.LoadFromPathAsync(request.DmdLocationPaths);
await _efCodeGenerator.GenerateEfCodeAsync(model, outputPath: request.OutputDirectoryPath);
```

This is documented as intentional in [`docs/cli/shift-ef-commands.md`](../cli/shift-ef-commands.md) ("`ef files` uses default code-generation settings and does not accept namespace/context/interface/base-class options. For custom output settings, generate from SQL Server with `ef sql-custom`").

**The consequence:** the *customisable* generation path requires a database that already has the schema in it. That inverts the source of truth — DMD is supposed to drive the database, not follow it — and it is a chicken-and-egg problem in CI, where you would have to provision and migrate a SQL Server instance just to emit C# that the DMD files fully determine on their own.

### Gap 2 — there is no module concept anywhere in the model

`DatabaseModel` is a flat pair of dictionaries:

```csharp
// src/Shift/Model/DatabaseModel.cs:5-7
public Dictionary<string, TableModel> Tables { get; set; } = new();
public Dictionary<string, MixinModel> Mixins { get; set; } = new();
```

and `TableModel` ([`TableModel.cs:5-10`](../../src/Shift/Model/TableModel.cs)) carries `Name`, `Fields`, `ForeignKeys`, `Indexes`, `Attributes` and `Mixins` — **no module, no namespace, no schema**. `--schema` is a single global value for an entire run, and `NamespaceName` is a single value applied to every generated file.

The nearest existing construct is embedded-resource namespace *filtering* in `LoadFromAssembliesAsync`, via `IsResourceInNamespace` ([`src/Shift/Shift.cs:219-232`](../../src/Shift/Shift.cs), used at line 46). That decides **which resources get loaded**; it does not tag the resulting tables, and it does not scope references. Once a table is in the dictionary, nothing records where it came from.

The only inter-set relationship that exists at all is duplicate resolution by load order — "first assembly wins" for both mixins ([`Shift.cs:66-75`](../../src/Shift/Shift.cs)) and tables ([`Shift.cs:109-121`](../../src/Shift/Shift.cs)):

```csharp
// src/Shift/Shift.cs:112-120
if (!model.Tables.ContainsKey(table.Key))
{
    model.Tables.Add(table.Key, table.Value);
    ...
}
else
{
    Logger.LogDebug("Skipped table {TableName} ... (already loaded)", ...);
}
```

A silent skip is a reasonable override mechanism for assembly priority. It is a bad foundation for module ownership.

### Gap 3 — `BaseClassName` alone cannot express inheritance

Because no table knows which module owns it, a derived context has no way to distinguish the entities it *owns* from the entities it *inherits*. Setting `BaseClassName = "CoreDbContext"` today and running the generator over the union of Core + Forestry DMD would emit, for every Core table:

- a second `{Table}Entity` class, in the derived module's namespace ([`EntityGenerator.cs:36`](../../src/Shift.Ef/EntityGenerator.cs) has no ownership filter);
- a second `{Table}EntityMap`;
- a duplicate `DbSet<{Table}Entity>` on the derived context, shadowing the inherited one ([`DbContextGenerator.cs:44-47`](../../src/Shift.Ef/DbContextGenerator.cs));
- a duplicate `ApplyConfiguration` call for a type the base context already configured ([`DbContextGenerator.cs:57-60`](../../src/Shift.Ef/DbContextGenerator.cs)).

Ownership is the missing primitive. Everything else in this proposal follows from it.

## Goals / Non-goals

### Goals

- **DMD stays the single source of truth** for EF artifacts — no generation path that requires a live database.
- **Per-module generation**: namespace, context class, interface name and output directory are driven by configuration that lives *with the model*, not by CLI flags supplied at each call site.
- **Cross-module context inheritance**: a module's generated `DbContext` can derive from another module's generated `DbContext`, with no duplicated entity classes, `DbSet`s or `IEntityTypeConfiguration`s.
- **Generation stays a pure, deterministic function of the DMD files** — same inputs, same bytes out — so a CI job can regenerate and assert a zero diff.

### Non-goals

- **Adopting EF Migrations.** Shift's `MigrationPlanner` and `SqlMigrationPlanRunner` ([`Shift.cs:189-191`](../../src/Shift/Shift.cs)) remain the only authors of DDL.
- **DMD-level type inheritance for entities** — no TPH/TPT/TPC. The inheritance in this proposal is between *contexts*, not between *entities*.
- **Changing the DMD type system.** No new field types, no changes to `DmdTypeHelper`/`SqlTypeHelper`.

## Why not EF Core's own scaffolder

Since the DMD generator predates this proposal, it is worth restating why the scaffolder is not the answer for the *new* work either.

**Source-of-truth direction.** `dotnet ef dbcontext scaffold` reads a live database, so generated code can only exist *after* a migration has been applied. DMD already drives migrations (`MigrationPlanner` → `SqlMigrationPlanRunner`) and DMD export (`ModelExporter`), and this organisation already generates other artifacts off the same DMD files — the Forestry repository generates audit triggers from its `.dmd` files and runs a CI check that fails when the committed triggers are stale.<sup>1</sup> Adding a DB-first generator would give one schema two masters and guarantee drift.

**Determinism and CI.** DMD → code is a pure function of text files. Scaffolding needs a provisioned, migrated database, which means it cannot be a cheap, reviewable diff check on a pull request.

**Round-tripping and customisation.** The scaffolder regenerates whole files and is hostile to hand edits. Shift's `.g.cs` + `partial` split already gives a clean customisation seam: every generated type is `partial` ([`EntityGenerator.cs:36`](../../src/Shift.Ef/EntityGenerator.cs), [`EntityMapGenerator.cs:35`](../../src/Shift.Ef/EntityMapGenerator.cs), [`DbContextGenerator.cs:28`](../../src/Shift.Ef/DbContextGenerator.cs), [`DbContextInterfaceGenerator.cs:27`](../../src/Shift.Ef/DbContextInterfaceGenerator.cs)), so hand-written code lives in a sibling non-generated file and survives regeneration.

**Fidelity.** Scaffolding can only see what SQL Server retains. DMD-level intent that SQL does not preserve, or preserves only ambiguously, includes:

| DMD intent | Where it lives | Why scaffolding can't recover it |
|---|---|---|
| Mixins (`.dmdx`) | `ApplyMixin` flattens mixin fields and FKs into the table and records membership on `TableModel.Mixins` ([`Parser.cs:384-415`](../../src/Shift/Parser.cs)) | After DDL the columns are indistinguishable from hand-written ones; mixin membership is gone |
| Relationship aliases (`model User as AssignedUser`) | [`Parser.cs:150-168`](../../src/Shift/Parser.cs) derives the column name from the alias | Only the derived column name survives; `ModelExporter` has to re-infer the alias heuristically (`ExtractSemanticName`, [`ModelExporter.cs:117-136`](../../src/Shift/ModelExporter.cs)) |
| One-to-many `models X` declarations | `RelationshipType.OneToMany` ([`Parser.cs:139-156`](../../src/Shift/Parser.cs)) | Nothing in SQL distinguishes it. `SqlServerLoader` hardcodes `RelationshipType.OneToOne // Default assumption` ([`SqlServerLoader.cs:257`](../../src/Shift/SqlServerLoader.cs)), so a DB-first load loses the distinction outright |
| `@NoIdentity` | [`Parser.cs:221-228`](../../src/Shift/Parser.cs) clears `IsIdentity` on PK fields | Recoverable in principle, but as an inference from column metadata rather than a declared intent |
| `key(...)` vs `index(...)` | `ParseKey` sets `IsUnique = true, IsAlternateKey = true` ([`Parser.cs:351-365`](../../src/Shift/Parser.cs)); `ParseIndex` sets `IsAlternateKey = false` ([`Parser.cs:367-382`](../../src/Shift/Parser.cs)) | The distinction survives only as the `AK_` vs `IX_` name prefix ([`IndexNameHelper.cs:27`](../../src/Shift/Helpers/IndexNameHelper.cs)) — recoverable only by parsing index names |

**And the decisive one:** the scaffolder has no notion of module boundaries, and no notion of one context inheriting another context's entity set. That is the feature this proposal is actually about, and it has to be ours regardless of which generator sits underneath.

## Design 1 — a module concept in DMD

Module configuration should live **with the model**, in DMD, rather than in CLI flags or a side JSON file. The DMD files are already the single source of truth for the schema; the namespace and context name that the schema generates into belong in the same place, versioned in the same commit.

```dmd
module Forestry {
  namespace Compile.Forestry.Data
  context ForestryDbContext
  interface IForestryDbContext
  schema dbo
  extends module Core
}
```

### Model-layer changes

| Change | Detail |
|---|---|
| New `ModuleModel` | `Name`, `Namespace`, `ContextClassName`, `InterfaceName`, `Schema`, `BaseModuleName?` |
| `DatabaseModel` gains | `Dictionary<string, ModuleModel> Modules { get; set; } = new();` alongside `Tables` and `Mixins` |
| `TableModel` gains | an owning-module reference (e.g. `string? ModuleName`), set at parse time |

### Parser changes

`Parser` is hand-written and line-oriented — `ParseMixin` dispatches on `line.StartsWith("mixin ")` ([`Parser.cs:29`](../../src/Shift/Parser.cs)) and `ParseTable` on `line.StartsWith("model ")` / `"extends "` ([`Parser.cs:64`](../../src/Shift/Parser.cs), [`Parser.cs:117`](../../src/Shift/Parser.cs)). Adding a module declaration is therefore a new `ParseModule` method and a new `StartsWith("module ")` branch — **not a grammar change**, because there is no grammar to change.

### Back-compatibility

A DMD set with no `module` declaration lands in a **synthetic default module** that reproduces today's values exactly: namespace `Generated`, context `GeneratedDbContext`, interface `IGeneratedDbContext`, schema from `--schema` (default `dbo`). Existing `apply`, `export` and `ef` invocations behave identically, byte for byte.

### Reference resolution stays flat

Foreign-key resolution is **not** module-scoped. There is one physical database and table names are already globally unique — `DatabaseModel.Tables` is keyed by bare table name, `ForeignKeyModel.TargetTable` is a bare table name, and `NormalizeForeignKeyTypes` ([`Shift.cs:234`](../../src/Shift/Shift.cs)) aligns FK column types against a flat PK-type map built over all tables. A Forestry table referencing a Core table writes `model Species` exactly as it does today; no qualified `Core.Species` syntax is needed.

> Module ownership changes **codegen**, not **resolution**. That distinction is what keeps this proposal small.

One new validation is required: **two modules must never own the same table name.** Today that case resolves silently via first-assembly-wins ([`Shift.cs:109-121`](../../src/Shift/Shift.cs)). With explicit modules, a collision means two modules both claim to generate the same entity — that should be a hard error with both module names in the message, not a debug log line.

## Design 2 — per-module generation and the base-class feature

### Options additions

| Addition | Purpose |
|---|---|
| `ModuleName` | Which module this generation run emits |
| `BaseContextClassName` | The base module's generated context type, e.g. `CoreDbContext` |
| `BaseContextNamespace` | Namespace of that base context |
| Inherited-entity namespace map | Table name → owning module's namespace, so cross-module FK targets can be `using`-imported |
| `EntityBaseClassName?` | The missing entity-side hook (today `BaseClassName` reaches only the context) |
| Per-module output subdirectory | `out/<Module>/` instead of today's flat dump into one `outputPath` ([`EfCodeGenerator.cs:63-99`](../../src/Shift.Ef/EfCodeGenerator.cs)) |

### The ownership rule

> **Every table is owned by exactly one module.** Generating module `M` with base module `B` emits entities, entity maps, `DbSet<>`s and interface members **only** for tables owned by `M`.

Foreign keys whose target is owned by `B` are referenced through `using B.Namespace;` against `B`'s already-generated entity types. No duplicate entity class, no duplicate `DbSet`, no duplicate configuration. This is the single rule that makes `BaseClassName` actually usable, and it is what Gap 3 is missing.

### `OnModelCreating`

The generated derived context must:

1. call `base.OnModelCreating(modelBuilder)` **first**, so the base module's configurations are applied by the base module's own code;
2. then apply **only** `M`'s configurations;
3. and do so with **explicit** `modelBuilder.ApplyConfiguration(new XEntityMap())` calls, one per owned table.

Point 3 matters: `modelBuilder.ApplyConfigurationsFromAssembly(...)` would sweep in every `IEntityTypeConfiguration` in the assembly — including the base module's maps if the modules ship in one assembly — and double-apply them. The current generator already does the right thing here ([`DbContextGenerator.cs:51-60`](../../src/Shift.Ef/DbContextGenerator.cs)); the change is to filter the loop by ownership rather than iterate all of `model.Tables`.

### Constructor chaining — the concrete gotcha

`DbContextGenerator` today emits a parameterless constructor and a single generic-options constructor ([`DbContextGenerator.cs:32-41`](../../src/Shift.Ef/DbContextGenerator.cs)):

```csharp
public {Context}() { }
public {Context}(DbContextOptions<{Context}> options) : base(options) { }
```

A base context whose only options constructor takes `DbContextOptions<CoreDbContext>` **cannot be chained** from a derived context that is constructed with `DbContextOptions<ForestryDbContext>` — the types are unrelated, so `: base(options)` will not compile. The fix is for every generated context to emit **both**:

- a `public` constructor taking `DbContextOptions<TSelf>` (what DI resolves, unchanged for callers), and
- a `protected` constructor taking the non-generic `DbContextOptions`, which a derived context can chain into.

`DbContextGenerator` must emit that pair for **every** context, not only for contexts known in advance to be base contexts — the generator cannot know, at the time it emits Core, that Forestry will later derive from it.

### Generated derived context — sketch

```csharp
// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Compile.Core.Data;              // base module's entity types

namespace Compile.Forestry.Data;

public partial class ForestryDbContext : CoreDbContext, IForestryDbContext
{
    public ForestryDbContext() { }

    public ForestryDbContext(DbContextOptions<ForestryDbContext> options)
        : base((DbContextOptions)options) { }

    protected ForestryDbContext(DbContextOptions options)
        : base(options) { }

    // Only tables owned by the Forestry module. Species, User, etc.
    // are inherited from CoreDbContext and are NOT redeclared.
    public virtual DbSet<StandEntity> Stand { get; set; }
    public virtual DbSet<HarvestEntity> Harvest { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);   // applies Core's maps

        modelBuilder.ApplyConfiguration(new StandEntityMap());
        modelBuilder.ApplyConfiguration(new HarvestEntityMap());
    }
}
```

### Interface chaining

The generated interface chains the same way, declaring only `M`'s `DbSet`s:

```csharp
public partial interface IForestryDbContext : ICoreDbContext
{
    DbSet<StandEntity> Stand { get; set; }
    DbSet<HarvestEntity> Harvest { get; set; }
}
```

Consumers keep depending on the narrow interface they need, and `DbContextInterfaceGenerator` gains a base-interface parameter mirroring the context's base-class one ([`DbContextInterfaceGenerator.cs:27`](../../src/Shift.Ef/DbContextInterfaceGenerator.cs)).

### DI note

Registering **both** `CoreDbContext` and `ForestryDbContext` in a single application gives you two `DbContext` instances — two change trackers, two identity maps — over the same connection and the same tables. Entities tracked by one are invisible to the other, and saving through both in one logical unit of work is a data-integrity hazard.

> **Recommendation:** an application that consumes Forestry registers **only** `ForestryDbContext`. `CoreDbContext` is registered on its own only in Core-only applications. If a Core-facing service needs a narrow dependency, inject `ICoreDbContext` — `ForestryDbContext` satisfies it through the interface chain.

## Migrations and rollout implications

### Multi-module `apply`

Shift's `apply` is already module-agnostic and already accepts multiple paths — `apply <connection-string> <path1> <path2> ... [--schema <name>]` ([`CommandHelper.cs:58-88`](../../src/Shift.Cli/CommandHelper.cs)), and `LoadFromPathAsync` enumerates all of them ([`Shift.cs:140-177`](../../src/Shift/Shift.cs)). A multi-module deploy therefore applies the **union** of the modules with no change to `apply` itself.

**Ordering matters, and it already does.** Base-module paths must load first, for two reasons:

- `extends <Table>` resolves against tables already in the model and throws `Cannot extend {tableName}` if the base table has not been parsed yet ([`Parser.cs:117-125`](../../src/Shift/Parser.cs));
- `NormalizeForeignKeyTypes` ([`Shift.cs:234`](../../src/Shift/Shift.cs)) runs once over the fully merged model, aligning FK column types to their target PK types, so it must see all modules together.

### Do not turn on EF Migrations for these contexts

This deserves to be explicit. A derived context exposes the base module's inherited `DbSet`s — that is the whole point of the design — so `dotnet ef migrations add` run against `ForestryDbContext` would see Core's tables in its model and try to own them, emitting DDL for tables Core already owns. That is precisely the duplication the ownership rule exists to prevent, reintroduced at the DDL layer.

**Shift's planner stays the only DDL author.** `MigrationPlanner` diffs the DMD model against the live database and `SqlMigrationPlanRunner` executes it ([`Shift.cs:186-212`](../../src/Shift/Shift.cs)). EF Core's role here is runtime mapping only.

### Phased rollout

| Phase | Scope | Value delivered | New concepts |
|---|---|---|---|
| 1 | Give `ef files` the same flags `ef sql-custom` already has — `--namespace`, `--context`, `--interface`, `--base-class` | Closes **Gap 1**: customised EF codegen from DMD, no live database | None |
| 2 | `module` declaration + `ModuleModel` + `TableModel` ownership + per-module output directories, with a synthetic default module preserving current behaviour | Closes **Gap 2**: modules exist in the model | `module` DMD block |
| 3 | Base-context inheritance: ownership-filtered emission, interface chaining, the constructor pair | Closes **Gap 3**: the feature we actually want | Module `extends` |
| 4 | CI check that regenerating produces **no diff** against committed generated code | Locks determinism in, mirroring the audit-trigger staleness check the Forestry repository already runs<sup>1</sup> | None |

Phase 1 is independently valuable and can ship on its own. Phases 2-4 are only worth doing if the cross-module context is actually wanted — which is the decision this document is asking for.

## Risks and open questions

**Where should module config live?** DMD `module` block (recommended above — it keeps configuration with the model and versioned alongside it), a `module.json` sidecar (easier to add, but a second source of truth), or CLI flags only (no new concepts, but the configuration then lives in build scripts and drifts per call site).

**Multi-level chains and the diamond case.** Is `Core → Forestry → Something` supported, or capped at one level? And what happens when two modules both extend `Core` and are combined in one application? A single derived context cannot have two base classes, so the diamond either needs an explicit "combined" module that extends one and absorbs the other, or a clear error. Either is defensible; it should be decided before implementation, not discovered during it.

**Should entities get the base-class/interface hook too?** `EntityBaseClassName` is proposed above because the asymmetry is odd (contexts have a base-class hook, entities do not), but every generated entity is already `partial` ([`EntityGenerator.cs:36`](../../src/Shift.Ef/EntityGenerator.cs)), so a hand-written partial can already add a base type. It may be that `partial` is enough and the option is unnecessary surface area.

**Per-module SQL schema.** `--schema` is global today, threaded through `ApplyCommand`, `ExportCommand` and every `ef` subcommand. If a module owns its schema, what does a cross-module foreign key mean — and does `MigrationPlanner`, which currently plans against one schema at a time, need to become schema-aware first? This may be the single largest hidden cost in the proposal, and it is worth scoping before committing to Phase 2.

**One assembly or one project per module?** If modules become separate assemblies, the derived context's `using B.Namespace;` becomes a project reference, and Shift may need to emit a `.csproj` — or at minimum document the expected project layout. If they stay one assembly, the `ApplyConfigurationsFromAssembly` hazard described above is a live footgun for hand-written code, not just a theoretical one.

**Table-name validation becomes load-bearing.** Output paths are built directly from table names — `Path.Combine(outputPath, $"{table.Name}Entity.g.cs")` ([`EfCodeGenerator.cs:68`](../../src/Shift.Ef/EfCodeGenerator.cs)) and `$"{table.Name}EntityMap.g.cs"` ([`EfCodeGenerator.cs:79`](../../src/Shift.Ef/EfCodeGenerator.cs)) — and per-module output subdirectories add another path segment derived from a DMD identifier. C#-identifier validation on table and module names should land alongside this work rather than after it.

**Adjacent limitation, worth knowing about.** Navigation properties are named after the FK's *target table*, not its alias — `propertyName = fk.TargetTable` ([`EntityGenerator.cs:107-119`](../../src/Shift.Ef/EntityGenerator.cs)), and `EntityMapGenerator.ConfigureForeignKey` binds `builder.HasOne(e => e.{fk.TargetTable})` ([`EntityMapGenerator.cs:108-112`](../../src/Shift.Ef/EntityMapGenerator.cs)). Two aliased FKs to the same table (`model User as AssignedUser`, `model User as ReviewedByUser`) therefore generate two properties both named `User`, which will not compile. This is orthogonal to modules and out of scope here, but cross-module FKs make aliased relationships more likely, so it is a plausible companion fix.

---

<sup>1</sup> The Forestry audit-trigger generator and its staleness check live in that repository and are cited here from project context, not verified against this repository's source.

> **Decision requested:** ship Phase 1 regardless (it is small and closes a real gap), and confirm whether cross-module `DbContext` inheritance is wanted before Phases 2-4 are scoped in detail.
