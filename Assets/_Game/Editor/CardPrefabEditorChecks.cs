using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShenYi.Prototype.Editor
{
    public static class CardPrefabEditorChecks
    {
        [MenuItem("神奕战场/卡牌工具/运行保存与预览自检")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("请退出 Play 模式后运行卡牌编辑器自检。"); return; }
            CardPrefabSamples.EnsureSamples();
            string folder="Assets/_Game/CardEditorCheck_"+Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                string samplePath=CardPrefabEditorWindow.Folder+"/egg_fire.prefab";
                var sample=AssetDatabase.LoadAssetAtPath<GameObject>(samplePath);
                var original=sample.GetComponent<CardPrefabView>();
                var draft=((GameObject)PrefabUtility.InstantiatePrefab(sample,scene)).GetComponent<CardPrefabView>();
                draft.cardId="check_card"; draft.displayName="保存验证";
                draft.layers=CardPrefabSamples.Template(true);
                draft.layers[3].text="验证标题"; draft.layers[3].rect=new Rect(42,28,500,66);
                var custom=new GameObject("ArtistCustomChild",typeof(RectTransform)); custom.transform.SetParent(draft.transform,false);
                draft.Rebuild();
                Require(original.cardId=="egg_fire", "编辑草稿不改动原始资源");
                string firstPath=folder+"/check_card.prefab";
                var first=CardPrefabEditorWindow.SaveDraft(draft,firstPath);
                string guid=AssetDatabase.AssetPathToGUID(firstPath);
                Require(first.GetComponent<CardPrefabView>().layers[3].text=="验证标题", "保存文字");
                Require(first.GetComponent<CardPrefabView>().layers[3].rect==new Rect(42,28,500,66), "保存布局");
                Require(first.transform.Find("ArtistCustomChild")!=null, "保留美术自定义节点");
                Require(first.GetComponentsInChildren<Text>().Any(x=>x.text=="验证标题"), "重建真实 UGUI 文字");
                draft.layers[3].text="覆盖验证";
                CardPrefabEditorWindow.SaveDraft(draft,firstPath);
                Require(guid==AssetDatabase.AssetPathToGUID(firstPath), "覆盖保存保留 GUID");
                draft.layers[3].text="另存验证";
                var second=CardPrefabEditorWindow.SaveDraft(draft,folder+"/copy_card.prefab");
                Require(AssetDatabase.LoadAssetAtPath<GameObject>(firstPath).GetComponent<CardPrefabView>().layers[3].text=="覆盖验证", "另存不修改原卡");
                var reopened=(GameObject)PrefabUtility.InstantiatePrefab(second,scene);
                Require(reopened.GetComponent<CardPrefabView>().layers[3].text=="另存验证", "重新打开保持修改");
                Require(reopened.GetComponentsInChildren<RawImage>().Any(x=>x.texture!=null), "保存图片引用");
                draft.designSize=new Vector2(float.NaN,800);
                bool rejected=false; try { draft.ValidateDesign(); } catch(InvalidOperationException) { rejected=true; }
                Require(rejected, "拒绝无效设计尺寸");
                Directory.CreateDirectory("Logs");
                CardPrefabEditorWindow.CaptureAsset(sample,"Logs/CardEditorFullFace.png");
                CardPrefabEditorWindow.CaptureAsset(second,"Logs/CardEditorLayered.png");
                PrototypeEditor.Verify();
                string report="PASS: 草稿隔离、保存与重开、图层位置/尺寸、图片与文字引用、覆盖保留 GUID、另存不覆盖原卡、自定义节点保留、无效尺寸校验、实际 UGUI 预览渲染。\n"+DateTime.Now.ToString("s");
                File.WriteAllText("Logs/CardPrefabEditorChecks.txt",report); Debug.Log(report);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                AssetDatabase.DeleteAsset(folder);
            }
        }
        static void Require(bool condition, string check) { if(!condition) throw new InvalidOperationException("卡牌编辑器验证失败："+check); }
        public static void ValidateAndBuild() { Run(); PrototypeEditor.BuildPerspective(); }
    }
}
