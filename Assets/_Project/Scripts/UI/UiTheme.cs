using TMPro;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>
    /// Shared look for every in-world panel: font, base material, sounds and the
    /// colour palette. Lives in Resources so runtime-built UI can find it without
    /// each event script carrying its own references.
    /// </summary>
    [CreateAssetMenu(menuName = "CyberQuest/UI Theme")]
    public class UiTheme : ScriptableObject
    {
        public const string ResourcePath = "CyberQuestUiTheme";

        public TMP_FontAsset font;
        public Material unlit;
        public PokeButton3D button;
        public Texture2D qrCode;
        public AudioClip click;
        public AudioClip good;
        public AudioClip okay;
        public AudioClip bad;
        public AudioClip notify;

        // Palette (sRGB). Light-on-dark panels read best in a headset.
        public static readonly Color PanelColor = new Color(0.06f, 0.09f, 0.15f);
        public static readonly Color TextColor = new Color(0.95f, 0.97f, 0.99f);
        public static readonly Color MutedText = new Color(0.62f, 0.69f, 0.78f);
        public static readonly Color ButtonColor = new Color(0.15f, 0.39f, 0.92f);
        public static readonly Color ButtonHover = new Color(0.35f, 0.58f, 1f);
        public static readonly Color Blue = new Color(0.11f, 0.31f, 0.85f);
        public static readonly Color Amber = new Color(0.80f, 0.45f, 0.05f);
        public static readonly Color Red = new Color(0.78f, 0.13f, 0.13f);
        public static readonly Color Green = new Color(0.09f, 0.55f, 0.27f);
        public static readonly Color Purple = new Color(0.43f, 0.16f, 0.85f);
        public static readonly Color Teal = new Color(0.05f, 0.50f, 0.55f);

        private static UiTheme _instance;

        public static UiTheme Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<UiTheme>(ResourcePath);
                }
                return _instance;
            }
        }
    }
}
