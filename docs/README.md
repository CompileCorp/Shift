# Shift Documentation

Shift is a database schema toolkit built around the **DMD** (Database Model Definition) language. You describe your schema in human-readable `.dmd`/`.dmdx` files (or load it from compiled assemblies), and Shift generates SQL Server migrations and Entity Framework Core code from it.

This directory contains the project's reference and developer documentation, organized by topic below.

## DSL — the DMD language

| Doc | What it covers |
|-----|----------------|
| [dsl/dmd-file-format.md](dsl/dmd-file-format.md) | Complete reference for the DMD/DMDX language — types, nullability, relationships, indexes, mixins, and the SQL it generates. |
| [dsl/dmd-agent-reference.md](dsl/dmd-agent-reference.md) | Compact, generation-focused cheat sheet of DMD/DMDX syntax for AI agents and quick lookup. |

## CLI — the `shift` command-line tool

| Doc | What it covers |
|-----|----------------|
| [cli/shift-cli-reference.md](cli/shift-cli-reference.md) | Full reference for the `shift` CLI: every command, argument, option, and usage example. |
| [cli/shift-ef-commands.md](cli/shift-ef-commands.md) | The `shift ef` subcommands that generate Entity Framework Core code from SQL or DMD sources. |

## Architecture — how Shift works internally

| Doc | What it covers |
|-----|----------------|
| [architecture/assembly-loading.md](architecture/assembly-loading.md) | Loading DMD models from compiled .NET assemblies (the `apply-assemblies` flow). |
| [architecture/migration-planner.md](architecture/migration-planner.md) | How the migration planner diffs the target model against the live database to produce an ordered plan. |
| [architecture/sql-migration-plan-runner.md](architecture/sql-migration-plan-runner.md) | How a migration plan is translated into SQL Server DDL and executed, including the data-loss safety checks. |
| [architecture/shift-ef-generator.md](architecture/shift-ef-generator.md) | How Shift.Ef generates EF Core entities, entity maps, and the DbContext from a model. |
| [architecture/shift-dbml-exporter.md](architecture/shift-dbml-exporter.md) | How Shift.Dbml renders a model as a DBML diagram for dbdiagram.io, and the `erd-*` plugin attributes that shape it. |

## Development — patterns and developer notes

| Doc | What it covers |
|-----|----------------|
| [development/vnum-pattern.md](development/vnum-pattern.md) | Guide to the Vnum (`CompileCorp.Vnum`) value-enumeration pattern used throughout the codebase — opens with a copy-paste [Quick Reference](development/vnum-pattern.md#quick-reference) cheat sheet, then the full pattern, testing, and serialization details. |
| [development/shift-ef-development.md](development/shift-ef-development.md) | Developer notes on the Shift.Ef code-generation library — generators, type mapping, and internals. |

## Testing

| Doc | What it covers |
|-----|----------------|
| [testing/testing-strategy.md](testing/testing-strategy.md) | Overall testing approach — frameworks, project layout, unit vs. integration, and snapshot testing. |
| [testing/database-model-builder.md](testing/database-model-builder.md) | The fluent `DatabaseModelBuilder` test helper for constructing model fixtures. |
| [testing/migration-plan-builder.md](testing/migration-plan-builder.md) | The fluent `MigrationPlanBuilder` test helper for constructing migration-plan fixtures. |
| [testing/docker-testing-setup.md](testing/docker-testing-setup.md) | The SQL Server Testcontainers setup used by the integration tests. |

## CI/CD

| Doc | What it covers |
|-----|----------------|
| [ci-cd/pipeline.md](ci-cd/pipeline.md) | Overview of the GitHub Actions workflows: PR build/test, release publishing, and pre-release publishing. |

---

> New to Shift? Start with the [DMD language reference](dsl/dmd-file-format.md) to learn the schema syntax, then the [CLI reference](cli/shift-cli-reference.md) to apply it.
