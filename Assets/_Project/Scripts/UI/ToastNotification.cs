using System.Collections;
using TMPro;
using UnityEngine;

namespace CyberQuest.UI
{
    /// <summary>Small pop-up (chat ping, phone notification) that hides itself after a few seconds.</summary>
    public class ToastNotification : MonoBehaviour
    {
        private UiPanel _panel;
        private TextMeshPro _text;
        private Coroutine _hide;

        public bool Visible => _panel != null && _panel.Root.activeSelf;
        public string Text => _text != null ? _text.text : "";

        public static ToastNotification Create(Transform parent, string name, Vector2 size, float viewDistance,
            float fontSize = 0.18f, float scale = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var toast = go.AddComponent<ToastNotification>();
            toast._panel = UiPanel.Create(go.transform, "Panel", size, "Notification", UiTheme.Amber, viewDistance, scale);
            toast._text = toast._panel.Body("", toast._panel.ContentTop, size.y - UiPanel.HeaderHeight - 2f * UiPanel.Pad, fontSize);
            toast._panel.Root.SetActive(false);
            return toast;
        }

        public void Show(string title, string message, float seconds = 6f, bool sound = true)
        {
            _panel.Title.text = title;
            _text.text = message.Length > 110 ? message.Substring(0, 107) + "..." : message;
            _panel.Root.SetActive(true);
            if (sound)
            {
                UiTheme.Play(t => t.notify, transform.position, 0.7f);
            }
            if (_hide != null)
            {
                StopCoroutine(_hide);
            }
            if (isActiveAndEnabled)
            {
                _hide = StartCoroutine(HideAfter(seconds));
            }
        }

        public void Hide() => _panel.Root.SetActive(false);

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _panel.Root.SetActive(false);
            _hide = null;
        }
    }
}
