using CyberQuest.Core;
using CyberQuest.Props;
using CyberQuest.UI;
using UnityEngine;

namespace CyberQuest.Situations
{
    /// <summary>
    /// Situation 1 (CLAUDE.md 5.1): invoice email from a lookalike Finance address
    /// with a QR code in the attachment. Scanning = holding the phone within
    /// 0.4 m of the QR on the monitor. Forwarding to IT-Security defuses it.
    /// </summary>
    public class EmailQrSituation : SituationTrigger
    {
        public const string InvoiceId = "invoice";
        public const float ScanDistance = 0.4f;

        [SerializeField] private MailApp mail;
        [SerializeField] private PhoneDevice phone;

        private void Start()
        {
            mail.AddEmail(new MailApp.Email
            {
                Id = "welcome",
                FromName = "HR Team",
                ShownAddress = "hr@nexora.com",
                Subject = "Welcome to NEXORA!",
                Body = "Welcome aboard! Your badge works for all open areas. Meeting rooms are locked after hours " +
                       "and can be opened from the panel outside each door. IT room access is IT staff only.",
                Unread = false,
            }, notify: false);
            mail.AddEmail(new MailApp.Email
            {
                Id = "report",
                FromName = "Dan Whitfield",
                ShownAddress = "dan.whitfield@nexora.com",
                Subject = "Client report - final numbers",
                Body = "The numbers are in the shared sheet. Drop them into the report and send it to Harbor Logistics tonight. " +
                       "Thanks for staying late!",
                Unread = false,
            }, notify: false);
            mail.EmailOpened += e =>
            {
                if (e.Id == InvoiceId)
                {
                    State.emailOpened = true;
                }
            };
        }

        protected override void OnTriggered()
        {
            mail.AddEmail(new MailApp.Email
            {
                Id = InvoiceId,
                FromName = "Finance",
                ShownAddress = "finance@nexora.com",
                RealAddress = "finance@nexora-invoices.net",
                Subject = "Updated client invoice",
                Body = "Hi, please review the attached invoice before sending the report. Thanks.",
                Attachment = "Invoice_0924.pdf",
                AttachmentText = "<b>NEXORA FINANCE</b>\nInvoice 0924\n\nClient: Harbor Logistics\nAmount: $48,250.00\nStatus: PENDING",
                AttachmentHasQr = true,
                QrCaption = "Scan to verify payment status",
                Actions = new[]
                {
                    new ChoicePanel.Option("Forward to IT-Security", Forward),
                    new ChoicePanel.Option("Ignore it", Ignore),
                },
            }, notify: true);
            GameDirector.Notify("New email in your inbox.");
        }

        protected override void OnActiveUpdate()
        {
            if (mail.QrTransform != null && phone != null &&
                Vector3.Distance(phone.transform.position, mail.QrTransform.position) <= ScanDistance)
            {
                ScanQr();
            }
        }

        // --- player choices (public for tests / debug window)

        public void OpenEmail() => mail.Open(InvoiceId);

        public void RevealSender() => mail.RevealSender(InvoiceId);

        public void OpenInvoice() => mail.OpenAttachment(InvoiceId);

        public void ScanQr()
        {
            if (Resolved || !Triggered)
            {
                return;
            }
            State.emailOpened = true;
            State.qrScanned = true;
            if (phone != null)
            {
                phone.ShowToast("Camera", "QR code detected. Opening link...", 3f);
            }
            Complete("scanned the QR code");
        }

        public void Forward()
        {
            if (Resolved || !Triggered)
            {
                return;
            }
            State.emailOpened = true;
            State.emailForwardedToSecurity = true;
            Complete("forwarded to IT-Security");
            mail.AddEmail(new MailApp.Email
            {
                Id = "autoreply",
                FromName = "IT-Security",
                ShownAddress = "security@nexora.com",
                Subject = "Re: Updated client invoice",
                Body = "Thanks, we're reviewing. Continue your task normally.",
            }, notify: true);
            mail.ShowInbox();
        }

        public void Ignore()
        {
            if (Resolved || !Triggered)
            {
                return;
            }
            State.emailOpened = true;
            State.emailIgnored = true;
            Complete("ignored the email");
            mail.SetActions(InvoiceId);
            mail.ShowInbox();
        }
    }
}
