# JumpDeck manual

JumpDeck is an editor-only Unity package for keeping personal project pins and
reusable live queries in one navigation panel.

## Open JumpDeck

Use **Window > JumpDeck** or press **Alt+J**.

## Pins

Drag assets, folders, components, or saved scene objects onto a deck. Clicking
a pin name uses its configured click action.

Each object pin also has dedicated actions: **Ping** highlights its target in
Project or Hierarchy, while **Open/Inspect** opens scenes and prefabs or shows
configuration assets in the Inspector.

Use **Pin menu > Click Action** to choose **Use Type Default**, **Inspect/Open**,
**Ping**, or both for an individual pin. Type defaults are intentionally small
and predictable: Unity-native `.asset` files and scene objects use Inspector,
imported media and folders use Ping, and scenes, prefabs, and scripts use Open.
The dedicated action buttons are not affected by this setting.

Scene objects must belong to a saved scene. JumpDeck uses Unity
`GlobalObjectId` values so scene pins survive editor restarts and hierarchy
reordering.

## Live Pins

Create a Live Pin from the toolbar or a deck menu. Live Pins execute Unity
Hierarchy Search queries against the currently loaded scenes.

Examples:

```text
h: t:Light
h: tag:Enemy
h: path:/Gameplay/Enemies
```

The scope can include all loaded scenes or only the active scene. Results are
evaluated on demand, selected in the Hierarchy, and invalidated when the
hierarchy changes.

## Local data

Personal data is stored at `UserSettings/JumpDeck.asset`. Unity's standard
`.gitignore` excludes `UserSettings`, so each project clone can keep an
independent setup without producing source-control changes.

## Import and export

Choose **Export Deck...** from a deck's menu to save a `.jumpdeck` snapshot.
The file is readable JSON with an explicit format version and can be stored in
the Unity project or sent to another developer.

Choose **Import Deck...** from the JumpDeck window menu to inspect the number
of object and query pins before importing. Import always creates a new personal
deck. It never overwrites current data, and duplicate deck names receive a
numeric suffix.

Object pins use GUID, local file ID, and `GlobalObjectId` data. Unresolved
references are preserved so they can resolve later when their scene becomes
available. Live Pins preserve their query and scene scope.
