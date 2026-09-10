# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Shift carries no version
number in the repository: a release *is* the `vX.Y.Z` git tag, which is what publishes the
`Compile.Shift` package to nuget.org. See
[docs/ci-cd/pipeline.md](docs/ci-cd/pipeline.md#cutting-a-release) for the procedure.

## [0.0.18] - 2026-09-10

### Added

- Integer columns (`tinyint`, `smallint`, `int`, `bigint`) can now migrate to variable-width string
  columns (`varchar`, `nvarchar`). Fixed-width targets (`char`, `nchar`) are refused, because SQL
  Server right-pads the converted value and so rewrites the stored data rather than just its type.
- `varchar` columns can migrate to `nvarchar` (the `astring` to `ustring` direction). The reverse is
  still refused: narrowing `nvarchar` to `varchar` silently replaces characters outside the target
  collation's code page.
- Skips, refusals and failures are reported as typed diagnostics (`MigrationDiagnostic`, with a
  `MigrationDiagnosticKind` of `UnsupportedTypeChange`, `TargetTooNarrow`, `DataLossRisk` or
  `BlockedByDependency`) instead of only as log prose.

### Changed

- A base-type change that Shift still does not support now warns instead of being silently skipped.
- Apply reports what was actually applied rather than what was planned. `Apply completed` is no
  longer logged when every step was skipped; a run that changed nothing now says so.
- Dependent objects (indexes, foreign keys, default and check constraints, computed columns and the
  IDENTITY property) are checked before an `ALTER COLUMN` is attempted, so the conflict is named up
  front instead of surfacing as a database error.

### Fixed

- Comment lines in `.dmd`/`.dmdx` files are no longer parsed as fields. A `//` comment inside a
  mixin previously became a bogus column and produced invalid `ALTER TABLE` SQL.
- `decimal` scale changes are now detected, so a change to the scale of a `decimal(p,s)` column
  migrates instead of being ignored.
- The EF map generator emitted `nvarchar(-1)` instead of `nvarchar(max)`.
- The CLI `ef` commands failed at runtime because the code generator was not registered for
  dependency injection.
- The parser produced the table name `Order guid` for `model Order guid with Mixin`.
- Rendering a table with no fields threw instead of returning a string.
- Line endings are forced to LF, fixing cross-platform build failures.
- NuGet dependencies refreshed with patch and minor bumps; the `net9.0` target is unchanged.
- Documentation moved from the GitHub wiki into `docs/`, and test coverage was raised to 99.5% with
  a 99% line-coverage gate in CI.

### Upgrade notes

**`bigint` columns will no longer migrate to `astring(19)`.** The width guard uses the
sign-inclusive maximum width of the source type, and a `bigint` renders as up to 20 characters
(`-9223372036854775808`). A 19-character target is therefore refused as too narrow, even though
every positive `bigint` fits in 19. Widen the target to `astring(20)` to keep such a column
migrating.

**Any model that has been quietly disagreeing with the database on an integer-versus-string column
will start migrating on the next `apply`.** These conversions were previously skipped; now they are
applied. There is no opt-in flag, and integer-to-string is irreversible. Review a plan against
production data before applying it.

`IShift.ApplyToSqlAsync` and the migration runner's `Run()` now return a result object
(`MigrationRunResult`) instead of `Task` and `List<(MigrationStep, Exception)>`. This is source
compatible for callers that simply await or ignore the return value, but it is a binary-breaking
change for anything compiled against the old signatures.

---

Entries below this line predate the changelog and are reconstructed from commit history. They
summarise each tag's commit subject rather than a full account of the release.

## [0.0.17] - 2026-04-15

- Prevent spurious `ALTER COLUMN` on matching types, and add a command timeout.

## [0.0.16] - 2026-03-09

- Added support for schema overriding.

## [0.0.15] - 2026-01-16

- Schema changes log as warnings.

## [0.0.14] - 2026-01-12

- Fix indexes not being created on first run for new tables.
