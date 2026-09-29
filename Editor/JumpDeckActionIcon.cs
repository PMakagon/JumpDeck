using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JumpDeck.Editor
{
    internal enum JumpDeckIconKind { Ping, Inspector, PinSelection, LivePin, Settings, Lock }

    internal sealed class JumpDeckActionIcon : VisualElement
    {
        private readonly JumpDeckIconKind kind;
        internal JumpDeckActionIcon(JumpDeckIconKind kind)
        {
            this.kind = kind;
            pickingMode = PickingMode.Ignore;
            AddToClassList("button-icon");
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            float scale = Mathf.Min(contentRect.width, contentRect.height) / 16;
            if (scale <= 0) return;
            Vector2 origin = contentRect.center - new Vector2(8, 8) * scale;
            Vector2 Point(float x, float y) => origin + new Vector2(x, y) * scale;
            painter.strokeColor = EditorGUIUtility.isProSkin ? new Color(.82f, .82f, .82f) : new Color(.22f, .22f, .22f);
            painter.lineWidth = 1.25f * scale;
            void Line(float x1, float y1, float x2, float y2)
            {
                painter.BeginPath(); painter.MoveTo(Point(x1, y1)); painter.LineTo(Point(x2, y2)); painter.Stroke();
            }
            void Circle(float x, float y, float radius)
            {
                painter.BeginPath();
                for (int i = 0; i <= 40; i++)
                {
                    float angle = i * Mathf.PI * 2 / 40;
                    var point = Point(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
                    if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
                }
                painter.ClosePath(); painter.Stroke();
            }
            void Box(float left, float top, float right, float bottom)
            {
                painter.BeginPath(); painter.MoveTo(Point(left, top)); painter.LineTo(Point(right, top));
                painter.LineTo(Point(right, bottom)); painter.LineTo(Point(left, bottom));
                painter.ClosePath(); painter.Stroke();
            }
            switch (kind)
            {
                case JumpDeckIconKind.Inspector:
                    Box(2, 2, 14, 14);
                    Line(2, 5, 14, 5); Line(5, 5, 5, 14);
                    Line(7, 8, 12, 8); Line(7, 11, 12, 11);
                    break;
                case JumpDeckIconKind.Lock:
                    Box(3.5f, 7, 12.5f, 14);
                    painter.BeginPath(); painter.MoveTo(Point(5, 7)); painter.LineTo(Point(5, 5));
                    for (int i = 0; i <= 20; i++)
                    {
                        float angle = Mathf.PI + i * Mathf.PI / 20;
                        painter.LineTo(Point(8 + Mathf.Cos(angle) * 3, 5 + Mathf.Sin(angle) * 3));
                    }
                    painter.LineTo(Point(11, 7)); painter.Stroke();
                    Circle(8, 10, .65f); Line(8, 10.6f, 8, 12);
                    break;
                case JumpDeckIconKind.PinSelection:
                    painter.BeginPath(); painter.MoveTo(Point(5, 2)); painter.LineTo(Point(11, 2));
                    painter.LineTo(Point(10, 7)); painter.LineTo(Point(13, 10));
                    painter.LineTo(Point(3, 10)); painter.LineTo(Point(6, 7));
                    painter.ClosePath(); painter.Stroke(); Line(8, 10, 8, 14);
                    break;
                case JumpDeckIconKind.Settings:
                    painter.BeginPath();
                    for (int i = 0; i < 64; i++)
                    {
                        int part = i % 8;
                        float radius = part >= 2 && part <= 5 ? 6.5f : 5;
                        float angle = (i - .5f) * Mathf.PI * 2 / 64;
                        var point = Point(8 + Mathf.Cos(angle) * radius, 8 + Mathf.Sin(angle) * radius);
                        if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
                    }
                    painter.ClosePath(); painter.Stroke(); Circle(8, 8, 2.3f);
                    break;
                default:
                    Circle(6.5f, 6.5f, 4.5f); Line(10, 10, 14, 14);
                    if (kind == JumpDeckIconKind.LivePin)
                    { Line(4, 6.5f, 9, 6.5f); Line(6.5f, 4, 6.5f, 9); }
                    break;
            }
        }
    }
}
