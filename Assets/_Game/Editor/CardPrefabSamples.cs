using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShenYi.Prototype.Editor
{
    public static class CardPrefabSamples
    {
        [MenuItem("神奕战场/卡牌工具/补充示例预设体（保留已有修改）")]
        public static void EnsureSamples()
        {
            Directory.CreateDirectory(CardPrefabEditorWindow.Folder);
            AssetDatabase.Refresh();
            var scene=EditorSceneManager.NewPreviewScene();
            try { CreateSamples(scene); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void CreateSamples(Scene scene)
        {
            Create("egg_fire", "火属性蛋卡", "EggFire", scene);
            Create("egg_dual", "双属性蛋卡", "EggDual", scene);
            Create("monster_fire", "火属性怪兽卡", "MonsterFire", scene);
            Create("monster_dual", "双属性怪兽卡", "MonsterDual", scene);
            Create("monster_grass", "草属性怪兽卡", "MonsterGrass", scene);
            string path = CardPrefabEditorWindow.Folder + "/layered_example.prefab";
            if (File.Exists(path)) return;
            var obj = new GameObject("layered_example", typeof(RectTransform), typeof(CardPrefabView));
            SceneManager.MoveGameObjectToScene(obj,scene);
            try
            {
                var view = obj.GetComponent<CardPrefabView>();
                view.cardId = "layered_example"; view.displayName = "分层编辑示例";
                view.font = Resources.Load<Font>("Prototype/PrototypeChinese"); view.layers = Template(true);
                CardPrefabEditorWindow.SaveDraft(view, path);
            }
            finally { Object.DestroyImmediate(obj); }
        }
        static void Create(string id, string name, string image, Scene scene)
        {
            string path = CardPrefabEditorWindow.Folder + "/" + id + ".prefab";
            if (File.Exists(path)) return;
            var obj = new GameObject(id, typeof(RectTransform), typeof(CardPrefabView));
            SceneManager.MoveGameObjectToScene(obj,scene);
            try
            {
                var view = obj.GetComponent<CardPrefabView>(); view.cardId = id; view.displayName = name;
                view.font = Resources.Load<Font>("Prototype/PrototypeChinese");
                var texture = Resources.Load<Texture2D>("Prototype/CardFaces/" + image);
                if (texture == null) throw new FileNotFoundException("缺少示例卡面：" + image);
                view.layers = new List<CardVisualLayer> { new CardVisualLayer { name = "完整原始卡面", kind = CardLayerKind.Image, texture = texture } };
                CardPrefabEditorWindow.SaveDraft(view, path);
            }
            finally { Object.DestroyImmediate(obj); }
        }
        public static List<CardVisualLayer> Template(bool layered)
        {
            if (!layered) return new List<CardVisualLayer> { new CardVisualLayer { name="完整卡面（替换此图片）", kind=CardLayerKind.Image,
                texture=Resources.Load<Texture2D>("Prototype/CardFaces/EggFire") } };
            return new List<CardVisualLayer>
            {
                Block("卡框底色", new Rect(0,0,600,800), new Color(.67f,.5f,.25f)),
                Block("内衬", new Rect(14,14,572,772), new Color(.08f,.1f,.16f)),
                new CardVisualLayer { name="插画", kind=CardLayerKind.Image, texture=Resources.Load<Texture2D>("Prototype/MonsterArt"), rect=new Rect(34,100,532,420) },
                Label("卡名", "新怪兽", new Rect(36,26,528,58), 42, TextAnchor.MiddleCenter),
                Block("效果区底色", new Rect(34,538,532,166), new Color(.17f,.2f,.28f)),
                Label("属性 / 种族", "火属性  /  灵族", new Rect(48,543,504,40), 25, TextAnchor.MiddleLeft),
                Label("效果描述", "在这里填写效果描述。\n可单独调整图片、文字和图层位置。", new Rect(48,589,504,106), 27, TextAnchor.UpperLeft),
                Label("攻击力", "攻击 6", new Rect(38,721,244,54), 36, TextAnchor.MiddleLeft),
                Label("生命值", "生命 3", new Rect(318,721,244,54), 36, TextAnchor.MiddleRight)
            };
        }
        static CardVisualLayer Block(string name, Rect rect, Color color) { return new CardVisualLayer { name=name, kind=CardLayerKind.Color, rect=rect, color=color }; }
        static CardVisualLayer Label(string name, string text, Rect rect, int size, TextAnchor alignment)
        { return new CardVisualLayer { name=name, kind=CardLayerKind.Text, text=text, rect=rect, fontSize=size, alignment=alignment }; }
    }
}
