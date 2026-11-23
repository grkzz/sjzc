using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShenYi.Prototype
{
    public enum CardLayerKind { Image, Text, Color }

    [Serializable]
    public sealed class CardVisualLayer
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "图层";
        public bool visible = true;
        public CardLayerKind kind;
        // Top-left origin, in card design pixels. The complete design scales as a unit in game.
        public Rect rect = new Rect(0, 0, 600, 800);
        public Texture2D texture;
        [TextArea(2, 8)] public string text = "卡牌名称";
        public Color color = Color.white;
        public int fontSize = 36;
        public TextAnchor alignment = TextAnchor.MiddleCenter;
        public bool bestFit;
        public FontStyle fontStyle;
        public CardVisualLayer Clone()
        {
            var copy = (CardVisualLayer)MemberwiseClone();
            copy.id = Guid.NewGuid().ToString("N"); return copy;
        }
    }

    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class CardPrefabView : MonoBehaviour
    {
        public string cardId = "new_card";
        public string displayName = "新卡牌";
        [TextArea] public string notes = "卡面美术预设；战斗规则与数值由战斗配置决定。";
        public Vector2 designSize = new Vector2(600, 800);
        public Font font;
        public List<CardVisualLayer> layers = new List<CardVisualLayer>();
        [SerializeField, HideInInspector] RectTransform generatedVisuals;
        [SerializeField, HideInInspector] RectTransform selectionBorder;

        void Awake() { if (generatedVisuals == null) Rebuild(); }

        public void ValidateDesign()
        {
            if (string.IsNullOrWhiteSpace(cardId)) throw new InvalidOperationException("请填写卡牌 ID。");
            if (!Finite(designSize.x) || !Finite(designSize.y) || designSize.x < 100 || designSize.y < 100 || designSize.x > 4096 || designSize.y > 4096)
                throw new InvalidOperationException("设计尺寸应在 100–4096 之间。");
            var ids = new HashSet<string>();
            foreach (var layer in layers)
            {
                if (layer == null || string.IsNullOrEmpty(layer.id) || !ids.Add(layer.id))
                    throw new InvalidOperationException("存在无效或重复图层 ID。");
                if (!Finite(layer.rect.width) || !Finite(layer.rect.height) || layer.rect.width < 1 || layer.rect.height < 1 || layer.fontSize < 1)
                    throw new InvalidOperationException("图层尺寸和字号必须大于 0：" + layer.name);
                if (float.IsNaN(layer.rect.x) || float.IsNaN(layer.rect.y) || float.IsInfinity(layer.rect.x) || float.IsInfinity(layer.rect.y))
                    throw new InvalidOperationException("图层位置无效：" + layer.name);
            }
        }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        // Rebuild only owned visual children; artist-added root children/components are retained.
        public void Rebuild()
        {
            var rect = (RectTransform)transform;
            rect.sizeDelta = designSize;
            // Undo can restore a stale generated reference. Recover the owned subtree before rebuilding.
            if (generatedVisuals == null) generatedVisuals = transform.Find("__CardVisuals") as RectTransform;
            if (generatedVisuals != null)
            {
                generatedVisuals.gameObject.SetActive(false);
                if (Application.IsPlaying(gameObject)) Destroy(generatedVisuals.gameObject); else DestroyImmediate(generatedVisuals.gameObject);
            }
            generatedVisuals = RectObject("__CardVisuals", rect);
            generatedVisuals.anchorMin = Vector2.zero; generatedVisuals.anchorMax = Vector2.one;
            generatedVisuals.offsetMin = generatedVisuals.offsetMax = Vector2.zero;
            foreach (var layer in layers)
            {
                if (layer == null || !layer.visible) continue;
                var child = RectObject(layer.name, generatedVisuals);
                child.anchorMin = child.anchorMax = child.pivot = new Vector2(0, 1);
                child.anchoredPosition = new Vector2(layer.rect.x, -layer.rect.y);
                child.sizeDelta = layer.rect.size;
                if (layer.kind == CardLayerKind.Text)
                {
                    var label = child.gameObject.AddComponent<Text>(); label.font = font;
                    label.text = layer.text; label.fontSize = layer.fontSize; label.color = layer.color;
                    label.alignment = layer.alignment; label.fontStyle = layer.fontStyle;
                    label.resizeTextForBestFit = layer.bestFit; label.resizeTextMinSize = 10; label.resizeTextMaxSize = layer.fontSize;
                    label.supportRichText = false; label.raycastTarget = false;
                    label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
                }
                else if (layer.kind == CardLayerKind.Image)
                {
                    var image = child.gameObject.AddComponent<RawImage>(); image.texture = layer.texture;
                    image.color = layer.color; image.raycastTarget = false;
                }
                else
                {
                    var image = child.gameObject.AddComponent<Image>(); image.color = layer.color; image.raycastTarget = false;
                }
            }
            selectionBorder = RectObject("__Selection", generatedVisuals);
            selectionBorder.anchorMin = Vector2.zero; selectionBorder.anchorMax = Vector2.one;
            selectionBorder.offsetMin = selectionBorder.offsetMax = Vector2.zero;
            Border(new Rect(-7, -7, designSize.x + 14, 7));
            Border(new Rect(-7, designSize.y, designSize.x + 14, 7));
            Border(new Rect(-7, 0, 7, designSize.y)); Border(new Rect(designSize.x, 0, 7, designSize.y));
            selectionBorder.gameObject.SetActive(false);
        }
        void Border(Rect bounds)
        {
            var rect = RectObject("Highlight", selectionBorder);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = new Vector2(bounds.x,-bounds.y); rect.sizeDelta = bounds.size;
            var image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false; image.color = new Color(.82f,.95f,.26f);
        }
        static RectTransform RectObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }
        public void SetSelected(bool selected) { if (selectionBorder != null) selectionBorder.gameObject.SetActive(selected); }
    }

    // Full-card fallback remains available when a specific card has not been authored yet.
    public sealed class CardPrefabHost : MonoBehaviour
    {
        CardPrefabView instance;
        string currentAsset;
        public void Show(string exactId, string fallbackId, Texture fallbackTexture, bool selected)
        {
            var asset = Resources.Load<GameObject>("CardPrefabs/" + exactId);
            string resolved = exactId;
            if (asset == null) { asset = Resources.Load<GameObject>("CardPrefabs/" + fallbackId); resolved = fallbackId; }
            var backing = GetComponent<RawImage>();
            if (asset == null)
            {
                if (instance != null) { instance.gameObject.SetActive(false); Destroy(instance.gameObject); instance = null; }
                currentAsset = null; backing.enabled = true; backing.texture = fallbackTexture;
                var outline = GetComponent<Outline>(); if (outline != null) outline.enabled = selected;
                return;
            }
            backing.enabled = false;
            var existingOutline = GetComponent<Outline>(); if (existingOutline != null) existingOutline.enabled = false;
            if (instance == null || currentAsset != resolved)
            {
                if (instance != null) { instance.gameObject.SetActive(false); Destroy(instance.gameObject); }
                var obj = Instantiate(asset, transform, false); instance = obj.GetComponent<CardPrefabView>(); currentAsset = resolved;
                if (instance == null) { Destroy(obj); currentAsset = null; backing.enabled = true; backing.texture = fallbackTexture; return; }
                SetLayer(obj.transform, gameObject.layer);
            }
            var host = (RectTransform)transform; var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f); rect.anchoredPosition = Vector2.zero;
            float scale = Mathf.Min(host.rect.width / instance.designSize.x, host.rect.height / instance.designSize.y);
            rect.localScale = new Vector3(scale,scale,1); instance.SetSelected(selected);
        }
        static void SetLayer(Transform root, int layer) { root.gameObject.layer = layer; foreach (Transform child in root) SetLayer(child,layer); }
    }
}
