# Sources

The outside sources used to build the app, and what each was used for. The calculation comes from the statute and the
official worksheets; judgement calls are in `DECISIONS.md`.

| # | Date | Source | What was used |
|---|---|---|---|
| 1 | 2026-11-17 | Maryland Legal Aid: project requirements and design materials | Requirements; statute text §§12-101, 12-102, 12-201 – 12-204 (mgaleg.maryland.gov, downloaded 2026-11-16); the app's XAML layout, styles and NumberBox helpers; design screenshots as the visual target; MLA brand assets (kept in the private brand folder) |
| 2 | 2026-11-17 | mdcourts.gov court form search, `https://www.mdcourts.gov/courtforms` (search "worksheet") | Listing shows CC-DR-034 and CC-DR-035 "Last Revised 07/2026" (English and bilingual Chinese editions). This is the revision in effect on the date of retrieval |
| 3 | 2026-11-17 | `https://www.mdcourts.gov/sites/default/files/court-forms/family/forms/ccdr034.pdf/ccdr034.pdf` | CC-DR-034 Worksheet A (Rev. 07/2026): line numbers, captions, line order, arithmetic instructions, Comment (2) on a negative line 9. Caption text saved in `docs/sources/ccdr034-rev-07-2026.txt` |
| 4 | 2026-11-17 | `https://www.mdcourts.gov/sites/default/files/court-forms/family/forms/ccdr035.pdf/ccdr035.pdf` | CC-DR-035 Worksheet B and Worksheet C (Rev. 07/2026): as above. Saved in `docs/sources/ccdr035-rev-07-2026.txt` |
| 5 | 2026-11-17 | `https://mgaleg.maryland.gov/mgawebsite/Legislation/Details/hb0275?ys=2025RS` | Bill page: HB 275 (2025) became Chapter 532; links to chapter text and fiscal note |
| 6 | 2026-11-17 | `https://mgaleg.maryland.gov/2025RS/Chapters_noln/CH_532_hb0275t.pdf` | Chaptered text of HB 275 (Ch. 532, 2025 Laws): shows the pre-October-2025 wording of §12-201(c), §12-202(a)(2)(iii)–(iv) and §12-204(a)(2), and the October 1, 2025 effective date. Saved in `docs/sources/` |
| 7 | 2026-11-17 | `https://mgaleg.maryland.gov/2025RS/fnotes/bil_0005/hb0275.pdf` | Fiscal and policy note (Third Reader): restates the §12-201(c)(2) formula. Used for D13. Saved in `docs/sources/` |
| 8 | 2026-11-17 | NuGet: `api.nuget.org` nuspec metadata for PDFsharp 6.2.2 – 7.0.0-preview-2 | Licence expressions: 6.2.x = MIT; 6.5.0 and 7.0 previews = AGPL-3.0-only. See `docs/licensing.md` |
| 9 | 2026-11-17 | NuGet metadata for Microsoft.WindowsAppSDK 2.6.5, PdfPig 0.1.16, xunit.v3 4.0.3, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk | Latest stable versions and licences (Windows App SDK: MS licence file; PdfPig, xUnit: Apache-2.0; Test SDK: MIT) |
| 10 | 2026-11-17 | Microsoft Learn: .NET and WinUI 3 documentation | WinUI 3 windowing, title bar, file pickers, ContentDialog, unpackaged self-contained deployment |
| 11 | 2026-11-17 | `cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174` | Tool only: used in a browser to extract text from the two court-form PDFs (mdcourts.gov refused direct command-line download) |
| 12 | 2026-11-17 | PyPI: PyMuPDF (installed in a local scratch folder) | Tool only: rendered the app's own worksheet PDFs to images for visual review. Not a dependency |
| 13 | 2026-11-17 | NuGet: Microsoft.WindowsAppSDK.WinUI / .Runtime 2.6.5 dependency metadata | Chose the WinUI component packages instead of the meta-package to keep AI/ML runtimes out of the app |
| 14 | 2026-11-17 | `https://docs.pdfsharp.net/General/History/README-v6.5.html` (empira) | From 6.5.0 an application must declare its PDFsharp licence; `PdfSharpLicense.SetAgplLicense()` selects the AGPL |
| 15 | 2026-11-17 | `https://www.gnu.org/licenses/agpl-3.0.txt` | Text of `LICENSE` |

## Gaps

- **CC-DR-034/035 Rev. 10/2025.** A 10/2025 revision also exists. mdcourts.gov now lists only
  07/2026, and the Internet Archive was offline on 2026-11-17, so the 10/2025 revision was not obtained and the
  differences could not be listed. The engine follows Rev. 07/2026, which already contains line 1d (multifamily
  adjustment). For the July 2022 guidelines the app prints line 1d as "not applicable". If the 10/2025 text
  is needed, add it to `docs/sources/` and update this entry.
