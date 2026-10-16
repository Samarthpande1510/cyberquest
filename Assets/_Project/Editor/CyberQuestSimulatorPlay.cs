using System;
using System.IO;
using CyberQuest.Dev;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CyberQuest.EditorTools
{
    /// <summary>
    /// Play the CyberQuest scene in Meta XR Simulator, optionally running the
    /// automated playthrough. Also picks up Temp/cq_playtest.request so a playtest
    /// can be started from outside the editor.
    /// </summary>
    [InitializeOnLoad]
    public static class CyberQuestSimulatorPlay
    {
        private const string ScenePath = "Assets/_Project/Scenes/CyberQuest.unity";
        private const string RequestFile = "Temp/cq_playtest.request";
        private const string ActivateMenu = "Window/Meta/Meta XR Simulator/Activate";

        private static double _playAt;

        static CyberQuestSimulatorPlay()
        {
            EditorApplication.update += PollRequest;
        }

        [MenuItem("CyberQuest/5. Play in Meta XR Simulator")]
        public static void PlayInSimulator()
        {
            Start(autoTest: false);
        }

        [MenuItem("CyberQuest/6. Automated Playtest in Simulator")]
        public static void AutoPlaytest()
        {
            Start(autoTest: true);
        }

        private static void PollRequest()
        {
            if (_playAt > 0 && EditorApplication.timeSinceStartup >= _playAt)
            {
                _playAt = 0;
                EditorApplication.EnterPlaymode();
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || !File.Exists(RequestFile))
            {
                return;
            }
            string request = File.ReadAllText(RequestFile);
            bool rebuild = request.Contains("rebuild");
            File.Delete(RequestFile);
            if (request.Contains("apk"))
            {
                bool ok = CyberQuestBuilder.BuildApk();
                File.WriteAllText("Builds/apk_result.txt", ok ? "SUCCEEDED" : "FAILED");
                return;
            }
            if (rebuild)
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                CyberQuestBuilder.BuildScene();
            }
            Start(autoTest: true);
        }

        private static void Start(bool autoTest)
        {
            try
            {
                CyberQuestBuilder.ConfigureXR();
                AssetDatabase.SaveAssets();

                if (!EditorApplication.ExecuteMenuItem(ActivateMenu))
                {
                    Debug.Log("[CyberQuest] Simulator activate menu unavailable (already active or not installed).");
                }

                if (EditorSceneManager.GetActiveScene().path != ScenePath)
                {
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                    EditorSceneManager.OpenScene(ScenePath);
                }

                if (autoTest)
                {
                    Directory.CreateDirectory("Temp");
                    File.WriteAllText(CyberQuestAutoPlaytest.ActiveFlag, DateTime.Now.ToString("O"));
                }
                else if (File.Exists(CyberQuestAutoPlaytest.ActiveFlag))
                {
                    File.Delete(CyberQuestAutoPlaytest.ActiveFlag);
                }

                // Give the simulator a few seconds to register as the OpenXR runtime.
                _playAt = EditorApplication.timeSinceStartup + 4.0;
                Debug.Log($"[CyberQuest] Entering play mode in Meta XR Simulator (autoTest={autoTest})...");
            }
            catch (Exception e)
            {
                Debug.LogError("[CyberQuest] Could not start simulator play: " + e);
            }
        }
    }
}
