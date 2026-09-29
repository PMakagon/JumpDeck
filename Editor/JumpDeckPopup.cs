using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JumpDeck.Editor
{
    internal abstract class JumpDeckPopup : PopupWindowContent
    {
        public override void OnGUI(Rect rect) { }
        public override void OnOpen()
        {
#if !UNITY_6000_5_OR_NEWER
            editorWindow.rootVisualElement.Add(CreateContent());
#endif
        }
#if UNITY_6000_5_OR_NEWER
        public override VisualElement CreateGUI() => CreateContent();
#endif
        internal abstract VisualElement CreateContent();
        internal static Rect AnchorToRight(Rect button) => new(button.xMax + 6, button.y, 0, button.height);
        internal static VisualElement Root(Vector2 size)
        {
            var root = new VisualElement();
            root.style.width = size.x;
            root.style.height = size.y;
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = root.style.paddingBottom = 10;
            return root;
        }
    }

    internal sealed class JumpDeckActionsPopup : JumpDeckPopup
    {
        private readonly Func<VisualElement> create;
        private readonly Vector2 size;
        internal JumpDeckActionsPopup(Vector2 size, Func<VisualElement> create)
        {
            this.size = size;
            this.create = create;
        }
        public override Vector2 GetWindowSize() => size;
        internal override VisualElement CreateContent() => create();
    }
}
