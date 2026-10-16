using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace CyberQuest.EditorTools
{
    /// <summary>
    /// Runs the EditMode tests inside the open editor when Temp/cq_tests.request
    /// appears, and writes Builds/test_results.txt. Equivalent to the batch-mode
    /// test run in CLAUDE.md 11, without having to close Unity.
    /// Menu: CyberQuest > 7. Run EditMode Tests.
    /// </summary>
    [InitializeOnLoad]
    public static class EditModeTestHook
    {
        private const string RequestFile = "Temp/cq_tests.request";
        private const string ResultFile = "Builds/test_results.txt";

        static EditModeTestHook()
        {
            EditorApplication.update += Poll;
        }

        [MenuItem("CyberQuest/7. Run EditMode Tests")]
        public static void Run()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        private static void Poll()
        {
            if (!File.Exists(RequestFile) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            File.Delete(RequestFile);
            Run();
        }

        private class Callbacks : ICallbacks
        {
            private readonly StringBuilder _log = new StringBuilder();

            public void RunStarted(ITestAdaptor testsToRun)
            {
                _log.AppendLine("EditMode tests started");
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren)
                {
                    _log.AppendLine($"{result.TestStatus,-8} {result.FullName}" +
                                    (result.TestStatus == TestStatus.Failed ? "\n         " + result.Message : ""));
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                _log.AppendLine($"TOTAL passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}");
                Directory.CreateDirectory("Builds");
                File.WriteAllText(ResultFile, _log.ToString());
                Debug.Log("[CyberQuest] " + _log.ToString().Trim().Split('\n')[^1]);
            }
        }
    }
}
