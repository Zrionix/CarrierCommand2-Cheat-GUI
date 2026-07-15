# Carrier Command 2 Save Editor + Trainer

A Windows tool for cheating your way through single-player Carrier Command 2. You can edit your save file while the game is closed, or hook into the running game and change things on the fly without reloading.

It's styled to look like it belongs next to CC2 instead of a plain spreadsheet, and it covers the things you actually want to mess with: money, stockpiles, blueprints, island ownership, and your fleet's health and fuel.

## Before you start

This is for single-player only. Using it on a co-op game that someone else is hosting will either get your changes rejected or desync the session, so don't do that.

The tool backs up your save automatically before it writes anything, but always close the game before you edit a save file. If the game is still running it can autosave right over your changes.

## Getting it

Download `CC2CheatGUI.exe` from the [Releases page](https://github.com/Zrionix/CarrierCommand2-Cheat-GUI/releases) and run it. It's a single file with nothing to install, and it doesn't need .NET or any other dependency.

The first time you open it, Windows SmartScreen might show a "Windows protected your PC" box. That happens with basically any small program that isn't code-signed. Click "More info", then "Run anyway".

## Editing your save (game closed)

This is the safe, reliable way to cheat. Close CC2, make your changes, hit Save, then load the game back up.

The app finds your saves on its own (they live in `%APPDATA%\Carrier Command 2\saved_games`). Pick a slot from the dropdown at the top. If for some reason it doesn't find them, use Open to browse to a `save.xml` yourself.

The tabs down the left side:

- **Overview** — a summary of your save, plus one-click cheats: max out credits, unlock every blueprint, take every island, fill your carrier hold, or repair/refuel/rearm your whole fleet. The Armageddon button does all of them at once.
- **Currency** — set your credits. It reads and writes the real value straight from the save, and handles the huge numbers CC2 likes to use.
- **Inventory** — every stockpile in the save: your carrier hold, each deployed vehicle, and every island warehouse. Edit item counts one at a time or set a whole list at once.
- **Blueprints** — unlock every vehicle and attachment, or clear them.
- **Islands** — see who owns what and hand islands over to yourself (or any other faction).
- **Fleet** — your units with their hull, fuel and ammo. Patch them up one at a time or all together.

Every time you save, it drops a timestamped `.bak` copy of your save next to the original, so you can always undo by restoring that file.

## Editing the running game (the LIVE tab)

The LIVE tab changes the game's memory while you play, so cheats take effect instantly with no reload. This is the more experimental half of the tool, but it works well once you get the hang of it.

Get into an actual mission first, then click Attach.

- **Credit** — your current money gets filled in from your save automatically. Click Find to lock onto it in memory, then set it to whatever you want, or freeze it so it never drops.
- **Unlimited Ammo** — weapons stop using up ammo. One catch: the game runs your weapons and the enemy's on the same code, so the AI gets unlimited ammo too.
- **Live Inventory** — edit your carrier hold, or a well-stocked island warehouse, in real time. Save your game first (that's how the tool knows what your hold looks like), pick the target, click Locate, change the numbers, and Apply.
- **Protect Carrier** — freezes your carrier's hull so it can't be destroyed. This one only affects your carrier, not the enemy. It finds your ship by its fuel level, so turn it on soon after loading a save, before you've burned much fuel.

None of the memory locations are hardcoded. The tool searches for everything fresh each time you attach, so a game update won't quietly send it to the wrong place, and it double-checks that it owns the memory before writing to it.

## A note on item names

Item slots in CC2 are numbered, and those numbers can move around between game versions. If the tool shows something as "Item 34" instead of a proper name, it just means it isn't certain what that slot is on your version. Nothing is broken and the number is still safe to edit.

## Building from source

You'll need the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build -c Release
dotnet run --project src/CC2CheatGUI
```

To produce the standalone single-file exe:

```powershell
dotnet publish src/CC2CheatGUI -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

It lands in `src/CC2CheatGUI/bin/Release/net8.0-windows/win-x64/publish/`.

## Credits

The pixel font is LanaPixel (SIL Open Font License), the same one Carrier Command 2 uses, bundled so the tool matches the game's look.

## Disclaimer

Personal, single-player use only. Back up your saves. Use at your own risk.
