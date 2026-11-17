# Deployment

The app is a WinUI 3 desktop app, **unpackaged and fully self-contained** (.NET 10 runtime and Windows App SDK
runtime inside the folder). It runs on Windows 10 1809+ and Windows 11, x64 or ARM64, without installing anything else.

## Build the publish folder

```bash
dotnet publish src/MdChildSupport.App -c Release -r win-x64 -p:Platform=x64 -o publish/win-x64
```

```bash
dotnet publish src/MdChildSupport.App -c Release -r win-arm64 -p:Platform=ARM64 -o publish/win-arm64
```

**Branding:** before publishing the official build, put Maryland Legal Aid's brand folder at `branding/local/` or add
`-p:BrandDir=<path to the brand folder>` to the commands above. The build prints which branding it used; a build that
says `PLACEHOLDER branding` is not the official app (`branding/README.md`).

The folder contains `MdChildSupport.exe` and everything it needs. `MdChildSupport.settings.json` holds the configurable
worksheet footer line (`"footerLine"`), so each office can set its own without rebuilding.

## Route 1 — Intune Win32 app (install script)

1. Publish (above), then copy `deploy/install.ps1` and `deploy/uninstall.ps1` into the publish folder.
2. Wrap it with the Microsoft Win32 Content Prep Tool:
   `IntuneWinAppUtil.exe -c publish\win-x64 -s install.ps1 -o out`
3. In Intune, add a **Windows app (Win32)** with `out\install.intunewin`:
   - Install command: `powershell.exe -ExecutionPolicy Bypass -File .\install.ps1`
   - Uninstall command: `powershell.exe -ExecutionPolicy Bypass -File "%ProgramFiles%\Maryland Child Support Calculator\uninstall.ps1"`
   - Install behaviour: **System**. Requirements: Windows 10 1809+, architecture x64 (publish and upload `win-arm64`
     as a separate app for ARM64 devices).
   - Detection rule: file `%ProgramFiles%\Maryland Child Support Calculator\MdChildSupport.exe` exists (optionally
     "version greater than or equal to" the release's file version).

`install.ps1` copies the files to `%ProgramFiles%\Maryland Child Support Calculator`, adds an all-users Start menu
shortcut, registers the **`.mdcase`** file type (`HKLM\SOFTWARE\Classes`, ProgID `MdChildSupport.Case`, open command
`"MdChildSupport.exe" "%1"`), and adds an Apps & features entry. The app opens a path passed on its command line.

An MSI built from the same folder (WiX, Advanced Installer, etc.) is an equivalent alternative; it needs the same
four pieces: files, shortcut, file-type registration, uninstall entry.

## Route 2 — MSIX signed with the firm's certificate

MSIX gives clean install/uninstall and automatic updates through Intune's line-of-business app type.

1. Switch the app project to packaged for the MSIX build only:
   `dotnet publish src/MdChildSupport.App -c Release -r win-x64 -p:Platform=x64 -p:WindowsPackageType=MSIX -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false`
   This needs a `Package.appxmanifest` in the project (add it when this route is chosen). In it:
   - `Identity Name="MarylandLegalAid.ChildSupportCalculator" Publisher="CN=…"` — the Publisher must equal the
     subject of the firm's code-signing certificate.
   - `Application Executable="MdChildSupport.exe" EntryPoint="Windows.FullTrustApplication"`.
   - Register the file type:
     ```xml
     <Extensions>
       <uap:Extension Category="windows.fileTypeAssociation">
         <uap:FileTypeAssociation Name="mdcase">
           <uap:DisplayName>Child support case</uap:DisplayName>
           <uap:SupportedFileTypes><uap:FileType>.mdcase</uap:FileType></uap:SupportedFileTypes>
         </uap:FileTypeAssociation>
       </uap:Extension>
     </Extensions>
     ```
     The packaged app receives the file through the command line in the same way, so no code change is needed.
2. Sign: `signtool sign /fd SHA256 /a /f firm-codesign.pfx /p <password> /tr http://timestamp.digicert.com /td SHA256 MdChildSupport.msix`
   (or sign through Azure Trusted Signing / the firm's HSM).
3. Devices must trust the certificate (it is if issued by a public CA; for an internal CA, deploy the root/intermediate
   via an Intune trusted-certificate profile).
4. Upload the `.msix` to Intune as a **Line-of-business app**.

## Updating

Bump `<Version>` in `Directory.Build.props`, publish, and redeploy. Case files carry their own `schemaVersion`
(docs/file-format.md), so older files keep opening.
