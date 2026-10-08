# PoeRedux — PoE 2 Performance Toolkit

A customized, PoE 2-only build of PoeRedux focused on safer visual-performance patches, automatic backups, and a modern bilingual interface.

**Application languages:** English and Vietnamese

**Current release:** 1.2.5

**Platform:** Windows 10/11, x64

> This is an unofficial community tool. It is not affiliated with or endorsed by Grinding Gear Games.

## Overview

### Highlights

- Modern, scalable dark interface with English and Vietnamese support.
- Remembers the selected interface language between sessions.
- Remembers the game file, selected patches, and camera zoom between sessions.
- Remembers color groups and selected patches.
- One-file, self-contained Windows executable.
- PoE 2 only; legacy PoE 1 menu and workflow are not included.
- Backs up original GGPK entries before writing and can restore them from the app.

### Available patches

- Camera zoom and full minimap reveal.
- Three focused Visual Noise options: monster density, dense player-skill layers, and reduced Delirium fog/contact effects.
- Sparse player-skill layers (four particles or fewer), skill timing, projectiles, trails, impacts, ground markers, render blocks, fog, lighting, and post-processing remain enabled.
- Named map-mod color groups with editable shared colors, whole-line or short-tag styles, compact tooltip mod lines, and always-visible monster HP.

The blanket Particles, Effects, Light, Shadow, corpse, fog, and Delirium removers are not offered. Both density reducers edit only particle counts and particles-per-second values. They never replace a particle file with `0`, and they leave `.trl` trails untouched. The player-skill reducer targets audited PoE 2 roots under `metadata/effects/spells`; shared, legacy, environment, NPC, and monster roots are skipped. Restore previously patched game data before evaluating the new options.

The Delirium reducer keeps the encounter intact. It scales dense mirror-contact particles, player haze/blur/shimmer, and the dedicated Delirium fog material while preserving environment settings, lighting, monster telegraphs, projectiles, trails, animation, and sound. It refuses to run over old destructive Delirium edits.

The compact tooltip patch originated from [PoeRedux PR #47](https://github.com/Gineticus/PoeRedux/pull/47), authored by Vinicius Amorim. The PR's blanket particle/effect removal was reviewed but not copied because it can hide monster skills completely.

Run the focused checks with `dotnet test PoeRedux.Tests/PoeRedux.Tests.csproj`. These checks cover preservation of lighting and gameplay screen cues, loading-artwork rewrites, and DAT string writes. They do not measure brightness inside the game.

Gameplay-critical visuals such as poison clouds, ground effects, projectiles, AoE markers, and danger telegraphs are intentionally preserved by the safe reducers.

### Usage

1. Close Path of Exile 2.
2. Run the final `.exe`.
3. Select `Content.ggpk` for the standalone client, or the applicable bundle index `.bin` file for a bundle-based installation.
4. Select only the patches you want.
5. Click **Apply patches** and wait for the success message.

Use **Restore** to restore entries saved by PoeRedux. Backups are stored at:

```text
%LOCALAPPDATA%\PoeRedux\Backups\poe2.zip
```

After a game update, PackCheck, or Steam file verification, delete an obsolete PoeRedux backup before creating a new one.

### Repairing game files

- **Standalone client:** close the game and run `PackCheck.exe` from the Path of Exile 2 installation directory.
- **Steam:** use **Properties → Installed Files → Verify integrity of game files**.

File verification restores original game data and removes applied GGPK patches.

---

## Credits and attribution

This customized build exists thanks to the following open-source projects:

- **[Gineticus/PoeRedux](https://github.com/Gineticus/PoeRedux)** — the original PoeRedux application and the primary base of this project: WPF patcher architecture, UI workflow, backup/restore approach, and baseline patch implementations. The upstream repository is licensed under the **MIT License**.
- **[PoeRedux PR #47](https://github.com/Gineticus/PoeRedux/pull/47)** — feature source for the compact tooltip patch in this build. The source commit is authored by Vinicius Amorim.
- **[aianlinb/LibGGPK3](https://github.com/aianlinb/LibGGPK3)** — provides **LibGGPK3**, **LibBundle3**, and **LibBundledGGPK3**, used to read, edit, and save `Content.ggpk` and bundle index data. The upstream repository is licensed under **AGPL-3.0**.
- **[aianlinb/VisualGGPK2](https://github.com/aianlinb/VisualGGPK2)** — credited as the earlier LibGGPK2/VisualGGPK2 project from which LibGGPK3 was rewritten. It is part of the GGPK library lineage; this app directly depends on LibGGPK3 rather than VisualGGPK2.

All upstream authors retain copyright over their work. Third-party code and binaries remain subject to their respective upstream licenses. The repository's local `LICENSE` does not replace or weaken third-party license terms.

## Safety notes

- Always close the game before patching or restoring.
- A game update can change data layouts; verify game files if a patch reports an unexpected layout.
- Keep gameplay-indicator effects enabled unless you understand the risk of removing them.
- This software is provided without warranty. Use it at your own risk and follow the game's applicable terms and policies.
