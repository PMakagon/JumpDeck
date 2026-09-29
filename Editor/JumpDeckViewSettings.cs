using System;
using UnityEditor;
using UnityEngine;

namespace JumpDeck.Editor
{
    [Serializable]
    public sealed class JumpDeckDisplaySettings
    {
        internal const int DefaultIconSize = 16;
        internal const int MinIconSize = 12;
        internal const int MaxIconSize = 64;

        [SerializeField] private int iconSize = DefaultIconSize;
        [SerializeField] private bool showPingButton;
        [SerializeField] private bool showOpenButton;

        public int IconSize => Mathf.Clamp(iconSize, MinIconSize, MaxIconSize);
        public bool ShowPingButton => showPingButton;
        public bool ShowOpenButton => showOpenButton;

        internal void Update(int size, bool ping, bool open)
        {
            size = Mathf.Clamp(size, MinIconSize, MaxIconSize);
            iconSize = size;
            showPingButton = ping;
            showOpenButton = open;
        }

    }

    // Global panel preferences. The icon/button fields are read only during migration.
    [FilePath("UserSettings/JumpDeckViewSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class JumpDeckViewSettings : ScriptableSingleton<JumpDeckViewSettings>
    {
        [SerializeField] private int iconSize = JumpDeckDisplaySettings.DefaultIconSize;
        [SerializeField] private bool showPingButton;
        [SerializeField] private bool showOpenButton;

        [SerializeField] private bool showPinCounts;
        [SerializeField] private bool minimizeHeaders;
        internal bool ShowPinCounts => showPinCounts;
        internal bool MinimizeHeaders => minimizeHeaders;
        internal static event Action Changed;

        internal void UpdateGlobal(bool counts, bool minimize)
        {
            if (showPinCounts == counts && minimizeHeaders == minimize) return;
            showPinCounts = counts;
            minimizeHeaders = minimize;
            Save(true);
            Changed?.Invoke();
        }

        internal void CopyTo(JumpDeckDisplaySettings target) =>
            target.Update(iconSize, showPingButton, showOpenButton);
    }
}
