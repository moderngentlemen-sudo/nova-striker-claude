// One-time project setup: menu Nova Striker > Set Up Project. It makes everything the port needs that is an
// asset rather than code, so the repository holds only scripts and shaders:
//   - the template materials (Resources/NovaStriker/*.mat) the game clones its materials from (they also keep
//     their shaders and keyword variants in builds)
//   - the URP pipeline asset with two renderers (0: High and Low; 1: Ultra, adding Screen Space Ambient
//     Occlusion), each with the Grade pass as a Full Screen Pass feature after post-processing
//   - player settings: linear colour space, both input backends (the Input System drives the game)
//   - the scene (Scenes/NovaStriker.unity) with the GameMain object, added to the build
// Running it again rebuilds the generated assets in place.
using System.IO;
using NovaStriker.Game;
using NovaStriker.Game.Three;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NovaStriker.EditorTools
{
    public static class NovaSetup
    {
        const string ROOT = "Assets/NovaStriker", RES = ROOT + "/Resources/NovaStriker", SETTINGS = ROOT + "/Settings", SCENES = ROOT + "/Scenes";

        [MenuItem("Nova Striker/Set Up Project", priority = 0)]
        public static void SetUp()
        {
            foreach (var d in new[] { RES, SETTINGS, SCENES }) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
            MakeTemplates();
            var asset = MakePipeline();
            GraphicsSettings.defaultRenderPipeline = asset;
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = null; }
            PlayerSettings.colorSpace = ColorSpace.Linear;
            SetInputBackends();
            MakeScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Nova Striker: project set up. Open Scenes/NovaStriker.unity and press Play. (If Unity asks to restart for the input backends, say yes.)");
        }

        // ---- Template materials ----
        static readonly (string name, string shader, bool transparent, bool surface, bool coat)[] TEMPLATES =
        {
            ("Lit", "Universal Render Pipeline/Lit", false, false, false),
            ("LitTransparent", "Universal Render Pipeline/Lit", true, false, false),
            ("LitSurface", "Universal Render Pipeline/Lit", false, true, false),
            ("LitSurfaceTransparent", "Universal Render Pipeline/Lit", true, true, false),
            ("Coat", "Universal Render Pipeline/Complex Lit", false, false, true),
            ("CoatTransparent", "Universal Render Pipeline/Complex Lit", true, false, true),
            ("Unlit", "NovaStriker/Unlit", false, false, false),
            ("Outline", "NovaStriker/Outline", false, false, false),
            ("Rim", "NovaStriker/Rim", false, false, false),
            ("Sky", "NovaStriker/Sky", false, false, false),
            ("Grade", "NovaStriker/Grade", false, false, false),
            ("Aegis", "NovaStriker/Aegis", false, false, false),
        };
        static void MakeTemplates()
        {
            foreach (var (name, shader, transparent, surface, coat) in TEMPLATES)
            {
                var sh = Shader.Find(shader);
                if (sh == null) { Debug.LogError($"Nova Striker setup: shader '{shader}' not found"); continue; }
                string path = $"{RES}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(sh) { name = name }; AssetDatabase.CreateAsset(m, path); }
                else m.shader = sh;
                Templates.Configure(m, transparent, surface, coat);
                EditorUtility.SetDirty(m);
            }
        }

        // ---- The pipeline ----
        static UniversalRenderPipelineAsset MakePipeline()
        {
            var high = MakeRenderer($"{SETTINGS}/Renderer.asset", false);
            var ultra = MakeRenderer($"{SETTINGS}/Renderer Ultra.asset", true);
            string path = $"{SETTINGS}/NovaStriker URP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null) { asset = UniversalRenderPipelineAsset.Create(high); AssetDatabase.CreateAsset(asset, path); }
            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            list.arraySize = 2; list.GetArrayElementAtIndex(0).objectReferenceValue = high; list.GetArrayElementAtIndex(1).objectReferenceValue = ultra;
            Set(so, "m_DefaultRendererIndex", 0);
            Set(so, "m_MainLightShadowsSupported", true);
            Set(so, "m_SoftShadowsSupported", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            asset.supportsHDR = true;
            asset.msaaSampleCount = 4;                  // (the prototype renders through a 4x multisampled target)
            asset.supportsCameraDepthTexture = true;
            asset.mainLightShadowmapResolution = 2048;
            asset.shadowDistance = 70;
            asset.shadowCascadeCount = 2;
            EditorUtility.SetDirty(asset);
            return asset;
        }
        static void Set(SerializedObject so, string prop, object v)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning($"Nova Striker setup: no property {prop} on {so.targetObject.name}"); return; }
            switch (v)
            {
                case bool b: p.boolValue = b; break;
                case int i: if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = i; else p.intValue = i; break;
                case float f: p.floatValue = f; break;
            }
        }
        static UniversalRendererData MakeRenderer(string path, bool ao)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<UniversalRendererData>();
                data.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(data, path);
            }
            // Rebuild the features: ambient occlusion (Ultra), then the grade
            foreach (var f in data.rendererFeatures.ToArray()) { if (f != null) Object.DestroyImmediate(f, true); }
            data.rendererFeatures.Clear();
            if (ao)
            {
                var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>(); ssao.name = "Ambient Occlusion";
                AddFeature(data, ssao);
            }
            var grade = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>(); grade.name = "Grade";
            grade.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            grade.passMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{RES}/Grade.mat");
            grade.fetchColorBuffer = true;
            AddFeature(data, grade);
            data.SetDirty(); EditorUtility.SetDirty(data);
            return data;
        }
        static void AddFeature(UniversalRendererData data, ScriptableRendererFeature f)
        {
            AssetDatabase.AddObjectToAsset(f, data);
            data.rendererFeatures.Add(f);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f, out _, out long id);
            var so = new SerializedObject(data); var map = so.FindProperty("m_RendererFeatureMap");
            if (map != null)
            {
                if (map.arraySize > data.rendererFeatures.Count - 1) map.arraySize = data.rendererFeatures.Count - 1;
                map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---- Input: the Input System runs the game; the old backend stays on for the editor's own tools ----
        static void SetInputBackends()
        {
            var ps = Resources.FindObjectsOfTypeAll<PlayerSettings>();
            if (ps.Length == 0) return;
            var so = new SerializedObject(ps[0]);
            var p = so.FindProperty("activeInputHandler");
            if (p != null && p.intValue != 2) { p.intValue = 2; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        // ---- The scene ----
        static void MakeScene()
        {
            string path = $"{SCENES}/NovaStriker.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Nova Striker").AddComponent<GameMain>();
            EditorSceneManager.SaveScene(scene, path);
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == path)) scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
