using System;
using System.IO;
using System.Linq;
using ShenYi.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShenYi.Prototype.Editor
{
    [InitializeOnLoad]
    public static class PrototypeEditor
    {
        const string ScenePath = "Assets/_Game/Scenes/PrototypeBattle.unity";
        const string CommandPath = "Library/ShenYiPrototypeCommand.json";
        static double nextCheck;
        [Serializable] sealed class Command { public string action = "", path = ""; }
        static PrototypeEditor()
        {
            EditorApplication.delayCall += Initialize;
            EditorApplication.update += PollCommands;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            var game = UnityEngine.Object.FindObjectOfType<PrototypeGame>();
            if (game == null || !game.AutoSmokeTest || SceneManager.GetActiveScene().path != ScenePath) return;
            game.AutoSmokeTest = false; game.ScreenshotPath = "";
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        static void Initialize()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath)) Setup();
        }
        [MenuItem("神奕战场/创建或打开竖屏原型")]
        public static void Setup()
        {
            if (EditorApplication.isPlaying) return;
            if (!File.Exists(ScenePath))
            {
                var old = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .03f, .065f); camera.orthographic = true;
                cameraObject.transform.position = new Vector3(0, 0, -10);
                new GameObject("PrototypeGame", typeof(PrototypeGame));
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorSceneManager.CloseScene(scene, true);
                if (old.IsValid()) SceneManager.SetActiveScene(old);
            }
            var others = EditorBuildSettings.scenes.Where(x => x.path != ScenePath).ToList();
            others.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = others.ToArray();
            PlayerSettings.defaultScreenWidth = 540; PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false; PlayerSettings.allowedAutorotateToLandscapeRight = false;
            AssetDatabase.SaveAssets();
            // Do not discard unsaved scene changes when importing the prototype into an already open editor.
            if (!SceneManager.GetActiveScene().isDirty) EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("神奕战场：竖屏原型已准备好。打开 PrototypeBattle 并点击 Play 即可。");
        }
        [MenuItem("神奕战场/开始原型")]
        public static void Play()
        {
            if (EditorApplication.isPlaying) return;
            if (SceneManager.GetActiveScene().isDirty) { Debug.LogWarning("请先保存当前场景，再运行原型。"); return; }
            Setup(); EditorSceneManager.OpenScene(ScenePath); EditorApplication.isPlaying = true;
        }
        [MenuItem("神奕战场/运行规则自检")]
        public static void Verify()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Game/Resources/Prototype/PrototypeData.json");
            var data = JsonUtility.FromJson<PrototypeData>(text.text); data.Validate();
            string result = BattleChecks.Run(data);
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/PrototypeRules.txt", result); Debug.Log(result);
        }
        [MenuItem("神奕战场/构建 Windows 试玩")]
        public static void BuildWindows()
        {
            Setup(); Verify(); Directory.CreateDirectory("Builds/Windows");
            var result = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Windows/ShenYiPrototype.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
            File.WriteAllText("Logs/PrototypeBuild.txt", result.summary.result + "\nErrors: " + result.summary.totalErrors + "\nWarnings: " + result.summary.totalWarnings);
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Windows prototype build failed.");
        }
        [MenuItem("神奕战场/构建透视棋盘试玩")]
        public static void BuildPerspective()
        {
            Setup(); Verify(); Directory.CreateDirectory("Builds/PerspectiveWindows");
            var result = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/PerspectiveWindows/ShenYiPerspective.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
            File.WriteAllText("Logs/PerspectiveBuild.txt", result.summary.result + "\nErrors: " + result.summary.totalErrors + "\nWarnings: " + result.summary.totalWarnings);
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Perspective build failed.");
        }
        // Local editor task queue: only known prototype actions are accepted; no arbitrary code execution.
        static void PollCommands()
        {
            if (EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            if (!File.Exists(CommandPath)) return;
            try
            {
                var command = JsonUtility.FromJson<Command>(File.ReadAllText(CommandPath)); File.Delete(CommandPath);
                if (command.action == "verify") Verify();
                else if (command.action == "setup") Setup();
                else if (command.action == "cardEditor") CardPrefabEditorWindow.Open();
                else if (command.action == "cardChecks") CardPrefabEditorChecks.Run();
                else if (command.action == "build") BuildWindows();
                else if (command.action == "play") Play();
                else if (command.action == "stop") EditorApplication.isPlaying = false;
                else if (command.action == "smoke")
                {
                    Setup(); EditorSceneManager.OpenScene(ScenePath);
                    var game = UnityEngine.Object.FindObjectOfType<PrototypeGame>(); game.AutoSmokeTest = true; game.ScreenshotPath = command.path;
                    EditorApplication.isPlaying = true;
                }
            }
            catch (Exception error) { Debug.LogException(error); File.WriteAllText("Logs/PrototypeError.txt", error.ToString()); }
        }
    }
}
