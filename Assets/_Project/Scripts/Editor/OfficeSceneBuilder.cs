using System;
using System.Collections;
using System.IO;
using System.Linq;
using CyberQuest.Core;
using CyberQuest.Props;
using CyberQuest.Situations;
using CyberQuest.UI;
using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Locomotion;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CyberQuest.EditorTools
{
    /// <summary>
    /// Generates Scenes/Office_Main.unity from scratch (safe to re-run): the
    /// seven-space office floor of CLAUDE.md 5.0, a night skyline, the player
    /// rig and every situation. Menu: CyberQuest > 2. Build Office Scene.
    ///
    /// Floor plan (x = east, z = north, metres, 3 m ceilings):
    ///   Open plan    x 0-10,  z 0-8   (spawn, player desk, empty desk)
    ///   Corridor     x 0-18,  z 8-10  + south leg x 10-12, z 2-8 (L-shaped spine)
    ///   Break room   x 12-18, z 0-8
    ///   Lobby        x 18-24, z 0-10  (main door / exit, intercom, terminal, directory)
    ///   Finance pod  x 0-6.5, z 10-16
    ///   Meeting Rm 3 x 6.5-13.5, z 10-16 (glass door, locked)
    ///   IT room      x 13.5-20, z 10-16 (locked)
    /// </summary>
    public static class OfficeSceneBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ButtonPrefabPath = Root + "/Prefabs/PokeButton.prefab";
        private const string ThemePath = Root + "/Resources/" + UiTheme.ResourcePath + ".asset";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const float H = 3f;
        private const float WallT = 0.12f;

        public static readonly Vector3 PlayerOrigin = new Vector3(3f, 0f, 2.35f);

        private static TMP_FontAsset _font;
        private static Material _fontMaterial;

        private struct Gap
        {
            public readonly float C, W, H;

            public Gap(float center, float width, float height = 2.3f)
            {
                C = center;
                W = width;
                H = height;
            }
        }

        // ------------------------------------------------------------------ entry

        public static void Build()
        {
            foreach (var dir in new[] { "Scenes", "Prefabs", "Materials", "Audio", "Textures", "Resources" })
            {
                Directory.CreateDirectory(Path.Combine(Root, dir));
            }
            AssetDatabase.Refresh();
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            _fontMaterial = CreateUrpFontMaterial(_font);

            EnsureEnvironmentLayer();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var m = new Mats();
            var button = CreateButtonPrefab(m);
            CreateTheme(m, button);

            BuildLighting(m);
            var shell = BuildShell(m);
            var furniture = BuildFurniture(m);
            BuildSkyline(m);
            var rig = BuildRig();
            BuildGameplay(m, rig);
            BuildRoomZones();
            MarkStatic(shell);
            MarkStatic(furniture);
            AssignBlockingLayer(rig);

            EditorSceneManager.SaveScene(scene, CyberQuestBuilder.ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(CyberQuestBuilder.ScenePath, true) };
            AssetDatabase.SaveAssets();
            WriteFloorPlan();
            Debug.Log("[CyberQuest] Office scene built: " + CyberQuestBuilder.ScenePath);
        }

        // ------------------------------------------------------------------ textures / materials / audio

        private static float Hash(int x, int y, int seed)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f + seed * 74.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private static Texture2D MakeTexture(string name, int w, int h, Func<int, int, Color> px, bool clamp = false, bool point = false)
        {
            string path = $"{Root}/Textures/{name}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var cols = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    cols[y * w + x] = px(x, y);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.filterMode = point ? FilterMode.Point : FilterMode.Trilinear;
            importer.mipmapEnabled = !point;
            importer.anisoLevel = point ? 0 : 4;
            importer.textureCompression = point ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private class Textures
        {
            public readonly Texture2D Carpet = MakeTexture("Carpet", 256, 256, (x, y) =>
            {
                float n = ValueNoise(x / 6f, y / 6f, 1) * 0.5f + Hash(x, y, 2) * 0.5f;
                bool stripe = (x / 32 + y / 32) % 2 == 0;
                float v = 0.2f + n * 0.06f + (stripe ? 0.012f : 0f);
                return new Color(v * 0.95f, v * 1.0f, v * 1.18f);
            });

            public readonly Texture2D Tile = MakeTexture("Tile", 128, 128, (x, y) =>
            {
                bool grout = x % 64 < 2 || y % 64 < 2;
                float v = grout ? 0.55f : 0.86f - Hash(x / 64, y / 64, 15) * 0.06f - Hash(x, y, 16) * 0.02f;
                return new Color(v, v * 0.98f, v * 0.95f);
            });

            public readonly Texture2D Stone = MakeTexture("Stone", 256, 256, (x, y) =>
            {
                float n = ValueNoise(x / 20f, y / 20f, 17) * 0.08f + ValueNoise(x / 4f, y / 4f, 18) * 0.04f;
                bool seam = x % 128 < 1 || y % 128 < 1;
                float v = seam ? 0.5f : 0.66f + n;
                return new Color(v, v * 0.99f, v * 0.97f);
            });

            public readonly Texture2D CeilingTile = MakeTexture("CeilingTile", 128, 128, (x, y) =>
            {
                bool edge = x < 3 || y < 3 || x > 124 || y > 124;
                float speck = Hash(x, y, 3) > 0.93f ? 0.06f : 0f;
                float v = edge ? 0.62f : 0.9f - speck;
                return new Color(v, v, v * 0.98f);
            });

            public readonly Texture2D Wall = MakeTexture("Wall", 128, 128, (x, y) =>
            {
                float n = ValueNoise(x / 9f, y / 9f, 4) * 0.04f + Hash(x, y, 5) * 0.02f;
                return new Color(0.96f - n, 0.95f - n, 0.93f - n);
            });

            public readonly Texture2D Wood = MakeTexture("Wood", 256, 256, (x, y) =>
            {
                float grain = Mathf.Sin(y * 0.35f + ValueNoise(x / 40f, y / 6f, 6) * 6f) * 0.5f + 0.5f;
                float v = 0.75f + grain * 0.18f + Hash(x, y, 7) * 0.04f;
                return new Color(0.62f * v, 0.43f * v, 0.28f * v);
            });

            public readonly Texture2D Cork = MakeTexture("Cork", 128, 128, (x, y) =>
            {
                float n = Hash(x, y, 19) * 0.15f + ValueNoise(x / 5f, y / 5f, 20) * 0.1f;
                return new Color(0.62f + n, 0.45f + n * 0.8f, 0.28f + n * 0.5f);
            });

            public readonly Texture2D CityWindows = MakeTexture("CityWindows", 256, 256, (x, y) =>
            {
                int cx = x / 16, cy = y / 16;
                int lx = x % 16, ly = y % 16;
                bool glass = lx >= 3 && lx <= 12 && ly >= 3 && ly <= 13;
                var facade = new Color(0.035f, 0.04f, 0.06f);
                if (!glass) return facade;
                float r = Hash(cx, cy, 8);
                if (r < 0.58f) return new Color(0.06f, 0.07f, 0.1f);
                if (r < 0.82f) return new Color(1f, 0.82f, 0.5f) * (0.75f + 0.25f * Hash(cx, cy, 9));
                if (r < 0.94f) return new Color(0.85f, 0.92f, 1f) * 0.85f;
                return new Color(0.45f, 0.7f, 1f) * 0.8f;
            });

            public readonly Texture2D SkySide = MakeTexture("SkySide", 512, 512, (x, y) =>
            {
                float t = y / 511f;
                var horizon = new Color(0.36f, 0.2f, 0.24f);
                var mid = new Color(0.08f, 0.09f, 0.22f);
                var top = new Color(0.01f, 0.015f, 0.05f);
                Color c = t < 0.5f ? new Color(0.02f, 0.02f, 0.04f)
                    : t < 0.6f ? Color.Lerp(horizon, mid, (t - 0.5f) / 0.1f)
                    : Color.Lerp(mid, top, (t - 0.6f) / 0.4f);
                if (t > 0.58f && Hash(x, y, 10) > 0.9975f) c += Color.white * (0.4f + 0.6f * Hash(x, y, 11));
                return c;
            }, clamp: true);

            public readonly Texture2D SkyUp = MakeTexture("SkyUp", 512, 512, (x, y) =>
            {
                var c = new Color(0.01f, 0.015f, 0.05f);
                if (Hash(x, y, 12) > 0.997f) c += Color.white * (0.4f + 0.6f * Hash(x, y, 13));
                return c;
            }, clamp: true);

            public readonly Texture2D SkyDown = MakeTexture("SkyDown", 64, 64, (x, y) => new Color(0.02f, 0.02f, 0.04f), clamp: true);

            public readonly Texture2D Qr = MakeQr();

            private static Texture2D MakeQr()
            {
                const int modules = 29, quiet = 2, scale = 8;
                int size = (modules + quiet * 2) * scale;
                return MakeTexture("QrCode", size, size, (px, py) =>
                {
                    int mx = px / scale - quiet, my = (size - 1 - py) / scale - quiet;
                    if (mx < 0 || my < 0 || mx >= modules || my >= modules) return Color.white;
                    bool dark;
                    if (InFinder(mx, my, 0, 0) || InFinder(mx, my, modules - 7, 0) || InFinder(mx, my, 0, modules - 7))
                    {
                        int fx = mx < 8 ? mx : mx - (modules - 7), fy = my < 8 ? my : my - (modules - 7);
                        int ring = Mathf.Min(Mathf.Min(fx, fy), Mathf.Min(6 - fx, 6 - fy));
                        dark = ring != 1;
                    }
                    else if ((mx < 8 && my < 8) || (mx >= modules - 8 && my < 8) || (mx < 8 && my >= modules - 8))
                    {
                        dark = false;
                    }
                    else if (mx == 6 || my == 6)
                    {
                        dark = (mx + my) % 2 == 0;
                    }
                    else
                    {
                        dark = Hash(mx, my, 14) > 0.5f;
                    }
                    return dark ? new Color(0.05f, 0.05f, 0.07f) : Color.white;
                }, clamp: true, point: true);
            }

            private static bool InFinder(int x, int y, int ox, int oy) => x >= ox && x < ox + 7 && y >= oy && y < oy + 7;
        }

        private class Mats
        {
            public readonly Textures Tex = new Textures();

            public readonly Material Floor, CorridorFloor, TileFloor, LobbyFloor, Ceiling, Wall, AccentWall, Wood, Cork;
            public readonly Material Metal = Make("Metal", new Color(0.16f, 0.17f, 0.19f), smooth: 0.55f);
            public readonly Material Frame = Make("Frame", new Color(0.22f, 0.23f, 0.25f), smooth: 0.4f);
            public readonly Material Fabric = Make("Fabric", new Color(0.34f, 0.42f, 0.52f));
            public readonly Material ChairFabric = Make("ChairFabric", new Color(0.12f, 0.13f, 0.16f));
            public readonly Material Sofa = Make("Sofa", new Color(0.18f, 0.3f, 0.42f));
            public readonly Material White = Make("WhitePlastic", new Color(0.88f, 0.89f, 0.9f), smooth: 0.4f);
            public readonly Material Leaf = Make("Leaf", new Color(0.18f, 0.48f, 0.22f));
            public readonly Material Pot = Make("Pot", new Color(0.32f, 0.33f, 0.36f));
            public readonly Material Vending = Make("Vending", new Color(0.7f, 0.1f, 0.12f), smooth: 0.5f);
            public readonly Material Water = Make("Water", new Color(0.45f, 0.7f, 0.95f), smooth: 0.8f);
            public readonly Material LightPanel = Make("LightPanel", new Color(1f, 0.97f, 0.9f), unlit: true);
            public readonly Material ScreenOff = Make("ScreenOff", new Color(0.03f, 0.035f, 0.045f), unlit: true);
            public readonly Material ScreenSaver = Make("ScreenSaver", new Color(0.08f, 0.22f, 0.45f), unlit: true);
            public readonly Material VendingGlow = Make("VendingGlow", new Color(0.95f, 0.9f, 0.75f), unlit: true);
            public readonly Material Usb = Make("Usb", new Color(0.1f, 0.1f, 0.12f), smooth: 0.6f);
            public readonly Material UsbLabel = Make("UsbLabel", new Color(0.95f, 0.85f, 0.2f), unlit: true);
            public readonly Material Paper = Make("Paper", new Color(0.97f, 0.97f, 0.95f), unlit: true);
            public readonly Material Sticky = Make("Sticky", new Color(1f, 0.9f, 0.35f), unlit: true);
            public readonly Material ExitGreen = Make("ExitGreen", new Color(0.1f, 0.85f, 0.35f), unlit: true);
            public readonly Material SignRed = Make("SignRed", new Color(0.75f, 0.15f, 0.12f), unlit: true);
            public readonly Material SignNavy = Make("SignNavy", new Color(0.1f, 0.16f, 0.3f), unlit: true);
            public readonly Material Ground = Make("Ground", new Color(0.03f, 0.035f, 0.05f), unlit: true);
            public readonly Material RedLight = Make("RedLight", new Color(1f, 0.1f, 0.1f), unlit: true);
            public readonly Material Moon = Make("Moon", new Color(0.92f, 0.92f, 0.85f), unlit: true);
            public readonly Material Led = Make("Led", new Color(0.2f, 1f, 0.4f), unlit: true);
            public readonly Material UiUnlit = Make("UiUnlit", Color.white, unlit: true);
            public readonly Material Button = Make("Button", new Color(0.15f, 0.39f, 0.92f), unlit: true);
            public readonly Material Glass = MakeGlass("Glass", new Color(0.6f, 0.75f, 0.85f, 0.18f));
            public readonly Material DoorGlass = MakeGlass("DoorGlass", new Color(0.55f, 0.75f, 0.9f, 0.35f));
            public readonly Material[] City = new Material[3];
            public readonly Material Sky;

            public Mats()
            {
                Floor = Make("Floor", Color.white, Tex.Carpet, new Vector2(10f, 8f));
                CorridorFloor = Make("CorridorFloor", new Color(0.8f, 0.75f, 0.7f), Tex.Carpet, new Vector2(18f, 2f));
                TileFloor = Make("TileFloor", Color.white, Tex.Tile, new Vector2(6f, 8f), smooth: 0.45f);
                LobbyFloor = Make("LobbyFloor", Color.white, Tex.Stone, new Vector2(3f, 5f), smooth: 0.55f);
                Ceiling = Make("Ceiling", Color.white, Tex.CeilingTile, new Vector2(40f, 27f));
                Wall = Make("Wall", Color.white, Tex.Wall, new Vector2(3f, 2f));
                AccentWall = Make("AccentWall", new Color(0.2f, 0.36f, 0.48f), Tex.Wall, new Vector2(3f, 2f));
                Wood = Make("Desk", Color.white, Tex.Wood, Vector2.one, smooth: 0.35f);
                Cork = Make("Cork", Color.white, Tex.Cork, new Vector2(2f, 2f));
                City[0] = Make("CityA", Color.white, Tex.CityWindows, new Vector2(2f, 6f), unlit: true);
                City[1] = Make("CityB", new Color(0.85f, 0.9f, 1f), Tex.CityWindows, new Vector2(3f, 9f), unlit: true);
                City[2] = Make("CityC", new Color(1f, 0.92f, 0.85f), Tex.CityWindows, new Vector2(1.5f, 4f), unlit: true);

                string skyPath = $"{Root}/Materials/NightSky.mat";
                Sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
                var skyShader = Shader.Find("Skybox/6 Sided");
                if (Sky == null)
                {
                    Sky = new Material(skyShader);
                    AssetDatabase.CreateAsset(Sky, skyPath);
                }
                Sky.shader = skyShader;
                foreach (var side in new[] { "_FrontTex", "_BackTex", "_LeftTex", "_RightTex" })
                {
                    Sky.SetTexture(side, Tex.SkySide);
                }
                Sky.SetTexture("_UpTex", Tex.SkyUp);
                Sky.SetTexture("_DownTex", Tex.SkyDown);
                Sky.SetFloat("_Exposure", 1f);
                EditorUtility.SetDirty(Sky);
            }

            public static Material Make(string name, Color color, Texture2D tex = null, Vector2? tiling = null,
                bool unlit = false, float smooth = 0.15f)
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
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
                if (!unlit)
                {
                    mat.SetFloat("_Smoothness", smooth);
                }
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                return mat;
            }

            private static Material MakeGlass(string name, Color color)
            {
                var mat = Make(name, color, smooth: 0.9f);
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
                EditorUtility.SetDirty(mat);
                return mat;
            }
        }

        /// <summary>
        /// Copy of the font's SDF material on TextMesh Pro's URP shader graph. The legacy CG shaders
        /// render with the wrong eye's matrices under URP single-pass stereo, which shoved every
        /// label sideways off its panel in the headset.
        /// </summary>
        private static Material CreateUrpFontMaterial(TMP_FontAsset font)
        {
            var shader = Shader.Find("TextMeshPro/SRP/TMP_SDF-URP Unlit");
            if (font == null || shader == null)
            {
                Debug.LogWarning("[CyberQuest] URP TMP shader not found - text may be offset in stereo.");
                return null;
            }
            string path = $"{Root}/Materials/TextURP.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            var src = font.material;
            foreach (var prop in new[] { "_MainTex", "_GradientScale", "_TextureWidth", "_TextureHeight", "_ScaleRatioA", "_ScaleRatioB", "_ScaleRatioC" })
            {
                if (!src.HasProperty(prop) || !mat.HasProperty(prop)) continue;
                if (prop == "_MainTex") mat.SetTexture(prop, src.GetTexture(prop));
                else mat.SetFloat(prop, src.GetFloat(prop));
            }
            if (mat.HasProperty("_FaceColor")) mat.SetColor("_FaceColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static AudioClip MakeClip(string name, float seconds, Func<float, float> wave)
        {
            string path = $"{Root}/Audio/{name}.wav";
            const int rate = 22050;
            int samples = (int)(rate * seconds);
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write("RIFF".ToCharArray()); w.Write(36 + samples * 2); w.Write("WAVE".ToCharArray());
                w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write("data".ToCharArray()); w.Write(samples * 2);
                for (int i = 0; i < samples; i++)
                {
                    float s = Mathf.Clamp(wave(i / (float)rate), -1f, 1f);
                    w.Write((short)(s * short.MaxValue));
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static float Tone(float t, float freq, float start, float length)
        {
            float local = t - start;
            if (local < 0f || local > length) return 0f;
            float env = Mathf.Min(1f, local / 0.01f) * Mathf.Exp(-local * 6f);
            return Mathf.Sin(2f * Mathf.PI * freq * local) * env;
        }

        private static float Noise(float t) => Hash(Mathf.FloorToInt(t * 22050f), 7, 99) * 2f - 1f;

        // ------------------------------------------------------------------ UI theme

        private static PokeButton3D CreateButtonPrefab(Mats mats)
        {
            var size = new Vector3(0.2f, 0.07f, 0.014f);
            var root = new GameObject("PokeButton");

            var surfaceGo = new GameObject("Surface");
            surfaceGo.transform.SetParent(root.transform, false);
            surfaceGo.transform.localPosition = new Vector3(0f, 0f, -size.z / 2f);
            var plane = surfaceGo.AddComponent<PlaneSurface>();
            plane.InjectAllPlaneSurface(PlaneSurface.NormalFacing.Backward, false);
            var clipper = surfaceGo.AddComponent<BoundsClipper>();
            clipper.Size = new Vector3(size.x, size.y, 0.03f);
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

            var label = CreateLabel(root.transform, "Label", new Vector3(0f, 0f, -size.z / 2f - 0.001f), 0.2f, "Button", Color.white);
            label.rectTransform.sizeDelta = new Vector2(size.x - 0.02f, size.y - 0.01f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.12f;
            label.fontSizeMax = 0.22f;
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.Normal;

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

        private static void CreateTheme(Mats m, PokeButton3D button)
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            theme.font = _font;
            theme.fontMaterial = _fontMaterial;
            theme.unlit = m.UiUnlit;
            theme.button = button;
            theme.qrCode = m.Tex.Qr;
            theme.click = MakeClip("Click", 0.06f, t => Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-t * 90f) * 0.6f);
            theme.good = MakeClip("Good", 0.6f, t => (Tone(t, 660f, 0f, 0.3f) + Tone(t, 990f, 0.12f, 0.45f)) * 0.45f);
            theme.okay = MakeClip("Okay", 0.45f, t => Tone(t, 520f, 0f, 0.4f) * 0.5f);
            theme.bad = MakeClip("Bad", 0.6f, t => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(300f, 160f, t / 0.6f) * t)) * Mathf.Exp(-t * 3f) * 0.25f);
            theme.notify = MakeClip("Notify", 0.5f, t => (Tone(t, 880f, 0f, 0.25f) + Tone(t, 1320f, 0.13f, 0.35f)) * 0.45f);
            theme.beep = MakeClip("Beep", 0.09f, t => Mathf.Sin(2f * Mathf.PI * 1000f * t) * (t < 0.08f ? 0.3f : 0f));
            theme.thud = MakeClip("Thud", 0.35f, t => (Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.8f + Noise(t) * 0.3f) * Mathf.Exp(-t * 16f));
            theme.doorBuzzer = MakeClip("DoorBuzzer", 1.2f, t => (t % 0.6f) < 0.45f ? Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 220f * t)) * 0.35f : 0f);
            theme.deskRing = MakeClip("DeskRing", 3f, t =>
            {
                float c = t % 3f;
                bool on = c < 0.4f || (c > 0.6f && c < 1.0f);
                return on ? (Mathf.Sin(2f * Mathf.PI * 480f * t) + Mathf.Sin(2f * Mathf.PI * 440f * t)) * 0.2f : 0f;
            });
            theme.phoneBuzz = MakeClip("PhoneBuzz", 0.35f, t => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 170f * t)) * 0.3f * (t < 0.3f ? 1f : 0f));
            theme.unlock = MakeClip("Unlock", 0.4f, t => Tone(t, 1200f, 0f, 0.1f) * 0.5f + Tone(t, 1600f, 0.12f, 0.2f) * 0.5f);
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ lighting

        private static void BuildLighting(Mats m)
        {
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = 0.3f;
            moon.color = new Color(0.65f, 0.72f, 1f);
            moon.shadows = LightShadows.None;
            moon.transform.rotation = Quaternion.Euler(50f, 150f, 0f);

            // One light per space, none casting shadows (CLAUDE.md 9: one shadow-caster at most).
            var lights = new (string, Vector3, float)[]
            {
                ("Light_OpenPlan_A", new Vector3(3f, 2.75f, 3.5f), 6f),
                ("Light_OpenPlan_B", new Vector3(7.5f, 2.75f, 5f), 6f),
                ("Light_Corridor", new Vector3(9f, 2.75f, 9f), 7f),
                ("Light_BreakRoom", new Vector3(15f, 2.75f, 4f), 6f),
                ("Light_Lobby", new Vector3(21f, 2.75f, 5f), 7f),
                ("Light_Finance", new Vector3(3.25f, 2.75f, 13f), 5.5f),
                ("Light_MeetingRoom3", new Vector3(10f, 2.75f, 13f), 5.5f),
            };
            foreach (var (name, pos, range) in lights)
            {
                var l = new GameObject(name).AddComponent<Light>();
                l.type = LightType.Point;
                l.range = range;
                l.intensity = 1.5f;
                l.color = new Color(1f, 0.94f, 0.84f);
                l.shadows = LightShadows.None;
                l.transform.position = pos;
            }

            RenderSettings.skybox = m.Sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.57f, 0.64f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.28f, 0.3f);
            RenderSettings.fog = false;
        }

        // ------------------------------------------------------------------ shell (floors, walls, ceiling)

        private static Transform BuildShell(Mats m)
        {
            var shell = new GameObject("Shell").transform;

            // Floors: every walkable space is a teleport surface.
            Floor(shell, "Floor_OpenPlan", 0f, 10f, 0f, 8f, m.Floor, true);
            Floor(shell, "Floor_Corridor", 0f, 18f, 8f, 10f, m.CorridorFloor, true);
            Floor(shell, "Floor_CorridorSouth", 10f, 12f, 2f, 8f, m.CorridorFloor, true);
            Floor(shell, "Floor_BreakRoom", 12f, 18f, 0f, 8f, m.TileFloor, true);
            Floor(shell, "Floor_Lobby", 18f, 24f, 0f, 10f, m.LobbyFloor, true);
            Floor(shell, "Floor_Vestibule", 24f, 26.5f, 3.5f, 6.5f, m.LobbyFloor, true);
            Floor(shell, "Floor_FinancePod", 0f, 6.5f, 10f, 16f, m.Floor, true);
            Floor(shell, "Floor_MeetingRoom3", 6.5f, 13.5f, 10f, 16f, m.Floor, true);
            Floor(shell, "Floor_ItRoom", 13.5f, 20f, 10f, 16f, m.CorridorFloor, false);
            Floor(shell, "Floor_Void", 10f, 12f, 0f, 2f, m.CorridorFloor, false);

            Box("Ceiling", shell, new Vector3(13.25f, H + 0.05f, 8f), new Vector3(27f, 0.1f, 16.4f), m.Ceiling);
            foreach (var (x, z) in new[]
                     {
                         (2f, 2f), (5f, 2f), (8f, 2f), (2f, 5.5f), (5f, 5.5f), (8f, 5.5f),
                         (3f, 9f), (7f, 9f), (11f, 9f), (15f, 9f), (11f, 4f), (11f, 6.5f),
                         (14f, 2.5f), (16f, 5.5f), (20f, 2.5f), (22f, 2.5f), (20f, 7f), (22f, 7f), (25.2f, 5f),
                         (2f, 13f), (4.5f, 13f), (8.5f, 13f), (11.5f, 13f),
                     })
            {
                var panel = Box("LightPanel", shell, new Vector3(x, H - 0.005f, z), new Vector3(1.2f, 0.02f, 0.6f), m.LightPanel);
                Object.DestroyImmediate(panel.GetComponent<Collider>());
            }

            // Exterior: window walls look out on the city.
            WindowWall(shell, "S_OpenPlan", true, 0f, 0f, 10f, m);
            Seg(shell, "S_Void", true, 0f, 10f, 12f, 0f, H, m.Wall);
            WindowWall(shell, "S_BreakRoom", true, 0f, 12f, 18f, m);
            WindowWall(shell, "S_Lobby", true, 0f, 18f, 24f, m);
            WindowWall(shell, "W_OpenPlan", false, 0f, 0f, 8f, m);
            Seg(shell, "W_Corridor", false, 0f, 8f, 10f, 0f, H, m.Wall);
            WindowWall(shell, "W_Finance", false, 0f, 10f, 16f, m);
            WindowWall(shell, "N_Finance", true, 16f, 0f, 6.5f, m);
            WindowWall(shell, "N_MeetingRoom3", true, 16f, 6.5f, 13.5f, m);
            Seg(shell, "N_ItRoom", true, 16f, 13.5f, 20f, 0f, H, m.Wall);
            Seg(shell, "E_ItRoom", false, 20f, 10f, 16f, 0f, H, m.Wall);
            Wall(shell, "E_Lobby", false, 24f, 0f, 10f, m.AccentWall, new Gap(5f, 1.7f, 2.4f));

            // Interior walls.
            Wall(shell, "OpenPlan_N", true, 8f, 0f, 10f, m.Wall, new Gap(5f, 1.4f));
            Seg(shell, "BreakRoom_N", true, 8f, 12f, 18f, 0f, H, m.Wall);
            Wall(shell, "Corridor_N", true, 10f, 0f, 24f, m.Wall, new Gap(3f, 1.2f), new Gap(10f, 1.2f), new Gap(16.5f, 1.1f));
            Wall(shell, "OpenPlan_E", false, 10f, 0f, 8f, m.Wall, new Gap(4f, 1.4f));
            Wall(shell, "BreakRoom_W", false, 12f, 0f, 8f, m.Wall, new Gap(5f, 1.4f));
            Seg(shell, "CorridorSouth_S", true, 2f, 10f, 12f, 0f, H, m.Wall);
            Seg(shell, "Lobby_W", false, 18f, 0f, 8f, 0f, H, m.AccentWall);
            Seg(shell, "Lobby_W_Lintel", false, 18f, 8f, 10f, 2.4f, H, m.Wall);
            Seg(shell, "Finance_E", false, 6.5f, 10f, 16f, 0f, H, m.Wall);
            Seg(shell, "MeetingRoom3_E", false, 13.5f, 10f, 16f, 0f, H, m.Wall);

            // Vestibule beyond the main door (lifts).
            Seg(shell, "Vest_S", true, 3.5f, 24f, 26.5f, 0f, H, m.Wall);
            Seg(shell, "Vest_N", true, 6.5f, 24f, 26.5f, 0f, H, m.Wall);
            Seg(shell, "Vest_E", false, 26.5f, 3.5f, 6.5f, 0f, H, m.Metal);

            // Skirting along the main interior runs.
            Box("Skirt_CorridorN", shell, new Vector3(9f, 0.05f, 9.93f), new Vector3(18f, 0.1f, 0.02f), m.Frame);
            Box("Skirt_CorridorS", shell, new Vector3(9f, 0.05f, 8.07f), new Vector3(18f, 0.1f, 0.02f), m.Frame);
            return shell;
        }

        private static void Floor(Transform p, string name, float x0, float x1, float z0, float z1, Material mat, bool teleport)
        {
            var go = Box(name, p, new Vector3((x0 + x1) / 2f, -0.05f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.1f, z1 - z0), mat);
            if (teleport)
            {
                var surface = go.AddComponent<ColliderSurface>();
                surface.InjectAllColliderSurface(go.GetComponent<Collider>());
                var tp = go.AddComponent<TeleportInteractable>();
                tp.InjectAllTeleportInteractable(surface);
            }
        }

        private static void Wall(Transform p, string name, bool alongX, float fixedCoord, float a0, float a1, Material mat, params Gap[] gaps)
        {
            float cursor = a0;
            foreach (var g in gaps.OrderBy(g => g.C))
            {
                float s = g.C - g.W / 2f, e = g.C + g.W / 2f;
                Seg(p, name, alongX, fixedCoord, cursor, s, 0f, H, mat);
                Seg(p, name + "_Lintel", alongX, fixedCoord, s, e, g.H, H, mat);
                DoorFrame(p, alongX, fixedCoord, g);
                cursor = e;
            }
            Seg(p, name, alongX, fixedCoord, cursor, a1, 0f, H, mat);
        }

        private static Material _frameMat;

        private static void DoorFrame(Transform p, bool alongX, float f, Gap g)
        {
            _frameMat ??= Mats.Make("Frame", new Color(0.22f, 0.23f, 0.25f), smooth: 0.4f);
            float s = g.C - g.W / 2f, e = g.C + g.W / 2f;
            Seg(p, "Frame", alongX, f, s - 0.05f, s, 0f, g.H, _frameMat, WallT + 0.04f);
            Seg(p, "Frame", alongX, f, e, e + 0.05f, 0f, g.H, _frameMat, WallT + 0.04f);
            Seg(p, "Frame", alongX, f, s - 0.05f, e + 0.05f, g.H, g.H + 0.05f, _frameMat, WallT + 0.04f);
        }

        private static GameObject Seg(Transform p, string name, bool alongX, float f, float a0, float a1, float y0, float y1, Material m,
            float thick = WallT)
        {
            if (a1 - a0 < 0.01f || y1 - y0 < 0.01f)
            {
                return null;
            }
            float c = (a0 + a1) / 2f, len = a1 - a0, cy = (y0 + y1) / 2f, h = y1 - y0;
            Vector3 pos = alongX ? new Vector3(c, cy, f) : new Vector3(f, cy, c);
            Vector3 scale = alongX ? new Vector3(len, h, thick) : new Vector3(thick, h, len);
            return MarkWall(Box(name, p, pos, scale, m));
        }

        private const string EnvironmentLayer = "Environment";

        /// <summary>Walls, glass and closed doors: on the Environment layer (head blocking) and blocking the teleport arc.</summary>
        private static GameObject MarkWall(GameObject go)
        {
            int layer = LayerMask.NameToLayer(EnvironmentLayer);
            if (layer >= 0)
            {
                go.layer = layer;
            }
            var surface = go.AddComponent<ColliderSurface>();
            surface.InjectAllColliderSurface(go.GetComponent<Collider>());
            var blocker = go.AddComponent<TeleportInteractable>();
            blocker.InjectAllTeleportInteractable(surface);
            blocker.AllowTeleport = false;
            return go;
        }

        /// <summary>Every solid prop (desks, chairs, cabinets, machines, pedestals) blocks the player like a wall.</summary>
        private static void AssignBlockingLayer(GameObject rig)
        {
            int layer = LayerMask.NameToLayer(EnvironmentLayer);
            if (layer < 0)
            {
                return;
            }
            int n = 0;
            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = col.gameObject;
                if (col.isTrigger || go.transform.IsChildOf(rig.transform) || go.GetComponentInParent<Rigidbody>(true) != null ||
                    go.GetComponentInParent<PokeButton3D>(true) != null || go.name.StartsWith("Floor_") || go.name == "Ceiling")
                {
                    continue;
                }
                go.layer = layer;
                n++;
            }
            Debug.Log($"[CyberQuest] {n} colliders block the player (walls, doors, furniture).");
        }

        private static void EnsureEnvironmentLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == EnvironmentLayer)
                {
                    return;
                }
            }
            for (int i = 8; i < layers.arraySize; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue))
                {
                    el.stringValue = EnvironmentLayer;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }
        }

        private static void WindowWall(Transform p, string name, bool alongX, float f, float a0, float a1, Mats m)
        {
            Seg(p, name + "_Sill", alongX, f, a0, a1, 0f, 0.9f, m.Wall);
            Seg(p, name + "_Header", alongX, f, a0, a1, 2.4f, H, m.Wall);
            Seg(p, name + "_Ledge", alongX, f, a0, a1, 0.9f, 0.93f, m.Frame, 0.24f);
            Seg(p, name + "_Glass", alongX, f, a0, a1, 0.93f, 2.4f, m.Glass, 0.012f);
            int n = Mathf.Max(1, Mathf.RoundToInt((a1 - a0) / 1.6f));
            for (int k = 0; k <= n; k++)
            {
                float a = a0 + k * (a1 - a0) / n;
                Seg(p, name + "_Mullion", alongX, f, a - 0.04f, a + 0.04f, 0.93f, 2.4f, m.Frame, 0.14f);
            }
        }

        // ------------------------------------------------------------------ furniture & dressing

        private static Transform BuildFurniture(Mats m)
        {
            var f = new GameObject("Furniture").transform;

            // Open plan
            Desk(f, "PlayerDesk", new Vector3(3f, 0f, 3f), 0f, new Vector2(1.6f, 0.75f), m);
            Box("Keyboard", f, new Vector3(3f, 0.765f, 2.8f), new Vector3(0.44f, 0.02f, 0.14f), m.Metal);
            Box("Mouse", f, new Vector3(3.32f, 0.765f, 2.8f), new Vector3(0.06f, 0.025f, 0.1f), m.Metal);
            Cylinder(f, "Mug", new Vector3(2.55f, 0.8f, 3.25f), new Vector3(0.08f, 0.05f, 0.08f), m.White);
            Plant(f, new Vector3(2.4f, 0.75f, 3.25f), 0.4f, m);
            Box("PhoneMat", f, new Vector3(3.62f, 0.752f, 2.82f), new Vector3(0.14f, 0.004f, 0.22f), m.Frame);
            Desk(f, "EmptyDesk", new Vector3(6.2f, 0f, 3f), 0f, new Vector2(1.4f, 0.7f), m);
            Monitor(f, new Vector3(6.2f, 0.75f, 3.2f), 0f, m.ScreenOff, m);
            Chair(f, new Vector3(6.2f, 0f, 2.3f), 0f, m);
            Sign(f, "EmptyNameplate", new Vector3(6.2f, 0.8f, 2.7f), Quaternion.Euler(30f, 0f, 0f), "J. Morgan - on leave",
                0.11f, new Vector2(0.32f, 0.05f), Color.white, m.Metal);
            foreach (var (x, yaw) in new[] { (2.5f, 180f), (6.5f, 180f) })
            {
                Desk(f, "BackDesk", new Vector3(x, 0f, 6.1f), yaw, new Vector2(1.4f, 0.7f), m);
                Monitor(f, new Vector3(x, 0.75f, 5.9f), yaw, m.ScreenSaver, m);
                Chair(f, new Vector3(x, 0f, 6.8f), yaw, m);
            }
            Box("Partition", f, new Vector3(4.5f, 0.65f, 4.6f), new Vector3(7f, 1.3f, 0.05f), m.Fabric);
            Plant(f, new Vector3(0.5f, 0f, 0.5f), 1.2f, m);
            Plant(f, new Vector3(9.4f, 0f, 0.5f), 1.2f, m);
            Plant(f, new Vector3(0.5f, 0f, 7.4f), 1.1f, m);
            Box("Printer", f, new Vector3(9.5f, 0.45f, 6.5f), new Vector3(0.6f, 0.9f, 0.55f), m.White);
            Box("PrinterTop", f, new Vector3(9.5f, 0.95f, 6.5f), new Vector3(0.55f, 0.1f, 0.5f), m.Metal);
            WaterCooler(f, new Vector3(8.4f, 0f, 7.6f), m);
            Sign(f, "Poster_Lock", new Vector3(1.6f, 1.7f, 7.93f), Quaternion.identity,
                "<b>LEAVING YOUR DESK?</b>\nLock your screen.\n<size=70%>Win + L</size>", 0.22f, new Vector2(0.6f, 0.45f),
                Color.white, Mats.Make("PosterBlue", new Color(0.12f, 0.3f, 0.65f), unlit: true));
            Sign(f, "Sign_ToCorridor", new Vector3(5f, 2.55f, 7.93f), Quaternion.identity,
                "CORRIDOR  -  Finance  |  Meeting Room 3  |  IT  |  Lobby", 0.16f, new Vector2(1.35f, 0.1f), Color.white, m.SignNavy);
            Sign(f, "Sign_ToBreakRoom", new Vector3(9.93f, 2.5f, 4f), Quaternion.LookRotation(Vector3.right),
                "BREAK ROOM  |  LOBBY", 0.16f, new Vector2(1.2f, 0.1f), Color.white, m.SignNavy);

            // Corridor signage (door signs face whoever walks the corridor).
            Sign(f, "Sign_Finance", new Vector3(3f, 2.5f, 9.93f), Quaternion.identity, "FINANCE", 0.24f, new Vector2(1f, 0.14f), Color.white, m.SignNavy);
            Sign(f, "Sign_MeetingRoom3", new Vector3(10f, 2.5f, 9.93f), Quaternion.identity, "MEETING ROOM 3", 0.24f, new Vector2(1.1f, 0.14f), Color.white, m.SignNavy);
            Sign(f, "Sign_Lobby", new Vector3(17.93f, 2.6f, 9f), Quaternion.LookRotation(Vector3.right), "LOBBY  |  EXIT", 0.22f,
                new Vector2(1.4f, 0.14f), Color.white, m.SignNavy);
            Sign(f, "Sign_BreakRoomDoor", new Vector3(11.93f, 2.5f, 5f), Quaternion.LookRotation(Vector3.right), "BREAK ROOM", 0.24f,
                new Vector2(1.1f, 0.14f), Color.white, m.SignNavy);
            Plant(f, new Vector3(0.5f, 0f, 9f), 1.1f, m);
            Box("Bench", f, new Vector3(13f, 0.22f, 9.7f), new Vector3(1.4f, 0.44f, 0.4f), m.Wood);

            // Break room
            Box("Vending", f, new Vector3(17.45f, 0.95f, 1.2f), new Vector3(0.85f, 1.9f, 0.8f), m.Vending);
            var glow = Box("VendingGlass", f, new Vector3(17.02f, 1.15f, 1.2f), new Vector3(0.02f, 1.2f, 0.6f), m.VendingGlow);
            Object.DestroyImmediate(glow.GetComponent<Collider>());
            Cylinder(f, "Table", new Vector3(14.6f, 0.72f, 3.6f), new Vector3(1.1f, 0.02f, 1.1f), m.Wood);
            Cylinder(f, "TableLeg", new Vector3(14.6f, 0.36f, 3.6f), new Vector3(0.1f, 0.36f, 0.1f), m.Metal);
            Chair(f, new Vector3(14.6f, 0f, 2.8f), 0f, m);
            Chair(f, new Vector3(15.4f, 0f, 3.6f), -90f, m);
            Chair(f, new Vector3(13.8f, 0f, 3.6f), 90f, m);
            Box("Counter", f, new Vector3(14.5f, 0.45f, 7.6f), new Vector3(3f, 0.9f, 0.6f), m.White);
            Box("CounterTop", f, new Vector3(14.5f, 0.92f, 7.6f), new Vector3(3.05f, 0.04f, 0.65f), m.Wood);
            Box("CoffeeMachine", f, new Vector3(13.6f, 1.12f, 7.65f), new Vector3(0.35f, 0.4f, 0.35f), m.Metal);
            Box("Fridge", f, new Vector3(16.6f, 0.9f, 7.6f), new Vector3(0.7f, 1.8f, 0.65f), m.White);

            // Lobby
            Box("Reception", f, new Vector3(21.6f, 0.55f, 6.2f), new Vector3(2.2f, 1.1f, 0.6f), m.Wood);
            Box("ReceptionTop", f, new Vector3(21.6f, 1.12f, 6.2f), new Vector3(2.3f, 0.04f, 0.7f), m.White);
            Box("Sofa", f, new Vector3(19.2f, 0.25f, 1.2f), new Vector3(1.8f, 0.5f, 0.8f), m.Sofa);
            Box("SofaBack", f, new Vector3(19.2f, 0.6f, 0.85f), new Vector3(1.8f, 0.6f, 0.15f), m.Sofa);
            Plant(f, new Vector3(23.4f, 0f, 0.6f), 1.3f, m);
            Plant(f, new Vector3(18.6f, 0f, 9.3f), 1.2f, m);
            Sign(f, "Logo", new Vector3(18.07f, 2.0f, 4.5f), Quaternion.LookRotation(Vector3.left),
                "<b>NEXORA</b>\n<size=35%>SECURE BY DESIGN</size>", 0.9f, new Vector2(1.8f, 0.7f), new Color(0.85f, 0.95f, 1f), null);
            Sign(f, "LiftSign", new Vector3(26.43f, 2.45f, 5f), Quaternion.LookRotation(Vector3.right), "LIFTS  -  EXIT", 0.26f,
                new Vector2(1.2f, 0.16f), Color.white, m.ExitGreen);
            Box("LiftDoorL", f, new Vector3(26.4f, 1.05f, 4.55f), new Vector3(0.04f, 2.1f, 0.85f), m.Metal);
            Box("LiftDoorR", f, new Vector3(26.4f, 1.05f, 5.45f), new Vector3(0.04f, 2.1f, 0.85f), m.Metal);
            Sign(f, "ThanksSign", new Vector3(26.42f, 1.6f, 5f), Quaternion.LookRotation(Vector3.right),
                "<b>THANKS FOR PLAYING</b>\nCYBERQUEST", 0.2f, new Vector2(0.9f, 0.3f), Color.white, null);

            // Finance pod
            Desk(f, "FinanceSpareDesk", new Vector3(1.4f, 0f, 14.6f), 90f, new Vector2(1.4f, 0.7f), m);
            Box("Cabinet", f, new Vector3(5.9f, 0.65f, 15.5f), new Vector3(0.6f, 1.3f, 0.5f), m.White);
            Plant(f, new Vector3(0.6f, 0f, 10.6f), 1.1f, m);
            Sign(f, "Poster_Finance", new Vector3(6.43f, 1.7f, 12.2f), Quaternion.LookRotation(Vector3.right),
                "<b>Q3 CLOSE</b>\nAll invoices by Friday", 0.2f, new Vector2(0.6f, 0.35f), Color.white,
                Mats.Make("PosterGreen", new Color(0.1f, 0.45f, 0.3f), unlit: true));

            // Meeting Room 3
            Box("MeetingTable", f, new Vector3(10f, 0.73f, 13.2f), new Vector3(2.6f, 0.05f, 1.2f), m.Wood);
            Box("MeetingTableBase", f, new Vector3(10f, 0.36f, 13.2f), new Vector3(1.8f, 0.7f, 0.3f), m.Metal);
            foreach (float x in new[] { 9.1f, 10f, 10.9f })
            {
                Chair(f, new Vector3(x, 0f, 12.3f), 0f, m);
                Chair(f, new Vector3(x, 0f, 14.1f), 180f, m);
            }
            Sign(f, "Whiteboard", new Vector3(13.43f, 1.5f, 13f), Quaternion.LookRotation(Vector3.right),
                "<b>Q3 REVIEW</b>\n- Harbor Logistics renewal\n- Payroll audit Monday\n- Priya: budget deck", 0.2f,
                new Vector2(1.6f, 0.9f), new Color(0.1f, 0.25f, 0.6f), m.Paper);
            Box("Charger", f, new Vector3(9.4f, 0.775f, 12.85f), new Vector3(0.08f, 0.04f, 0.08f), m.White);
            Box("ChargerCable", f, new Vector3(9.55f, 0.758f, 12.85f), new Vector3(0.25f, 0.006f, 0.01f), m.White);
            Sign(f, "ChargerTag", new Vector3(9.4f, 0.797f, 12.85f), Quaternion.Euler(90f, 0f, 0f), "PN", 0.1f,
                new Vector2(0.07f, 0.05f), new Color(0.2f, 0.2f, 0.25f), null);

            // IT room (locked, seen only through its door)
            Box("ServerRack", f, new Vector3(18.8f, 1f, 14.8f), new Vector3(0.7f, 2f, 0.9f), m.Metal);
            return f;
        }

        private static void Desk(Transform parent, string name, Vector3 pos, float yaw, Vector2 size, Mats m)
        {
            var desk = new GameObject(name).transform;
            desk.SetParent(parent, false);
            desk.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            Box("Top", desk, new Vector3(0f, 0.73f, 0f), new Vector3(size.x, 0.04f, size.y), m.Wood, Space.Self);
            Box("Modesty", desk, new Vector3(0f, 0.45f, size.y / 2f - 0.04f), new Vector3(size.x - 0.1f, 0.45f, 0.02f), m.Metal, Space.Self);
            float lx = size.x / 2f - 0.05f, lz = size.y / 2f - 0.05f;
            foreach (var (x, z) in new[] { (-lx, -lz), (lx, -lz), (-lx, lz), (lx, lz) })
            {
                Box("Leg", desk, new Vector3(x, 0.355f, z), new Vector3(0.05f, 0.71f, 0.05f), m.Metal, Space.Self);
            }
        }

        private static GameObject Monitor(Transform parent, Vector3 deskPos, float yaw, Material screen, Mats m)
        {
            var mon = new GameObject("Monitor").transform;
            mon.SetParent(parent, false);
            mon.SetPositionAndRotation(deskPos, Quaternion.Euler(0f, yaw, 0f));
            Box("Base", mon, new Vector3(0f, 0.01f, 0.05f), new Vector3(0.22f, 0.02f, 0.16f), m.Metal, Space.Self);
            Box("Stand", mon, new Vector3(0f, 0.15f, 0.07f), new Vector3(0.05f, 0.28f, 0.03f), m.Metal, Space.Self);
            Box("Body", mon, new Vector3(0f, 0.38f, 0.04f), new Vector3(0.6f, 0.36f, 0.03f), m.Metal, Space.Self);
            var s = Box("Screen", mon, new Vector3(0f, 0.38f, 0.024f), new Vector3(0.56f, 0.32f, 0.002f), screen, Space.Self);
            Object.DestroyImmediate(s.GetComponent<Collider>());
            return mon.gameObject;
        }

        private static void Chair(Transform parent, Vector3 pos, float yaw, Mats m)
        {
            var c = new GameObject("Chair").transform;
            c.SetParent(parent, false);
            c.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            Box("Seat", c, new Vector3(0f, 0.48f, 0f), new Vector3(0.48f, 0.08f, 0.46f), m.ChairFabric, Space.Self);
            Box("Back", c, new Vector3(0f, 0.85f, -0.22f), new Vector3(0.46f, 0.6f, 0.06f), m.ChairFabric, Space.Self);
            Box("Post", c, new Vector3(0f, 0.25f, 0f), new Vector3(0.05f, 0.42f, 0.05f), m.Metal, Space.Self);
            Box("BaseX", c, new Vector3(0f, 0.04f, 0f), new Vector3(0.6f, 0.04f, 0.06f), m.Metal, Space.Self);
            Box("BaseZ", c, new Vector3(0f, 0.04f, 0f), new Vector3(0.06f, 0.04f, 0.6f), m.Metal, Space.Self);
        }

        private static void Plant(Transform parent, Vector3 pos, float scale, Mats m)
        {
            var p = new GameObject("Plant").transform;
            p.SetParent(parent, false);
            p.position = pos;
            p.localScale = Vector3.one * scale;
            Part(PrimitiveType.Cylinder, "Pot", p, new Vector3(0f, 0.15f, 0f), new Vector3(0.3f, 0.15f, 0.3f), m.Pot);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Part(PrimitiveType.Sphere, "Leaf", p, new Vector3(Mathf.Cos(a) * 0.1f, 0.42f + (i % 2) * 0.12f, Mathf.Sin(a) * 0.1f),
                    new Vector3(0.22f, 0.3f, 0.22f), m.Leaf);
            }
            Part(PrimitiveType.Sphere, "Crown", p, new Vector3(0f, 0.62f, 0f), new Vector3(0.26f, 0.32f, 0.26f), m.Leaf);
        }

        private static void WaterCooler(Transform parent, Vector3 pos, Mats m)
        {
            var w = new GameObject("WaterCooler").transform;
            w.SetParent(parent, false);
            w.position = pos;
            Box("Body", w, new Vector3(0f, 0.5f, 0f), new Vector3(0.32f, 1.0f, 0.32f), m.White, Space.Self);
            Part(PrimitiveType.Cylinder, "Bottle", w, new Vector3(0f, 1.2f, 0f), new Vector3(0.26f, 0.2f, 0.26f), m.Water);
        }

        private static void Cylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ------------------------------------------------------------------ skyline

        private static void BuildSkyline(Mats m)
        {
            var city = new GameObject("Skyline").transform;
            var rng = new System.Random(42);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var center = new Vector3(13f, 0f, 8f);

            const float baseY = -45f;
            for (int i = 0; i < 110; i++)
            {
                float angle = R(0f, Mathf.PI * 2f);
                float dist = R(30f, 120f);
                var p = center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                float w = R(7f, 18f), d = R(7f, 18f), h = R(25f, 100f);
                var b = Box("Tower", city, new Vector3(p.x, baseY + h / 2f, p.z), new Vector3(w, h, d), m.City[rng.Next(3)]);
                Object.DestroyImmediate(b.GetComponent<Collider>());
                if (h > 75f)
                {
                    var beacon = Box("Beacon", city, new Vector3(p.x, baseY + h + 0.6f, p.z), new Vector3(0.8f, 1.2f, 0.8f), m.RedLight);
                    Object.DestroyImmediate(beacon.GetComponent<Collider>());
                }
            }
            var ground = Box("Street", city, new Vector3(center.x, baseY, center.z), new Vector3(400f, 0.2f, 400f), m.Ground);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            Part(PrimitiveType.Sphere, "Moon", city, new Vector3(-150f, 95f, 260f), Vector3.one * 16f, m.Moon);
            MarkStatic(city);
        }

        // ------------------------------------------------------------------ rig

        private static GameObject BuildRig()
        {
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
            var eye = cameraRig.GetComponent<OVRCameraRig>()?.centerEyeAnchor?.GetComponent<Camera>();
            if (eye != null)
            {
                eye.clearFlags = CameraClearFlags.Skybox;
                eye.farClipPlane = 600f;
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

            var cc = cameraRig.AddComponent<UnityEngine.CharacterController>();
            cc.height = 1.7f;
            cc.radius = 0.25f;
            cc.center = new Vector3(0f, 0.85f, 0f);
            cc.enabled = false;
            var smooth = cameraRig.AddComponent<CyberQuest.Player.SmoothLocomotion>();
            Set(smooth, "headTransform", cameraRig.GetComponent<OVRCameraRig>().centerEyeAnchor);
            var blocker = cameraRig.AddComponent<CyberQuest.Player.HeadBlocker>();
            blocker.Configure(cameraRig.GetComponent<OVRCameraRig>().centerEyeAnchor, LayerMask.GetMask(EnvironmentLayer));
            var toggle = cameraRig.AddComponent<CyberQuest.Player.LocomotionToggle>();
            Set(toggle, "smoothLocomotion", smooth);
            Set(toggle, "teleportInteractorsRoot", cameraRig.transform);

            Debug.Log($"[CyberQuest] Rig: poke={cameraRig.GetComponentsInChildren<PokeInteractor>(true).Length} " +
                      $"ray={cameraRig.GetComponentsInChildren<RayInteractor>(true).Length} " +
                      $"teleport={cameraRig.GetComponentsInChildren<TeleportInteractor>(true).Length}");
            return cameraRig;
        }

        private static bool TryRunInteractionRigWizard()
        {
            try
            {
                var wizardType = CyberQuestBuilder.FindType("OVRComprehensiveInteractionRigWizard");
                var baseType = CyberQuestBuilder.FindType("QuickActionsWizard");
                var create = baseType?.GetMethod("CreateWithDefaults",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (wizardType == null || create == null)
                {
                    return false;
                }
                var result = create.MakeGenericMethod(wizardType).Invoke(null, new object[] { null, true, null });
                if (result is IEnumerable created)
                {
                    foreach (var _ in created) { }
                }
                return Object.FindObjectsByType<PokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CyberQuest] Interaction rig wizard failed: " + (e.InnerException ?? e).Message);
                return false;
            }
        }

        // ------------------------------------------------------------------ gameplay

        private static void BuildGameplay(Mats m, GameObject rig)
        {
            var game = new GameObject("CyberQuest").transform;

            // --- Systems
            var systems = new GameObject("Systems");
            systems.transform.SetParent(game, false);
            systems.AddComponent<GameDirector>();
            systems.AddComponent<RoomTracker>();
            var hud = new GameObject("ShiftHud").AddComponent<ShiftHud>();
            hud.transform.SetParent(game, false);
            Ambient(game, "Ambient_OpenPlanHvac", new Vector3(5f, 2.6f, 4f), "HvacHum", 4f,
                t => (Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.4f + Noise(t) * 0.25f) * 0.25f, 0.35f, 9f);
            Ambient(game, "Ambient_BreakRoomVending", new Vector3(17f, 1f, 1.2f), "VendingHum", 4f,
                t => (Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.2f) * 0.25f, 0.5f, 6f);
            Ambient(game, "Ambient_BreakRoomRadio", new Vector3(13.6f, 1.2f, 7.6f), "RadioNoise", 4f,
                t => Noise(t) * 0.12f * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.5f * t)), 0.3f, 5f);

            // --- Welcome
            var welcomeGo = new GameObject("Welcome");
            welcomeGo.transform.SetParent(game, false);
            welcomeGo.transform.SetPositionAndRotation(new Vector3(3f, 1.5f, 3.12f), Quaternion.identity);
            var welcome = welcomeGo.AddComponent<WelcomePanel>();

            // --- Desk monitor (Report / Mail / Chat)
            var monitorRoot = new GameObject("DeskMonitor").transform;
            monitorRoot.SetParent(game, false);
            monitorRoot.SetPositionAndRotation(new Vector3(3f, 1.3f, 3.3f), Quaternion.Euler(8f, 0f, 0f));
            var ms = MonitorScreen.ScreenSize;
            Box("Body", monitorRoot, new Vector3(0f, 0f, 0.012f), new Vector3(ms.x + 0.05f, ms.y + 0.05f, 0.03f), m.Metal, Space.Self);
            Box("Arm", monitorRoot, new Vector3(0f, -0.4f, 0.04f), new Vector3(0.06f, 0.42f, 0.04f), m.Metal, Space.Self);
            var screenGo = Child(monitorRoot, "Screen", new Vector3(0f, 0f, -0.01f));
            var monitor = screenGo.AddComponent<MonitorScreen>();
            var mail = screenGo.AddComponent<MailApp>();
            var chat = screenGo.AddComponent<ChatApp>();
            screenGo.AddComponent<ReportApp>();
            Sign(monitorRoot, "StickyNote_Portal", new Vector3(ms.x / 2f - 0.02f, -ms.y / 2f - 0.005f, -0.012f),
                Quaternion.Euler(0f, 0f, -6f), "Portal:\nportal.nexora.com", 0.1f, new Vector2(0.14f, 0.07f),
                new Color(0.2f, 0.15f, 0.05f), m.Sticky, local: true);

            // --- Desk phone (voicemail, IT service desk)
            var deskPhoneRoot = new GameObject("DeskPhone").transform;
            deskPhoneRoot.SetParent(game, false);
            deskPhoneRoot.SetPositionAndRotation(new Vector3(2.3f, 0.75f, 2.95f), Quaternion.Euler(0f, 20f, 0f));
            Box("Base", deskPhoneRoot, new Vector3(0f, 0.03f, 0f), new Vector3(0.2f, 0.06f, 0.22f), m.Metal, Space.Self);
            var handset = MakeGrabbable(game, "Handset", new Vector3(2.3f, 0.82f, 2.95f), Quaternion.Euler(0f, 20f, 0f),
                new Vector3(0.06f, 0.04f, 0.22f), m.Frame, 0.2f, out var handsetWrapper);
            var ringer = deskPhoneRoot.gameObject.AddComponent<AudioSource>();
            ringer.playOnAwake = false;
            ringer.spatialBlend = 0.8f;
            ringer.maxDistance = 25f;
            var phonePanel = Child(game, "DeskPhonePanelAnchor", Vector3.zero).transform;
            phonePanel.SetPositionAndRotation(new Vector3(2.0f, 1.08f, 2.95f), FacePlayer(new Vector3(2.0f, 1.08f, 2.95f)) * Quaternion.Euler(12f, 0f, 0f));
            var subtitleAnchor = Child(game, "SubtitleAnchor", Vector3.zero).transform;
            subtitleAnchor.SetPositionAndRotation(new Vector3(3f, 1.85f, 3.3f), Quaternion.identity);
            var deskPhone = deskPhoneRoot.gameObject.AddComponent<DeskPhone>();
            Set(deskPhone, "ringer", ringer);
            Set(deskPhone, "handset", handsetWrapper);
            Set(deskPhone, "panelAnchor", phonePanel);
            Set(deskPhone, "subtitleAnchor", subtitleAnchor);

            // --- Work phone (grabbable), lying screen-up on the desk
            var phoneGo = MakeGrabbable(game, "WorkPhone", new Vector3(3.62f, 0.762f, 2.82f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.085f, 0.17f, 0.012f), m.Metal, 0.18f, out _);
            var phoneScreen = Child(phoneGo.transform, "ScreenAnchor", new Vector3(0f, 0f, -0.0085f)).transform;
            var speaker = phoneGo.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 1f;
            speaker.maxDistance = 20f;
            var phone = phoneGo.AddComponent<PhoneDevice>();
            Set(phone, "screenAnchor", phoneScreen);
            Set(phone, "speaker", speaker);

            // --- Situation 1-2: email / QR / fake login
            var sitEmail = Situation(game, "Situation_EmailQr");
            var email = sitEmail.AddComponent<EmailQrSituation>();
            email.Configure(SituationId.EmailQr, 20f, -1f, 0, null, 2f, true, false, 120f);
            Set(email, "mail", mail);
            Set(email, "phone", phone);
            var login = sitEmail.AddComponent<FakeLoginSituation>();
            login.Configure(SituationId.FakeLogin, 0f, -1f, 0, null, 2f, true, true, 120f);
            Set(login, "phone", phone);
            Set(login, "monitor", monitor);

            // --- Situation 3: USB
            var sitUsb = Situation(game, "Situation_Usb");
            var usbDrive = BuildUsb(sitUsb.transform, m);
            var dropPoint = Child(sitUsb.transform, "UsbDropPoint", Vector3.zero).transform;
            dropPoint.SetPositionAndRotation(new Vector3(6.0f, 0.1f, 3.05f), Quaternion.LookRotation(new Vector3(-0.6f, 0f, -1f)));
            var dock = Box("UsbDock", sitUsb.transform, new Vector3(3.68f, 0.775f, 3.15f), new Vector3(0.12f, 0.05f, 0.08f), m.Metal);
            var port = Child(sitUsb.transform, "PcPort", Vector3.zero).transform;
            port.SetPositionAndRotation(new Vector3(3.68f, 0.79f, 3.1f), Quaternion.identity);
            Sign(sitUsb.transform, "DockLabel", new Vector3(3.68f, 0.81f, 3.105f), Quaternion.Euler(20f, 0f, 0f), "USB", 0.06f,
                new Vector2(0.1f, 0.03f), Color.white, null);
            Object.DestroyImmediate(dock.GetComponent<Collider>());
            var dropBox = new GameObject("FoundDevicesBox").transform;
            dropBox.SetParent(sitUsb.transform, false);
            dropBox.position = new Vector3(15.3f, 0f, 9.6f);
            Box("Box", dropBox, new Vector3(0f, 0.4f, 0f), new Vector3(0.55f, 0.8f, 0.4f), m.SignNavy, Space.Self);
            Box("Slot", dropBox, new Vector3(0f, 0.805f, 0f), new Vector3(0.3f, 0.01f, 0.06f), m.ScreenOff, Space.Self);
            var boxLabel = Sign(dropBox, "Label", new Vector3(0f, 0.5f, -0.205f), Quaternion.identity,
                "<b>FOUND DEVICES</b>\nDrop lost USB drives here", 0.13f, new Vector2(0.5f, 0.2f), Color.white, null, local: true);
            var dropZone = Child(dropBox, "DropZone", new Vector3(0f, 0.95f, 0f)).transform;
            var usb = sitUsb.AddComponent<UsbSituation>();
            var emptyDeskTarget = Child(sitUsb.transform, "EmptyDeskTrigger", Vector3.zero).transform;
            emptyDeskTarget.position = new Vector3(6.2f, 0f, 3f);
            usb.Configure(SituationId.Usb, 0f, 150f, 0, emptyDeskTarget, 2f, false, false, 0f);
            Set(usb, "usb", usbDrive);
            Set(usb, "dropPoint", dropPoint);
            Set(usb, "pcPort", port);
            Set(usb, "dropBox", dropZone);
            Set(usb, "dropBoxLabel", boxLabel);
            Set(usb, "chat", chat);
            Set(usb, "monitor", monitor);

            // --- Situation 4: colleague (Meeting Room 3 door + panel)
            var sitColleague = Situation(game, "Situation_Colleague");
            var glassDoor = MarkWall(Box("MeetingRoom3_GlassDoor", sitColleague.transform, new Vector3(10f, 1.15f, 10f), new Vector3(1.2f, 2.3f, 0.04f), m.DoorGlass));
            Box("DoorHandle", glassDoor.transform, new Vector3(-0.4f, 0f, -0.8f), new Vector3(0.03f, 0.12f, 1.2f), m.Frame, Space.Self);
            var mrDoor = glassDoor.AddComponent<SlidingDoor>();
            mrDoor.Configure(new Vector3(1.25f, 0f, 0f));
            var doorPanelAnchor = Child(sitColleague.transform, "DoorPanelAnchor", Vector3.zero).transform;
            doorPanelAnchor.SetPositionAndRotation(new Vector3(8.75f, 1.4f, 9.92f), Quaternion.identity);
            var colleague = sitColleague.AddComponent<ColleagueSituation>();
            colleague.Configure(SituationId.Colleague, 0f, 180f, 1, null, 2f, true, false, 150f);
            Set(colleague, "chat", chat);
            Set(colleague, "doorPanelAnchor", doorPanelAnchor);
            Set(colleague, "meetingRoomDoor", mrDoor);

            // --- Clues: lobby directory board, IT room door sign, break-room maintenance notice
            var clues = new GameObject("Clues").transform;
            clues.SetParent(game, false);
            Sign(clues, "DirectoryBoard", new Vector3(21f, 1.6f, 9.93f), Quaternion.identity,
                "<b>NEXORA  -  FLOOR 12 DIRECTORY</b>\n\n" +
                "<b>IT INFRASTRUCTURE</b><pos=55%>IT room, north corridor\n" +
                "<size=85%>   IT service desk: ext 2100</size>\n" +
                "<b>FINANCE</b><pos=55%>Finance pod\n" +
                "<size=85%>   Priya Nair - ext 2147 - after-hours access approved today</size>\n" +
                "<b>MEETING ROOM 3</b><pos=55%>North corridor\n" +
                "<b>SECURITY DESK</b><pos=55%>Ground floor - ext 1000",
                0.3f, new Vector2(1.9f, 1.0f), new Color(0.9f, 0.95f, 1f), m.SignNavy).alignment = TextAlignmentOptions.MidlineLeft;
            // Gaze targets sit on the two relevant lines (text is vertically centred on y = 1.6).
            Clue(clues, "Clue_PriyaEntry", new Vector3(21f, 1.54f, 9.9f), ClueReadable.Clue.PriyaDirectoryEntry, 0.35f, 3.5f);
            Clue(clues, "Clue_ItEntry", new Vector3(21f, 1.66f, 9.9f), ClueReadable.Clue.ItDirectoryEntry, 0.35f, 3.5f);
            var itDoor = MarkWall(Box("ItRoom_Door", clues, new Vector3(16.5f, 1.15f, 10f), new Vector3(1.1f, 2.3f, 0.06f), m.Metal));
            Box("ItRoom_Keypad", itDoor.transform, new Vector3(0.62f, 0.05f, -1.2f), new Vector3(0.1f, 0.07f, 0.4f), m.Frame, Space.Self);
            Sign(clues, "ItRoom_Sign", new Vector3(16.5f, 2.55f, 9.93f), Quaternion.identity,
                "<b>IT INFRASTRUCTURE</b>\n<size=70%>Authorised staff only</size>", 0.22f, new Vector2(1.3f, 0.26f), Color.white, m.SignRed);
            var board = new GameObject("BulletinBoard").transform;
            board.SetParent(clues, false);
            board.SetPositionAndRotation(new Vector3(17.93f, 1.55f, 4.5f), Quaternion.LookRotation(Vector3.right));
            Box("Cork", board, new Vector3(0f, 0f, 0.01f), new Vector3(1.4f, 1.0f, 0.02f), m.Cork, Space.Self);
            Sign(board, "MaintenanceNotice", new Vector3(-0.32f, 0.02f, -0.005f), Quaternion.Euler(0f, 0f, 2f),
                "<b>BUILDING MAINTENANCE</b>\nTonight 6:30 PM\n\nPlant-room contractors only.\nNo IT work scheduled tonight.\n\nIT service desk: ext 2100",
                0.13f, new Vector2(0.5f, 0.6f), new Color(0.1f, 0.1f, 0.12f), m.Paper, local: true);
            Sign(board, "BakeSale", new Vector3(0.36f, 0.22f, -0.005f), Quaternion.Euler(0f, 0f, -3f),
                "<b>BAKE SALE</b>\nFriday 12:00\nBreak room", 0.12f, new Vector2(0.4f, 0.28f), new Color(0.3f, 0.1f, 0.1f),
                Mats.Make("PaperPink", new Color(1f, 0.85f, 0.88f), unlit: true), local: true);
            Sign(board, "FireDrill", new Vector3(0.36f, -0.22f, -0.005f), Quaternion.Euler(0f, 0f, 2f),
                "Fire drill\nfirst Monday\nof the month", 0.11f, new Vector2(0.4f, 0.26f), new Color(0.1f, 0.1f, 0.12f), m.Paper, local: true);
            Clue(board, "Clue_MaintenanceNotice", new Vector3(17.9f, 1.57f, 4.82f), ClueReadable.Clue.MaintenanceNotice, 0.3f, 2.5f);

            // --- Situation 5: intercom (lobby, beside the main door)
            var sitIntercom = Situation(game, "Situation_Intercom");
            var mainDoorGo = MarkWall(Box("MainDoor", sitIntercom.transform, new Vector3(24f, 1.2f, 5f), new Vector3(0.05f, 2.4f, 1.7f), m.DoorGlass));
            Box("MainDoorBar", mainDoorGo.transform, new Vector3(-1f, 0f, 0f), new Vector3(0.6f, 0.02f, 0.8f), m.Frame, Space.Self);
            var mainDoor = mainDoorGo.AddComponent<SlidingDoor>();
            mainDoor.Configure(new Vector3(0f, 0f, 1.75f));
            Sign(sitIntercom.transform, "ExitSign", new Vector3(23.93f, 2.6f, 5f), Quaternion.LookRotation(Vector3.right), "EXIT", 0.28f,
                new Vector2(0.6f, 0.16f), Color.white, m.ExitGreen);
            var intercomAnchor = new GameObject("Intercom").transform;
            intercomAnchor.SetParent(sitIntercom.transform, false);
            intercomAnchor.SetPositionAndRotation(new Vector3(23.9f, 1.55f, 7.6f), Quaternion.LookRotation(Vector3.right));
            Box("IntercomUnit", intercomAnchor, new Vector3(0f, 0f, 0.03f), new Vector3(0.56f, 0.42f, 0.04f), m.Metal, Space.Self);
            Sign(intercomAnchor, "IntercomLabel", new Vector3(0f, 0.27f, 0f), Quaternion.identity, "DOOR INTERCOM", 0.16f,
                new Vector2(0.56f, 0.06f), Color.white, null, local: true);
            var intercomScreen = Child(intercomAnchor, "Screen", new Vector3(0f, 0f, 0f)).transform;
            var buzzer = mainDoorGo.AddComponent<AudioSource>();
            buzzer.playOnAwake = false;
            buzzer.spatialBlend = 0.55f;
            buzzer.maxDistance = 40f;
            buzzer.volume = 1f;
            var intercom = sitIntercom.AddComponent<IntercomSituation>();
            intercom.Configure(SituationId.Intercom, 0f, 360f, 2, null, 2f, true, false, 0f);
            Set(intercom, "buzzer", buzzer);
            Set(intercom, "intercomAnchor", intercomAnchor);
            Set(intercom, "screenAnchor", intercomScreen);
            Set(intercom, "mainDoor", mainDoor);
            Set(intercom, "monitor", monitor);
            Set(intercom, "mail", mail);

            // --- Situation 6: MFA
            var sitMfa = Situation(game, "Situation_Mfa");
            var mfa = sitMfa.AddComponent<MfaSituation>();
            mfa.Configure(SituationId.Mfa, 0f, 600f, 0, null, 2f, true, false, 0f);
            Set(mfa, "phone", phone);

            // --- Situation 7: unlocked workstation (Finance pod)
            var sitWs = Situation(game, "Situation_Workstation");
            Desk(sitWs.transform, "ColleagueDesk", new Vector3(3.25f, 0f, 13.6f), 0f, new Vector2(1.6f, 0.75f), m);
            Chair(sitWs.transform, new Vector3(3.25f, 0f, 12.9f), 0f, m);
            var wsMon = new GameObject("ColleagueMonitor").transform;
            wsMon.SetParent(sitWs.transform, false);
            wsMon.SetPositionAndRotation(new Vector3(3.25f, 1.15f, 13.85f), Quaternion.Euler(5f, 0f, 0f));
            Box("Body", wsMon, new Vector3(0f, 0f, 0.015f), new Vector3(0.66f, 0.42f, 0.03f), m.Metal, Space.Self);
            Box("Stand", wsMon, new Vector3(0f, -0.3f, 0.03f), new Vector3(0.05f, 0.25f, 0.03f), m.Metal, Space.Self);
            var unlocked = Sign(wsMon, "UnlockedScreen", new Vector3(0f, 0f, -0.006f), Quaternion.identity,
                "<b>Q3 payroll.xlsx</b>\n" +
                "Name<pos=45%>Salary<pos=75%>Bonus\n" +
                "A. Okafor<pos=45%>$84,200<pos=75%>$6,000\n" +
                "D. Whitfield<pos=45%>$112,500<pos=75%>$9,500\n" +
                "P. Nair<pos=45%>$78,900<pos=75%>$5,200\n" +
                "J. Morgan<pos=45%>$69,300<pos=75%>$4,100",
                0.15f, new Vector2(0.6f, 0.36f), new Color(0.05f, 0.25f, 0.1f), Mats.Make("Spreadsheet", new Color(0.93f, 0.97f, 0.93f), unlit: true), local: true);
            unlocked.alignment = TextAlignmentOptions.TopLeft;
            var locked = Sign(wsMon, "LockedScreen", new Vector3(0f, 0f, -0.009f), Quaternion.identity,
                "<b>Locked</b>\nPress Ctrl + Alt + Del to unlock", 0.18f, new Vector2(0.6f, 0.36f), Color.white, m.ScreenSaver, local: true);
            Box("ColleagueKeyboard", sitWs.transform, new Vector3(3.25f, 0.765f, 13.45f), new Vector3(0.44f, 0.02f, 0.14f), m.Metal);
            var note = Sign(sitWs.transform, "PasswordNote", new Vector3(3.5f, 0.756f, 13.33f), Quaternion.Euler(90f, 12f, 0f),
                "pw: Summer2024!", 0.09f, new Vector2(0.11f, 0.06f), new Color(0.2f, 0.15f, 0.05f), m.Sticky);
            var wsPanel = Child(sitWs.transform, "WorkstationPanelAnchor", Vector3.zero).transform;
            wsPanel.SetPositionAndRotation(new Vector3(4.3f, 1.35f, 13.5f), FacePlayerFrom(new Vector3(3.25f, 0f, 12.2f), new Vector3(4.3f, 1.35f, 13.5f)));
            wsPanel.gameObject.AddComponent<FaceViewer>();
            var ws = sitWs.AddComponent<WorkstationSituation>();
            ws.Configure(SituationId.Workstation, 0f, -1f, 0, wsMon, 1.6f, false, false, 0f);
            Set(ws, "panelAnchor", wsPanel);
            Set(ws, "unlockedScreen", unlocked.transform.parent.gameObject);
            Set(ws, "lockedScreen", locked.transform.parent.gameObject);
            Set(ws, "stickyNote", note.transform.parent.gameObject);

            // --- Ending flow (lobby security terminal) and ending screen
            var terminal = new GameObject("SecurityTerminal").transform;
            terminal.SetParent(game, false);
            terminal.SetPositionAndRotation(new Vector3(20.6f, 0f, 3.6f), Quaternion.LookRotation(Vector3.right));
            Box("Pedestal", terminal, new Vector3(0f, 0.5f, 0.12f), new Vector3(0.5f, 1.0f, 0.3f), m.Metal, Space.Self);
            Box("Bezel", terminal, new Vector3(0f, 1.5f, 0.03f), new Vector3(0.96f, 0.96f, 0.04f), m.Metal, Space.Self);
            var terminalAnchor = Child(terminal, "ScreenAnchor", new Vector3(0f, 1.5f, -0.002f)).transform;
            var endingGo = new GameObject("EndingScreen");
            endingGo.transform.SetParent(game, false);
            endingGo.transform.SetPositionAndRotation(new Vector3(20f, 1.6f, 3.6f), Quaternion.LookRotation(Vector3.right));
            var endingScreen = endingGo.AddComponent<EndingScreen>();
            endingGo.AddComponent<FaceViewer>();
            var flow = systems.AddComponent<EndingFlow>();
            Set(flow, "terminalAnchor", terminalAnchor);
            Set(flow, "exitDoor", mainDoor);
            Set(flow, "endingScreen", endingScreen);
            var all = new SituationTrigger[] { email, login, usb, colleague, intercom, mfa, ws };
            var so = new SerializedObject(flow);
            var arr = so.FindProperty("situations");
            arr.arraySize = all.Length;
            for (int i = 0; i < all.Length; i++)
            {
                arr.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // --- Opening wiring
            var start = systems.AddComponent<ShiftStart>();
            Set(start, "welcome", welcome);
            Set(start, "deskPhone", deskPhone);
            Set(start, "monitor", monitor);

            // Wall clocks bound to game time.
            WallClockAt(game, new Vector3(3f, 2.45f, 7.93f), Quaternion.identity, m);
            WallClockAt(game, new Vector3(23.93f, 2.45f, 2.5f), Quaternion.LookRotation(Vector3.right), m);
            WallClockAt(game, new Vector3(12.07f, 2.45f, 2.5f), Quaternion.LookRotation(Vector3.left), m);
        }

        private static GameObject Situation(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Clue(Transform parent, string name, Vector3 worldPos, ClueReadable.Clue kind, float radius, float distance)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.AddComponent<ClueReadable>().Configure(kind, radius, distance);
        }

        private static void WallClockAt(Transform parent, Vector3 pos, Quaternion rot, Mats m)
        {
            var label = Sign(parent, "WallClock", pos, rot, "5:45 PM", 0.42f, new Vector2(0.75f, 0.2f), new Color(0.55f, 0.9f, 1f), m.ScreenOff);
            label.fontStyle = FontStyles.Bold;
            var clock = label.transform.parent.gameObject.AddComponent<WallClock>();
            Set(clock, "label", label);
        }

        private static void Ambient(Transform parent, string name, Vector3 pos, string clipName, float seconds, Func<float, float> wave,
            float volume, float maxDistance)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var a = go.AddComponent<AudioSource>();
            a.clip = MakeClip(clipName, seconds, wave);
            a.loop = true;
            a.playOnAwake = true;
            a.volume = volume;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 1f;
            a.maxDistance = maxDistance;
        }

        /// <summary>Grabbable prop: near grab, hand grab and distance grab, plus a select event wrapper.</summary>
        private static GameObject MakeGrabbable(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 size, Material mat,
            float mass, out PointableUnityEventWrapper wrapper)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            var visual = Part(PrimitiveType.Cube, "Visual", go.transform, Vector3.zero, size, mat);
            var col = go.AddComponent<BoxCollider>();
            col.size = size;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var grabbable = go.AddComponent<Grabbable>();
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.InjectOptionalThrowWhenUnselected(false);

            var interactables = new GameObject("Interactables");
            interactables.transform.SetParent(go.transform, false);
            var grab = interactables.AddComponent<GrabInteractable>();
            grab.InjectAllGrabInteractable(rb);
            grab.InjectOptionalPointableElement(grabbable);
            var handGrab = interactables.AddComponent<HandGrabInteractable>();
            handGrab.InjectAllHandGrabInteractable(GrabTypeFlags.All, rb, GrabbingRule.DefaultPinchRule, GrabbingRule.DefaultPalmRule);
            handGrab.InjectOptionalPointableElement(grabbable);
            var distanceGrab = interactables.AddComponent<DistanceGrabInteractable>();
            distanceGrab.InjectAllGrabInteractable(rb);
            distanceGrab.InjectOptionalPointableElement(grabbable);
            var distanceHandGrab = interactables.AddComponent<DistanceHandGrabInteractable>();
            distanceHandGrab.InjectAllDistanceHandGrabInteractable(GrabTypeFlags.All, rb, GrabbingRule.DefaultPinchRule, GrabbingRule.DefaultPalmRule);
            distanceHandGrab.InjectOptionalPointableElement(grabbable);

            wrapper = go.AddComponent<PointableUnityEventWrapper>();
            wrapper.InjectAllPointableUnityEventWrapper(grabbable);
            visual.name = "Visual";
            return go;
        }

        private static GameObject BuildUsb(Transform parent, Mats m)
        {
            var usb = MakeGrabbable(parent, "UsbDrive", new Vector3(6f, 0.1f, 3f), Quaternion.identity,
                new Vector3(0.1f, 0.022f, 0.036f), m.Usb, 0.05f, out _);
            Part(PrimitiveType.Cube, "Plug", usb.transform, new Vector3(0.062f, 0f, 0f), new Vector3(0.024f, 0.01f, 0.018f), m.Frame);
            Part(PrimitiveType.Cube, "LabelBack", usb.transform, new Vector3(-0.005f, 0.0115f, 0f), new Vector3(0.085f, 0.001f, 0.032f), m.UsbLabel);
            var tag = CreateLabel(usb.transform, "Label", new Vector3(-0.005f, 0.0125f, 0f), 0.05f, "<b>CLIENT DATA\nURGENT</b>", new Color(0.6f, 0.05f, 0.05f));
            tag.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tag.rectTransform.sizeDelta = new Vector2(0.08f, 0.03f);
            tag.lineSpacing = -15f;
            return usb;
        }

        private static void BuildRoomZones()
        {
            var zones = new GameObject("RoomZones").transform;
            void Zone(string room, Vector3 center, Vector3 size, int priority = 0)
            {
                var go = new GameObject("Zone_" + room);
                go.transform.SetParent(zones, false);
                go.transform.position = center;
                go.AddComponent<RoomZone>().Configure(room, size, priority);
            }
            Zone("OpenPlan", new Vector3(5f, 1.5f, 4f), new Vector3(10f, 3f, 8f));
            Zone("Corridor", new Vector3(9f, 1.5f, 9f), new Vector3(18f, 3f, 2f));
            Zone("Corridor", new Vector3(11f, 1.5f, 5f), new Vector3(2f, 3f, 6f));
            Zone("BreakRoom", new Vector3(15f, 1.5f, 4f), new Vector3(6f, 3f, 8f));
            Zone("Lobby", new Vector3(21f, 1.5f, 5f), new Vector3(6f, 3f, 10f));
            Zone("Lobby", new Vector3(25.25f, 1.5f, 5f), new Vector3(2.5f, 3f, 3f));
            Zone("FinancePod", new Vector3(3.25f, 1.5f, 13f), new Vector3(6.5f, 3f, 6f));
            Zone("MeetingRoom3", new Vector3(10f, 1.5f, 13f), new Vector3(7f, 3f, 6f));
            Zone("ItRoomDoor", new Vector3(16.5f, 1.5f, 9f), new Vector3(3f, 3f, 2f), priority: 1);
        }

        private static void WriteFloorPlan()
        {
            Directory.CreateDirectory("docs");
            File.WriteAllText("docs/FLOORPLAN.md",
@"# CyberQuest - Office_Main floor plan

Generated by `CyberQuest > 2. Build Office Scene` (Scripts/Editor/OfficeSceneBuilder.cs).
x = east, z = north, metres. Ceilings 3 m. About 26 m x 16 m.

```
 z=16 +-----------------+----------------------+-----------------+
      |  FINANCE POD    |   MEETING ROOM 3     |    IT ROOM      |
      |  colleague PC   |   table, whiteboard, |   (locked)      |
      |  (Situation 7)  |   Priya's charger    |                 |
      |                 |                      |                 |
 z=10 +----[open]-------+-----[glass door]-----+--[locked door]--+------------+
      |  CORRIDOR (2 m)    MR3 door panel       drop box  'IT INFRASTRUCTURE' |
 z=8  +---------[open]-------------+     +-----------------------+   LOBBY    |
      |                            |  C  |                       |  directory |
      |   OPEN-PLAN OFFICE         |  O  |     BREAK ROOM        |  board     |[MAIN DOOR]
      |   spawn + player desk      |  R [open]  bulletin board   |  intercom  |  > lifts
      |   empty desk 3 m east  [open] R  |  (maintenance notice) |  security  |
      |                            |  .  |  vending machine      |  terminal  |
 z=0  +----------------------------+-----+-----------------------+------------+
     x=0                         x=10  x=12                    x=18         x=24
```

| Space | Bounds (x, z) | Contents |
|---|---|---|
| Open plan (spawn) | 0-10, 0-8 | Player desk (monitor, desk phone, work phone, USB dock, sticky note), empty desk (USB drop), wall clock |
| Corridor | 0-18, 8-10 and 10-12, 2-8 | Meeting Room 3 door panel, Found devices drop box, IT room door + sign |
| Break room | 12-18, 0-8 | Bulletin board with the maintenance notice, vending machine, ambient hum/radio |
| Lobby | 18-24, 0-10 | Main door (exit), intercom, security terminal, directory board |
| Finance pod | 0-6.5, 10-16 | Unlocked colleague workstation + password note |
| Meeting Room 3 | 6.5-13.5, 10-16 | Glass door (unlocked from the corridor panel), Priya's charger |
| IT room | 13.5-20, 10-16 | Locked |

Every walkable floor is a teleport surface. Longest trip (Finance pod to lobby intercom) is about 22 m:
roughly 15-20 s by teleport.
");
        }

        // ------------------------------------------------------------------ helpers

        private static void MarkStatic(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                                                     StaticEditorFlags.OccludeeStatic);
            }
        }

        /// <summary>Sign: optional backing quad plus text. rot's forward = the viewer's look direction.</summary>
        private static TextMeshPro Sign(Transform parent, string name, Vector3 pos, Quaternion rot, string text, float fontSize,
            Vector2 size, Color color, Material background, bool local = false)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            if (local)
            {
                root.localPosition = pos;
                root.localRotation = rot;
            }
            else
            {
                root.SetPositionAndRotation(pos, rot);
            }
            if (background != null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Background";
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(root, false);
                quad.transform.localPosition = new Vector3(0f, 0f, 0.002f);
                quad.transform.localScale = new Vector3(size.x + 0.03f, size.y + 0.03f, 1f);
                quad.GetComponent<MeshRenderer>().sharedMaterial = background;
            }
            var label = CreateLabel(root, "Text", Vector3.zero, fontSize, text, color);
            label.rectTransform.sizeDelta = size;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
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
            if (_fontMaterial != null) tmp.fontSharedMaterial = _fontMaterial;
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

        /// <summary>Rotation whose forward points away from the spawn point, so panels read correctly from the desk.</summary>
        private static Quaternion FacePlayer(Vector3 pos) => Quaternion.LookRotation(Flat(pos - PlayerOrigin));

        private static Quaternion FacePlayerFrom(Vector3 viewer, Vector3 pos) => Quaternion.LookRotation(Flat(pos - viewer));

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
    }
}
