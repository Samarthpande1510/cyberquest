using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CyberQuest.Core;
using CyberQuest.Events;
using CyberQuest.UI;
using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Locomotion;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using Object = UnityEngine.Object;

namespace CyberQuest.EditorTools
{
    /// <summary>
    /// Builds the CyberQuest office scene from the event scripts, configures the
    /// project for Meta Quest (OpenXR, Android) and builds the APK.
    /// Menu: CyberQuest/... or batch: -executeMethod CyberQuest.EditorTools.CyberQuestBuilder.BatchConfigure / BatchBuild
    /// </summary>
    public static class CyberQuestBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ScenePath = Root + "/Scenes/CyberQuest.unity";
        private const string ButtonPrefabPath = Root + "/Prefabs/PokeButton.prefab";
        private const string BuzzClipPath = Root + "/Audio/PhoneBuzz.wav";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string ApkPath = "Builds/CyberQuest.apk";

        private static readonly Vector3 PlayerOrigin = Vector3.zero;

        private static TMP_FontAsset _font;
        private static Material _backdropMat;

        // ------------------------------------------------------------------ menu / batch

        [MenuItem("CyberQuest/1. Configure Project for Quest")]
        public static void ConfigureProject()
        {
            ConfigurePlayerSettings();
            ConfigureXR();
            ConfigureMetaProjectConfig();
            RunMetaFixAll();
            AssetDatabase.SaveAssets();
            Debug.Log("[CyberQuest] Project configured for Meta Quest.");
        }

