# Shift.Dbml — DBML diagram exporter

`Shift.Dbml` turns a loaded `DatabaseModel` into a single [DBML](https://dbml.dbdiagram.io) document
you can paste into [dbdiagram.io](https://dbdiagram.io) to get an entity-relationship diagram.

It is the first Shift **plugin**: Shift core parses and preserves plugin attributes without knowing
what they mean, and this project is the only place in the repository that knows the `erd`
vocabulary.

## Usage

```bash
shift dbml <path> [path...] <output-path>
```

- **`<path>`** — a directory of `.dmd`/`.dmdx` files; repeatable.
- **`<output-path>`** — ends in `.dbml` (case-insensitive) to name the file, otherwise it is treated
  as a directory and the file is written as `<dir>/model.dbml`.

```bash
shift dbml ./Models ./Diagrams                  # writes ./Diagrams/model.dbml
shift dbml ./Models ./Diagrams/schema.dbml      # writes that exact file
```

The output file name is a compile-time constant. Nothing derived from a table name, a column name or
an attribute value ever reaches `Path.Combine`, and the resolved path is asserted to sit inside the
output directory before anything is written.

## Attributes

Run `shift attributes dbml` to list these from the CLI.

The exporter claims the **`erd` namespace**, declared once as `DbmlErdAttributes.Namespace` and
surfaced through `IShiftPlugin.AttributeNamespace`. Shift hands it only the attributes in that
namespace, with the prefix already stripped, via `attributes.InNamespace(...)` — so the names in
`DbmlErdAttributes` are bare local names (`hide`, `group`, `note`, `color`) and no lookup site in the
exporter mentions the prefix. Attributes in any other namespace are never delivered here; they stay
on the model untouched so a round trip preserves them.

| Attribute | Kind | Level | DBML output |
|-----------|------|-------|-------------|
| `@erd:hide` | flag | model + field | Omits the `Table` (with every `Ref` touching it and its group membership), or omits the column (and refs using it) |
| `@erd:group <name>` | valued | model | Membership in a `TableGroup`. Ignored on a field, because DBML has no column groups — it is still parsed and round-tripped |
| `@erd:note <text>` | valued | model + field | `Note: '<text>'` in the table block, or `[note: '<text>']` on the column |
| `@erd:color <hex>` | valued | model | `Table X [headercolor: #rrggbb]`. Write the hex without a leading `#` (`3498DB`, `38D`) — `#` is not a permitted attribute-value character; the exporter adds it. Anything that is not 3 or 6 hex digits fails |

```dmd
model Invoice {
  ustring(50) Reference
  decimal(19,4) Total
  ustring(64) InternalKey @erd:hide
  model User? as CreatedBy @erd:note 'Who raised it'

  @erd:group 'Billing Ops'
  @erd:note 'One customer invoice'
  @erd:color 3498DB
}
```

## What gets emitted

**Tables** — one `Table` block per visible table, in `Name` order (matching `ModelExporter`).

**Columns** — in declaration order, with these settings:

| Model state | DBML setting |
|-------------|--------------|
| `IsPrimaryKey` | `pk` |
| `IsPrimaryKey` + `IsIdentity` | `pk, increment` |
| `IsNullable == false` | `not null` (DBML columns are nullable by default, so only this direction needs saying) |
| Single-field unique index | `unique` |
| `@erd:note` | `note: '...'` |

**Types** — `FieldModel.Type` is already the normalised SQL type code with precision and scale kept
separately, and DBML passes types through verbatim, so the column type is the SQL Server spelling:
`int`, `nvarchar(256)`, `varchar(max)`, `decimal(10,2)`, `money`, `uniqueidentifier`. A type Shift
does not model is emitted as written (double-quoted if it contains whitespace) and logged at Debug —
never dropped, because a missing column is a silently wrong diagram.

**Indexes** — a single-field unique index becomes `unique` on the column. Everything else becomes an
`indexes { ... }` block, with `[unique]` where applicable. Index field names are resolved through
`IndexFieldResolver`, so an index naming a model (`index (Client)`) resolves to its FK column
(`ClientID`). An index naming a hidden column is dropped, since it would be a DBML parse error.

**Relationships** — one standalone short-form `Ref:` per `ForeignKeyModel`, emitted after every table
because a `Ref` naming an undeclared table is a parse error. DBML treats the *second* column as the
foreign key, so refs are always oriented target-first:

```text
Ref: User.UserID < Task.OwnerUserID    // OneToMany
Ref: User.UserID - Profile.UserID      // OneToOne
```

The optional-side `?` marker is not emitted; `null`/`not null` already conveys it.

**Groups** — tables are bucketed by `@erd:group` value, compared case-insensitively. Hidden tables are
excluded, empty groups are dropped, and groups are emitted in sorted order after all tables. A table
with no `@erd:group` belongs to no group. A group name that is not a valid DBML identifier is
slugified (runs of non-`[A-Za-z0-9_]` become `_`) with the original kept in a note:

```text
TableGroup Billing_Ops [note: 'Billing Ops'] {
  Invoice
  Payment
}
```

## Design notes

**One resolver per concern.** Hiding goes through `IsHidden`, grouping through `ResolveGroup`. If the
group name should one day fall back to the `.dmd` folder structure, `ResolveGroup` is the single seam
where that fallback is consulted — and only when no explicit `@erd:group` is present, so an explicit
attribute always wins.

**Refs are filtered, not repaired.** A `Ref` pointing at a hidden table, a hidden FK column or a
hidden target column is dropped rather than emitted, because a dangling `Ref` makes the whole document
fail to parse.

**Escaping is not optional.** Attribute values are validated when they are parsed, but table and
column names are not validated anywhere in Shift today and they reach this output too. Names that are
not valid DBML identifiers are double-quoted, and a name that cannot be represented at all is
rejected. Note text has `\` and `'` escaped the way DBML expects (`\'`).

## Example output

```text
// Generated by Shift. Paste into https://dbdiagram.io to view the diagram.

Table Invoice {
  InvoiceID int [pk, increment, not null]
  UserID int [not null]
  Reference varchar(max) [not null]
}

Table User [headercolor: #3498DB] {
  UserID int [pk, increment, not null]
  Email nvarchar(256) [not null, unique]
  DisplayName nvarchar(100)

  Note: 'Application user'
}

Ref: User.UserID < Invoice.UserID

TableGroup Billing_Ops [note: 'Billing Ops'] {
  Invoice
}
```

## Not implemented

Deliberately left for later: `@erd:alias`, group-level colour and notes, inactive refs
(`@erd:ref-inactive`), `@erd:schema`, sticky notes and `DiagramView`. DBML `default:` is not emitted
either — `FieldModel` has no default-value concept, so there is nothing to source and inventing one
would put a fiction in the diagram.
