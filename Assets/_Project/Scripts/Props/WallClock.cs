using CyberQuest.Core;
using TMPro;
using UnityEngine;

namespace CyberQuest.Props
{
    /// <summary>Wall clock bound to the game clock (CLAUDE.md 3).</summary>
    public class WallClock : MonoBehaviour
    {
        [SerializeField] private TextMeshPro label;

        private void Update()
        {
            var director = GameDirector.Instance;
            if (label != null)
            {
                label.text = director != null ? director.ClockText : "5:45 PM";
            }
        }
    }
}
