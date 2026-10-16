using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CyberQuest.UI
{
    /// <summary>
    /// Builds a readable in-world panel: accent border, dark background, coloured
    /// header bar, wrapped text and sized poke/ray buttons. Coordinates are in
    /// metres, panel-local, origin at the panel centre; -Z faces the viewer.
    /// Font sizes are TextMeshPro 3D sizes (size 0.1 = 1 cm em height).
    /// </summary>
    public class UiPanel
    {
        public const float Pad = 0.03f;
        public const float HeaderHeight = 0.075f;
        public const float TitleSize = 0.26f;
        public const float BodySize = 0.22f;

        private static readonly Dictionary<Color, Material> MaterialCache = new Dictionary<Color, Material>();

        public GameObject Root { get; }
        public Vector2 Size { get; }
        public TextMeshPro Title { get; private set; }
        public Renderer Header { get; private set; }

        /// <summary>Top edge (panel-local y) of the area below the header.</summary>
        public float ContentTop { get; }

        public float Left => -Size.x / 2f + Pad;
        public float Width => Size.x - 2f * Pad;
        public float Bottom => -Size.y / 2f + Pad;

        private UiPanel(GameObject root, Vector2 size, float contentTop)
        {
            Root = root;
            Size = size;
            ContentTop = contentTop;
        }

        public static UiPanel Create(Transform parent, string name, Vector2 size, string title, Color accent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.AddComponent<UiPanelBounds>().size = size;

            bool hasHeader = !string.IsNullOrEmpty(title);
            var panel = new UiPanel(root, size, size.y / 2f - (hasHeader ? HeaderHeight : 0f) - Pad);

            panel.Quad("Border", Vector2.zero, size + new Vector2(0.014f, 0.014f), accent, 0.006f);
            panel.Quad("Background", Vector2.zero, size, UiTheme.PanelColor, 0.004f);
            if (hasHeader)
            {
                panel.Header = panel.Quad("Header", new Vector2(0f, size.y / 2f - HeaderHeight / 2f),
                    new Vector2(size.x, HeaderHeight), accent, 0.002f);
                panel.Title = panel.Text(title, new Rect(-size.x / 2f + Pad, size.y / 2f - HeaderHeight, size.x - 2f * Pad, HeaderHeight),
                    TitleSize, TextAlignmentOptions.MidlineLeft, bold: true);
            }
            return panel;
        }

        public void SetAccent(Color accent)
        {
            SetColor(Root.transform.Find("Border")?.GetComponent<Renderer>(), accent);
            SetColor(Header, accent);
        }

        /// <summary>Text in a panel-local rect (x, y = bottom-left corner). Wraps inside the rect.</summary>
        public TextMeshPro Text(string content, Rect area, float fontSize, TextAlignmentOptions align,
            Color? color = null, bool bold = false)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(Root.transform, false);
            go.transform.localPosition = new Vector3(area.center.x, area.center.y, -0.001f);
            var tmp = go.AddComponent<TextMeshPro>();
            var theme = UiTheme.Instance;
            if (theme != null && theme.font != null)
            {
                tmp.font = theme.font;
            }
            tmp.rectTransform.sizeDelta = area.size;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.fontSize = fontSize;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.alignment = align;
            tmp.color = color ?? UiTheme.TextColor;
            tmp.text = content;
            return tmp;
        }

        /// <summary>Full-width text block hanging down from <paramref name="top"/>.</summary>
        public TextMeshPro Body(string content, float top, float height, float fontSize = BodySize,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, Color? color = null)
        {
            return Text(content, new Rect(Left, top - height, Width, height), fontSize, align, color);
        }

        public PokeButton3D Button(string label, Vector2 center, Vector2 size, UnityAction onPress)
        {
            var theme = UiTheme.Instance;
            if (theme == null || theme.button == null)
            {
                Debug.LogWarning("UiPanel: no UiTheme button prefab found in Resources.");
                return null;
            }
            var btn = Object.Instantiate(theme.button, Root.transform);
            btn.name = "Button_" + label;
            btn.transform.localPosition = new Vector3(center.x, center.y, -0.008f);
            btn.transform.localRotation = Quaternion.identity;
            btn.SetSize(size);
            btn.SetLabel(label);
            btn.OnPressed.AddListener(onPress);
            return btn;
        }

        /// <summary>Evenly spaced row of equal buttons across the panel width.</summary>
        public void ButtonRow(float centerY, float height, params (string label, UnityAction onPress)[] buttons)
        {
            const float gap = 0.02f;
            float w = (Width - gap * (buttons.Length - 1)) / buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                float x = Left + w / 2f + i * (w + gap);
                Button(buttons[i].label, new Vector2(x, centerY), new Vector2(w, height), buttons[i].onPress);
            }
        }

        /// <summary>Full-width buttons stacked downward from <paramref name="topY"/>.</summary>
        public void ButtonColumn(float topY, float height, params (string label, UnityAction onPress)[] buttons)
        {
            const float gap = 0.018f;
            for (int i = 0; i < buttons.Length; i++)
            {
                float y = topY - height / 2f - i * (height + gap);
                Button(buttons[i].label, new Vector2(0f, y), new Vector2(Width, height), buttons[i].onPress);
            }
        }

        public Renderer Quad(string name, Vector2 center, Vector2 size, Color color, float z)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(Root.transform, false);
            quad.transform.localPosition = new Vector3(center.x, center.y, z);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = quad.GetComponent<Renderer>();
            SetColor(r, color);
            return r;
        }

        public static void SetColor(Renderer r, Color color)
        {
            if (r == null)
            {
                return;
            }
            r.sharedMaterial = MaterialFor(color);
        }

        public static Material MaterialFor(Color color)
        {
            if (MaterialCache.TryGetValue(color, out var mat) && mat != null)
            {
                return mat;
            }
            var theme = UiTheme.Instance;
            mat = theme != null && theme.unlit != null
                ? new Material(theme.unlit)
                : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            MaterialCache[color] = mat;
            return mat;
        }
    }

    /// <summary>Marks a panel root and records its size (used for layout checks).</summary>
    public class UiPanelBounds : MonoBehaviour
    {
        public Vector2 size;
    }
}
