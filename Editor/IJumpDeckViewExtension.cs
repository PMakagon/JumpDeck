using System;
using UnityEngine.UIElements;

namespace JumpDeck.Editor
{
    public interface IJumpDeckViewExtension : IDisposable
    {
        float SettingsHeight { get; }
        void AppendSettings(VisualElement root, Action close);
    }
}
