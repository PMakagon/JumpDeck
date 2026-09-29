# JumpDeck

[Русский](README.md) · **English**

**Stop jumping between folders and windows.**

JumpDeck is a bookmark panel for everything you often need in Unity.
Bookmarks are grouped into decks that you can build around different kinds of work:
UI, level design, lighting, or animation. Dock the panel in your layout
and access the objects you need from one place.

Decks can hold assets, scripts, settings, scene objects, and even search queries.

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
