# Branding

The app's branding — logo, icon, colours, company name and worksheet footer — comes from a **brand folder**. The
repository contains only neutral placeholders (`default/`), so a fresh clone builds and runs. The official build
supplies its own brand folder from outside the repository.

## Which folder the build uses

`Directory.Build.props` picks the first of:

1. `-p:BrandDir=<folder>` on the command line (for a release pipeline that fetches the brand files from a private
   location);
2. `branding/local/` if it exists — **git-ignored**, so brand files placed there are never committed;
3. `branding/default/` — the placeholders in this repository.

The build prints which one it used. With the placeholders it says
`building with PLACEHOLDER branding`, so an unbranded build can't be shipped by mistake.

## What a brand folder contains

Every brand folder has the same five files:

| File | Used for |
|---|---|
| `BrandResources.xaml` | Colours and the logo's accessible name. Copy `default/BrandResources.xaml` and change the values; keep every key. `Themes/Brand.xaml` builds the app's brushes from these and maps them to system colours in high-contrast mode |
| `BrandLogo.png` | Title-bar logo, shown 28 px high on `BrandPrimaryColor`. Use a light (usually white) logo on a transparent background, at least 3× that height |
| `AppIcon.ico` | Window, taskbar and executable icon. `tools/branding/make-app-icon.ps1` draws one from your colours and, optionally, your logomark |
| `brand.props` | MSBuild properties: `BrandCompany` (shown in the executable's properties and as the installer's publisher) |
| `MdChildSupport.settings.json` | `footerLine`: the line printed in the worksheet PDF footer, e.g. `"Prepared by …"`. Can also be edited after deployment, next to the executable |

Anything else in the folder (style guides, source logos, scripts) is ignored by the build.

## Making your own

```bash
cp -r branding/default branding/local
```

Then edit the five files. To draw the icon:

```bash
pwsh tools/branding/make-app-icon.ps1 -OutPath branding/local/AppIcon.ico -LogomarkPath my-logomark.png -DeepColor "#123456" -MidColor "#234567" -LightColor "#DDEEFF" -AccentColor "#FFCC00" -PreviewPath icon-preview.png
```

`tools/branding/make-placeholder-logo.ps1` regenerates the placeholder logo.
