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

Pages are turned with the arrows at the bottom, with L1 and R1 on a controller,
or with the d-pad left and right. A page with nothing on it today is skipped
rather than shown empty, and the page you are on is remembered even when pages
appear and disappear around it.

### Today

The date, the place you are standing, and the clock, which turns red past
midnight. A bar runs from six in the morning to two the next, with the hours and
minutes left beside it.

Weather today and tomorrow, gold with the game's own coin, the day's luck as a
word, and what the day has earned or spent so far.

Your hotbar, drawn with the game's own slots and the lit one for what you are
holding. Your whole bag underneath, with a sort button. Every skill with the
experience still owed to its next level.

The pet, but only when it has not been petted or its water bowl is empty. The
shipping bin with what today is worth so far. A bar chart of the last ten days'
shipping, which is the one thing a second screen can show that the game cannot.

A line when the travelling cart is in the forest.

### Map

The valley as the game paints it, with your own farmer rendered as the marker
and the place you are standing named on the game's scroll. Interiors the valley
map cannot point at draw the map without a marker rather than guessing.

### Farm

Crops that need water, grouped by where they are. Every machine with something
in it, what it is making, and both how long is left and the time it will be
ready. Fruit trees with fruit waiting. Animals not yet petted and produce ready
to collect. Everything you have the ingredients to craft or cook right now.

### Journal

Quests with their current objective rather than only their title, which is the
line a journal is actually opened for. Deadlines are marked, and the last day is
marked in red. Tap a quest to see the letter it came from, what it pays, and to
cancel it if the game allows that quest to be cancelled.

### Bundles

The Community Center grouped by room, each bundle on one line with its missing
items as icons and its progress on the right. Anything in your bag that a bundle
wants is called out above the rest, with the bundle it belongs to.

### Valley

Whose birthday is today and this week, with their portraits and hearts. Special
orders with their deadlines. People you are carrying a gift they love for. Who
you have not spoken to today. The rest of the season's calendar: birthdays with
portraits, festivals with the time and place they run, and the days the
travelling merchant comes.

### Depths

How deep you have been in the mines, the nearest elevator, and Skull Cavern
separately. The museum, with what you have donated and anything in your bag
Gunther would take.

### Settings

The backdrop: the season's own ground, any of the game's floor tiles, or a
colour you mix. Whether the game's art is used at all. Which pages appear.
Alerts. And what the mod bothers to compute, since each of those costs the game
work.

Settings are kept per save, so two farms do not share one setup. A new farm
starts from your existing settings rather than from nothing.

## Doing things

Tapping a hotbar slot switches what you are holding. Tapping a bag slot brings
it to hand. Holding a slot eats what is in it. The d-pad up and down step
through the hotbar without touching the screen.

The backpack has a sort button, and the journal can cancel a quest.

Every one of those is done by the game on its own thread, so what the panel
shows is always what the game actually holds rather than a guess. All of them
can be switched off in the mod's settings.

## Alerts

The panel buzzes when a machine finishes, when a birthday starts, and once at
2am. Each fires on the change rather than the state, so a finished machine is
one buzz and not one every ten minutes while it waits.

## Running it

Install the mod into the game, start the game, open this app.

By default the game and the app have to be on the same device. To use the app
beside a PC, set `AllowRemote` in the mod's `config.json`. The mod then
announces itself on the local network and appears under **Found on this
network**, so there is no address to type. If it does not, the address can be
entered by hand.

Inside wemu the same pages appear on the console's Mods tab, and this app is not
needed.

## Building

This folder is the `:dualscreen` module of the wemu build. It is published on
its own so the app and its mods can be downloaded without the emulator, but
building the app from source needs the parent project, which supplies the design
system and the `:secondscreen` library the pages live in.

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
