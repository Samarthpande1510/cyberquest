namespace CyberQuest.Core
{
    /// <summary>
    /// Placeholder for the editor-only debug menu required by CLAUDE.md 2.4
    /// (force-set any GameState field, call EndingResolver.Resolve immediately).
    /// </summary>
    public class DebugMenu
    {
        // TODO (Stage 10): build the editor-only debug menu UI and register every
        // event's outcome fields here as they're implemented, so all four endings
        // can be triggered without replaying the full session.
        //
        // Registered so far:
        // - EmailEvent: GameState.emailOpened, qrScanned, emailAction
        // - FakeLoginEvent: GameState.loginEventTriggered, loginResult
        // - UsbEvent: GameState.usbFound, usbAction
        // - PriyaEvent: GameState.priyaEventTriggered, priyaResponse
        // - ImposterEvent: GameState.imposterEventTriggered, imposterResponse
        // - MfaEvent: GameState.mfaEventTriggered, mfaResponse
        // - ExitTerminalEvent: calls EndingResolver.Resolve + ScoreCalculator.Compute -
        //   the debug menu should expose a direct "force-resolve now" button too
    }
}
