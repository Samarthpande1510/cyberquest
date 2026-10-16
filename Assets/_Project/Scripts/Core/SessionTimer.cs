using UnityEngine;

namespace CyberQuest.Core
{
    /// <summary>
    /// Advances GameState.sessionTimeElapsed. Time-gated event triggers
    /// (e.g. Priya, imposter) read this field directly rather than each
    /// running their own clock.
    /// </summary>
    public class SessionTimer : MonoBehaviour
    {
        private void Update()
        {
            GameState.Current.sessionTimeElapsed += Time.deltaTime;
        }
    }
}
