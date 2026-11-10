# itch.io page (text to paste)

Dashboard → Create new project. Suggested settings:

| Field | Value |
|---|---|
| Title | CONTRACT: Seoul Shadows |
| Project URL | `https://<your-itch-name>.itch.io/contract-seoul-shadows` |
| Short description | A non-lethal stealth-contract game in a neon Seoul, with a real-walking GPS mode on Android. |
| Classification | Games |
| Kind of project | Downloadable |
| Release status | In development |
| Pricing | No payments (or Donations) |
| Uploads | Windows zip (tick *Windows*), Linux zip (tick *Linux*), APK (tick *Android*), or `tools/itch_package.sh push` |
| Genre | Action (or Simulation) |
| Tags | stealth, korea, seoul, third-person, gps, walking, unity |
| Community / comments | Your choice |
| Visibility | **Draft** while preparing → **Restricted** (secret link) for a presentation → **Public** |

---

## Description

**CONTRACT: Seoul Shadows** (서울의 그림자) is a third-person stealth game set in a fictional
night-time Seoul. You are a freelance operative: take contracts at your safehouse, find people
and evidence without being spotted, get paid, upgrade, and work towards **$1,000,000**.

No one is harmed: guards warn, escort or report you, and failed contracts can be retried.

**Features**

- Two districts to explore: **Gangnam** (glass towers, neon Hangul signage, food alley, subway)
  and **Myeongdong** (street-food carts, cosmetics shops, the red-brick cathedral).
- Five contracts, three operatives with their own strengths, 13 upgrades and outfits.
- A living crowd, guards with line of sight and suspicion, terminals to hack, people to follow.
- **GPS Life mode (Android):** walk in the real world and your operative walks the city. Distance
  and steps earn XP, rewards, missions and collectibles. Only totals are kept: no location data
  is shown, stored or sent.
- Original synthesized music and ambience.

**Controls (PC)**: WASD move · mouse look · Shift sprint · Ctrl crouch · Space jump ·
E interact (hold for terminals) · M map · Esc pause. Gamepads are supported.
**Android:** on-screen joystick, drag to look, touch buttons.

**Requirements**

- Windows 10/11 64-bit with DirectX 11, or Linux 64-bit with Vulkan / OpenGL 4.5. Runs at
  about 60 fps on Intel UHD integrated graphics with the Low preset (measured on Linux).
- Android 8.0+ (64-bit). The GPS mode needs location and, for steps, "Physical activity".

**Installing**

- *itch app:* Install → Launch (updates automatically).
- *Windows:* unzip and run `Contract_SeoulShadows.exe`. The game is not code-signed, so
  SmartScreen may say "Windows protected your PC": click *More info → Run anyway*.
- *Linux:* unzip, then `chmod +x Contract_SeoulShadows.x86_64` and run it.
- *Android:* download the APK on the phone, allow your browser to install unknown apps, and
  install. Play Protect may warn about an unknown developer: *Install anyway*.

**Safety (GPS mode)**: watch your surroundings, not the screen, and never use the phone while
crossing a road. You can pause the walking session at any time.

**Status**: in development. Built with Unity 6 (URP). All characters, companies and events are
fictional. Korean font: Noto Sans KR (OFL). Textures: Poly Haven (CC0).

---

## Screenshots to add (3–5)

Main menu · Gangnam street with the HUD · Myeongdong cathedral street · contract board ·
the phone with the touch controls (from `adb exec-out screencap -p > shot.png`).
