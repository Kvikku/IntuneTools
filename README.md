# InToolz

A bulk management toolset for Microsoft Intune — stop clicking a million times in the admin console.

> **Note:** This application is a work in progress. Errors, crashes, and unexpected behaviour can occur.

## Features

| Feature | Description |
|---------|-------------|
| **Import** | Bulk import policies and profiles from a source tenant to a destination tenant via Microsoft Graph |
| **Assignment** | Bulk assign Entra groups to policies and applications, with optional assignment filters |
| **Renaming** | Bulk rename display names (prefix/suffix) and update description fields |
| **Cleanup** | Bulk delete Intune content with confirmation and progress tracking |
| **JSON Export/Import** | Export Intune content to JSON files and reimport from JSON |

### Supported content types

- Settings Catalog policies
- Device Compliance policies
- Device Configuration (OMA-URI) policies
- Windows Quality Update policies & profiles
- Windows Feature Update policies
- Windows Driver Update policies
- Windows AutoPilot enrollment profiles
- PowerShell scripts
- Proactive Remediations
- macOS Shell scripts
- Apple BYOD enrollment profiles
- Assignment Filters

## Download

Available on the [Microsoft Store](https://apps.microsoft.com/detail/9phqrcx3gkxd?hl=neutral&gl=NO&ocid=pdpshare).

You can also grab a release from the [Releases page](https://github.com/Kvikku/IntuneTools/releases), or clone the repo and build locally.

## Getting started

1. Install and launch InToolz.
2. Go to **Settings** and authenticate to your **source** tenant (read-only) and **destination** tenant (read-write).
3. Navigate to the page for the operation you want to perform (Import, Assignment, Renaming, Cleanup, or JSON).

For detailed walkthroughs, check out [the wiki](https://github.com/Kvikku/IntuneTools/wiki).


## Building from source

**Prerequisites:** .NET 8 SDK, Windows App SDK, Windows 10 SDK (build 22621)

```powershell
git clone https://github.com/Kvikku/IntuneTools.git
cd IntuneTools
dotnet build
```

## Roadmap

Planned and community-requested features:

- [ ] Import applications
- [ ] Delete duplicate policies/apps
- [ ] Delete group assignments
- [ ] Bulk add objects to groups

## Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

## Acknowledgements

- [Emifo](https://github.com/emifo) — help with the user authentication part

## License

See [LICENSE.txt](LICENSE.txt) for details.
