# Case file format (`.mdcase`)

The app saves a case as **UTF-8 JSON without a byte-order mark**, extension **`.mdcase`**. Only what the user entered
is stored; computed results are never saved, so opening a file always recalculates under the current engine.
Implementation: `src/MdChildSupport.Core/CaseFile.cs`. Round-trip tests: `tests/MdChildSupport.Core.Tests/CaseFileTests.cs`.

## Envelope

```json
{
  "schemaVersion": 1,
  "application": "Maryland Child Support Calculator",
  "case": { ... }
}
```

- `schemaVersion` (integer, required). This version reads 1 and refuses anything newer with a clear message.
  Increase it only for changes an older reader would misread; adding an optional field does not need a new version.
- `application` (string, informational).

## `case`

Property names are camelCase; enums are camelCase strings. Missing properties take the defaults shown.

| Property | Type | Default | Meaning |
|---|---|---|---|
| `caseNumber`, `plaintiff`, `defendant`, `preparer` | string | `""` | Caption and "Prepared by" |
| `date` | string | today, `MM/dd/yyyy` | Date printed on the worksheet (free text) |
| `county` | string | `"Allegany County"` | One of the 24 names in `Counties.All`; unknown values fall back to the default |
| `court` | `"circuit"` \| `"district"` | `"circuit"` | |
| `useParentLabels` | bool | `false` | Labels only: "Parent 1/2" instead of "Mother/Father" |
| `guidelines` | `"october2025"` \| `"july2022"` | `"october2025"` | DECISIONS D15 |
| `custody` | `"shared"` \| `"soleWithParent1"` \| `"soleWithParent2"` | `"shared"` | |
| `children` | array of `{ "name", "birthDate" }` | 6 empty rows | Always normalised to exactly 6 rows; a row counts when `name` is not blank |
| `parent1`, `parent2` | object, below | zeros | Parent 1 = Mother, Parent 2 = Father |
| `alimonyToParent1` | number | 0 | Alimony awarded in this case; positive = received by Parent 1 |
| `parent1Overnights` | integer | 183 | As entered; the engine limits it to 92–273 and shows a caution if it had to |
| `comments` | string | `""` | Printed under "Comments or special adjustments" |
| `footnotes` | array of notes, below | `[]` | Notes attached to input fields |

### Parent object

All money is monthly whole dollars; `cashMedicalPercent` is a percent with one decimal place.

`income`, `multiFamily`, `existingSupport`, `alimonyPaid`, `childCare`, `healthInsurance`, `extraordinaryMedical`,
`cashMedicalPercent`, `additionalExpenses`.

### Footnote object

| Property | Type | Meaning |
|---|---|---|
| `field` | string | The input field: `parent1.<name>` / `parent2.<name>` with a parent-object property name, or `alimonyToParent1`, `parent1Overnights` |
| `text` | string | The note as last saved |
| `multiFamilyChildren` | integer, optional | Present when the multifamily calculator made the note; the printed text is regenerated from the current inputs using this number of additional children |

Any input field may carry a note; the worksheet prints a marker `[n]` next to that field's line and the note in Notes.

## Example

`samples/shared-oct2025.mdcase` and `samples/sole-jul2022-parent-labels.mdcase` (shared and sole custody examples).

## Out of scope

Importing or exporting other programs' case files is not supported.
