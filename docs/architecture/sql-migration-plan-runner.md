# SqlMigrationPlanRunner Architecture

## Overview

`SqlMigrationPlanRunner` executes the migration plans produced by `MigrationPlanner`. It
translates each high-level `MigrationStep` into SQL Server statements and runs them against a
database, returning the steps that failed.

## Responsibilities

- Generate SQL for each supported migration action.
- Execute statements against SQL Server and report failures.
- Guard against data loss at runtime, skipping unsafe alterations.
- Log per-step progress and the SQL executed.

## Component

```csharp
public class SqlMigrationPlanRunner
{
    public required ILogger Logger { private get; init; }

    public SqlMigrationPlanRunner(string connectionString, MigrationPlan plan, string schema = "dbo");

    public List<(MigrationStep Step, Exception Exception)> Run();
}
```

The `schema` parameter (default `dbo`) qualifies every generated object name, e.g.
`[dbo].[TableName]`.

## Execution flow

`Run` opens a single connection, iterates the plan's steps ordered by `MigrationAction`, and
for each step generates and executes the appropriate SQL. Failures are captured and returned
rather than thrown, so a failed step does not stop the ones that follow.

## Supported migration actions

### CreateTable

```sql
CREATE TABLE [dbo].[TableName] (
  [FieldName] FieldType [IDENTITY(1,1)] [NULL|NOT NULL],
  CONSTRAINT [PK_TableName] PRIMARY KEY ([PrimaryKeyField])
)
```

Handles identity columns, precision/scale, nullability, and the primary-key constraint.

### AddColumn

```sql
ALTER TABLE [dbo].[TableName] ADD [FieldName] FieldType [NULL|NOT NULL] [DEFAULT_VALUE]
```

A default is emitted only for **non-nullable** columns, so existing rows receive a valid
value. The default is type-specific:

| Type group | Default |
|------------|---------|
| Numeric (`int`, `bigint`, `smallint`, `tinyint`, `decimal`, `numeric`, `float`, `real`) | `0`, or `1` when the field name ends in `ID` |
| Boolean (`bit`) | `0` |
| Date (`datetime`, `smalldatetime`, `date`, `datetime2`, `datetimeoffset`) | `GETDATE()` |
| String (`char`, `nchar`, `varchar`, `nvarchar`, `text`, `ntext`) | `''` |
| GUID (`uniqueidentifier`) | `NEWID()` |

Nullable columns are added without a default. If a default constraint was nonetheless created,
a follow-up statement looks it up in `sys.default_constraints` and drops it:

```sql
DECLARE @dfname nvarchar(128);
SELECT @dfname = df.name
FROM sys.default_constraints df
INNER JOIN sys.columns c ON df.parent_object_id = c.object_id AND df.parent_column_id = c.column_id
WHERE df.parent_object_id = OBJECT_ID('dbo.TableName') AND c.name = 'FieldName';
IF @dfname IS NOT NULL EXEC('ALTER TABLE [dbo].[TableName] DROP CONSTRAINT [' + @dfname + ']');
```

### AlterColumn

The alter is wrapped in an `IF EXISTS` guard so it runs only when the live column actually
differs (data type, size/precision, scale, or nullability):

```sql
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'TableName'
      AND COLUMN_NAME = 'FieldName'
      AND (
          DATA_TYPE <> 'fieldtype'
          OR COALESCE(CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, 0) <> <precision>
          OR COALESCE(NUMERIC_SCALE, 0) <> <scale>
          OR IS_NULLABLE <> 'YES'|'NO'
      )
)
BEGIN
    ALTER TABLE [dbo].[TableName] ALTER COLUMN [FieldName] FieldType [NULL|NOT NULL]
END
```

The runner does not restrict alterations to widening. Instead, before generating the SQL it
calls `IsAlterColumnPotentiallyUnsafe`, which probes the live data (with `WITH (READPAST)`) and
**skips the alter (logging a warning)** when it would lose data:

- **String/binary** (`varchar`, `nvarchar`, `char`, `nchar`, `binary`, `varbinary`): when the
  new size is smaller and an existing value exceeds it. Lengths use `LEN` for `char`/`nchar`
  and `DATALENGTH` otherwise (Unicode counts two bytes per character). Resizing to `MAX`
  (`-1`) is always safe.
- **Integer becoming a string** (`int` to `varchar(n)` and the rest of the
  `SqlTypeConversion` allow-list): the probe reads the column's current `DATA_TYPE` from
  `INFORMATION_SCHEMA.COLUMNS` first, and when the source is an integer it measures the
  *rendered character length* instead — `LEN(CONVERT(varchar(50), [col])) > n`. `DATALENGTH`
  would be wrong here: it reports the integer's storage size (always 4 bytes for an `int`),
  so it would wave through `int` to `varchar(2)`. A character count is the correct limit for
  both `varchar` (where `CHARACTER_MAXIMUM_LENGTH` counts bytes) and `nvarchar` (where it
  counts characters), because a rendered integer is ASCII.
- **Decimal/numeric**: when an existing value would not round-trip through
  `TRY_CONVERT(decimal(p,s), ...)` (truncation, rounding, or conversion failure).