        [MenuItem("CyberQuest/2. Build Scene")]
        public static void BuildScene()
        {
            EnsureFolders();
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (_font == null)
            {
                Debug.LogWarning("[CyberQuest] TMP font not found at " + FontPath);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var mats = new Mats();
            _backdropMat = mats.Backdrop;
            PokeButton3D button = CreateButtonPrefab(mats);

            BuildLighting();
            BuildRoom(mats);
            GameObject cameraRig = BuildRig();
            BuildEvents(mats, button, cameraRig);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[CyberQuest] Scene built: " + ScenePath);
        }

        [MenuItem("CyberQuest/3. Build APK")]
        public static bool BuildApk()
        {
            if (!File.Exists(ScenePath))
            {
                BuildScene();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[CyberQuest] Build {summary.result}: {summary.outputPath} " +
                      $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors, {summary.totalTime})");
            return summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        }

        [MenuItem("CyberQuest/4. Activate Meta Quest Build Profile")]
        public static void ActivateMetaBuildProfile()
        {
            const string profilePath = "Assets/Settings/Build Profiles/Meta Quest.asset";
            var profile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(profilePath);
            if (profile == null)
            {
                throw new InvalidOperationException("Build profile not found: " + profilePath);
            }
            UnityEditor.Build.Profile.BuildProfile.SetActiveBuildProfile(profile);
            Debug.Log("[CyberQuest] Active build profile: " +
                      UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile()?.name);
        }

        public static void BatchActivateProfile()
        {
            RunBatch(() => { ActivateMetaBuildProfile(); AssetDatabase.SaveAssets(); return true; });
        }

        public static void BatchConfigure()
        {
            RunBatch(() => { ConfigureProject(); return true; });
        }

        public static void BatchBuild()
        {
            RunBatch(() =>
            {
                ConfigurePlayerSettings();
                ConfigureXR();
                BuildScene();
                return BuildApk();
            });
        }

        private static void RunBatch(Func<bool> step)
        {
            int code;
            try
            {
                code = step() ? 0 : 2;
            }
            catch (Exception e)
            {
                Debug.LogError("[CyberQuest] FAILED: " + e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        // ------------------------------------------------------------------ project settings

        private static void ConfigurePlayerSettings()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            PlayerSettings.companyName = "CyberQuest";
            PlayerSettings.productName = "CyberQuest";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.cyberquest.vr");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });

            // Meta's OVR scripts still use UnityEngine.Input in places; "Both" avoids runtime exceptions.
            var playerSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (playerSettings != null)
            {
                var so = new SerializedObject(playerSettings);
                var handler = so.FindProperty("activeInputHandler");
                if (handler != null && handler.intValue != 2)
                {
                    handler.intValue = 2;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        public static void ConfigureXR()
        {
            ConfigureXRFor(BuildTargetGroup.Android);
            ConfigureXRFor(BuildTargetGroup.Standalone); // editor play mode / Meta XR Simulator
        }

        private static void ConfigureXRFor(BuildTargetGroup group)
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                Directory.CreateDirectory("Assets/XR");
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasSettingsForBuildTarget(group))
            {
                perTarget.CreateDefaultSettingsForBuildTarget(group);
            }
            var xr = perTarget.SettingsForBuildTarget(group);
            xr.InitManagerOnStart = true;
            if (!xr.Manager.activeLoaders.Any(l => l != null && l.GetType().Name == "OpenXRLoader"))
            {
                XRPackageMetadataStore.AssignLoader(xr.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group);
            }
            EditorUtility.SetDirty(xr);
            EditorUtility.SetDirty(perTarget);

            var featureSet = UnityEditor.XR.OpenXR.Features.OpenXRFeatureSetManager
                .GetFeatureSetWithId(group, "com.meta.openxr.featureset.metaxr");
            if (featureSet != null)
            {
                featureSet.isEnabled = true;
                UnityEditor.XR.OpenXR.Features.OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(group);
            }

            var oxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (oxr != null)
            {
                string[] wanted =
                {
                    "MetaQuestFeature", "MetaXRFeature", "MetaXRFoveationFeature",
                    "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile", "MetaQuestTouchProControllerProfile",
                };
                foreach (var feature in oxr.GetFeatures())
                {
                    if (wanted.Contains(feature.GetType().Name) && !feature.enabled)
                    {
                        feature.enabled = true;
                        EditorUtility.SetDirty(feature);
                    }
                }
                EditorUtility.SetDirty(oxr);
                Debug.Log("[CyberQuest] OpenXR " + group + " features enabled: " +
                          string.Join(", ", oxr.GetFeatures().Where(f => f.enabled).Select(f => f.GetType().Name)));
            }
        }

        private static void ConfigureMetaProjectConfig()
        {
            // OVRProjectConfig lives in Meta's editor assembly; set hand tracking via reflection.
            var type = FindType("OVRProjectConfig");
            if (type == null) return;
            var config = type.GetProperty("CachedProjectConfig", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var field = type.GetField("handTrackingSupport");
            if (config == null || field == null) return;
            field.SetValue(config, Enum.ToObject(field.FieldType, 1)); // ControllersAndHands
            type.GetMethod("CommitProjectConfig", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new[] { config });
        }

        private static void RunMetaFixAll()
        {
            try
            {
                var setup = FindType("OVRProjectSetup");
                var fix = setup?.GetMethod("FixTasks", BindingFlags.NonPublic | BindingFlags.Static);
                if (fix == null)
                {
                    Debug.LogWarning("[CyberQuest] OVRProjectSetup.FixTasks not found; skipping Meta Fix All.");
                    return;
                }
                var logEnum = fix.GetParameters()[2].ParameterType;
                fix.Invoke(null, new object[] { BuildTargetGroup.Android, null, Enum.ToObject(logEnum, 3), true, null, false });
                Debug.Log("[CyberQuest] Meta Project Setup Tool: Fix All requested for Android.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CyberQuest] Meta Fix All failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ assets

        private static void EnsureFolders()
        {
            foreach (var dir in new[] { "Scenes", "Prefabs", "Materials", "Audio" })
            {
                Directory.CreateDirectory(Path.Combine(Root, dir));
            }
            AssetDatabase.Refresh();
        }

        private class Mats
        {
            public readonly Material Floor = Make("Floor", new Color(0.32f, 0.34f, 0.38f));
            public readonly Material Wall = Make("Wall", new Color(0.82f, 0.83f, 0.86f));
            public readonly Material Desk = Make("Desk", new Color(0.45f, 0.31f, 0.2f));
            public readonly Material Metal = Make("Metal", new Color(0.18f, 0.19f, 0.21f));
            public readonly Material Button = Make("Button", new Color(0.2f, 0.45f, 0.85f));
            public readonly Material Usb = Make("Usb", new Color(0.8f, 0.12f, 0.12f));
            public readonly Material Priya = Make("Priya", new Color(0.85f, 0.45f, 0.6f));
            public readonly Material Imposter = Make("Imposter", new Color(0.15f, 0.2f, 0.4f));
            public readonly Material Skin = Make("Skin", new Color(0.86f, 0.68f, 0.55f));
            public readonly Material ExitDoor = Make("ExitDoor", new Color(0.65f, 0.15f, 0.12f));
            public readonly Material Badge = Make("Badge", Color.white);
            public readonly Material Backdrop = Make("Backdrop", new Color(0.96f, 0.96f, 0.96f), unlit: true);

            private static Material Make(string name, Color color, bool unlit = false)
            {
                string path = $"{Root}/Materials/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = shader;
                mat.SetColor("_BaseColor", color);
                mat.color = color;
                EditorUtility.SetDirty(mat);
                return mat;
            }
        }

        private static AudioClip CreateBuzzClip()
        {
            if (!File.Exists(BuzzClipPath))
            {
                const int rate = 22050;
                int samples = (int)(rate * 0.9f);
                using (var w = new BinaryWriter(File.Create(BuzzClipPath)))
                {
                    w.Write("RIFF".ToCharArray()); w.Write(36 + samples * 2); w.Write("WAVE".ToCharArray());
                    w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
                    w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                    w.Write("data".ToCharArray()); w.Write(samples * 2);
                    for (int i = 0; i < samples; i++)
                    {
                        float t = i / (float)rate;
                        bool on = (t % 0.45f) < 0.3f;
                        float s = on ? Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 170f * t)) * 0.35f : 0f;
                        w.Write((short)(s * short.MaxValue));
                    }
                }
                AssetDatabase.ImportAsset(BuzzClipPath);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(BuzzClipPath);
        }

        /// <summary>Poke + ray pressable 3D button used as every event's pokeButtonTemplate.</summary>
        private static PokeButton3D CreateButtonPrefab(Mats mats)
        {
            var size = new Vector3(0.085f, 0.04f, 0.012f);

            var root = new GameObject("PokeButton");

            var surfaceGo = new GameObject("Surface");
            surfaceGo.transform.SetParent(root.transform, false);
            surfaceGo.transform.localPosition = new Vector3(0f, 0f, -size.z / 2f);
            var plane = surfaceGo.AddComponent<PlaneSurface>();
            plane.InjectAllPlaneSurface(PlaneSurface.NormalFacing.Backward, false);
            var clipper = surfaceGo.AddComponent<BoundsClipper>();
            clipper.Size = new Vector3(size.x, size.y, 0.02f);
            var clipped = surfaceGo.AddComponent<ClippedPlaneSurface>();
            clipped.InjectAllClippedPlaneSurface(plane, new IBoundsClipper[] { clipper });

            var poke = root.AddComponent<PokeInteractable>();
            poke.InjectAllPokeInteractable(clipped);
            var wrapper = root.AddComponent<PointableUnityEventWrapper>();
            wrapper.InjectAllPointableUnityEventWrapper(poke);
            root.AddComponent<PokeButton3D>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = size;
            visual.GetComponent<MeshRenderer>().sharedMaterial = mats.Button;

            var label = CreateLabel(root.transform, "Label", new Vector3(0f, 0f, -size.z / 2f - 0.001f), 0.12f, "Button", Color.white);
            label.rectTransform.sizeDelta = new Vector2(size.x - 0.006f, size.y - 0.004f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.04f;
            label.fontSizeMax = 0.12f;
            label.textWrappingMode = TextWrappingModes.Normal;

            // Ray press support (controller / hand pointer) alongside poke.
            var rayGo = new GameObject("Ray");
            rayGo.transform.SetParent(root.transform, false);
            var box = rayGo.AddComponent<BoxCollider>();
            box.size = size;
            var colliderSurface = rayGo.AddComponent<ColliderSurface>();
            colliderSurface.InjectAllColliderSurface(box);
            var ray = rayGo.AddComponent<RayInteractable>();
            ray.InjectAllRayInteractable(colliderSurface);
            var rayWrapper = rayGo.AddComponent<PointableUnityEventWrapper>();
            rayWrapper.InjectAllPointableUnityEventWrapper(ray);
            rayGo.AddComponent<PressForwarder>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ButtonPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<PokeButton3D>();
        }

        // ------------------------------------------------------------------ scene

        private static void BuildLighting()
        {
            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);
        }

        private static void BuildRoom(Mats m)
        {
            var room = new GameObject("Office").transform;

            var floor = Box("Floor", room, new Vector3(0f, -0.05f, 0.25f), new Vector3(5f, 0.1f, 4.5f), m.Floor);
            Box("CorridorFloor_Entrance", room, new Vector3(3.3f, -0.05f, -0.8f), new Vector3(1.6f, 0.1f, 1.2f), m.Floor);
            Box("CorridorFloor_Exit", room, new Vector3(1.5f, -0.05f, -2.8f), new Vector3(1.2f, 0.1f, 1.6f), m.Floor);
            MakeTeleportable(floor);

            // Walls (entrance doorway on the right wall, exit doorway on the back wall).
            Box("Wall_Front", room, new Vector3(0f, 1.3f, 2.55f), new Vector3(5.2f, 2.6f, 0.1f), m.Wall);
            Box("Wall_Left", room, new Vector3(-2.55f, 1.3f, 0.25f), new Vector3(0.1f, 2.6f, 4.7f), m.Wall);
            Box("Wall_Right_A", room, new Vector3(2.55f, 1.3f, -1.65f), new Vector3(0.1f, 2.6f, 0.7f), m.Wall);
            Box("Wall_Right_B", room, new Vector3(2.55f, 1.3f, 1.1f), new Vector3(0.1f, 2.6f, 2.8f), m.Wall);
            Box("Wall_Right_Lintel", room, new Vector3(2.55f, 2.35f, -0.8f), new Vector3(0.1f, 0.5f, 1.0f), m.Wall);
            Box("Wall_Back_A", room, new Vector3(-0.75f, 1.3f, -2.05f), new Vector3(3.5f, 2.6f, 0.1f), m.Wall);
            Box("Wall_Back_B", room, new Vector3(2.25f, 1.3f, -2.05f), new Vector3(0.5f, 2.6f, 0.1f), m.Wall);
            Box("Wall_Back_Lintel", room, new Vector3(1.5f, 2.35f, -2.05f), new Vector3(1.0f, 0.5f, 0.1f), m.Wall);

            Desk("PlayerDesk", room, new Vector3(0f, 0f, 0.75f), new Vector2(1.4f, 0.7f), m);
            Desk("EmptyDesk", room, new Vector3(-1.3f, 0f, 0.5f), new Vector2(1.0f, 0.6f), m);

            // Staff directory kiosk - lists the real department name the imposter gets wrong.
            var kiosk = new GameObject("DirectoryKiosk").transform;
            kiosk.SetParent(room, false);
            kiosk.position = new Vector3(2.47f, 1.45f, 1.2f);
            kiosk.rotation = Quaternion.LookRotation(Vector3.right);
            Box("Board", kiosk, Vector3.zero, new Vector3(0.62f, 0.5f, 0.03f), m.Metal, space: Space.Self);
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = "Face";
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.transform.SetParent(kiosk, false);
            face.transform.localPosition = new Vector3(0f, 0f, -0.017f);
            face.transform.localScale = new Vector3(0.58f, 0.46f, 1f);
            face.GetComponent<MeshRenderer>().sharedMaterial = m.Backdrop;
            var dir = CreateLabel(kiosk, "Directory", new Vector3(0f, 0f, -0.02f), 0.2f,
                "<b>STAFF DIRECTORY</b>\n\n" +
                "IT INFRASTRUCTURE      Floor 3   x4410\n" +
                "FACILITIES             Floor 1   x2210\n" +
                "HUMAN RESOURCES        Floor 2   x3120\n" +
                "SECURITY OFFICE        Lobby     x1000", Color.black);
            dir.rectTransform.sizeDelta = new Vector2(0.54f, 0.42f);
            dir.alignment = TextAlignmentOptions.Center;
        }

        private static void Desk(string name, Transform parent, Vector3 pos, Vector2 size, Mats m)
        {
            var desk = new GameObject(name).transform;
            desk.SetParent(parent, false);
            desk.position = pos;
            Box("Top", desk, new Vector3(0f, 0.73f, 0f), new Vector3(size.x, 0.04f, size.y), m.Desk, space: Space.Self);
            float lx = size.x / 2f - 0.05f, lz = size.y / 2f - 0.05f;
            foreach (var (x, z) in new[] { (-lx, -lz), (lx, -lz), (-lx, lz), (lx, lz) })
            {
                Box("Leg", desk, new Vector3(x, 0.355f, z), new Vector3(0.05f, 0.71f, 0.05f), m.Metal, space: Space.Self);
            }
        }

        private static GameObject BuildRig()
        {
            // Camera rig set up the way Meta's "Add OVR Comprehensive Interaction Rig" wizard does it.
            var camPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
            var cameraRig = (GameObject)PrefabUtility.InstantiatePrefab(camPrefab);
            cameraRig.transform.SetPositionAndRotation(PlayerOrigin, Quaternion.identity);
            var ovrManager = cameraRig.GetComponent<OVRManager>();
            if (ovrManager != null)
            {
                var so = new SerializedObject(ovrManager);
                var origin = so.FindProperty("_trackingOriginType");
                if (origin != null) origin.enumValueIndex = (int)OVRManager.TrackingOrigin.FloorLevel;
                so.ApplyModifiedPropertiesWithoutUndo();
                ovrManager.controllerDrivenHandPosesType = OVRManager.ControllerDrivenHandPosesType.ConformingToController;
            }

            if (!TryRunInteractionRigWizard())
            {
                Debug.LogWarning("[CyberQuest] Wizard unavailable - adding the comprehensive interaction rig prefab directly.");
                var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Packages/com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/OVRComprehensiveInteractionRig.prefab");
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, cameraRig.transform);
                var ovrRig = cameraRig.GetComponent<OVRCameraRig>();
                foreach (var rigRef in rig.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null && c.GetType().Name == "OVRCameraRigRef"))
                {
                    var so = new SerializedObject(rigRef);
                    so.FindProperty("_ovrCameraRig").objectReferenceValue = ovrRig;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Debug.Log($"[CyberQuest] Rig: poke={cameraRig.GetComponentsInChildren<PokeInteractor>(true).Length} " +
                      $"ray={cameraRig.GetComponentsInChildren<RayInteractor>(true).Length} " +
                      $"teleport={cameraRig.GetComponentsInChildren<TeleportInteractor>(true).Length}");
            return cameraRig;
        }

        private static bool TryRunInteractionRigWizard()
        {
            try
            {
                var wizardType = FindType("OVRComprehensiveInteractionRigWizard");
                var baseType = FindType("QuickActionsWizard");
                var create = baseType?.GetMethod("CreateWithDefaults", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (wizardType == null || create == null)
                {
                    return false;
                }
                var result = create.MakeGenericMethod(wizardType).Invoke(null, new object[] { null, true, null });
                int count = 0;
                if (result is IEnumerable created)
                {
                    foreach (var _ in created) count++;
                }
                Debug.Log($"[CyberQuest] Interaction rig wizard created {count} object(s).");
                return Object.FindObjectsByType<PokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CyberQuest] Interaction rig wizard failed: " + (e.InnerException ?? e).Message);
                return false;
            }
        }

        private static void BuildEvents(Mats m, PokeButton3D button, GameObject cameraRig)
        {
            var events = new GameObject("CyberQuest").transform;

            // --- Core systems
            var systems = new GameObject("Systems");
            systems.transform.SetParent(events, false);
            systems.AddComponent<SessionTimer>();

            var cc = cameraRig.AddComponent<UnityEngine.CharacterController>();
            cc.height = 1.7f;
            cc.radius = 0.25f;
            cc.center = new Vector3(0f, 0.85f, 0f);
            cc.enabled = false; // only needed if smooth locomotion is switched on
            var smooth = cameraRig.AddComponent<SmoothLocomotion>();
            Set(smooth, "headTransform", cameraRig.GetComponent<OVRCameraRig>().centerEyeAnchor);
            var toggle = systems.AddComponent<LocomotionToggle>();
            Set(toggle, "smoothLocomotion", smooth);
            Set(toggle, "teleportInteractorsRoot", cameraRig.transform);

            // --- Event 1-2: phishing email on the desk monitor -> fake login on the phone
            var monitor = new GameObject("DeskMonitor").transform;
            monitor.SetParent(events, false);
            monitor.position = new Vector3(0f, 1.12f, 0.86f);
            Box("Body", monitor, Vector3.zero, new Vector3(0.5f, 0.34f, 0.03f), m.Metal, space: Space.Self);
            Box("Stand", monitor, new Vector3(0f, -0.26f, 0.01f), new Vector3(0.06f, 0.2f, 0.04f), m.Metal, space: Space.Self);

            var phone = new GameObject("Phone").transform;
            phone.SetParent(events, false);
            phone.position = new Vector3(0.5f, 0.95f, 0.6f);
            phone.rotation = FacePlayer(phone.position) * Quaternion.Euler(15f, 0f, 0f);
            Box("Body", phone, new Vector3(0f, 0.025f, 0.01f), new Vector3(0.37f, 0.23f, 0.015f), m.Metal, space: Space.Self);
            Box("Stand", phone, new Vector3(0f, -0.12f, 0.05f), new Vector3(0.1f, 0.18f, 0.03f), m.Metal, space: Space.Self);

            var fakeLoginGo = Child(phone, "FakeLogin", new Vector3(0f, 0f, -0.006f));
            var fakeLogin = fakeLoginGo.AddComponent<FakeLoginEvent>();
            Set(fakeLogin, "pokeButtonTemplate", button);
            Set(fakeLogin, "font", _font);
            Backdrop(fakeLoginGo, "Screen", new Vector2(0.35f, 0.21f), new Vector2(0f, 0.025f));

            var emailGo = Child(monitor, "EmailTerminal", new Vector3(0f, 0f, -0.025f));
            var email = emailGo.AddComponent<EmailEvent>();
            Set(email, "pokeButtonTemplate", button);
            Set(email, "font", _font);
            Set(email, "fakeLoginEvent", fakeLogin);
            Backdrop(emailGo, "Screen", new Vector2(0.47f, 0.26f), Vector2.zero);

            // --- Event 6: MFA push on the phone
            var mfaAnchor = new GameObject("MfaNotificationAnchor");
            mfaAnchor.transform.SetParent(events, false);
            mfaAnchor.transform.position = new Vector3(0.5f, 1.3f, 0.65f);
            mfaAnchor.transform.rotation = FacePlayer(mfaAnchor.transform.position);
            Backdrop(mfaAnchor, "Panel", new Vector2(0.3f, 0.18f), new Vector2(0f, 0.02f));
            var audio = phone.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            var mfa = phone.gameObject.AddComponent<MfaEvent>();
            Set(mfa, "pokeButtonTemplate", button);
            Set(mfa, "font", _font);
            Set(mfa, "panelAnchor", mfaAnchor.transform);
            Set(mfa, "audioSource", audio);
            Set(mfa, "buzzClip", CreateBuzzClip());

            // --- Event 3: planted USB drive on the empty desk
            var usbAnchor = new GameObject("UsbPanelAnchor");
            usbAnchor.transform.SetParent(events, false);
            usbAnchor.transform.position = new Vector3(-1.0f, 1.2f, 0.75f);
            usbAnchor.transform.rotation = FacePlayer(usbAnchor.transform.position);
            Backdrop(usbAnchor, "Panel", new Vector2(0.34f, 0.16f), new Vector2(0f, 0.015f));
            BuildUsb(events, m, button, usbAnchor.transform);

            // --- Event 4: Priya (false positive) appears at the entrance
            var priyaVisuals = Npc("Priya", events, new Vector3(2.0f, 0f, -0.8f), m.Priya, m, null);
            var priyaAnchor = new GameObject("PriyaPanelAnchor");
            priyaAnchor.transform.SetParent(events, false);
            priyaAnchor.transform.position = new Vector3(1.75f, 1.45f, -0.05f); // beside her, not over her face
            priyaAnchor.transform.rotation = FacePlayer(priyaAnchor.transform.position);
            Backdrop(priyaAnchor, "Panel", new Vector2(0.44f, 0.2f), new Vector2(0f, 0.04f));
            var priya = new GameObject("PriyaEvent").AddComponent<PriyaEvent>();
            priya.transform.SetParent(events, false);
            Set(priya, "visualsRoot", priyaVisuals);
            Set(priya, "pokeButtonTemplate", button);
            Set(priya, "font", _font);
            Set(priya, "panelAnchor", priyaAnchor.transform);

            // --- Event 5: imposter walks in from the entrance
            var doorPoint = new GameObject("ImposterDoorPoint").transform;
            doorPoint.SetParent(events, false);
            doorPoint.SetPositionAndRotation(new Vector3(3.3f, 0f, -0.8f), Quaternion.LookRotation(Vector3.left));
            var stopPoint = new GameObject("ImposterStopPoint").transform;
            stopPoint.SetParent(events, false);
            stopPoint.position = new Vector3(0.95f, 0f, 0.05f);
            stopPoint.rotation = Quaternion.LookRotation(Flat(PlayerOrigin - stopPoint.position));
            var imposterVisuals = Npc("Imposter", events, doorPoint.position, m.Imposter, m, "IT SERVICES");
            var imposterAnchor = new GameObject("ImposterPanelAnchor");
            imposterAnchor.transform.SetParent(events, false);
            imposterAnchor.transform.position = new Vector3(0.75f, 1.55f, 0.35f);
            imposterAnchor.transform.rotation = FacePlayer(imposterAnchor.transform.position);
            Backdrop(imposterAnchor, "Panel", new Vector2(0.46f, 0.28f), new Vector2(0f, 0.035f));
            var imposter = new GameObject("ImposterEvent").AddComponent<ImposterEvent>();
            imposter.transform.SetParent(events, false);
            Set(imposter, "visualsRoot", imposterVisuals);
            Set(imposter, "doorPoint", doorPoint);
            Set(imposter, "stopPoint", stopPoint);
            Set(imposter, "pokeButtonTemplate", button);
            Set(imposter, "font", _font);
            Set(imposter, "panelAnchor", imposterAnchor.transform);

            // --- Exit terminal + results + locked exit door
            var exitDoor = Box("ExitDoor", events, new Vector3(1.5f, 1.05f, -2.05f), new Vector3(1.0f, 2.1f, 0.08f), m.ExitDoor);

            var terminal = new GameObject("ExitTerminal").transform;
            terminal.SetParent(events, false);
            terminal.position = new Vector3(0.6f, 0f, -1.7f);
            terminal.rotation = FacePlayer(terminal.position);
            Box("Pedestal", terminal, new Vector3(0f, 0.5f, 0.05f), new Vector3(0.35f, 1.0f, 0.3f), m.Metal, space: Space.Self);
            var buttonAnchor = Child(terminal, "ButtonAnchor", new Vector3(0f, 1.08f, -0.02f)).transform;
            Box("Panel", terminal, new Vector3(0f, 1.1f, 0.0f), new Vector3(0.3f, 0.16f, 0.03f), m.Metal, space: Space.Self);
            var sign = CreateLabel(terminal, "Sign", new Vector3(0f, 1.15f, -0.017f), 0.12f, "EXIT SECURITY TERMINAL", Color.white);
            sign.rectTransform.sizeDelta = new Vector2(0.28f, 0.03f);

            var resultsGo = new GameObject("ResultsScreen");
            resultsGo.transform.SetParent(events, false);
            resultsGo.transform.position = new Vector3(0.6f, 1.55f, -1.75f);
            resultsGo.transform.rotation = FacePlayer(resultsGo.transform.position);
            var results = resultsGo.AddComponent<ResultsScreen>();
            Set(results, "font", _font);
            Backdrop(resultsGo, "Panel", new Vector2(0.5f, 0.3f), new Vector2(0f, 0.03f));

            var exit = terminal.gameObject.AddComponent<ExitTerminalEvent>();
            Set(exit, "pokeButtonTemplate", button);
            Set(exit, "buttonAnchor", buttonAnchor);
            Set(exit, "resultsScreen", results);
            Set(exit, "exitDoorBlocker", exitDoor);
        }

        private static void BuildUsb(Transform parent, Mats m, PokeButton3D button, Transform panelAnchor)
        {
            var usb = new GameObject("UsbDrive");
            usb.transform.SetParent(parent, false);
            usb.transform.position = new Vector3(-1.25f, 0.775f, 0.45f);
            usb.transform.rotation = Quaternion.Euler(0f, 25f, 0f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(usb.transform, false);
            body.transform.localScale = new Vector3(0.07f, 0.02f, 0.025f);
            body.GetComponent<MeshRenderer>().sharedMaterial = m.Usb;
            var plug = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plug.name = "Plug";
            Object.DestroyImmediate(plug.GetComponent<Collider>());
            plug.transform.SetParent(usb.transform, false);
            plug.transform.localPosition = new Vector3(0.043f, 0f, 0f);
            plug.transform.localScale = new Vector3(0.016f, 0.008f, 0.014f);
            plug.GetComponent<MeshRenderer>().sharedMaterial = m.Metal;

            var col = usb.AddComponent<BoxCollider>();
            col.size = new Vector3(0.086f, 0.02f, 0.025f);
            col.center = new Vector3(0.008f, 0f, 0f);
            var rb = usb.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var grabbable = usb.AddComponent<Grabbable>();
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.InjectOptionalThrowWhenUnselected(false);

            var interactables = new GameObject("Interactables");
            interactables.transform.SetParent(usb.transform, false);
            var grab = interactables.AddComponent<GrabInteractable>();
            grab.InjectAllGrabInteractable(rb);
            grab.InjectOptionalPointableElement(grabbable);
            var handGrab = interactables.AddComponent<HandGrabInteractable>();
            handGrab.InjectAllHandGrabInteractable(GrabTypeFlags.All, rb, GrabbingRule.DefaultPinchRule, GrabbingRule.DefaultPalmRule);
            handGrab.InjectOptionalPointableElement(grabbable);

            var wrapper = usb.AddComponent<PointableUnityEventWrapper>();
            wrapper.InjectAllPointableUnityEventWrapper(grabbable);
            var usbEvent = usb.AddComponent<UsbEvent>();
            Set(usbEvent, "pokeButtonTemplate", button);
            Set(usbEvent, "font", _font);
            Set(usbEvent, "panelAnchor", panelAnchor);
        }

        private static GameObject Npc(string name, Transform parent, Vector3 pos, Material clothes, Mats m, string badge)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.LookRotation(Flat(PlayerOrigin - pos));

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localScale = new Vector3(0.45f, 0.8f, 0.3f);
            body.GetComponent<MeshRenderer>().sharedMaterial = clothes;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.72f, 0f);
            head.transform.localScale = Vector3.one * 0.22f;
            head.GetComponent<MeshRenderer>().sharedMaterial = m.Skin;

            if (badge != null)
            {
                var badgeRoot = new GameObject("Badge").transform;
                badgeRoot.SetParent(root.transform, false);
                badgeRoot.localPosition = new Vector3(0.08f, 1.25f, 0.155f);
                badgeRoot.localRotation = Quaternion.Euler(0f, 180f, 0f); // readable from in front of the NPC
                var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(card.GetComponent<Collider>());
                card.transform.SetParent(badgeRoot, false);
                card.transform.localScale = new Vector3(0.1f, 0.06f, 1f);
                card.GetComponent<MeshRenderer>().sharedMaterial = m.Badge;
                var text = CreateLabel(badgeRoot, "BadgeText", new Vector3(0f, 0f, -0.001f), 0.11f, "NEXORA\n<b>" + badge + "</b>", Color.black);
                text.rectTransform.sizeDelta = new Vector2(0.09f, 0.05f);
            }
            return root;
        }

        // ------------------------------------------------------------------ helpers

        private static void MakeTeleportable(GameObject floor)
        {
            var surface = floor.AddComponent<ColliderSurface>();
            surface.InjectAllColliderSurface(floor.GetComponent<Collider>());
            var teleport = floor.AddComponent<TeleportInteractable>();
            teleport.InjectAllTeleportInteractable(surface);
        }

        private static void Backdrop(GameObject host, string panelName, Vector2 size, Vector2 offset)
        {
            var backdrop = host.AddComponent<PanelBackdrop>();
            var so = new SerializedObject(backdrop);
            so.FindProperty("panelChildName").stringValue = panelName;
            so.FindProperty("size").vector2Value = size;
            so.FindProperty("offset").vector2Value = offset;
            so.FindProperty("material").objectReferenceValue = _backdropMat;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Space space = Space.World)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (space == Space.World)
            {
                go.transform.position = pos;
            }
            else
            {
                go.transform.localPosition = pos;
            }
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject Child(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        private static TextMeshPro CreateLabel(Transform parent, string name, Vector3 localPos, float fontSize, string text, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            if (_font != null) tmp.font = _font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }

        /// <summary>Rotation whose forward points away from the player, so TMP text / panels read correctly.</summary>
        private static Quaternion FacePlayer(Vector3 pos)
        {
            return Quaternion.LookRotation(Flat(pos - PlayerOrigin));
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'");
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                var match = types.FirstOrDefault(t => t.Name == name);
                if (match != null) return match;
            }
            return null;
        }
    }
}
