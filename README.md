# AYN Thor DualScreen Mods

Native lower-screen companions for games running through WEMU on the AYN Thor.
The Android companion keeps useful information and touch controls on the lower
display while the game remains playable on the upper display.

The current release includes the DualScreen Android app and the Stardew Valley
SMAPI integration.

## Downloads

Open the [latest release](https://github.com/JoeCorrell/AYN-Thor-Dualscreen-Mods/releases/latest) and download:

- `DualScreen Mods.apk` for the AYN Thor lower screen.
- `WemuSecondScreen.zip` for a complete Stardew Valley mod installation.
- `StardewDualScreen.dll` when updating an existing mod folder manually.

## Stardew Valley pages

| Page | Contents |
| --- | --- |
| Today | Weekday, date, time, weather, luck, gold, energy, health, shipping, skills, hotbar, and all unlocked backpack rows |
| Map | Stardew's valley map, the current location, and the farmer marker |
| Farm | Tall, independently scrollable machine, animal, and fruit-tree lists with compact farm summaries |
| Journal | Active quests, objectives, deadlines, rewards, and cancellation where Stardew permits it |
| Bundles | Community Center rooms, bundle progress, and missing item icons |
| Calendar | A fixed four-week season calendar with weekday headers, birthday portraits and labels, festivals, and travelling-cart days |
| Settings | Appearance, visible pages, connection details, and live mod options |

## Inventory controls and item details

- The Today page places the backpack directly below the hotbar.
- L/R rotates every unlocked backpack row through the active hotbar, so all
  inventory rows remain usable without tiny transfer controls.
- Tap selects or moves supported inventory items; hold uses or eats supported
  items.
- Watering cans show a compact blue capacity bar beneath the item.
- Weapons show their special-action cooldown.
- Skills show their current level without the removed XP-to-next-level line.

Farm lists use Stardew's real machine, animal, produce, and fruit-tree data.
Entries are arranged vertically and can be scrolled. Animals with no produce do
not receive a placeholder item icon.

Weather uses full wording such as `Sunny`, and the Today page includes the day
of the week alongside the date, season, and time.

## Installation

### Android companion

Install `DualScreen Mods.apk` on the AYN Thor. Android may ask you to allow
installation from the app that opened the APK.

### Stardew Valley mod

The mod requires Stardew Valley 1.6 and SMAPI 4.5 or later.

1. Extract `WemuSecondScreen.zip`.
2. Copy the extracted `WemuSecondScreen` folder into Stardew Valley's `Mods`
   directory inside the Steam container.
3. Start Stardew Valley through SMAPI.

For a manual update, replace the old DLL with `StardewDualScreen.dll` and ensure
the mod's `manifest.json` contains:

```json
"EntryDll": "StardewDualScreen.dll"
```

The default socket is `ws://127.0.0.1:7786`. Local connections work without
configuration. Set `AllowRemote` to `true` in the mod's `config.json` only when
the companion must connect over the network.

## Building

Build the SMAPI integration from `mods/StardewSecondScreen`:

```powershell
dotnet build -c Release
```

The output is `bin/Release/net6.0/StardewDualScreen.dll`.
