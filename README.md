# JumpDeck

JumpDeck is a personal navigation panel for the Unity Editor. Keep important
assets, folders, scene objects, and reusable Hierarchy searches close without
adding personal configuration files to source control.

## Features

- Organize pins into named decks.
- Pin assets, folders, sub-assets, components, and saved scene objects.
- Add pins by drag-and-drop or from Project and Hierarchy context menus.
- Create Live Pins from Unity Hierarchy Search (`h:`) queries.
- Keep all primary actions visible in narrow docked layouts.
- Ping targets or open/show them in the Inspector with separate buttons.
- Choose the default name-click action per pin: type default, Inspect/Open,
  Ping, or both.
- Export portable `.jumpdeck` snapshots and import them as new decks.
- Search, rename, reorder, move, and remove pins.
- Keep independent settings for every project clone.

## Requirements

- Unity 2023.2 or newer.

## Installation

In Unity, open **Window > Package Manager**, choose **Add package from git URL**,
and enter:

```text
https://github.com/PMakagon/JumpDeck.git
```

For local development, choose **Add package from disk** and select this
repository's `package.json`.

## Usage

Open **Window > JumpDeck** or press **Alt+J**.

Clicking a pin name uses a type-aware default: Unity-native `.asset` files and
scene objects are shown in the Inspector, imported media and folders are pinged,
and scenes, prefabs, and scripts are opened. Override that behavior at any time
from **Pin menu > Click Action**. The dedicated Ping and Inspect/Open buttons
always keep their explicit actions.

Use **+ Pin** for the current selection, drag Project or Hierarchy objects onto
a deck, or use the **JumpDeck > Pin Selection** context-menu command.

Create a **Live Pin** to save a dynamic scene search:

```text
h: t:Light
h: tag:Enemy
h: path:/Gameplay/Enemies
```

Live Pins can search all loaded scenes or only the active scene.
Running a Live Pin selects its current results in the Hierarchy and shows them
inside the deck.

## Import and export

Open a deck's **⋮** menu and choose **Export Deck...** to save a portable,
versioned `.jumpdeck` JSON file. Use the JumpDeck window menu and choose
**Import Deck...** to preview that file and import it as a new personal deck.

Deck files can be stored under `Assets` and committed like Unity layouts. They
keep asset GUIDs, local file IDs, scene object IDs, fallback paths, and Live Pin
queries. Import never replaces an existing personal deck; duplicate names get
a numeric suffix.

## Personal data and source control

JumpDeck stores personal data in:

```text
UserSettings/JumpDeck.asset
```

The standard Unity `.gitignore` excludes `UserSettings`, so changing a deck does
not dirty the project's Git working tree.

## Status

JumpDeck is currently in early development. See
[Documentation~/index.md](Documentation~/index.md) for the package manual.

## License

[MIT](LICENSE.md)
