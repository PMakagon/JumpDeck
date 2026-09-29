# Changelog

## 0.2.0 — 2026-09-29

- Add per-deck icon sizes, optional Ping/Open buttons, locks, and compact headers. Keep display settings in exports.
- Reorder decks and pins by dragging their headers or rows, including adjacent items. Move pins between decks and drag their objects into other editor windows.
- Keep pin selection and dragging stable while opening menus and refreshing the panel. Add keyboard navigation.
- Edit existing Live Pin queries and scopes, with compact search examples in the query window.
- Distinguish unloaded scenes, missing objects, and invalid references. Preserve exact sub-asset references.
- Fix Undo/Redo persistence, validate imports and object identifiers, and cache references while filtering.
- Separate the shared panel, storage, editing commands, and search session. Preserve existing deck/pin IDs during storage migration and retain a backup of the old file.
- Replace oversized menus with compact settings popups and refresh the Russian and English guides.
- Verify compatibility with Unity 2023.2.22f1 and Unity 6 (6000.6.3f1).
