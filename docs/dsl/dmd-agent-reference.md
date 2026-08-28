# DMD File Format — AI Agent Reference

This is a compact, authoritative reference for generating valid `.dmd` and `.dmdx` files. All syntax shown here is correct and complete.

---

## File Types

| Extension | Purpose |
|-----------|---------|
| `.dmd`    | Model (table) definitions |
| `.dmdx`   | Mixin (reusable field set) definitions |

---

## Core Syntax Templates

### Model (one per `.dmd` file)

```dmd
model <ModelName> [<pk-type>] [with <MixinName>] {
  <field-definitions>
  <relationship-definitions>
  <index-definitions>
  [<attributes>]
}
```

### Mixin (one or more per `.dmdx` file)

```dmdx
mixin <MixinName> [with <OtherMixin>] {
  <field-definitions>
  <relationship-definitions>
}
```

---

## Primary Key Options

Every model auto-generates `{ModelName}ID` as the primary key.

| Syntax              | Generated Column        | SQL Type                      |
|---------------------|-------------------------|-------------------------------|
| `model Foo {`       | `FooID int IDENTITY`    | `int IDENTITY(1,1) NOT NULL`  |
| `model Foo int {`   | `FooID int IDENTITY`    | `int IDENTITY(1,1) NOT NULL`  |
| `model Foo guid {`  | `FooID uniqueidentifier`| `uniqueidentifier NOT NULL`   |
| `model Foo long {`  | `FooID bigint IDENTITY` | `bigint IDENTITY(1,1) NOT NULL`|
| `@NoIdentity`       | removes IDENTITY        | `int NOT NULL` (no auto-inc)  |

---

## Field Definitions

### Syntax

```
<type>[?] <FieldName>
<type>(<precision>)[?] <FieldName>
<type>(<precision>, <scale>)[?] <FieldName>
```

- `?` suffix = `NULL` (omitting `?` = `NOT NULL`)

### Type Reference

#### Recommended Types

| DMD Type           | SQL Server Type        | Notes |
|--------------------|------------------------|-------|
| `bool`             | `bit`                  |       |
| `int`              | `int`                  |       |
| `long`             | `bigint`               |       |
| `float`            | `float`                |       |
| `guid`             | `uniqueidentifier`     |       |
| `datetime`         | `datetime`             |       |
| `decimal(p,s)`     | `decimal(p,s)`         | Always specify p and s for money/financials |
| `ustring`          | `nvarchar(255)`        | Unicode, default 255 |
| `ustring(n)`       | `nvarchar(n)`          | Unicode, explicit length |
| `ustring(max)`     | `nvarchar(max)`        | Unicode, unlimited |
| `uchar`            | `nchar(1)`             | Single Unicode char |
| `uchar(n)`         | `nchar(n)`             | Unicode fixed-length char |
| `astring`          | `varchar(255)`         | ASCII, default 255 |
| `astring(n)`       | `varchar(n)`           | ASCII, explicit length |
| `astring(max)`     | `varchar(max)`         | ASCII, unlimited |
| `achar`            | `char(1)`              | Single ASCII char |
| `achar(n)`         | `char(n)`              | ASCII fixed-length char |

#### Deprecated Types (avoid in new files)

| Deprecated DMD | Use Instead  |
|----------------|--------------|
| `string`       | `ustring`    |
| `string(n)`    | `ustring(n)` |
| `string(max)`  | `ustring(max)` |
| `char`         | `uchar`      |
| `char(n)`      | `uchar(n)`   |

### Field Examples

```dmd
ustring(100) Username         // nvarchar(100) NOT NULL
ustring? Email                // nvarchar(255) NULL
bool IsActive                 // bit NOT NULL
datetime? LastLoginDate       // datetime NULL
decimal(10,2) Price           // decimal(10,2) NOT NULL
guid ExternalRef              // uniqueidentifier NOT NULL
astring(50) Code              // varchar(50) NOT NULL
```

---

## Relationships (Foreign Keys)

### Syntax Patterns

```dmd
model <TargetModel>                          // FK, required, uses TargetModel name
model <TargetModel>?                         // FK, nullable
model <TargetModel> as <Alias>              // FK, required, custom column prefix
model <TargetModel>? as <Alias>             // FK, nullable, custom column prefix
!model <TargetModel>? as <Alias>            // optional FK (mixin use, nullable)
models <TargetModel>                        // documentation only — no SQL generated
```

### Generated Column Names

| DMD | Generated Column |
|-----|-----------------|
| `model Customer` | `CustomerID int NOT NULL` + FK constraint |
| `model Customer?` | `CustomerID int NULL` + FK constraint |
| `model User as AssignedUser` | `AssignedUserID int NOT NULL` + FK constraint |
| `model User? as CreatedBy` | `CreatedByID int NULL` + FK constraint |

### FK Constraint Naming

`FK_{TableName}_{ColumnName}` → e.g., `FK_Order_CustomerID`

### Multiple FKs to Same Table (require aliases)

```dmd
model Task {
  model User as AssignedUser
  model User as CreatedBy
  ustring(200) Title
}
```

---

## Indexes

### Syntax

```dmd
index (<Field1>[, <Field2>, ...]) [@unique]
key (<Field1>[, <Field2>, ...])
```

### Index Types

| Syntax | Prefix | Unique? | Use For |
|--------|--------|---------|---------|
| `index (Field)` | `IX_` | No | Query optimization |
| `index (Field) @unique` | `IX_` | Yes | Unique constraint (performance index) |
| `key (Field)` | `AK_` | Yes | Business/alternate keys |

### Index Naming

