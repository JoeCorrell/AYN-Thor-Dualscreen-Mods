# Wemu Second Screen

A SMAPI mod that publishes what is happening in Stardew Valley to a second
screen.

It reads the game and sends it over a loopback socket. Nothing is patched, so it
keeps working across game updates rather than breaking on them.

## What it sends

- The day: date, clock, weather today and tomorrow, gold, luck, energy, health
- Your bag, with the game's own icons for every item
- The valley map, and where you are on it
- Crops needing water, anywhere you own them
- Machines with something in them, and how long is left
- Animals not yet petted, and produce waiting
- Community Center bundles, and what is still missing
- Villagers, birthdays and who you have not spoken to today
- Quests with their current objective
- The game's own interface art, so the panel is drawn out of Stardew's pixels

While a screen is attached the game's own HUD comes off the top screen, since
the panel repeats all of it. It goes back the moment the screen disconnects.

## What it accepts

Tapping a hotbar slot switches what you are holding. Tapping a bag slot brings
it to hand. Cancelling a quest, if the game allows that quest to be cancelled.

Every one of those runs on the game thread, and each can be turned off.

## Settings

`config.json`, written next to the mod. Every option is also on the second
screen's settings page, and changing it there writes this file.

| Option | Default | |
| --- | --- | --- |
| `HideGameHud` | true | Take Stardew's HUD off the top screen |
| `AllowInventoryEdits` | true | Let the panel move items |
| `AllowQuestCancel` | true | Let the panel cancel quests |
| `FarmerMarker` | true | Draw your farmer as the map marker |
| `SendMap` | true | |
| `SendCrops` | true | |
| `SendMachines` | true | The most expensive one |
| `SendAnimals` | true | |
| `SendBundles` | true | |
| `SendVillagers` | true | |

Each of the send options costs the game real work every ten in game minutes.
Turn off what you never look at.

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

## Reading it

The socket is `ws://127.0.0.1:7786`, plain JSON, one object per frame. The
handshake and framing are written out by hand rather than taken from .NET,
because the framework's WebSocket server is a wrapper over Windows components
that Wine does not implement.
