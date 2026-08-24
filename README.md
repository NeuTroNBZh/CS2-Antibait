# Antibait — CS2 Glow Highlight Plugin

> CounterStrikeSharp plugin for CS2 retake servers — makes selected players visually highlighted (glow) through walls and smokes for all other players.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-1.0.364-blue)](https://github.com/roflmuffin/CounterStrikeSharp)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com)
[![Version](https://img.shields.io/badge/version-1.2.0-green)](https://github.com/NeuTroNBZh/Antibait/releases)

---

## Overview

**Antibait** is a server-side admin tool for CS2 retake servers. It lets admins put specific players in a **permanent glow** visible through walls and smokes by everyone on the server, or automatically highlight the **last surviving player** of each team when they are the final one alive.

The plugin is built on top of CounterStrikeSharp and is fully compatible with **CS2Retake**, **CS2-SimpleAdmin**, and the `breakerandopendoor` orchestration plugin commonly found on retake servers.

---

## Features

- **Permanent glow** — toggle a persistent through-wall highlight on any player; survives round changes
- **Last-alive auto-glow** — automatically highlights a watched Counter-Terrorist player when they are the sole surviving CT of the round
- **Team-colored highlights** — Terrorists and Counter-Terrorists get distinct colors; fully configurable
- **CS2-SimpleAdmin integration** — commands appear automatically in the SimpleAdmin menu if the plugin is loaded
- **Crash-safe** — entity creation is guarded against the `breakerandopendoor` entity-scan window at round start
- **Lightweight** — no database, no external dependencies beyond the CSS API

---

## Requirements

| Dependency | Version |
|---|---|
| Counter-Strike 2 (dedicated server) | Latest |
| [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) | ≥ 1.0.228 |
| .NET Runtime | 8.0 (included with CSS) |

---

## Installation

1. Download the latest release ZIP from the [Releases](../../releases) page.
2. Extract and copy the `addons/` folder into your CS2 server root, merging it with the existing `addons/` directory:

```
game/csgo/addons/counterstrikesharp/plugins/Antibait/
    Antibait.dll
    lang/
        en.json
        fr.json
```

3. Restart the server or run `css_plugins load Antibait` in the server console.
4. A configuration file will be created automatically on first load at:

```
game/csgo/addons/counterstrikesharp/configs/plugins/Antibait/Antibait.json
```

---

## Configuration

The config file is generated automatically with the following defaults:

```json
{
  "AdminPermission": "@css/cheats",

  "PermanentGlow_R": 255,
  "PermanentGlow_G": 50,
  "PermanentGlow_B": 50,

  "LastAlive_T_R": 255,
  "LastAlive_T_G": 130,
  "LastAlive_T_B": 0,

  "ConfigVersion": 1
}
```

| Key | Description | Default |
|---|---|---|
| `AdminPermission` | CSS permission flag required to use commands | `@css/cheats` |
| `PermanentGlow_R/G/B` | Color of the permanent glow (RGB) | Red `255, 50, 50` |
| `LastAlive_T_R/G/B` | Glow color for the watched last alive Terrorist | Orange `255, 130, 0` |

---

## Commands

All commands require the `AdminPermission` flag defined in the config (default: `@css/cheats`).

### `!antibait_glow <name>` — Permanent glow toggle

Toggles a permanent through-wall glow on the target player. The glow persists across round changes until toggled off.

```
!antibait_glow PlayerName
css_antibait_glow PlayerName
```

- `<name>` — partial or full player name (case-insensitive). If multiple players match, the command lists the matches and asks for a more specific query.
- Running the command again on the same player **removes** the glow.

### `!antibait_last <name>` — Watched CT last-alive glow toggle

Adds or removes a specific player from the **last-CT-alive watch list**.

```
!antibait_last PlayerName
css_antibait_last PlayerName
```

- `<name>` — partial or full player name (case-insensitive).
- When **active**, the player glows **only when they are the sole surviving Counter-Terrorist** of the round.
- Running the command again on the same player **removes** them from the watch list.
- Multiple players can be watched simultaneously; whichever one is the last CT alive will glow.
- The watch list persists across round changes; the active highlight resets each round.
- If the watched player also has a **permanent glow**, the permanent color takes priority.

---

## Glow Color Priority

When both permanent and last-CT-alive conditions apply to the same player, **permanent always wins**:

```
Permanent glow   →  Red    (255, 50, 50)  — admin-defined, persists between rounds
Last alive CT    →  Orange (255, 130, 0)  — auto, round-scoped, only when sole CT survivor
```

---

## CS2-SimpleAdmin Integration

If **CS2-SimpleAdmin** is loaded on the server, Antibait registers itself in the admin menu automatically (no additional configuration needed). An **Antibait** category will appear with:

- **Permanent Glow on Player** — player picker to toggle permanent glow
- **Toggle Last CT Alive Glow** — toggle the last-CT-alive feature

The integration is purely optional; the plugin works standalone if SimpleAdmin is absent.

---

## Compatibility

Tested and designed to run alongside:

| Plugin | Notes |
|---|---|
| **CS2Retake 3.0.0** | Fully compatible; entity creation guards against `breakerandopendoor` scan window |
| **CS2-SimpleAdmin 1.7.9a** | Auto-registers in the admin menu |
| **breakerandopendoor** | `_roundInProgress` flag prevents entity creation during round-start scans |
| **PlayerSettings / MenuManager** | No conflicts |
| **CS2Stats** | No conflicts |
| **AntiSlow** | No conflicts |

---

## Technical Notes

### How the glow works

CS2 does not expose a reliable per-player glow property via CSS. Instead, Antibait creates two `prop_dynamic` entities per highlighted player:

- **ModelRelay** — invisible entity (RenderMode = None) that follows the player pawn via `FollowEntity`
- **GlowEnt** — the actual glow entity (follows ModelRelay), with `GlowType = 3` (through-wall) and `GlowTeam = -1` (visible to all)

Network visibility of these entities is controlled per-viewer via the `CheckTransmit` hook, so each player sees only the glows they should see.

### Crash prevention

`EventRoundStart` fires **during** `breakerandopendoor`'s entity-scan window (`round_start extra-1` through `extra-4`). Creating `CDynamicProp` entities while entities are still in the engine's staging list causes a `WriteEnterPVS: GetEntServerClass failed` server crash. Antibait uses a `_safeToCreate` flag that is only set to `true` inside `EventRoundFreezeEnd`, which fires **after** all scans complete. All entity creation (round start, player spawn, last-CT-alive trigger) is gated on this flag.

---

## Building from Source

```bash
git clone https://github.com/NeuTroNBZh/Antibait.git
cd Antibait
dotnet build -c Release
```

The compiled DLL and assets are output to:
```
addons/counterstrikesharp/plugins/Antibait/
```

---

## License

This project is licensed under the [MIT License](LICENSE).
