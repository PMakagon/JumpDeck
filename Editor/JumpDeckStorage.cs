using System;
using System.Collections.Generic;

namespace JumpDeck.Editor
{
    // Hosts supply an undo owner and a deck list; editing and presentation stay shared.
    public interface IJumpDeckStorage
    {
        UnityEngine.Object UndoTarget { get; }
        List<JumpDeckCollection> Decks { get; }
        event Action Changed;
        void SaveData();
    }

    public static class JumpDeckStorage
    {
        private static IJumpDeckStorage customDefault;
        private static Func<JumpDeckView, IJumpDeckViewExtension> viewExtension;
        private static Func<bool> openHandler;
        public static Func<string> DefaultDestination { get; private set; }
        public static event Action DefaultChanged;
        public static IJumpDeckStorage Default
        {
            get
            {
                if (customDefault != null) return customDefault;
                var data = JumpDeckData.instance;
                data.EnsureInitialized();
                return data;
            }
        }

        public static void UseDefault(IJumpDeckStorage storage, Func<string> destination = null,
            Func<JumpDeckView, IJumpDeckViewExtension> extension = null, Func<bool> open = null)
        {
            if (ReferenceEquals(customDefault, storage) && DefaultDestination == destination && viewExtension == extension && openHandler == open) return;
            customDefault = storage;
            DefaultDestination = destination;
            viewExtension = extension;
            openHandler = open;
            DefaultChanged?.Invoke();
        }

        internal static IJumpDeckViewExtension Extend(IJumpDeckStorage storage, JumpDeckView view) =>
            ReferenceEquals(customDefault, storage) ? viewExtension?.Invoke(view) : null;

        internal static bool TryOpen() => openHandler?.Invoke() ?? false;
    }
}