- `IX_{Table}_{Col1}_{Col2}` for `index`
- `AK_{Table}_{Col1}_{Col2}` for `key`

### Index Field Resolution

In index definitions, you may use the **model name** (e.g., `Customer`) instead of the FK column name (e.g., `CustomerID`) — Shift resolves it automatically.

```dmd
model Order {
  model Customer
  datetime OrderDate
  ustring(50) Status

  index (Customer, Status)      // resolved to [CustomerID], [Status]
  index (OrderDate)
}
```

> **Auto FK indexes**: Shift automatically creates non-clustered indexes on all FK columns. You do NOT need to manually define indexes for FK columns alone.

---

## Attributes

| Attribute    | Placement    | Effect |
|--------------|--------------|--------|
| `@NoIdentity`| inside model | Removes `IDENTITY` from integer PK |
| `@unique`    | on `index()` | Makes the index unique (`UNIQUE INDEX`) |

### Plugin attributes

Any other `@name` is a plugin attribute: Shift parses, validates and preserves it but does not
interpret it. Three placements:

```dmd
model Invoice {
  @erd-group 'Billing Ops'          // model level: its own line
  ustring(100) Email @erd-hide      // field level: trailing tokens, repeatable
  model User? as CreatedBy @erd-hide
}
```

```dmdx
mixin Auditable {
  @erd-group Audit                  // mixin level: inherited by every model using it
  datetime CreatedDateTime
}
```

- `@name` is a flag, `@name value` is valued, `@name 'value with spaces'` quotes a value containing
  spaces.
- Name: `^[A-Za-z][A-Za-z0-9_-]{0,63}$`. Value: letters, digits, spaces, `.`, `_`, `-` only (no
  quotes, brackets, braces, slashes, colons, `..`, `@`, `#` or `,`). Anything else fails the parse.
- Model wins over mixin on a same-name collision.
- Not stored in SQL, so `shift export` cannot emit them.
- `shift attributes` lists every attribute the installed plugins understand.

---

## Mixins

### Define in `.dmdx`

```dmdx
mixin Auditable {
  !model User? as CreatedBy
  !model User? as LastModifiedBy
  datetime CreatedDateTime
  datetime LastModifiedDateTime
  int LockNumber
}
```

### Apply with `with` keyword

```dmd
model Document with Auditable {
  ustring(200) Title
  ustring(max)? Content
}
```

### Mixin constraints

> A model applies **exactly one** mixin (`with <MixinName>`). Mixins **cannot** compose other mixins, and a model **cannot** apply multiple mixins. To share a field set, define a single self-contained mixin that includes every field.

---

## Column Ordering in Generated SQL

1. Primary key (`{Model}ID`)
2. Foreign key columns (in definition order)
3. Regular field columns (in definition order)

---

## Constraint Naming Reference

| Constraint    | Pattern                        | Example |
|---------------|--------------------------------|---------|
| Primary key   | `PK_{TableName}`               | `PK_Order` |
| Foreign key   | `FK_{TableName}_{ColumnName}`  | `FK_Order_CustomerID` |
| Index         | `IX_{TableName}_{Col1}_{Col2}` | `IX_Order_OrderDate` |
| Alternate key | `AK_{TableName}_{Col1}_{Col2}` | `AK_User_Email` |

---

## Complete Model Example

```dmd
model Order {
  model Customer
  model User as SalesRep
  decimal(10,2) Amount
  datetime OrderDate
  ustring(50) Status
  ustring(1000)? Notes
  index (OrderDate)
  index (Customer, Status)
  key (Customer, OrderDate)
}
```

**Generates:**

```sql
CREATE TABLE [Order] (
  [OrderID] int IDENTITY(1,1) NOT NULL,
  [CustomerID] int NOT NULL,
  [SalesRepID] int NOT NULL,
  [Amount] decimal(10,2) NOT NULL,
  [OrderDate] datetime NOT NULL,
  [Status] nvarchar(50) NOT NULL,
  [Notes] nvarchar(1000) NULL,
  CONSTRAINT [PK_Order] PRIMARY KEY ([OrderID]),
  CONSTRAINT [FK_Order_CustomerID] FOREIGN KEY ([CustomerID]) REFERENCES [Customer]([CustomerID]),
  CONSTRAINT [FK_Order_SalesRepID] FOREIGN KEY ([SalesRepID]) REFERENCES [User]([UserID])
)
CREATE INDEX [IX_Order_CustomerID] ON [dbo].[Order]([CustomerID])         -- auto FK index
CREATE INDEX [IX_Order_SalesRepID] ON [dbo].[Order]([SalesRepID])         -- auto FK index
CREATE INDEX [IX_Order_OrderDate] ON [dbo].[Order]([OrderDate])
CREATE INDEX [IX_Order_CustomerID_Status] ON [dbo].[Order]([CustomerID], [Status])
CREATE UNIQUE INDEX [AK_Order_CustomerID_OrderDate] ON [dbo].[Order]([CustomerID], [OrderDate])
```

---

## Quick Decision Guide

| Scenario | Syntax |
|----------|--------|
| Text field, Unicode | `ustring(n) FieldName` |
| Text field, ASCII/codes | `astring(n) FieldName` |
| Optional field | append `?` to type |
| Money/financial | `decimal(19,4)` or `decimal(10,2)` |
| Distributed system PK | `model Foo guid {` |
| Business unique key | `key (Field)` |
| Query performance index | `index (Field)` |
| Unique constraint index | `index (Field) @unique` |
| Reuse fields across models | mixin in `.dmdx`, apply with `with` |
| Multiple FKs to same table | `model User as Alias1` + `model User as Alias2` |
| FK without auto-increment | `@NoIdentity` inside model body |
