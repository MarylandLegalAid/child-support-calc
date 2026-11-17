# Licensing

**Decision (2026-11-17): the project is released under the GNU Affero General Public License, version 3 or
(at your option) any later version (`AGPL-3.0-or-later`), and uses current PDFsharp (6.5.0+) under its AGPL licence.**
`LICENSE` holds the AGPL-3.0 text from gnu.org.

## Why this was a decision

Checked 2026-11-17 against the licence expression in each package's nuspec on nuget.org (PROVENANCE #8):

| PDFsharp version | Licence |
|---|---|
| 6.2.2 – 6.2.4 | MIT |
| 6.5.0 (published 2026-10-14) and 7.0 previews | AGPL-3.0-only (or an empira commercial licence) |

The options were: stay on 6.2.4 (MIT, no further PDFsharp fixes) with an MIT or Apache-2.0 project, or move to 6.5+
with an AGPL project. The maintainers chose the AGPL route.

## Consequences

- PDFsharp's exact expression is `AGPL-3.0-only`. This project's `-or-later` grant is compatible, but while it uses
  PDFsharp 6.5+ the combined program is in practice distributed under AGPL-3.0. To make the project `AGPL-3.0-only`
  instead, change the wording in `NOTICE` and `README.md`; the `LICENSE` text is the same.
- From 6.5.0, PDFsharp requires the application to select its licence in code. `WindowsFontResolver.EnsureInstalled()`
  calls `PdfSharpLicense.SetAgplLicense()` before any PDF object is created.
- Anyone who distributes a modified version (including offering it over a network) must make its source available under
  the AGPL.
- PDFsharp 6.5.0 still depends on `System.Security.Cryptography.Pkcs` 8.0.1 (known advisories); the project lifts it to
  10.0.15.

## Other dependencies (for `NOTICE`)

| Component | Licence | Used in |
|---|---|---|
| PDFsharp 6.5.0 (empira Software GmbH) | AGPL-3.0-only | Core (PDF) |
| System.Security.Cryptography.Pkcs (Microsoft) | MIT | Core (transitive, pinned) |
| Windows App SDK 2.6.5 (Microsoft) | Microsoft Software License Terms (redistributable, see package `license.txt`) | App |
| Microsoft.Windows.SDK.BuildTools (Microsoft) | Microsoft Software License Terms | App (build only) |
| xUnit.net v3 4.0.3 | Apache-2.0 | Tests only |
| PdfPig 0.1.16 | Apache-2.0 | Tests only |
| Microsoft.NET.Test.Sdk | MIT | Tests only |

## Not covered by the code licence

- Maryland Legal Aid's name, logos, colours and icon: not in this repository; the official build supplies them from a
  private brand folder (`branding/README.md`, `TRADEMARKS.md`).
- Statute text and the schedule: public law (Md. Code, Family Law §12-204(e)); cited in `Schedule/schedule.txt`.
- Official court forms: text captions in `docs/sources/` are from the Maryland Judiciary for reference.
