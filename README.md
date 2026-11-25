# UNNAMED GAME

A cozy pixel-art 2D side platformer about dressing up, exploring and fishing. Written from scratch in C# with no game engine (raylib is used only for the window, drawing and audio).

## Play
Download the zip from Releases, unzip it and run `UNNAMED GAME.exe`. Windows 10/11, no install needed.

| Key | Action |
| --- | --- |
| A / D | Walk |
| Shift | Run |
| Space | Jump (S + Space drops through platforms) |
| S | Crouch, or slide while running |
| W / S | Climb, swim |
| F or click | Hold and release to cast, then hold to reel |
| E | Interact |
| Q (hold) | Emote wheel |
| Tab / J / M | Bag / Journal / Map |
| Esc | Menu, settings, avatar editor |
| F11 | Borderless / windowed fullscreen |

Keys can be rebound, and gamepads work.

## What is inside
- Avatar editor: body sliders, 46 hair styles with streaks, split dye, tips, roots and underlayers, 32 tops, 30 jackets, 30 pants, 30 shorts, 30 skirts, 30 shoes, 30 hats, accessories, 3 colors and 20 patterns per piece. Clothes and hair move in the wind.
- One fixed world for everyone (1500 x 170 blocks, bordered by sea) with a shared spawn plaza, forests, a lake and waterfall, highlands, caves and beaches.
- Day and night, weather, soft shadows, god rays, reflective water, reactive grass and animals.
- 40 fish with their own fighting styles, 5 of them legendary. Journal, bait foraging, tackle, six traders and an aquarium.

## Build
Needs the .NET 10 SDK.

```
dotnet run -c Release
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o Play
```

All art is painted by code at run time. Audio credits are in [CREDITS.md](CREDITS.md).
