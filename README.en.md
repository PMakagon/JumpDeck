<h1 align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset=".github/brand/wordmark-dark.svg">
    <img src=".github/brand/wordmark-light.svg" alt="JumpDeck" width="600">
  </picture>
</h1>

<p align="center"><strong>Bookmarks for everything you keep coming back to in Unity.</strong></p>

[Русский](README.md) · **English**

JumpDeck brings assets, scripts, settings, scene objects, and saved searches
into one Unity Editor panel. Organize pins into decks for UI, level design,
lighting, or other work, then dock the panel where you need it.

- **Quick access:** click a pin to locate or open its target.
- **Your own decks:** reorder pins, change icon sizes, and choose click actions.
- **Live Pins:** run saved searches across loaded scenes.
- **Sharing:** export a deck to a `.jumpdeck` file.

**MIT · Unity 2023.2+ and Unity 6**. Tested on Unity 2023.2.22f1 and Unity 6 (6000.6.3f1).

## Install

In **Window → Package Manager → Add package from git URL**, paste:

```text
https://github.com/PMakagon/JumpDeck.git
```

Open **Window → JumpDeck**, or press **Alt+J**.
To change the shortcut, open the gear menu, click **Open shortcut** and press a new key combination.
Dock it wherever it's handy.

## Make a deck

1. Use **⚙ → Create deck** and give the deck a name you'll recognize.
2. Drag in the objects you use most often. Start with a few you keep having to find.
3. Click a pin to jump to its object.

Each object pin lets you choose what a click does: **Ping**, **Open/Inspect**, or both.
You'll find this in the **⋮ → Click action** popup. If you've already got something useful
open in Inspector, choose **Ping**: it highlights the object and keeps your current
selection in place.

Drag a pin row to reorder pins or move them between decks. Drag a deck header
to move the deck. Drag a pin into Inspector or Scene to use the original object.
There are no separate drag handles.
The keyboard works too: **↑ / ↓** to pick a pin, **Enter** to run it.

A deck's **⋮** button opens its name, display settings, and actions. Change the icon
and button size, and independently enable **Ping** and **Open/Inspect**.
Both buttons are hidden by default. Each deck has its own look: large pins for
lighting, for example, and compact ones for UI. Export keeps these settings too.
**Lock deck** protects the deck from edits while its pins remain usable.

The global **⚙** button next to search holds deck creation, import, pin counts, and **Minimize headers**.
Minimize turns headers into thin strips without names; pins stay visible.

## Save a scene search

If you keep searching for the same things, add a **Live Pin** with a query, for
example, `h: t:Light` or `h: tag:Enemy`. Click it to find and select matching objects
in the loaded scenes. The creation window shows short filter examples with
explanations and a link to Unity's reference. You can tweak the query later with **Edit Query…**.

## Where decks live and how to share them

Decks live in `UserSettings/JumpDeck.asset`, a file that's usually excluded from Git.
To pass a deck to a teammate, choose **⋮ → Export deck…** and send them the
`.jumpdeck` file. Import it from the global gear to add a new deck; the existing
ones stay in place.

Save the scene before pinning its objects. When it's closed, its pins stay in the
deck and show that the scene needs to be loaded.
