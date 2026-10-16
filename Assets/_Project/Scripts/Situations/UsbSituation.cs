using System.Collections;
using CyberQuest.Core;
using CyberQuest.UI;
using Oculus.Interaction;
using TMPro;
using UnityEngine;

namespace CyberQuest.Situations
{
    /// <summary>
    /// Situation 3 (CLAUDE.md 5.3): a drive labelled CLIENT DATA - URGENT skitters
    /// out from under the empty desk when the player comes within 2 m (or at the
    /// 150 s fallback). Outcomes are physical: plug it into the PC port, carry it
    /// to the "Found devices" box outside the IT room, or leave it.
    /// A physical object, so it never blocks the director.
    /// </summary>
    public class UsbSituation : SituationTrigger
    {
        public const float PlugDistance = 0.1f;

        [SerializeField] private GameObject usb;
        [SerializeField] private Transform dropPoint;
        [SerializeField] private Transform pcPort;
        [SerializeField] private Transform dropBox;
        [SerializeField] private Vector3 dropBoxSize = new Vector3(0.4f, 0.3f, 0.4f);
        [SerializeField] private TextMeshPro dropBoxLabel;
        [SerializeField] private ChatApp chat;
        [SerializeField] private MonitorScreen monitor;

        private bool _nudged;
        private Rigidbody _rb;

        public GameObject Drive => usb;

        private void Awake()
        {
            if (usb != null)
            {
                usb.SetActive(false);
                _rb = usb.GetComponent<Rigidbody>();
                var wrapper = usb.GetComponent<PointableUnityEventWrapper>();
                if (wrapper != null)
                {
                    wrapper.WhenSelect.AddListener(_ => OnHeld());
                }
            }
        }

        protected override void OnTriggered()
        {
            if (usb == null)
            {
                return;
            }
            usb.transform.SetPositionAndRotation(dropPoint.position, dropPoint.rotation);
            usb.SetActive(true);
            if (_rb != null)
            {
                _rb.linearVelocity = dropPoint.forward * 1.2f + Vector3.up * 0.4f;
                _rb.angularVelocity = new Vector3(0f, 6f, 0f);
            }
            StartCoroutine(Thud());
        }

        private IEnumerator Thud()
        {
            yield return new WaitForSeconds(0.35f);
            UiTheme.Play(t => t.thud, usb.transform.position, 1f);
        }

        private void OnHeld()
        {
            if (_nudged || Resolved)
            {
                return;
            }
            _nudged = true;
            chat.Post("Dan Whitfield - Manager", "DW", "Almost done? Client's asking.", verified: true);
        }

        protected override void OnActiveUpdate()
        {
            if (usb == null || !usb.activeInHierarchy)
            {
                return;
            }
            Vector3 p = usb.transform.position;
            if (pcPort != null && Vector3.Distance(p, pcPort.position) <= PlugDistance)
            {
                PlugIn();
            }
            else if (dropBox != null && new Bounds(dropBox.position, dropBoxSize).Contains(p))
            {
                HandIn();
            }
        }

        // --- player choices (public for tests / debug window)

        public void PickUp() => OnHeld();

        public void PlugIn()
        {
            if (Resolved || !Triggered)
            {
                return;
            }
            State.usb = UsbAction.Plugged;
            Complete("plugged the USB drive into the PC");
            if (usb != null && pcPort != null)
            {
                foreach (var c in usb.GetComponentsInChildren<Collider>())
                {
                    c.enabled = false;
                }
                if (_rb != null)
                {
                    _rb.isKinematic = true;
                }
                usb.transform.SetPositionAndRotation(pcPort.position, pcPort.rotation);
            }
            monitor.ShowInstallBar("Windows Update", "Installing update KB5033120...\nDo not turn off your computer.", 6f);
        }

        public void HandIn()
        {
            if (Resolved || !Triggered)
            {
                return;
            }
            State.usb = UsbAction.Reported;
            Complete("handed the USB drive in at the Found devices box");
            if (usb != null)
            {
                usb.SetActive(false);
            }
            if (dropBoxLabel != null)
            {
                dropBoxLabel.text = "<b>FOUND DEVICES</b>\nDevice received - thank you";
            }
            UiTheme.Play(t => t.thud, dropBox != null ? dropBox.position : transform.position, 0.6f);
        }

        /// <summary>Called once by the ending flow: a drive that dropped but was never dealt with was left.</summary>
        public void FinalizeAtEnd()
        {
            if (Triggered && !Resolved)
            {
                State.usb = UsbAction.Ignored;
                SessionLog.Decision("Usb: left the drive (ignored)");
            }
        }
    }
}
