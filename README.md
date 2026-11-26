# Untitled Game

A cozy pixel-art 2D side platformer about dressing up, exploring, digging and building, with full controller support. Written from scratch in C# with no game engine (raylib is used only for the window, drawing and audio).

## Download and play

**[Download Untitled-Game.exe](https://github.com/ChocolateWafer1/untitled-game/releases/latest/download/Untitled-Game.exe)**, then double-click it. That's all: one file, no installer, nothing else to download. Windows 10 or 11.

Windows may show "Windows protected your PC" the first time, because the game isn't signed. Click **More info**, then **Run anyway**. Saves go to `%AppData%\Untitled Game`, so you can move or replace the .exe without losing anything.

Controllers work too: Xbox and PlayStation, with rumble on Xbox (and on PlayStation through Steam).

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

| Xbox | PlayStation | Action |
| --- | --- | --- |
| Left stick / D-pad | Left stick / D-pad | Walk, climb, crouch. Push the stick all the way (or click it) to run |
| A | Cross | Jump |
| RT (hold) | R2 (hold) | Dig the block you aim at |
| LT | L2 | Build |
| Right stick | Right stick | Aim (left alone, it picks the block in front of you) |
| LB / RB | L1 / R1 | Choose what is in your hand |
| Y | Triangle | Inventory |
| X (hold) | Square (hold) | Emote wheel, pick with the stick |
| View | Share | Map |
| Menu | Options | Menu |

In menus the D-pad jumps between buttons, the left stick moves a cursor, A/Cross clicks, B/Circle goes back, the right stick scrolls and LB/RB change tabs in the creator. Typing a name brings up an on-screen keyboard. Rumble works with Xbox controllers, and with PlayStation controllers through Steam. Settings > Controls says which controller the game found.

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
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:SingleExe=true -o Play
```

All art is painted by code at run time. Audio credits are in [CREDITS.md](CREDITS.md).
