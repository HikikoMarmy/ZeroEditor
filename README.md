<p align="center">
  <img src="ZeroEditor.png" alt="ZeroEditor Banner" width="600">
</p>

<h2 align="center">An all-purpose modding tool for <strong>Project ZERO / Fatal Frame</strong></h2>

ZeroEditor is a unified toolkit for extracting, editing, and rebuilding data for the **Project ZERO / Fatal Frame** trilogy on PlayStation 2. It provides powerful editors, extractors, and viewers for game files, archives, models, images, audio, and more — all through a user-friendly interface.

### ZeroEditor is still in active development and may contain bugs or missing features.

---

## Features
- Extract and rebuild **PS2 ISO images**
- Extract and rebuild **PK2, PK4, PHF, and PAK** archives
- Support for **file expansion**, automatically resolving offsets and relocations
- **TIM2** viewer, exporter, and importer
- **STR** and **BD** audio player with export and import support
- **ELF editor** for modifying enemies, item placement, fog, variables, and more

---

## Supported Games / Regions / Platforms

| Game Title                            | Regions / Versions | Platform | Supported |
|---------------------------------------|--------------------|----------|-----------|
| **Fatal Frame / Project ZERO**        | EU                 | PS2      | Yes       |
|                                       | US                 | PS2      | Yes       |
|                                       | JP                 | PS2      | Yes       |
| **Fatal Frame II / Project ZERO 2**   | EU                 | PS2      | Yes       |
|                                       | US                 | PS2      | Yes       |
|                                       | JP                 | PS2      | Yes       |
|                                       | Debug              | PS2      | Yes       |
|                                       | Prototype          | PS2      | Yes       |
| **Fatal Frame III / Project ZERO 3**  | EU                 | PS2      | Yes       |
|                                       | US                 | PS2      | Yes       |
|                                       | JP                 | PS2      | Yes       |
|                                       | Prototype (Aug)    | PS2      | Yes       |
|                                       | Prototype (Sep)    | PS2      | Yes       |

---

## How to Extract
1. Open ZeroEditor and select **ISO → Extract ISO**.
2. Choose the ISO of the game you want to extract.
3. Select the folder where the extracted files will be placed (your *working folder*).
4. After extraction completes, browse and edit files under the **bin** panel on the left side.

## How to Rebuild
1. Open ZeroEditor and select **ISO → Rebuild ISO**.
2. Select your working folder (the one containing `_manifest.json`).
3. Choose a name for the rebuilt ISO (default: `FatalFrame_rebuilt.iso`).
4. If prompted, select the **original/base ISO**.
5. Wait for the rebuild to complete.

### Rebuilt ISOs have currently only been tested in **PCSX2**.

---

## Special Thanks

- [@wagrenier](https://github.com/wagrenier) — Research, documentation, and reverse engineering
- [@weirdbeardgame](https://github.com/weirdbeardgame) — Research, documentation, and reverse engineering
