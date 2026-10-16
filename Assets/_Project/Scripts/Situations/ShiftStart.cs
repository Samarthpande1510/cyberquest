using CyberQuest.Core;
using CyberQuest.Props;
using CyberQuest.UI;
using UnityEngine;

namespace CyberQuest.Situations
{
    /// <summary>
    /// Opening (CLAUDE.md 3): START SHIFT starts the clock and the desk phone
    /// rings with the manager's voicemail, which sets the cover objective.
    /// Only wiring - situations still trigger independently.
    /// </summary>
    public class ShiftStart : MonoBehaviour
    {
        public const string Objective = "Finish the client report. Leave before 6:30.";

        [SerializeField] private WelcomePanel welcome;
        [SerializeField] private DeskPhone deskPhone;
        [SerializeField] private MonitorScreen monitor;

        private void Start()
        {
            monitor.SetObjective("Shift not started");
            welcome.StartPressed += Begin;
            deskPhone.VoicemailFinished += () =>
            {
                monitor.SetObjective(Objective);
                ShiftHud.Instance?.SetObjective(Objective);
            };
        }

        public void Begin()
        {
            GameDirector.Instance.StartShift();
            monitor.SetObjective(Objective);
            ShiftHud.Instance?.SetObjective("Answer your desk phone.");
            deskPhone.StartRinging();
        }
    }
}
