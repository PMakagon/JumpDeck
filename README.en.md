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

## See it in action

### 1. Build decks for your work

Create a deck from the gear menu, then drag in objects, folders, and settings.
Keep several decks in the same window for different tasks.

<p align="center"><a href=".github/screenshots/create-deck.png"><img src=".github/screenshots/create-deck.png" alt="Create and import deck menu" width="655"></a><br><sub>Create and import decks</sub></p>

Pins can lead to assets, scene objects, or saved searches.
Click a screenshot for a closer look.

<p align="center"><a href=".github/screenshots/decks.png"><img src=".github/screenshots/decks.png" alt="Decks with object, settings, and folder pins" width="337"></a><br><sub>Several decks in one panel</sub></p>

### 2. Choose what a click does

A pin can highlight its target with **Ping**, open it with **Open/Inspect**, or do both.
Choose the action for each pin.

<p align="center"><a href=".github/screenshots/click-action.png"><img src=".github/screenshots/click-action.png" alt="Pin click action menu" width="665"></a></p>

### 3. Make each deck fit its job

Each deck has its own icon size and buttons. Minimize headers when you need more room;
the pins stay visible.

<p align="center"><a href=".github/screenshots/icon-size.png"><img src=".github/screenshots/icon-size.png" alt="Deck icon and button size settings" width="646"></a><br><sub>Individual deck settings</sub></p>

<p align="center"><a href=".github/screenshots/compact-headers.png"><img src=".github/screenshots/compact-headers.png" alt="Decks with minimized headers" width="179"></a><br><sub>Minimized headers</sub></p>

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
