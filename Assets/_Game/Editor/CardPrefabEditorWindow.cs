using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShenYi.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShenYi.Prototype.Editor
{
    public sealed class CardPrefabEditorWindow : EditorWindow
    {
        internal const string Folder = "Assets/_Game/Resources/CardPrefabs";
        CardPrefabView draft;
        GameObject source;
        SerializedObject serialized;
        Scene previewScene;
        Camera previewCamera;
        Canvas previewCanvas;
        RenderTexture previewTexture;
        readonly List<GameObject> library = new List<GameObject>();
        Vector2 libraryScroll, propertiesScroll;
        string search = "", message = "";
        int selectedLayer = -1;
        bool dirty, needsRender = true, dragging, resizing;
        Vector2 dragOrigin;
        Rect originalRect, previewRect;
        float zoom = 1;
        string DraftLabel { get { return draft == null ? "未选择卡牌" : draft.displayName; } }

        [MenuItem("神奕战场/卡牌可视化编辑器", false, 1)]
        public static void Open() { GetWindow<CardPrefabEditorWindow>("卡牌预设体编辑器").Show(); }

        public static void OpenAsset(GameObject asset)
        { var window=GetWindow<CardPrefabEditorWindow>("卡牌预设体编辑器"); window.TryLoad(asset); window.Show(); }
        [UnityEditor.Callbacks.OnOpenAsset]
        static bool OnOpenCard(int instanceId, int line)
        {
            var asset=EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if(asset==null || asset.GetComponent<CardPrefabView>()==null || !AssetDatabase.GetAssetPath(asset).EndsWith(".prefab")) return false;
            OpenAsset(asset); return true;
        }

        [MenuItem("Assets/用卡牌可视化编辑器打开", true)]
        static bool CanOpenSelected() { var obj = Selection.activeObject as GameObject; return obj != null && obj.GetComponent<CardPrefabView>() != null && AssetDatabase.GetAssetPath(obj).EndsWith(".prefab"); }
        [MenuItem("Assets/用卡牌可视化编辑器打开")]
        static void OpenSelected() { OpenAsset(Selection.activeObject as GameObject); }
        void OnEnable()
        {
            minSize = new Vector2(1080,760);
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.delayCall += Initialize;
        }
        void Initialize()
        {
            if (this == null) return;
            CardPrefabSamples.EnsureSamples(); RefreshLibrary();
            if (draft == null && library.Count > 0) Load(library[0]);
        }
        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo; EditorApplication.delayCall -= Initialize;
            DisposePreview();
        }
        void OnUndo() { if (draft != null) { serialized.Update(); draft.Rebuild(); MarkDirty(); } }
        void RefreshLibrary()
        {
            library.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game" }))
            {
                var go=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if(go!=null && go.GetComponent<CardPrefabView>()!=null) library.Add(go);
            }
            library.Sort((a,b)=>string.Compare(a.name,b.name,StringComparison.Ordinal));
        }
        void EnsurePreview()
        {
            if(previewScene.IsValid()) return;
            previewScene=EditorSceneManager.NewPreviewScene();
            var cameraGo=new GameObject("CardEditorCamera",typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraGo,previewScene);
            previewCamera=cameraGo.GetComponent<Camera>(); previewCamera.scene=previewScene;
            previewCamera.enabled=false; previewCamera.orthographic=true; previewCamera.nearClipPlane=.1f; previewCamera.farClipPlane=50;
            previewCamera.transform.position=new Vector3(0,0,-10); previewCamera.clearFlags=CameraClearFlags.SolidColor;
            previewCamera.backgroundColor=new Color(.12f,.13f,.16f); previewCamera.allowHDR=false;
            var canvasGo=new GameObject("CardEditorPreview",typeof(RectTransform),typeof(Canvas)); SceneManager.MoveGameObjectToScene(canvasGo,previewScene);
            previewCanvas=canvasGo.GetComponent<Canvas>(); previewCanvas.renderMode=RenderMode.WorldSpace; previewCanvas.worldCamera=previewCamera;
            canvasGo.transform.localScale=Vector3.one*.001f;
        }
        void DisposePreview()
        {
            if(previewTexture!=null) { previewTexture.Release(); DestroyImmediate(previewTexture); }
            if(previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            previewTexture=null; draft=null; serialized=null;
        }
        void Load(GameObject asset)
        {
            EnsurePreview();
            if(draft!=null) { Undo.ClearUndo(draft); DestroyImmediate(draft.gameObject); }
            source=asset;
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(asset,previewScene);
            obj.transform.SetParent(previewCanvas.transform,false);
            var rect=(RectTransform)obj.transform; rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=Vector2.zero; rect.localScale=Vector3.one;
            draft=obj.GetComponent<CardPrefabView>(); serialized=new SerializedObject(draft);
            draft.Rebuild(); selectedLayer=-1; dirty=false; message=""; needsRender=true;
            hasUnsavedChanges=false; saveChangesMessage="卡牌还有未保存的修改。"; Repaint();
        }
        bool CanLeaveDraft()
        {
            if(!dirty) return true;
            int result=EditorUtility.DisplayDialogComplex("未保存的卡牌", "保存「"+DraftLabel+"」的修改？", "保存", "取消", "放弃修改");
            return result==2 || result==0 && Save(false);
        }
        void TryLoad(GameObject asset) { if(asset!=null && CanLeaveDraft()) Load(asset); }
        void NewCard(bool layered)
        {
            if(EditorApplication.isPlaying || !CanLeaveDraft()) return;
            EnsurePreview();
            if(draft!=null) { Undo.ClearUndo(draft); DestroyImmediate(draft.gameObject); }
            var obj=new GameObject("NewCard",typeof(RectTransform),typeof(CardPrefabView)); SceneManager.MoveGameObjectToScene(obj,previewScene);
            obj.transform.SetParent(previewCanvas.transform,false); draft=obj.GetComponent<CardPrefabView>(); source=null;
            draft.font=Resources.Load<Font>("Prototype/PrototypeChinese");
            draft.layers=CardPrefabSamples.Template(layered); serialized=new SerializedObject(draft);
            draft.Rebuild(); selectedLayer=0; MarkDirty();
        }
        void MarkDirty() { dirty=true; hasUnsavedChanges=true; needsRender=true; Repaint(); }
        public override void SaveChanges()
        {
            if(!Save(false)) throw new OperationCanceledException("卡牌尚未保存，保留编辑窗口。");
            base.SaveChanges();
        }

        internal static GameObject SaveDraft(CardPrefabView value,string path)
        {
            value.ValidateDesign();
            if(!path.StartsWith("Assets/",StringComparison.Ordinal) || !path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请保存到 Assets 下的 .prefab 文件。");
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(existing!=null && existing.GetComponent<CardPrefabView>()==null)
                throw new InvalidOperationException("目标不是卡牌预设体，请另选文件名。");
            GameObject clone=null;
            try
            {
                clone=Object.Instantiate(value.gameObject, value.transform.parent, false); clone.name=Path.GetFileNameWithoutExtension(path);
                if(value.gameObject.scene.IsValid()) SceneManager.MoveGameObjectToScene(RootClone(clone), value.gameObject.scene);
                if(PrefabUtility.IsPartOfPrefabInstance(clone)) PrefabUtility.UnpackPrefabInstance(clone,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                clone.hideFlags=HideFlags.None;
                var rect=(RectTransform)clone.transform; rect.SetParent(null,false); rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
                rect.localPosition=Vector3.zero; rect.localRotation=Quaternion.identity; rect.localScale=Vector3.one;
                var view=clone.GetComponent<CardPrefabView>(); view.Rebuild();
                bool success; var prefab=PrefabUtility.SaveAsPrefabAsset(clone,path,out success);
                if(!success || prefab==null) throw new InvalidOperationException("预设体保存失败。");
                AssetDatabase.SaveAssets(); return prefab;
            }
            finally { if(clone!=null) Object.DestroyImmediate(clone); }
        }
        static GameObject RootClone(GameObject clone) { clone.transform.SetParent(null,false); return clone; }

        internal static void CaptureAsset(GameObject asset, string path)
        {
            var window=CreateInstance<CardPrefabEditorWindow>();
            try
            {
                window.Load(asset); window.RenderPreview();
                var previous=RenderTexture.active;
                var image=new Texture2D(window.previewTexture.width,window.previewTexture.height,TextureFormat.RGB24,false);
                try
                {
                    RenderTexture.active=window.previewTexture;
                    image.ReadPixels(new Rect(0,0,image.width,image.height),0,0); image.Apply();
                    File.WriteAllBytes(path,image.EncodeToPNG());
                    if(image.GetPixel(image.width/2,image.height/2).grayscale<.01f) throw new InvalidOperationException("预览渲染为空。");
                }
                finally { RenderTexture.active=previous; DestroyImmediate(image); }
            }
            finally { DestroyImmediate(window); }
        }
        bool Save(bool saveAs)
        {
            if(draft==null) return false;
            try
            {
                draft.ValidateDesign();
                string path=source==null || saveAs ? "" : AssetDatabase.GetAssetPath(source);
                if(string.IsNullOrEmpty(path)) path=EditorUtility.SaveFilePanelInProject("保存卡牌预设体",draft.cardId,"prefab","保存为可复用的 Unity 卡牌预设体",Folder);
                if(string.IsNullOrEmpty(path)) return false;
                if(saveAs && source!=null && path==AssetDatabase.GetAssetPath(source)) throw new InvalidOperationException("另存新卡请使用不同文件名，或点「保存」修改原卡。");
                var saved=SaveDraft(draft,path); dirty=false; hasUnsavedChanges=false;
                RefreshLibrary(); Load(saved); message="已保存："+path; return true;
            }
            catch(Exception error) { message=error.Message; Debug.LogException(error); return false; }
        }
        void RenderPreview()
        {
            if(draft==null || !needsRender) return;
            int width=Mathf.Clamp(Mathf.RoundToInt(draft.designSize.x),100,2048);
            int height=Mathf.Clamp(Mathf.RoundToInt(draft.designSize.y),100,2048);
            if(previewTexture==null || previewTexture.width!=width || previewTexture.height!=height)
            {
                if(previewTexture!=null) { previewTexture.Release(); DestroyImmediate(previewTexture); }
                previewTexture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=2}; previewTexture.Create();
            }
            previewCamera.orthographicSize=draft.designSize.y*.0005f;
            previewCamera.aspect=draft.designSize.x/draft.designSize.y; previewCamera.targetTexture=previewTexture;
            Canvas.ForceUpdateCanvases(); previewCamera.Render(); needsRender=false;
        }
        void ExportPNG()
        {
            if(draft==null) return;
            string path=EditorUtility.SaveFilePanel("导出卡面 PNG",Application.dataPath,draft.cardId,"png"); if(string.IsNullOrEmpty(path)) return;
            needsRender=true; RenderPreview(); var old=RenderTexture.active; Texture2D image=null;
            try { RenderTexture.active=previewTexture; image=new Texture2D(previewTexture.width,previewTexture.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,image.width,image.height),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG()); AssetDatabase.Refresh(); message="已导出："+path; }
            finally { RenderTexture.active=old; if(image!=null) DestroyImmediate(image); }
        }
        void OnGUI()
        {
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if(GUILayout.Button("新建完整卡面",EditorStyles.toolbarButton)) NewCard(false);
                if(GUILayout.Button("新建分层卡牌",EditorStyles.toolbarButton)) NewCard(true);
                GUILayout.Space(10);
                using(new EditorGUI.DisabledScope(draft==null || EditorApplication.isPlaying))
                {
                    if(GUILayout.Button("保存",EditorStyles.toolbarButton)) Save(false);
                    if(GUILayout.Button("另存新卡",EditorStyles.toolbarButton)) Save(true);
                    if(GUILayout.Button("导出 PNG",EditorStyles.toolbarButton)) ExportPNG();
                }
                GUILayout.FlexibleSpace(); GUILayout.Label(DraftLabel+(dirty?"  *":""),EditorStyles.miniLabel);
            }
            if(EditorApplication.isPlaying) EditorGUILayout.HelpBox("请退出 Play 模式后编辑并保存卡牌。",MessageType.Info);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            using(new EditorGUILayout.HorizontalScope())
            {
                DrawLibrary();
                using(new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true))) DrawPreview();
                using(new EditorGUILayout.VerticalScope(GUILayout.Width(340))) DrawProperties();
            }
            if(!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message,MessageType.Info);
            HandleShortcuts();
        }
        void DrawLibrary()
        {
            using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox,GUILayout.Width(205)))
            {
                GUILayout.Label("卡牌预设体库",EditorStyles.boldLabel);
                search=EditorGUILayout.TextField(search,EditorStyles.toolbarSearchField);
                if(GUILayout.Button("刷新卡牌库")) RefreshLibrary();
                libraryScroll=EditorGUILayout.BeginScrollView(libraryScroll);
                foreach(var asset in library.ToArray())
                {
                    if(asset==null) continue;
                    var view=asset.GetComponent<CardPrefabView>();
                    if(!string.IsNullOrEmpty(search) && !(view.displayName+view.cardId+asset.name).ToLowerInvariant().Contains(search.ToLowerInvariant())) continue;
                    using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        var face=view.layers.FirstOrDefault(x=>x.visible && x.kind==CardLayerKind.Image && x.texture!=null);
                        var thumb=GUILayoutUtility.GetRect(74,98,GUILayout.ExpandWidth(true));
                        if(face!=null) GUI.DrawTexture(thumb,face.texture,ScaleMode.ScaleToFit);
                        if(GUILayout.Button(view.displayName+(source==asset?"  ✓":""))) TryLoad(asset);
                        GUILayout.Label(asset.name,EditorStyles.miniLabel);
                    }
                }
                EditorGUILayout.EndScrollView();
                if(source!=null && GUILayout.Button("在 Project 中定位")) { Selection.activeObject=source; EditorGUIUtility.PingObject(source); }
                var picked=(GameObject)EditorGUILayout.ObjectField("打开",null,typeof(GameObject),false);
                if(picked!=null) { if(picked.GetComponent<CardPrefabView>()!=null && AssetDatabase.GetAssetPath(picked).EndsWith(".prefab")) TryLoad(picked); else message="请选择带 CardPrefabView 的卡牌预设体。"; }
            }
        }
        void DrawPreview()
        {
            GUILayout.Label("实时预览 · 点击选择图层 / 拖动移动 / 右下角缩放",EditorStyles.boldLabel);
            zoom=EditorGUILayout.Slider("预览缩放",zoom,.35f,1.4f);
            Rect area=GUILayoutUtility.GetRect(240,400,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(area,new Color(.15f,.16f,.19f));
            if(draft==null) { GUI.Label(area,"选择左侧卡牌，或新建一张卡。",EditorStyles.centeredGreyMiniLabel); return; }
            RenderPreview();
            float scale=Mathf.Min((area.width-40)/draft.designSize.x,(area.height-40)/draft.designSize.y)*zoom;
            previewRect=new Rect(area.center.x-draft.designSize.x*scale*.5f,area.center.y-draft.designSize.y*scale*.5f,draft.designSize.x*scale,draft.designSize.y*scale);
            GUI.BeginClip(area);
            Rect imageRect=new Rect(previewRect.x-area.x,previewRect.y-area.y,previewRect.width,previewRect.height);
            if(previewTexture!=null) GUI.DrawTexture(imageRect,previewTexture,ScaleMode.StretchToFill,false);
            if(selectedLayer>=0 && selectedLayer<draft.layers.Count)
            {
                var r=draft.layers[selectedLayer].rect;
                Rect selected=new Rect(imageRect.x+r.x*scale,imageRect.y+r.y*scale,r.width*scale,r.height*scale);
                Handles.BeginGUI(); Handles.color=new Color(.75f,.95f,.25f);
                Handles.DrawSolidRectangleWithOutline(selected,Color.clear,Handles.color); Handles.EndGUI();
                EditorGUI.DrawRect(new Rect(selected.xMax-5,selected.yMax-5,10,10),new Color(.75f,.95f,.25f));
            }
            GUI.EndClip();
            HandlePreviewInput(scale,area);
            GUILayout.Label("设计坐标 "+draft.designSize.x+" × "+draft.designSize.y+"   ·   保存结果是标准 Unity .prefab",EditorStyles.centeredGreyMiniLabel);
        }
        void HandlePreviewInput(float scale,Rect area)
        {
            var e=Event.current;
            if(e.type==EventType.MouseDown && e.button==0 && area.Contains(e.mousePosition) && previewRect.Contains(e.mousePosition))
            {
                GUI.FocusControl(null); Vector2 point=(e.mousePosition-previewRect.position)/scale;
                resizing=false;
                if(selectedLayer>=0 && selectedLayer<draft.layers.Count)
                {
                    var r=draft.layers[selectedLayer].rect;
                    resizing=Vector2.Distance(e.mousePosition,previewRect.position+new Vector2(r.xMax,r.yMax)*scale)<12;
                }
                if(!resizing)
                {
                    selectedLayer=-1;
                    for(int i=draft.layers.Count-1;i>=0;i--) if(draft.layers[i].visible && draft.layers[i].rect.Contains(point)) { selectedLayer=i; break; }
                }
                if(selectedLayer>=0) { dragging=true; dragOrigin=point; originalRect=draft.layers[selectedLayer].rect; Undo.RecordObject(draft,"移动卡牌图层"); }
                e.Use(); Repaint();
            }
            else if(e.type==EventType.MouseDrag && dragging && selectedLayer>=0)
            {
                Vector2 delta=(e.mousePosition-previewRect.position)/scale-dragOrigin;
                Rect r=originalRect;
                if(resizing) r.size=new Vector2(Mathf.Max(1,r.width+delta.x),Mathf.Max(1,r.height+delta.y)); else r.position+=delta;
                if(e.shift) r.position=new Vector2(Mathf.Round(r.x/10)*10,Mathf.Round(r.y/10)*10);
                draft.layers[selectedLayer].rect=r; draft.Rebuild(); serialized.Update(); MarkDirty(); e.Use();
            }
            else if(e.type==EventType.MouseUp && dragging) { dragging=false; e.Use(); }
        }
        void DrawProperties()
        {
            if(draft==null) return;
            propertiesScroll=EditorGUILayout.BeginScrollView(propertiesScroll);
            serialized.Update(); EditorGUI.BeginChangeCheck();
            GUILayout.Label("卡牌信息",EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("cardId"),new GUIContent("卡牌 ID"));
            EditorGUILayout.PropertyField(serialized.FindProperty("displayName"),new GUIContent("名称"));
            EditorGUILayout.PropertyField(serialized.FindProperty("notes"),new GUIContent("备注"));
            EditorGUILayout.PropertyField(serialized.FindProperty("designSize"),new GUIContent("设计尺寸"));
            EditorGUILayout.PropertyField(serialized.FindProperty("font"),new GUIContent("字体"));
            GUILayout.Space(10); GUILayout.Label("图层 · 列表下方显示在上层",EditorStyles.boldLabel);
            for(int i=0;i<draft.layers.Count;i++)
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    var item=serialized.FindProperty("layers").GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(item.FindPropertyRelative("visible"),GUIContent.none,GUILayout.Width(18));
                    if(GUILayout.Toggle(selectedLayer==i,draft.layers[i].name,"Button")) selectedLayer=i;
                }
            }
            if(EditorGUI.EndChangeCheck()) ApplyProperties();
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("+ 图片")) AddLayer(CardLayerKind.Image);
                if(GUILayout.Button("+ 文字")) AddLayer(CardLayerKind.Text);
                if(GUILayout.Button("+ 色块")) AddLayer(CardLayerKind.Color);
            }
            if(selectedLayer>=0 && selectedLayer<draft.layers.Count) DrawLayerProperties();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("完整卡面模式：直接拖入已有 PNG。分层模式：叠加插画、卡框和文字。文字支持直接填写攻击、生命与效果；这里不修改战斗 JSON。",MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        void ApplyProperties()
        {
            serialized.ApplyModifiedProperties();
            draft.designSize=new Vector2(Mathf.Clamp(draft.designSize.x,100,4096),Mathf.Clamp(draft.designSize.y,100,4096));
            foreach(var layer in draft.layers) { layer.rect.width=Mathf.Max(1,layer.rect.width); layer.rect.height=Mathf.Max(1,layer.rect.height); layer.fontSize=Mathf.Clamp(layer.fontSize,1,300); }
            draft.Rebuild(); MarkDirty();
        }
        void AddLayer(CardLayerKind kind)
        {
            Undo.RecordObject(draft,"添加卡牌图层");
            draft.layers.Add(new CardVisualLayer { name=kind==CardLayerKind.Image?"新图片":kind==CardLayerKind.Text?"新文字":"新色块",kind=kind,
                rect=new Rect(40,40,kind==CardLayerKind.Text?520:260,kind==CardLayerKind.Text?70:260),color=kind==CardLayerKind.Color?new Color(.15f,.2f,.3f):Color.white });
            selectedLayer=draft.layers.Count-1; draft.Rebuild(); serialized.Update(); MarkDirty();
        }
        void DrawLayerProperties()
        {
            serialized.Update(); var item=serialized.FindProperty("layers").GetArrayElementAtIndex(selectedLayer);
            EditorGUILayout.Space(); GUILayout.Label("选中图层",EditorStyles.boldLabel); EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(item.FindPropertyRelative("name"),new GUIContent("图层名称"));
            EditorGUILayout.PropertyField(item.FindPropertyRelative("kind"),new GUIContent("图层类型"));
            EditorGUILayout.PropertyField(item.FindPropertyRelative("rect"),new GUIContent("位置 / 尺寸"));
            EditorGUILayout.PropertyField(item.FindPropertyRelative("color"),new GUIContent("颜色 / 透明度"));
            var layer=draft.layers[selectedLayer];
            if(layer.kind==CardLayerKind.Image) EditorGUILayout.PropertyField(item.FindPropertyRelative("texture"),new GUIContent("图片 PNG"));
            if(layer.kind==CardLayerKind.Text)
            {
                EditorGUILayout.PropertyField(item.FindPropertyRelative("text"),new GUIContent("显示文字"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("fontSize"),new GUIContent("字号"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("alignment"),new GUIContent("对齐"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("fontStyle"),new GUIContent("字形"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("bestFit"),new GUIContent("自动缩小文字"));
            }
            if(EditorGUI.EndChangeCheck()) ApplyProperties();
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("复制图层")) MutateLayers(0);
                if(GUILayout.Button("下移")) MutateLayers(-1);
                if(GUILayout.Button("上移")) MutateLayers(1);
                if(GUILayout.Button("删除")) MutateLayers(2);
            }
            if(layer.kind==CardLayerKind.Image && layer.texture!=null && GUILayout.Button("按图片比例适配设计区"))
            {
                Undo.RecordObject(draft,"适配卡牌图片");
                float scale=Mathf.Min(draft.designSize.x/layer.texture.width,draft.designSize.y/layer.texture.height);
                var size=new Vector2(layer.texture.width,layer.texture.height)*scale;
                layer.rect=new Rect((draft.designSize-size)*.5f,size); draft.Rebuild(); serialized.Update(); MarkDirty();
            }
        }
        void MutateLayers(int action)
        {
            Undo.RecordObject(draft,"编辑卡牌图层");
            int i=selectedLayer;
            if(action==0) { var copy=draft.layers[i].Clone(); copy.name+=" 副本"; draft.layers.Insert(i+1,copy); selectedLayer++; }
            else if(action==2) { draft.layers.RemoveAt(i); selectedLayer=Mathf.Min(i,draft.layers.Count-1); }
            else
            {
                int target=Mathf.Clamp(i+action,0,draft.layers.Count-1);
                var value=draft.layers[i]; draft.layers.RemoveAt(i); draft.layers.Insert(target,value); selectedLayer=target;
            }
            draft.Rebuild(); serialized.Update(); MarkDirty();
        }
        void HandleShortcuts()
        {
            var e=Event.current;
            if(!EditorApplication.isPlaying && e.type==EventType.KeyDown && e.control && e.keyCode==KeyCode.S) { Save(false); e.Use(); }
        }
    }

    [CustomEditor(typeof(CardPrefabView))]
    public sealed class CardPrefabViewInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("通过可视化窗口编辑卡面、图层和布局。",MessageType.Info);
            if(GUILayout.Button("打开卡牌可视化编辑器"))
            {
                var asset=PrefabUtility.IsPartOfPrefabAsset(target) ? ((CardPrefabView)target).gameObject : PrefabUtility.GetCorrespondingObjectFromSource(((CardPrefabView)target).gameObject);
                if(asset!=null) CardPrefabEditorWindow.OpenAsset(asset); else CardPrefabEditorWindow.Open();
            }
            using(new EditorGUI.DisabledScope(true)) DrawDefaultInspector();
            if(!EditorUtility.IsPersistent(target) && GUILayout.Button("刷新场景实例外观")) { var view=(CardPrefabView)target; Undo.RegisterFullObjectHierarchyUndo(view.gameObject,"刷新卡牌"); view.Rebuild(); EditorUtility.SetDirty(view); }
        }
    }
}
