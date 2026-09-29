# Using JumpDeck

Open **Window → JumpDeck** or **Alt+J**. Dock the window beside Project or Hierarchy.

## Decks and pins

Use **⚙ → Create deck** to group the objects you use together. Double-click its title to rename it.
Drag assets, folders, sub-assets, components, or objects from a saved scene into the deck.

- Click a name or icon to perform the pin's action.
- **Ping** locates the target in Project or Hierarchy without changing selection.
- **Open/Inspect** opens a scene, prefab, or script, or shows the object in Inspector.
- **⋮ → Click action** changes what clicking that pin does.

The separate Ping and Open/Inspect buttons are hidden by default. Enable either
one in display settings if you want a shortcut alongside the main click action.

Folders and imported media default to Ping. Scene objects and configuration assets
default to Inspect; scenes, prefabs, and scripts open in their editor.
A double-click uses Open/Inspect regardless of the single-click setting.

The toolbar and **JumpDeck → Pin Selection** context commands add to the first deck.
To add somewhere else, drop objects on that deck or use its **Add Current Selection** menu.

## Arrange and reuse

Drag a pin row to change its order or move it to another deck.
Drop on a row to insert above or below it, or on a deck header to append.
Drag a deck header onto another to move to its position.
The pin popup also has a **Move to** dropdown.

Drag a pin row to use its original Unity object in another window: assign a material
or object reference in Inspector, or drag a prefab into Scene. Live Pins do not represent
a single draggable object.

Use the search field to filter names, paths, and saved queries. Focus a pin row and use
**↑ / ↓** to navigate; **Enter** runs the pin's action, and **Escape** clears row selection.
Rename with the menu or a double-click on a deck title. **Enter** saves the name;
**Escape** cancels. Content edits support Unity Undo/Redo.

## Deck settings

Click **⋮** in the deck header. The popup opens to its right when space allows. Set icon size from 12 to 64 pixels;
the row height adjusts to fit. Larger icons use asset previews when Unity provides them.
Ping, Open/Inspect, Run and the pin menu button scale with the icon; text stays the same size.

**Show Ping button** and **Show Open/Inspect button** are independent options.
They do not change any pin's Click Action. **Reset to defaults** restores 16-pixel
icons and hides both buttons for this deck only. Live Pins keep their Run button.
Display changes support Undo/Redo.

Each deck stores its own display settings in `UserSettings/JumpDeck.asset`.
They survive editor restarts and travel with the deck on export/import. Older deck
files use the default size and hidden object buttons. Existing local global preferences
are copied once to decks that have no saved display settings.

## Live Pins

A Live Pin saves a Hierarchy Search query. Its creation and editing window has
short filter examples alongside their explanations, with a link to Unity's query reference:

```text
h: t:Light
h: tag:Enemy
h: path:/Gameplay/Enemies
```

Choose **All Loaded Scenes** or **Active Scene**. Press its search button to evaluate,
select the matching objects, and display results under the pin. Queries run on demand;
they are not continuous background searches. Results reset when the scene context changes.

Use **⋮ → Edit Query…** to change the name, query, or scope without recreating the pin.
Search errors are displayed under the pin. At most 200 results are shown; the search
still selects all matching objects.

## Unavailable objects

- **Scene is not loaded:** the scene still exists. Open it yourself or choose the pin's
  **Open Scene Additively** action.
- **Object no longer exists:** the asset, scene, or referenced object cannot be found.
- **Invalid object reference:** the stored identifier is not usable.

Unavailable pins stay in the deck. JumpDeck does not silently substitute a different
asset. Scene objects must belong to a scene saved at least once; moving an object to
another scene can invalidate its old Unity identifier.

## Save and share

Personal decks are stored in `UserSettings/JumpDeck.asset`. Standard Unity Git ignores
exclude this folder. Each project clone has its own setup.

Choose **⋮ → Export Deck…** to save a readable `.jumpdeck` file. Use **Import Deck…**
in the global gear, or select the file in Project and choose
**Assets → JumpDeck → Import Deck File**.

Import creates a new deck, gives duplicate names a numeric suffix, and preserves
unresolved references. Shared object pins require the corresponding assets/scenes
with the same GUIDs; export does not copy those assets.

## Global settings and Lock

The **⚙** beside search opens Create deck, Import deck, Show pin counts, and Minimize headers.
Counts are hidden by default. Minimize compresses only the headers to thin strips,
hides their titles, and keeps pin content visible. Hover a header to see its name.
The normal foldout still collapses content. Global preferences are saved in
`UserSettings/JumpDeckViewSettings.asset`; they are not part of deck exports.

Deck and pin **⋮** buttons open custom panels. Rename, display options, adding pins,
export and delete live in the deck panel; per-pin actions live in the pin panel.

**Lock deck** prevents editing the deck name, display, pin content, click actions,
and order. It rejects drops into the deck and moving its pins out to another deck.
Selection, Ping, queries, scene loading, export and dragging the original Unity
object into another editor window remain available. Unlock is always available;
locking supports Undo/Redo and is saved/exported with the deck.

Pin rows supply both internal reorder data and the Unity object for dragging out.
Action buttons keep their own click behavior and do not activate the row.
Ping and Inspector icons are drawn with vector geometry at the displayed size.

## Storage compatibility

`UserSettings/JumpDeck.asset` contains one storage container with decks and pins.
The window still shows the same deck list; it has no set-management controls.
Updating an old file moves its decks into that container, preserving IDs, order,
references, live queries, display settings, collapse state, and Lock. Before rewriting
an existing legacy file, JumpDeck keeps a one-time copy at
`UserSettings/JumpDeck.asset.before-set`. Keep that backup when reverting to an older
package, which does not understand the nested structure. Export/import still operate
on individual `.jumpdeck` decks and retain the existing portable format.

The toolbar search is a local filter of saved pins, separate from Live Pin queries.
