# Contributing to JumpDeck

Issues and pull requests are welcome.

## Development setup

1. Clone this repository outside a Unity project.
2. Add it to a Unity 2023.2 or newer project through **Package Manager > Add package from disk**.
3. Select this repository's `package.json`.
4. Open **Window > JumpDeck** and verify the changed workflow manually.

For EditMode tests, install Unity Test Framework and add `com.pmakagon.jumpdeck`
to the project manifest's `testables` list. UI lifecycle tests require a graphics device.

`JumpDeckWindow` hosts the reusable `JumpDeckView`. Hosts can supply `IJumpDeckStorage`
with a deck list, an Undo owner, a change event and a save method. `JumpDeckStorage.Default`
is the project's local storage unless a host supplies another source and optional insertion
destination callback. A view may also delegate object activation to its host; unhandled objects
keep the standard Click Action behavior. Rendering, menus and interaction
have separate files. `JumpDeckService` records edits and Undo; `JumpDeckData` saves
Undo/Redo once for all hosts. `JumpDeckSession` owns searches and reference caches;
`JumpDeckFileCodec` owns the portable file format.
Keep storage fields, the data script's `.meta` GUID and `UserSettings/JumpDeck.asset`
compatible so updates preserve existing decks. Storage is now a single passive
`JumpDeckSet` container containing decks, which contain pins. The legacy top-level
`decks` field is kept only for migration. A missing inline class may become an empty
instance on Unity deserialization: check the container's ID, not only null.
Migration retains deck/pin IDs and values and copies the old file to
`UserSettings/JumpDeck.asset.before-set` before writing the new structure.
The editor exposes the container's decks through the existing facade. There are
no set creation, selection, switching, or export commands.

`JumpDeckDisplaySettings` belongs to each deck and is included in portable exports.
Use `JumpDeckService.SetDisplaySettings` for edits so Undo and view updates stay consistent.
`JumpDeckSettingsPopup` resolves its deck by ID after Undo; its subscriptions live only
while the popup content is attached. The view-settings singleton stores global
counts/minimize preferences; its older icon/button fields are read only during
migration. All editing commands must respect the deck lock.
The format-1 display field is optional so older exported decks still import.

A storage provider can supply an `IJumpDeckViewExtension` through `JumpDeckStorage.UseDefault`.
It can add controls to the existing `Toolbar` and append a settings section with an explicit
height. Each view owns and disposes its extension. The optional open handler shares the
existing Alt+J shortcut; returning false opens the ordinary JumpDeck window.

Keep runtime dependencies out of the package unless a feature clearly requires
one. JumpDeck is intended to remain an editor-only tool.

## Documentation

`README.md` is the primary Russian README; `README.en.md` is its English counterpart.
Update both in the same change when installation, features or usage instructions change.
Keep their language links reciprocal and their examples and commands consistent.

## Pull requests

- Keep changes focused and describe their user-facing impact.
- Preserve existing `.meta` files and include `.meta` files for new Unity assets.
- Confirm that `JumpDeck.Editor` compiles without warnings.
