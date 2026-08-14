[中文](README.md) · [English](README.en.md) · [日本語](README.ja.md)

---

![OG$UI_JKGDD6QL21LEK)9DE](https://github.com/user-attachments/assets/04739895-457d-4806-8d93-6f3ed2b80bbf)
# Chinese Billiards Club Table · CBC Pool Table

A VRChat billiards table prefab, used in the VRChat world **[Chinese Billiards Club](https://vrchat.com/home/launch?worldId=wrld_0a35397b-2e7d-4f01-8552-034ab8e76e2e)**.

Thanks to our friends for their support: eijis-pan, RokaOvO, WangQAQ, COCOA GAME
Cue: Tempest, catte paling, tarsan, カボ
QQ Group: 780855553

> Original project: [MS-VRCSA-Billiards](https://github.com/Sacchan-VRC/MS-VRCSA-Billiards)

---

## Game Modes

| Mode | Description |
|------|-------------|
| Chinese 8-Ball | Full rules with ball return |
| 15-Red Snooker | 6-red / 15-red selectable |
| Russian Pyramid | Large balls and narrow pockets; any ball may be struck; pocket to score |
| 10-Ball | Balls 1–10, hit the lowest first, call the pocket, optional WPA rules |
| Carom | 0/1/2/3-cushion carom |
| 9-Ball | Balls 1–9, hit the lowest first, pocket the 9 to win |
| Lag | Decides who breaks first; each player shoots one ball, closest to the far cushion wins |

---

## NPC AI Opponent (PracticeManager)

In single-player practice mode you can play against an AI opponent. The NPC has full billiards decision-making:

### Shot Strategy (priority from high to low)

| Priority | Type | Description |
|----------|------|-------------|
| PASS 1 | Direct shot | Pockets directly, includes position play evaluation |
| PASS 2 | Single-cushion bank | Object ball hits one cushion then pockets |
| PASS 2b | Two-cushion bank | Object ball hits two cushions then pockets |
| PASS 2.5 | Thin cut | Thin cut at a large angle |
| PASS 3 | Single-cushion kick | Cue ball hits one cushion then hits the object ball |
| PASS 3b | Two-cushion kick | Cue ball hits two cushions then hits the object ball |
| — | K-ball | Safety, defensive strategy when no pocket route exists |

### Technical Features

- **Cushion friction compensation**: accounts for the rebound angle offset caused by cushion friction on banks, auto-adjusts aim
- **Trajectory verification**: simulates the object ball path before shooting to verify it passes the pocket
- **Path occlusion detection**: checks cue→object, object→cushion, cushion→pocket for blocking balls
- **Foul prediction**: predicts the cue ball trajectory after impact to avoid accidental fouls (e.g. cue ball in pocket, hitting a non-target ball)
- **Position play evaluation**: evaluates where the cue ball stops after the shot, prefers a position good for the next shot
- **Power control**: auto-adjusts shot power by distance and layout, caps power on banks/kicks
- **Repeat detection**: forces a safety ball after the same ball to the same pocket repeats 3+ times

### State Machine

```
IDLE → CALCULATING → CHARGING → DELAYING → SHOOTING → OBSERVING → IDLE
 Idle   Calculating  Charging   Delaying   Shooting   Observing
```

### Test Mode

PracticeManager supports an automatic test mode (`testMode`) that plays multiple games unattended:
- Auto-break, alternating turns, records every shot result
- Logs output to `Assets/npc_log.txt`
- Includes: ball positions, aim direction, cut angle, power, spin, trajectory verification
- Logs can be exported via `Editor/NpcLogExporter.cs`

---

## Table Features

### Player Customization (TableHook)

- Custom cue skin, ball material, table color (cue synced over network, ball and table synced locally)
- Cue size, thickness, smoothness, color offset adjustable
- Auto save/load settings (VRC PlayerData)
- Upload/download settings via Discord/QQ groups

### Scoring System (ScoreManagerV4)

- Automatically records game scores
- Leaderboard upload to backend (`wangqaq.com`, HMAC authentication)
- 45-second timer

### Other Features

- Fully automatic translation system (detects VRChat local language, zh/ja/en)
- Exclusive name color feature
- Persistent personal data and leaderboards
- Cushion color switching
- Coyote integration (loser gets shocked)

### Setup

1. Set `BilliardsModule` to layer 22, physics to interact only with itself (the MS button above can set this automatically)
2. Place a `TableHook` in the scene (auto-detected on run, just adjust position)

![image](https://github.com/user-attachments/assets/f453ae11-0735-4885-b700-87101d5971c7)

![Q84OOB{37Q{XY946MTR$E`F](https://github.com/user-attachments/assets/6bf18499-5926-4ca2-8a8c-8f8e33fd9faa)

- Custom cue skins and ball materials: add textures in TableHook, a few slots are reserved in code
- UdonChips: click the button in BilliardsModule
- Use the ready-made package; if cloning the repo, add VRCSDK (>=3.7.5) and UdonSharp yourself

> [How to set custom cue](https://youtu.be/YnoQ9jsUg0k?si=EfdxX1FDMUZXM2RX)

---

## VRC Light Volumes (VRCLV) Support

The billiard table's physical surface shaders have built-in VRC Light Volumes (VRCLV) support. VRCLV replaces Unity's static light probes with runtime spherical harmonics (SH) lighting, letting the table respond correctly to dynamic lighting.

**Status**: Shader import and compilation have been verified with Unity 2022.3.22f1 and VRC Light Volumes 3.0.0-dev.14. No switch or macro is required: valid Light Volumes are sampled when available, and `LightVolumeSH()` automatically falls back to Unity light probes when the scene is not configured for VRCLV.

**Supported shaders**:
- `metaphira/TableSurface` (table cloth, PC; currently assigned to the table materials)
- `metaphira/VRC LV Standard` (balls, cues, legs, and other solid parts; relevant materials have been converted)
- `metaphira/TableSurface (Quest)` (table cloth, Quest; the shader is available but is not referenced automatically by the current assets)
- `metaphira/TableSurface (Glass)` (glass surface; the shader is available but is not referenced automatically by the current assets)

**Enabling steps**:
1. Install the **VRC Light Volumes** package via VCC (it is already declared as a VCC dependency).
2. Follow the VRCLV setup instructions to create and configure a `LightVolumeManager` and Light Volumes in your world, then perform any required baking. Installing the package alone does not create LV lighting.
3. Add the billiards table; no code changes are required. The shaders already include `Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc`.

**Note**: The repository's non-sample scenes currently contain no Light Volume Manager or volumes, so running them directly only verifies the Unity light-probe fallback. The Quest and Glass shaders are also not wired into an automatic material replacement flow; assign them manually or add them to your own platform-switching configuration. UI, guideline, and shadow unlit materials are unaffected by lighting and need no changes.

---

## Possible Features (Future)

- More table controls
- Richer name colors
- Ball trajectory lines

---

## Credits / People

Final interpretation rights:
Roka: The Russian and Chinese tables were modified by me based on Sacc's 10ft and 12ft snooker table models. The Chinese table was initially used only in my world "FIVI Flight", then was allowed to be used by the Chinese Billiards Club, which also helped with development. However, the usage right was never explicitly limited to my world and the Chinese Billiards Club world, so usage is not restricted. Final interpretation rights belong to the Chinese Billiards Club.

![image](https://github.com/user-attachments/assets/362abbc4-c159-4617-a6a2-23b64765709a)
![image](https://github.com/user-attachments/assets/8da69556-b526-488a-8127-5fc319de84a9)
![image](https://github.com/user-attachments/assets/f1ff2b1e-e0a0-49d5-becb-be3bf18a4ea8)
![9DH{L{LM 4~0@{)PZ4TD_tmb](https://github.com/cheesestudio/VRChat-Pool-table-with-15-red-snooker-Pyramid-Chinese-8-ball-based-on-MS-VRCSA-Billiards/assets/52149451/7f894791-cf72-473e-bbe6-20bec9804917)
![image](https://github.com/user-attachments/assets/969415da-7bda-4689-9e19-54c2f88e8d73)
![image](https://github.com/user-attachments/assets/36cfebe4-d929-4ac5-a14d-f71371f40442)
