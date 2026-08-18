# Mods

The game side of DualScreen Mods. Each one runs inside its game, reads what is
happening and sends it to the app.

| Folder | Game | Framework |
| --- | --- | --- |
| `StardewSecondScreen` | Stardew Valley | SMAPI 4.5 |

They sit inside the app rather than beside it because they are two halves of one
feature. A mod and the page that draws it agree a wire format between them, and
that agreement is not written down anywhere except in the two files that
implement it, so they are shipped and changed together.

These are not built by Gradle. They are .NET projects:

```
cd StardewSecondScreen
dotnet build -c Release
```

The output lands in `bin/Release/net6.0` and is copied into the game's `Mods`
directory by hand, since the game usually runs somewhere other than the machine
that built it.

The pages they feed live in `:secondscreen`, shared with wemu so there is one
copy of each game's screen.
