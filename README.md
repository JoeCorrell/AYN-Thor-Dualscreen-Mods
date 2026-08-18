# DualScreen Mods

Puts a game's own information on a second screen while you play.

A mod inside the game opens a socket and says what it is. This app listens on
that socket and draws the page written for that game. Nothing is set up on this
side.

## Supported games

| Game | Mod |
| --- | --- |
| Stardew Valley | Wemu Second Screen (SMAPI) |

## Stardew Valley

Six pages, turned with the arrows at the bottom.

- **Today** the date, clock, weather today and tomorrow, gold, luck, energy,
  health, your hotbar and your bag
- **Map** the valley with your farmer on it
- **Farm** crops needing water, machines with something in them, animals not yet
  petted
- **Journal** quests with their current objective, tap one to open it
- **Bundles** the Community Center by room, with anything in your bag that a
  bundle wants called out first
- **Valley** birthdays and who you have not spoken to today

Tapping a hotbar slot switches what you are holding. Tapping a bag slot brings it
to hand. Both are done by the game on its own thread, so the panel always shows
what the game actually holds.

Settings live on the last page: the backdrop, whether the game's own art is used,
which pages appear, and what the mod bothers to compute.

## Running it

Install the mod into the game, start the game, open this app. The socket is
loopback only, so the game and this app have to be on the same device.

Inside wemu the same pages appear on the console's Mods tab, and this app is not
needed.

## Building

This folder is the `:dualscreen` module of the wemu build. It is published on its
own so the app and its mods can be downloaded without the emulator, but building
the app from source needs the parent project, which supplies the design system
and the `:secondscreen` library the pages live in.

The app, from the wemu checkout:

```
./gradlew :dualscreen:assembleDebug
```

The mods, which are .NET projects and not built by Gradle:

```
cd mods/StardewSecondScreen
dotnet build -c Release
```

The pages themselves live in `:secondscreen`, shared with wemu so there is one
copy of each game's screen. The game side lives in `mods/`.
