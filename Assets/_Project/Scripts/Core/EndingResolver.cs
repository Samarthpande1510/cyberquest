namespace CyberQuest.Core
{
    /// <summary>
    /// Resolves the ending tier and per-situation debrief from GameState
    /// (CLAUDE.md 6). Called exactly once, by the ending flow.
    /// </summary>
    public static class EndingResolver
    {
        public static EndingType ResolveTier(GameState s)
        {
            int sources = s.CompromiseSources;
            if (sources >= 2 || s.mfa == MfaResponse.Approved || (sources >= 1 && s.mfa == MfaResponse.Ignored))
            {
                return EndingType.Breach;
            }
            if (sources == 1)
            {
                return EndingType.ContainedButExposed;
            }
            if (s.colleague == ColleagueResponse.RefusedOutright)
            {
                return EndingType.SocialMiscalibration;
            }
            return EndingType.FullContainment;
        }

        public static EndingResult Resolve(GameState s)
        {
            var result = new EndingResult { Ending = ResolveTier(s) };
            var d = result.Debrief;

            // Email / QR / fake login
            if (s.emailOpened || s.qrScanned)
            {
                if (s.login == LoginResult.Entered)
                {
                    d.Add(new DebriefItem("Invoice email", 0,
                        "You scanned the invoice QR code and signed in on the page it opened.",
                        "The sender was really finance@nexora-invoices.net and the page was nexora-auth.com. Your password went straight to the attacker - the screen flicker 30 seconds later was them using it.",
                        "Check the real sender address and the address bar. Our portal is portal.nexora.com (it's on your sticky note)."));
                }
                else if (s.emailForwardedToSecurity || s.login == LoginResult.Reported)
                {
                    d.Add(new DebriefItem("Invoice email", 2,
                        s.emailForwardedToSecurity ? "You forwarded the invoice email to IT-Security." : "You reported the fake sign-in page as phishing.",
                        "The sender was a lookalike domain (nexora-invoices.net) and the QR code led to a fake login. Reporting lets Security block it for everyone.",
                        "Keep doing this: check the sender, don't scan unexpected QR codes, report."));
                }
                else if (s.login == LoginResult.Closed || s.qrScanned)
                {
                    d.Add(new DebriefItem("Invoice email", 1,
                        "You scanned the QR code, then closed the sign-in page.",
                        "Closing it kept your password safe, but the page was still live for anyone else in the company.",
                        "Don't scan QR codes from unexpected emails - and report the page so Security can take it down."));
                }
                else
                {
                    d.Add(new DebriefItem("Invoice email", 1,
                        "You opened the invoice email and left it alone.",
                        "You didn't fall for it, but it was a phishing email (finance@nexora-invoices.net) and colleagues may still click it.",
                        "Use 'Forward to IT-Security' so the whole company is protected."));
                }
            }

            // USB
            switch (s.usb)
            {
                case UsbAction.Plugged:
                    d.Add(new DebriefItem("Dropped USB drive", 0,
                        "You plugged the 'CLIENT DATA - URGENT' drive into your PC.",
                        "The 'installing update' bar was malware running. Planted drives are a classic way in.",
                        "Never plug in found media. Take it to the 'Found devices' box outside the IT room."));
                    break;
                case UsbAction.Reported:
                    d.Add(new DebriefItem("Dropped USB drive", 2,
                        "You took the drive to the 'Found devices' box.",
                        "The drive was attacker bait. Handing it in costs a walk but nobody gets infected.",
                        "Exactly right - the urgent label is part of the trick."));
                    break;
                case UsbAction.Ignored:
                    d.Add(new DebriefItem("Dropped USB drive", 1,
                        "You left the drive where it fell.",
                        "Not plugging it in was right, but the next person might.",
                        "Hand found drives to IT so they can be checked safely."));
                    break;
            }

            // Colleague (false positive)
            switch (s.colleague)
            {
                case ColleagueResponse.VerifiedThenHelped:
                    d.Add(new DebriefItem("Priya's charger", 2,
                        "You checked Priya's directory entry, then unlocked Meeting Room 3.",
                        "Priya was genuine. Verifying first and then helping is exactly the balance security needs.",
                        "Keep verifying through a channel you trust - then help."));
                    break;
                case ColleagueResponse.UnlockedBlindly:
                    d.Add(new DebriefItem("Priya's charger", 1,
                        "You unlocked Meeting Room 3 without checking who Priya was.",
                        "She was genuine this time, but you couldn't have known. Attackers use the same 'small favour' requests.",
                        "A quick check of the lobby directory would have confirmed her in seconds."));
                    break;
                case ColleagueResponse.RefusedOutright:
                    d.Add(new DebriefItem("Priya's charger", 1,
                        "You refused Priya's request.",
                        "Priya was a real colleague with after-hours access approved. Being suspicious of everyone has a cost too.",
                        "Verify (the lobby directory had her entry), then help."));
                    break;
            }

            // Intercom impersonator
            switch (s.intercom)
            {
                case IntercomResponse.BuzzedIn:
                    d.Add(new DebriefItem("Visitor at the door", 0,
                        "You buzzed the 'infrastructure' visitor in.",
                        "His badge said IT SERVICES, but NEXORA's team is IT INFRASTRUCTURE, no IT work was scheduled and no email existed. The remote-session window was him at work.",
                        "Verify through a channel you control: the directory, the service desk (ext 2100) or the maintenance notice."));
                    break;
                case IntercomResponse.VerifiedExternally:
                    d.Add(new DebriefItem("Visitor at the door", 2,
                        "You checked the visitor's story independently before deciding.",
                        "Nobody had been dispatched - he was an impersonator using urgency and your manager's name.",
                        "Perfect. Independent verification beats any amount of pressure."));
                    break;
                case IntercomResponse.RefusedWithoutVerifying:
                    d.Add(new DebriefItem("Visitor at the door", 1,
                        "You refused the visitor without checking his story.",
                        "Keeping him out was right, but a quick check (directory, service desk, notice board) would have confirmed it.",
                        "Verify and report, so Security knows someone tried."));
                    break;
            }

            // MFA
            switch (s.mfa)
            {
                case MfaResponse.Approved:
                    d.Add(new DebriefItem("Sign-in approval", 0,
                        "You approved a sign-in request you didn't start.",
                        "That handed the attacker the last key they needed.",
                        "Never approve a prompt you didn't trigger. Deny and report it."));
                    break;
                case MfaResponse.DeniedAndReported:
                    d.Add(new DebriefItem("Sign-in approval", 2,
                        "You denied the sign-in request and reported it.",
                        "Someone had (or was guessing) your password - reporting lets Security reset it.",
                        "Spot on."));
                    break;
                case MfaResponse.Denied:
                    d.Add(new DebriefItem("Sign-in approval", 1,
                        "You denied the sign-in request.",
                        "Good - but an unexpected prompt means someone may have your password.",
                        "Choose 'Deny and report' so your password gets reset."));
                    break;
                case MfaResponse.Ignored:
                    d.Add(new DebriefItem("Sign-in approval", s.CredentialsCompromised ? 0 : 1,
                        "You let the sign-in requests go unanswered.",
                        s.CredentialsCompromised
                            ? "With your credentials already stolen, nobody raised the alarm and the attacker kept trying."
                            : "Nothing got in, but an unanswered prompt is a warning sign nobody reported.",
                        "Deny unexpected prompts and report them."));
                    break;
            }

            // Workstation
            switch (s.workstation)
            {
                case WorkstationResult.Reported:
                    d.Add(new DebriefItem("Unlocked workstation", 2,
                        "You took the password note and reported the unlocked PC.",
                        "An unlocked payroll screen plus a written password is an open door.",
                        "Exactly right."));
                    break;
                case WorkstationResult.Locked:
                    d.Add(new DebriefItem("Unlocked workstation", 1,
                        "You locked the colleague's screen.",
                        "Good, but the password note under the keyboard was still there for anyone.",
                        "Remove the note and report it so the password gets changed."));
                    break;
                case WorkstationResult.Ignored:
                    d.Add(new DebriefItem("Unlocked workstation", 0,
                        "You saw the unlocked payroll screen and left it.",
                        "Anyone walking past could read or change payroll data.",
                        "Lock it (Win+L) and report the password note."));
                    break;
            }

            if (!s.reportFiled)
            {
                d.Add(new DebriefItem("Incident report", 0,
                    "No incident report was filed.",
                    "Security can't respond to attacks nobody tells them about.",
                    "Always report suspicious activity, even if you stopped it."));
            }
            return result;
        }
    }
}
