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
| Left click | Hit the block you point at (hold to keep digging) |
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
- Avatar creator: body sliders with exact sizes you can type in, 13 face shape sliders, freckles, wrinkles, scars, birthmarks, moles you place, vitiligo, makeup, full-arm tattoos (17 patterns, or draw your own in the Tattoo Studio with brush, fill, shapes, knife, select, mirror and repeat tools), 12 piercings, 96 hair styles with hairlines, length, curl, part side and streaks, split dye, tips, roots and underlayers, 22 beards, 25 glasses, 32 tops, 30 jackets, 30 pants, 30 shorts, 30 skirts, 30 shoes, 30 hats, more accessories, 3 colors and 20 patterns per piece. Clothes and hair move in the wind.
- Nine save files. Each one belongs to one character, who can be edited any time, is named after them, and has its own world grown from its own seed.
- 20 biomes in every world, 16 above ground and 4 below, each with its own ground, plants, sky, horizon and drifting air. Caves, lakes, waterfalls, floating islands, hot springs.
- 27 kinds of block to dig by hand, each with its own hardness, sound and debris. Blocks fracture from where you hit them, stone and ore throw sparks that light up the dark, and water runs into what you open up. Stacks hold 100.
- Day and night, weather, soft shadows, god rays, reflective water, reactive grass and animals.

## Build
Needs the .NET 10 SDK.

```
dotnet run -c Release
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o Play
```

All art is painted by code at run time. Audio credits are in [CREDITS.md](CREDITS.md).
