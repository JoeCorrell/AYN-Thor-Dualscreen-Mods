# WEMU Second Screen for Stardew Valley

A SMAPI mod that publishes live Stardew Valley state and game artwork to WEMU's
lower-screen companion.

It reads the game and sends it over a local socket. Gameplay state is read
through SMAPI without patching Stardew's methods.

## Companion pages

| Page | Contents |
| --- | --- |
| `Today` | Weekday, date, time, weather, luck, gold, energy, health, shipping, history, skills, hotbar, and backpack |
| `Map` | Valley map, current place, and farmer marker |
| `Farm` | Independently scrollable machine, animal, and fruit-tree lists with farm summaries |
| `Journal` | Expandable active quests, objectives, deadlines, and cancellation where allowed |
| `Bundles` | Community Center progress and missing items |
| `Calendar` | Fixed four-week calendar with NPC birthday portraits and labels, festivals, and travelling-cart days |
| `Settings` | Appearance, page visibility, connection details, and live mod configuration |

## What it sends

- The day: date, clock, weather today and tomorrow, gold, luck, energy, health
- Your bag, with the game's own icons for every item
- Water remaining in watering cans and weapon special-action cooldowns
- The valley map, where you are on it, and your farmer drawn as the marker
- The name of the place you are standing, as the game names it
- Crops needing water, anywhere you own them
- Fruit trees with fruit waiting
- Machines with something in them, and how long is left
- Animals not yet petted, and produce waiting
- The shipping bin, and what today is worth so far
- Community Center bundles, and what is still missing
- Villagers and birthdays
- What each villager loves, so the panel can flag a gift you are carrying
- Quests with their current objective, and special orders with their deadlines
- Every skill and its current level
- Recipes you have the ingredients to craft or cook right now
- The season's calendar: birthdays with portraits, and festivals with the time
  and place they run
- Whether the travelling cart is in the forest today
- How deep you have been in the mines and Skull Cavern
- Museum donations, and anything in your bag Gunther would take
- The game's own interface art, so the panel is drawn out of Stardew's pixels

While a screen is attached the game's own HUD comes off the top screen, since
the panel repeats all of it. It goes back the moment the screen disconnects.

## What it accepts

- Select a hotbar slot
- Tap a backpack item to move it to the active slot
- Hold a supported item to eat or use it
- Sort the bag
- Cancel a quest when Stardew allows it
- Press L/R to rotate each unlocked backpack row through the active hotbar

The Today page keeps the hotbar and all unlocked backpack rows together. L/R is
routed from WEMU's game session, so the physical controller changes rows without
moving focus away from gameplay.

Every one of those runs on the game thread, and each can be turned off.

## Settings

`config.json`, written next to the mod. Every option is also on the second
screen's settings page, and changing it there writes this file.

| Option | Default | |
| --- | --- | --- |
| `HideGameHud` | true | Take Stardew's HUD off the top screen |
| `AllowInventoryEdits` | true | Let the panel select, move, use, and sort items |
| `AllowQuestCancel` | true | Let the panel cancel quests |
| `FarmerMarker` | true | Draw your farmer as the map marker |
| `SendMap` | true | The valley picture, the marker and the portrait |
| `SendCrops` | true | |
| `SendMachines` | true | The most expensive one |
| `SendAnimals` | true | |
| `SendBundles` | true | |
| `SendVillagers` | true | Also the gift tastes behind the gift card |
| `SendCrafting` | true | Checks every known recipe against the bag |
| `AllowRemote` | false | Accept connections from off this machine |

Each of the send options costs the game real work every ten in game minutes.
Turn off what you never look at.

With `AllowRemote` on, the mod also announces itself on the local network every
few seconds, so the app can find it without an address being typed.

`AllowRemote` is the one option with no switch on the panel. It changes who can
reach a running save, so it is a deliberate act at a keyboard rather than
something tapped while looking for the backdrop colour. Off, the socket is
loopback only.

## Installing

Copy the folder into the game's `Mods` directory, next to SMAPI's own. It needs
SMAPI 4.5 or later and Stardew Valley 1.6.

## Building

```
dotnet build -c Release
```

The output is in `bin/Release/net6.0`. The project does not deploy itself,
because the game it is for usually runs somewhere other than the machine that
built it.

## What it costs

Everything above is read from live game state, so it is worked out rather than
stored. What that costs is kept off the clock: only the day, machines, animals,
the pet and mine depth are worked out as time passes, because only those change
with time. Anything that follows the bag is worked out when the bag changes, and
everything else once a day.

Nothing at all is computed while no screen is attached.

## Reading it

The socket is `ws://127.0.0.1:7786`, plain JSON, one object per frame. The
handshake and framing are written out by hand rather than taken from .NET,
because the framework's WebSocket server is a wrapper over Windows components
that Wine does not implement.
