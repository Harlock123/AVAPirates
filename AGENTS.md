# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls, what
the tests check and the project tree; this file covers how the code is put together and the rules that keep it
working.

## What this is

A life at sea in the style of Sid Meier's 1987 Pirates!, in C# on .NET 10 and Avalonia 12. It covers sailing in
real time, ports, sea fights, sword fights and a career that ends in retirement. One self-contained executable per
platform. Everything in it is original: the Saltwind Isles, the four crowns and the pictures are drawn in code, and
the sounds are synthesised by `tools/make_sounds.py`.

## Commands

```bash
dotnet build AVAPirates.slnx                    # the SDK version is in global.json (10.0.400 or later)
dotnet test AVAPirates.slnx                     # every test runs headless: no window opens
dotnet run --project src/AVAPirates             # play it; --no-audio for silence
PIRATES_SHOTS=$PWD/Images dotnet test tests/AVAPirates.Tests --filter ScreenshotTests   # refresh the README's pictures
PIRATES_BALANCE=1 dotnet test tests/AVAPirates.Tests --filter BalanceProbe --logger "console;verbosity=detailed"   # fight statistics for tuning
python3 tools/make_sounds.py                    # rebuild src/AVAPirates/Sounds (standard-library Python only)
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types, implicit usings and
`InvariantGlobalization` for every project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVAPirates/Rules/`: the crowns (`Nations.cs`), ship types, trade goods and `Rng`.
- `src/AVAPirates/World/`: `SeaMap` grows the isles from a fixed seed, `WorldState` is one career, and `SaveStore`
  writes the save slots.
- `src/AVAPirates/Sea/`: points of sail (`Sailing`), the ships met at sea (`Traffic`), the real-time `SeaBattle` and
  the sword fight (`Duel`).
- `src/AVAPirates/Game/`: `GameEngine` (split over partial files by mode: Title, Sea, Port, Battle, Career) holds the
  whole game apart from drawing.
  - `Panel.cs` is a menu or message; panels stack over the game. `DemoPilot` plays the attract demo, and
    `SoundManager` maps `Sfx` to sounds.
- `src/AVAPirates/Views/`: `GameCanvas` (split over partial files) draws each mode and runs the 16 ms timer.
  `Sprites.cs` paints the isles' ground and water as pixel bitmaps.
- `src/AVAPirates/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `tests/AVAPirates.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`. `Helpers.cs` sets up careers and runs the
  clock.

## Rules that keep it working

- **Fixed steps.** Sailing and fighting advance in fixed 1/60 s steps (`GameEngine.Step`) from an accumulator in
  `Update`, so tests can play deterministically. Game chance comes only from the engine's seeded `Rng` (`g.Rng`),
  whose state is saved with the career. Never use `Random` in gameplay. The seeded `Random` in
  `GameCanvas.Port.cs` only varies how a port is drawn.
- **Engine first, drawing second.** Gameplay belongs in `Game/`, `Sea/`, `World/` and `Rules/`, where tests reach it
  without a window. `Views/` only reads the engine and draws it.
- **Keys arrive by name.** `GameCanvas.KeyName` turns an Avalonia `Key` into a name ("Up", "Enter", "Space"). The
  engine takes them through `Press` and `Release`, and keeps the keys held down in `Held`. Tests drive the same
  methods, so test new controls through them rather than by setting state directly.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** Saves live in the app-data folder (`AVAPirates`, `save1.json` and so
  on). Tests use `Helpers.TempSaves()` for a temporary folder, and the screenshot tests run silent.
- **Line endings.** The sources are LF; keep them that way.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- If it changes fights, run the balance probe before and after.
- Update `README.md` (features, controls, the test list) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push and pull request. It then publishes self-contained
single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as
workflow artifacts. Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a
proper release. After a release it asks the portfolio site to rebuild, if the `SITE_REFRESH_TOKEN` secret is set.
