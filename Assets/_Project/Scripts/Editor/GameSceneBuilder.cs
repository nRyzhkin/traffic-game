using System;
using TrafficGame.App;
using TrafficGame.Views.Cameras;
using TrafficGame.Views.City;
using TrafficGame.Views.Debugging;
using System.Linq;
using TrafficGame.Views.Roads;
using TrafficGame.Views.Simulation;
using TrafficGame.Views.Tools;
using TrafficGame.Views.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TrafficGame.Editor
{
    /// <summary>
    /// Generates the Game scene from code so it never has to be edited by hand.
    /// Tune values in <see cref="GameConfig"/>, not in the scene.
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string ConfigPath = "Assets/_Project/Data/GameConfig.asset";
        private const string MapMaterialPath = "Assets/_Project/Art/Materials/M_Map.mat";
        private const string MapShaderName = "TrafficGame/UnlitVertexColor";
        private const string SandboxLevelPath = "Assets/_Project/Levels/Sandbox.json";
        private const string CampaignFolder = "Assets/_Project/Levels/Campaign";
        private const string LocalizationFolder = "Assets/_Project/Localization";
        private const string PanelSettingsPath = "Assets/_Project/UI/PanelSettings.asset";
        private const string ThemePath = "Assets/_Project/UI/GameTheme.tss";
        private const string GameLayoutPath = "Assets/_Project/UI/Game.uxml";

        [MenuItem("Traffic Game/Rebuild Game Scene", priority = 20)]
        public static void Rebuild()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Open the new scene first: switching scenes unloads unused assets, including ones created just before.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var config = LoadOrCreateAsset(ConfigPath, () => ScriptableObject.CreateInstance<GameConfig>());
            var mapMaterial = LoadOrCreateAsset(MapMaterialPath, () => new Material(FindShader(MapShaderName)));
            var panelSettings = LoadOrCreateAsset(PanelSettingsPath, CreatePanelSettings);
            AssignLevels(config);
            AssignLanguageFiles(config);
            AssetDatabase.SaveAssets();

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(CameraView), typeof(CameraInputAdapter))
            {
                tag = "MainCamera"
            };
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = config.LightMap.Background;
            camera.GetUniversalAdditionalCameraData();

            var ground = CreateMeshView<CheckerGroundView>("Ground", mapMaterial);
            var roads = CreateMeshView<RoadNetworkView>("Roads", mapMaterial);
            var laneGraph = CreateMeshView<LaneGraphDebugView>("LaneGraphDebug", mapMaterial);
            var roadPreview = CreateMeshView<RoadPreviewView>("RoadPreview", mapMaterial);
            var buildings = CreateMeshView<BuildingsView>("Buildings", mapMaterial);
            var congestion = CreateMeshView<CongestionView>("Congestion", mapMaterial);
            var junctionControls = CreateMeshView<JunctionControlsView>("JunctionControls", mapMaterial);
            var traffic = CreateMeshView<TrafficView>("Traffic", mapMaterial);
            var ticker = traffic.gameObject.AddComponent<FrameTicker>();
            var input = new GameObject("Input", typeof(MapPointerInputAdapter), typeof(ToolHotkeysAdapter));

            var ui = new GameObject("UI", typeof(UIDocument), typeof(GameUi));
            var document = ui.GetComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GameLayoutPath);

            var bootstrap = new GameObject("Bootstrap").AddComponent<GameBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            SetReference(serialized, "_config", config);
            SetReference(serialized, "_cameraView", cameraObject.GetComponent<CameraView>());
            SetReference(serialized, "_cameraInput", cameraObject.GetComponent<CameraInputAdapter>());
            SetReference(serialized, "_ground", ground);
            SetReference(serialized, "_views._roads", roads);
            SetReference(serialized, "_views._laneGraph", laneGraph);
            SetReference(serialized, "_views._preview", roadPreview);
            SetReference(serialized, "_views._buildings", buildings);
            SetReference(serialized, "_views._congestion", congestion);
            SetReference(serialized, "_views._junctionControls", junctionControls);
            SetReference(serialized, "_views._traffic", traffic);
            SetReference(serialized, "_views._frames", ticker);
            SetReference(serialized, "_views._pointer", input.GetComponent<MapPointerInputAdapter>());
            SetReference(serialized, "_views._hotkeys", input.GetComponent<ToolHotkeysAdapter>());
            SetReference(serialized, "_views._ui", ui.GetComponent<GameUi>());
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[{nameof(GameSceneBuilder)}] Rebuilt {ScenePath}.");
        }

        private static T CreateMeshView<T>(string name, Material material) where T : Component
        {
            var gameObject = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(T));
            gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            return gameObject.GetComponent<T>();
        }

        private static PanelSettings CreatePanelSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            return settings;
        }

        /// <summary>Sandbox from its fixed path; campaign levels are every JSON in the campaign folder, ordered by file name.</summary>
        private static void AssignLevels(GameConfig config)
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("_sandboxLevel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(SandboxLevelPath);
            SetArray(serialized.FindProperty("_campaignLevels"), FindTextAssets(CampaignFolder));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TextAsset[] FindTextAssets(string folder) =>
            AssetDatabase.FindAssets("t:TextAsset", new[] { folder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .OrderBy(file => file.name)
                .ToArray();

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        /// <summary>Points the config at every language file in the localization folder.</summary>
        private static void AssignLanguageFiles(GameConfig config)
        {
            var serialized = new SerializedObject(config);
            SetArray(serialized.FindProperty("_languageFiles"), FindTextAssets(LocalizationFolder));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T LoadOrCreateAsset<T>(string path, Func<T> create) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Shader FindShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null)
                throw new InvalidOperationException($"Shader '{name}' not found.");
            return shader;
        }

        private static void SetReference(SerializedObject target, string field, Object value)
        {
            var property = target.FindProperty(field) ??
                           throw new InvalidOperationException($"{target.targetObject.GetType().Name} has no serialized field '{field}'.");
            property.objectReferenceValue = value;
        }
    }
}
