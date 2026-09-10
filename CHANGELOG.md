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
- Models, mixins and fields can carry plugin attributes: `@namespace:name` for a flag, and
  `@namespace:name value` or `@namespace:name 'value with spaces'` for one with a value. A model- or
  mixin-level attribute sits on its own line inside the block; a field-level one is a trailing token
  on the field declaration, including on `model`/`models` relationship lines. Shift parses,
  validates and preserves them, so order, duplicates and the flag-versus-value distinction all
  survive a round trip, but it does not interpret them; `@NoIdentity` stays the one attribute Shift
  reads itself.
- The namespace before the `:` addresses an attribute to a plugin, which is handed only its own
  namespace's attributes, with the prefix already stripped. A mixin's attributes are inherited by
  every model that uses it, and the model wins a same-name collision. An attribute in a namespace no
  installed plugin claims is preserved rather than rejected, so a file annotated for a plugin you do
  not have installed still parses and round trips. Attributes are never stored in SQL, so
  `shift export` from a live database cannot emit any.
- A DBML exporter, `Shift.Dbml`, and a `shift dbml <path> [path...] <output-path>` command that
  render `.dmd`/`.dmdx` files as a DBML document to paste into
  [dbdiagram.io](https://dbdiagram.io). An output path ending in `.dbml` names the file; anything
  else is treated as a directory and receives `model.dbml`. Tables, columns with their primary keys
  and unique indexes, `indexes { ... }` blocks and a `Ref:` for each foreign key are emitted. The
  exporter claims the `erd` namespace and understands `@erd:hide` (omit a table or a column),
  `@erd:group` (put a table in a `TableGroup`), `@erd:note` (a DBML note on a table or column) and
  `@erd:color` (a table header colour, as 3 or 6 hex digits written without the leading `#`).
- `shift attributes [plugin]` lists the attributes the installed plugins understand, grouped by
  namespace, with each attribute's scope, whether it is a flag or takes a value, and what the plugin
  does with it, so attribute names can be found without reading plugin source.
- Plugins share one contract, `IShiftPlugin`, declaring the plugin's name, its description, the
  attribute namespace it claims and the attributes it interprets. `Shift.Dbml` claims `erd`; the EF
  generator implements the contract too but claims no namespace and consumes no attributes.
- [`docs/proposals/dmd-ef-codegen.md`](docs/proposals/dmd-ef-codegen.md): a proposal for making
  DMD-driven EF code generation first-class per module and letting one module's generated
  `DbContext` derive from another's.
- [`docs/architecture/shift-dbml-exporter.md`](docs/architecture/shift-dbml-exporter.md): how
  `Shift.Dbml` renders a model as DBML and what each `erd` attribute does. The attribute syntax and
  its validation rules are in
  [`docs/dsl/dmd-file-format.md`](docs/dsl/dmd-file-format.md#plugin-attributes), and the two new
  commands in [`docs/cli/shift-cli-reference.md`](docs/cli/shift-cli-reference.md).

### Changed

- A base-type change that Shift still does not support now warns instead of being silently skipped.
- Apply reports what was actually applied rather than what was planned. `Apply completed` is no
  longer logged when every step was skipped; a run that changed nothing now says so.
- Dependent objects (indexes, foreign keys, default and check constraints, computed columns and the
  IDENTITY property) are checked before an `ALTER COLUMN` is attempted, so the conflict is named up
  front instead of surfacing as a database error.
- Testcontainers moved from 3.10.0 to 4.14.0 in the test project, which replaces the transitively
  referenced SSH.NET 2023.0.0 with 2026.0.0 and so clears GHSA-q939-rpr3-3284 (High). The advisory
  reached the repository only through the test project; the published `Compile.Shift` package and
  its dependencies are unchanged.
- Attributes are validated as they are parsed. A name may hold at most one `:`, each half must start
  with a letter and then use only letters, digits, `_` or `-`, and the whole spelling is capped at
  64 characters; a value must start with a letter or digit, may hold only letters, digits, spaces,
  `.`, `_` or `-`, and may not contain `..`. Anything else fails the parse, naming the offending
  line.
- The DMD exporter writes attributes back out, model-level ones on their own lines and field-level
  ones as trailing tokens, single-quoting a value only when it contains whitespace, so a parse and
  re-export round trip preserves them.

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

**`@` lines that Shift used to accept verbatim can now fail the parse.** A model-level attribute
line was previously stored exactly as written, whatever it said, and nothing checked it. It is now
validated, so spellings such as `@erd.hide`, `@my:sub:name`, `@1flag` or a bare `@` are rejected,
and a line carrying more than one value token (`@Foo bar baz`) is rejected as malformed. Anything
that satisfies the new name and value rules, `@NoIdentity` included, parses as before.

**A `@NoIdentity` line inside a `.dmdx` mixin now takes effect, and the name is matched
case-insensitively.** A mixin carried no attributes at all before, so such a line was silently
ignored; mixin attributes are now merged onto every model that uses the mixin. The model-level
lookup also became case-insensitive, so `@noidentity` is honoured where the old exact-case lookup
ignored it. Either change can turn IDENTITY off on a primary key that previously kept it.

`IShift.ApplyToSqlAsync` and the migration runner's `Run()` now return a result object
(`MigrationRunResult`) instead of `Task` and `List<(MigrationStep, Exception)>`. This is source
compatible for callers that simply await or ignore the return value, but it is a binary-breaking
change for anything compiled against the old signatures.

`TableModel.Attributes` changed from `Dictionary<string, bool>` to `List<AttributeModel>`, and
`IModel` now requires an `Attributes` property, so anything implementing `IModel` or reading
`Attributes` as a dictionary has to be updated; `IEfCodeGenerator` also extends the new
`IShiftPlugin`. Read attributes through the `HasAttribute` and `AttributeValue` extensions rather
than by key.

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
