<div align="center">

# 🛠️ InToolz

**Bulk management for Microsoft Intune — stop clicking a million times.**

[![GitHub release](https://img.shields.io/github/v/release/Kvikku/IntuneTools?style=flat-square&color=blue)](https://github.com/Kvikku/IntuneTools/releases)
[![CI Build](https://github.com/Kvikku/IntuneTools/actions/workflows/ci.yml/badge.svg)](https://github.com/Kvikku/IntuneTools/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE.txt)
[![Microsoft Store](https://img.shields.io/badge/Microsoft_Store-Available-blue?style=flat-square&logo=microsoft)](https://apps.microsoft.com/detail/9phqrcx3gkxd)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![WinUI 3](https://img.shields.io/badge/WinUI-3-blue?style=flat-square&logo=windows)](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
[![GitHub stars](https://img.shields.io/github/stars/Kvikku/IntuneTools?style=flat-square)](https://github.com/Kvikku/IntuneTools/stargazers)
[![GitHub issues](https://img.shields.io/github/issues/Kvikku/IntuneTools?style=flat-square)](https://github.com/Kvikku/IntuneTools/issues)

Import, assign, rename, clean up, and export Intune policies and profiles in bulk — across tenants, in seconds.

[Get it from the Microsoft Store](https://apps.microsoft.com/detail/9phqrcx3gkxd) · [Wiki](https://github.com/Kvikku/IntuneTools/wiki) · [Releases](https://github.com/Kvikku/IntuneTools/releases)

</div>

---

## What can it do?

| | Feature | What it does |
|---|---------|-------------|
| 📥 | **Import** | Copy policies and profiles from one tenant to another via Microsoft Graph |
| 🎯 | **Assignment** | Assign Entra groups to policies and apps in bulk, with optional assignment filters |
| ✏️ | **Renaming** | Add prefixes/suffixes to display names and update descriptions across many items at once |
| 🧹 | **Cleanup** | Mass-delete Intune content with confirmation and progress tracking |
| 📄 | **JSON Export/Import** | Export Intune content to JSON files and reimport them — great for backup and version control |

## Supported content types

<table>
<tr>
<td>

- Settings Catalog policies
- Device Compliance policies
- Device Configuration (OMA-URI)
- Windows Quality Update policies & profiles
- Windows Feature Update policies
- Windows Driver Update policies

</td>
<td>

- Windows AutoPilot enrollment profiles
- PowerShell scripts
- Proactive Remediations
- macOS Shell scripts
- Apple BYOD enrollment profiles
- Assignment Filters

</td>
</tr>
</table>

## Getting started

1. **Install** — grab it from the [Microsoft Store](https://apps.microsoft.com/detail/9phqrcx3gkxd) or the [Releases page](https://github.com/Kvikku/IntuneTools/releases).
2. **Authenticate** — go to **Settings** and sign in to your **source** tenant (read-only) and **destination** tenant (read-write).
3. **Go** — pick an operation (Import, Assignment, Renaming, Cleanup, or JSON) and let InToolz do the heavy lifting.

For detailed walkthroughs, check out the [wiki](https://github.com/Kvikku/IntuneTools/wiki).

## Building from source

**Prerequisites:** .NET 8 SDK · Windows App SDK · Windows 10 SDK (build 22621)

```powershell
git clone https://github.com/Kvikku/IntuneTools.git
cd IntuneTools
dotnet build
```

## Releasing a new version

Version is managed through the `<Version>` property in `IntuneTools.csproj`. The `Package.appxmanifest` identity version is automatically kept in sync via an MSBuild target — no manual update needed.

**To ship a new release:**

1. Update the `<Version>` in `IntuneTools.csproj` (e.g. `1.4.0.0`).
2. Commit, push, and tag:
   ```powershell
   git add .
   git commit -m "Bump version to 1.4.0.0"
   git tag 1.4.0.0
   git push origin master --tags
   ```
3. The **Release** workflow builds the app for x64 and ARM64, then creates a **draft** GitHub Release with the zip files attached.
4. Open the draft release on GitHub, add release notes, and publish it.

> You can also trigger a release manually from the **Actions** tab using the _Release_ workflow dispatch.

## Roadmap

- [ ] Import applications
- [ ] Delete duplicate policies/apps
- [ ] Delete group assignments
- [ ] Bulk add objects to groups

Have an idea? [Open an issue](https://github.com/Kvikku/IntuneTools/issues) — community input shapes the roadmap.

## Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

## Acknowledgements

- [Emifo](https://github.com/emifo) — help with the user authentication part

## License

MIT — see [LICENSE.txt](LICENSE.txt) for details.

---

> ⚠️ **Heads up:** This application is a work in progress. Errors, crashes, and unexpected behaviour can occur. Use at your own risk and always test in a non-production environment first.
