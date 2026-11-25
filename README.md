# Untitled Game

A cozy pixel-art 2D side platformer about dressing up, exploring, digging and building. Written from scratch in C# with no game engine (raylib is used only for the window, drawing and audio).

## Play
Download the zip from Releases, unzip it and run `Untitled Game.exe`. Windows 10/11, no install needed.

| Key | Action |
| --- | --- |
| A / D | Walk |
| Shift | Run |
| Space | Jump |
| S | Crouch, or slide while running |
| W / S | Climb, swim |
| Hold left mouse | Dig the block you point at |
| Right mouse | Place the block in your hand |
| Scroll wheel | Slide through everything you own |
| Tab or E | Inventory |
| Q (hold) | Emote wheel |
| M | Map |
| Esc | Menu, settings |
| F1 | Admin menu: weather, sky events, time, travel, items |
| F11 | Borderless / windowed fullscreen |

Keys can be rebound.

## What is inside
- Avatar creator: body sliders, 96 hair styles with streaks, split dye, tips, roots and underlayers, 32 tops, 30 jackets, 30 pants, 30 shorts, 30 skirts, 30 shoes, 30 hats, accessories, 3 colors and 20 patterns per piece. Clothes and hair move in the wind.
- Six save files. Each one belongs to one character, is named after them, and has its own world grown from its own seed.
- 20 biomes in every world, 16 above ground and 4 below, each with its own ground, plants, sky, horizon and drifting air. Caves, lakes, waterfalls, floating islands, hot springs.
- 27 kinds of block to dig by hand, each with its own hardness. Blocks fracture from where you hit them, and water runs into what you open up.
- Day and night, weather, soft shadows, god rays, reflective water, reactive grass and animals.

## Build
Needs the .NET 10 SDK.

```
dotnet run -c Release
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o Play
```

All art is painted by code at run time. Audio credits are in [CREDITS.md](CREDITS.md).
