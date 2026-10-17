# Timberborn mods (Eromrab)

Harmony mods and blueprint packs for [Timberborn](https://store.steampowered.com/app/1062090/Timberborn/).

| Mod folder | C# project | Role |
|------------|------------|------|
| [`Tidetails/`](Tidetails/) | [`src/Tidetails/`](src/Tidetails/) | Tidetails faction (depends on Wetlands library) |
| [`Wetlands/`](Wetlands/) | [`src/Wetlands/`](src/Wetlands/) | Wetland landscaping building + shared runtime |
| [`BadwaterRush/`](BadwaterRush/) | [`src/BadwaterRush/`](src/BadwaterRush/) | Optional contaminated-water rush behavior |

## Repository layout

```
Timberborn/
├── BadwaterRush/     # Game mod content (manifest, blueprints, localization)
├── Tidetails/
├── Wetlands/
├── src/              # C# Harmony patches (one folder per assembly)
├── tools/            # Python helpers (thumbnails, asset scripts)
├── artist-brief/     # Notes for external art (meshes, style refs)
├── docs/             # Release plan and in-progress checklist
├── Timberborn.sln    # Open in Visual Studio / Rider
└── Directory.Build.props.example
```

Mod folders stay at the repo root so you can symlink or copy each folder into the game’s mod directory unchanged.

## Build

1. Install [.NET SDK](https://dotnet.microsoft.com/download) (6+ is fine for `netstandard2.1`).
2. Copy `Directory.Build.props.example` → `Directory.Build.props` and set:
   - `GameManagedDir` — `Timberborn_Data/Managed`
   - `HarmonyDir` — folder containing `0Harmony.dll` (Steam Workshop Harmony mod)
3. Build from the repo root:

   ```bash
   dotnet build Timberborn.sln -c Release
   ```

   DLLs are copied into each mod’s `Scripts/` folder (ignored by git).

4. Point Timberborn at the mod folders (symlink, junction, or copy into your mods path).

**Dependency:** build `Wetlands` before `Tidetails` (project reference). Building the solution handles order.

## Docs

- [Release roadmap](docs/VERSION_ROLLOUT.md)
- [Open tasks / verify list](docs/things-to-fix.txt)

## Workshop thumbnails

Generated PNGs are gitignored. Regenerate with:

```bash
python tools/generate_tidetails_thumbnail.py
python tools/generate_wetlands_thumbnail.py
python tools/generate_badwater_rush_thumbnail.py
```