### AddForeignKey

Creates the constraint and a supporting non-clustered index on the FK column:

```sql
ALTER TABLE [dbo].[TableName] WITH NOCHECK ADD CONSTRAINT [FK_TableName_ColumnName]
    FOREIGN KEY ([ColumnName]) REFERENCES [dbo].[TargetTable]([TargetColumnName])

ALTER TABLE [dbo].[TableName] CHECK CONSTRAINT [FK_TableName_ColumnName]

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TableName_ColumnName' AND object_id = OBJECT_ID('dbo.TableName'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TableName_ColumnName] ON [dbo].[TableName]([ColumnName])
END
```

`WITH NOCHECK` adds the constraint without validating existing rows, then a second statement
enables checking. FK columns are common JOIN/WHERE targets, so an index is created on each
automatically (guarded by `IF NOT EXISTS`).

### AddIndex

```sql
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TableName_Field1_Field2...' AND object_id = OBJECT_ID('dbo.TableName'))
BEGIN
    CREATE [UNIQUE ][NONCLUSTERED|CLUSTERED] INDEX [IX_TableName_Field1_Field2...] ON [dbo].[TableName]([Field1], [Field2], ...)
END
```

Supports single- and multi-column, unique and non-unique indexes. The clustering keyword
comes from `IndexModel.Kind` (`NonClustered` default, or `Clustered`); other kinds (columnstore,
fulltext) throw `NotImplementedException`. Model names in the field list are resolved to FK
column names (see below).

**Index name length.** Names are produced by `IndexNameHelper.GenerateIndexName`, which
enforces SQL Server's 128-character identifier limit. When the natural name (`IX`/`AK` + table
+ fields) would exceed it, the name is trimmed and an 8-character lowercase SHA-256 hash of the
full name is appended (`_xxxxxxxx`) to keep it unique.

## Index field resolution

`IndexFieldResolver` maps model names used in DMD index definitions to their foreign-key
column names, so DMD files can reference the related model rather than the technical column.
Resolution is built from the table's foreign keys (`TargetTable` → `ColumnName`):

| DMD index | Foreign key | Resolved columns |
|-----------|-------------|------------------|
| `index (Email, ClientStatus)` | `ClientStatusID` → `ClientStatus` | `[Email], [ClientStatusID]` |
| `index (User, OrderDate)` | `UserID` → `User` | `[UserID], [OrderDate]` |

- Matching is **case-insensitive**.
- Names that match no FK target table are used as-is, so regular and model names can be mixed.
- When multiple FKs reference the same target table, the last one wins (dictionary semantics).

## SQL generation reference

### Naming conventions

| Object | Pattern | Example |
|--------|---------|---------|
| Primary key | `PK_TableName` | `PK_User` |
| Foreign key | `FK_TableName_ColumnName` | `FK_Order_UserID` |
| Index | `IX_TableName_Field1_Field2` | `IX_User_Email_Username` |
| Alternate key | `AK_TableName_Field1_Field2` | `AK_User_Email` |

### Type rendering

```sql
nvarchar(256)   varchar(MAX)      -- strings (sized or MAX)
decimal(10,2)   numeric(18,4)     -- decimals (precision, scale)
binary(16)      varbinary(MAX)    -- binary (sized or MAX)
```

## Error handling and logging

Execution is **non-transactional**: each step runs independently and a failure does not roll
back earlier steps or stop later ones. `Run` returns the failed steps and their exceptions:

```csharp
List<(MigrationStep Step, Exception Exception)>
```

Typical exceptions are `SqlException` (constraint violations, syntax errors) and general
`Exception` (connection issues, timeouts). Logging levels:

- **Warning** — per-step progress (one line per step) and skipped data-loss alterations.
- **Debug** — the SQL being executed.
- **Error** — execution failures (`LogError(ex, ...)`).

Per-step progress is intentionally at Warning, not Information.

## Execution characteristics

- Steps run in `MigrationAction` order (tables before columns before indexes/foreign keys).
- A single connection is opened once and reused for the whole run, then disposed.
- Each command uses `CommandTimeout = 600` seconds to accommodate long-running schema changes
  on large tables.

## Limitations

- No rollback and no transactions — partial success is possible by design.
- No check constraints, computed columns, or data transformation.
- Schema changes are limited to table/column/index/foreign-key operations.

## Usage

```csharp
// Generate a plan and execute it
var plan = new MigrationPlanner().GeneratePlan(targetModel, actualModel);
var runner = new SqlMigrationPlanRunner(connectionString, plan) { Logger = logger };

var failures = runner.Run();
if (failures.Count > 0)
{
    foreach (var (step, ex) in failures)
        logger.LogError("Step {Action} on {Table} failed: {Error}", step.Action, step.TableName, ex.Message);
}
```

Most callers go through the `Shift` facade rather than constructing the runner directly:

```csharp
await shift.ApplyToSqlAsync(targetModel, connectionString, schema);
```

## Testing

The runner is covered by unit tests for SQL generation per action and by integration tests
that run against a real SQL Server (Testcontainers), asserting database state after a
migration and exercising failure and data-safety paths. See
[Testing Strategy](../testing/testing-strategy.md).
