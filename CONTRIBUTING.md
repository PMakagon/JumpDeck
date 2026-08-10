# Contributing to JumpDeck

Issues and pull requests are welcome.

## Development setup

1. Clone this repository outside a Unity project.
2. Add it to a Unity 2023.2 or newer project through **Package Manager > Add package from disk**.
3. Select this repository's `package.json`.
4. Open **Window > JumpDeck** and verify the changed workflow manually.

Keep runtime dependencies out of the package unless a feature clearly requires
one. JumpDeck is intended to remain an editor-only tool.

## Pull requests

- Keep changes focused and describe their user-facing impact.
- Preserve existing `.meta` files and include `.meta` files for new Unity assets.
- Confirm that `JumpDeck.Editor` compiles without warnings.
