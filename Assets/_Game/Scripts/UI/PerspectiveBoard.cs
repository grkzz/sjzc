using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShenYi.Prototype
{
    // The board is a real rectangular mesh. Only its camera output is composited with the illustrated arena.
    public sealed class PerspectiveBoard : MonoBehaviour, IPointerClickHandler
    {
        const int BoardLayer = 30;
        const float SourceWidth = 1080f, SourceHeight = 1773f, WorldWidth = 10f;
        public Camera BoardCamera { get; private set; }
        public RectTransform CardSurface { get; private set; }
        public int RaycastChecks { get; private set; }
        RenderTexture target;
        Mesh mesh;
        Material material, edgeMaterial;
        MeshCollider surfaceCollider;
        GameObject worldRoot;
        readonly List<Button> slots = new List<Button>();
        readonly List<Material> ownedMaterials = new List<Material>();

        public void Initialize(Texture2D texture)
        {
            var world = new GameObject("PerspectiveBoardWorld");
            // World canvases must not be nested under the screen-space HUD canvas.
            worldRoot = world;
            world.transform.position = new Vector3(400, 0, 0); // Isolate from any existing scene geometry.
            var cameraObject = new GameObject("BoardPerspectiveCamera", typeof(Camera));
            cameraObject.transform.SetParent(world.transform, false);
            BoardCamera = cameraObject.GetComponent<Camera>();
            BoardCamera.orthographic = false; BoardCamera.clearFlags = CameraClearFlags.SolidColor;
            BoardCamera.backgroundColor = new Color(.10f, .095f, .17f, 1);
            BoardCamera.cullingMask = 1 << BoardLayer; BoardCamera.nearClipPlane = .1f; BoardCamera.farClipPlane = 60;
            BoardCamera.allowHDR = false; BoardCamera.allowMSAA = true;
            target = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32) { name = "PortraitPerspectiveBoard", antiAliasing = 2 };
            target.Create(); BoardCamera.targetTexture = target;
            var image = GetComponent<RawImage>(); image.texture = target; image.raycastTarget = true;

            float length = WorldWidth * SourceHeight / SourceWidth;
            // Match the arena floor opening: far edge (240,410)-(840,410), near edge (50,1605)-(1030,1605).
            float farWidth = 600f / 1080f, nearWidth = 980f / 1080f, screenHeight = 1195f / 1920f;
            float ratio = nearWidth / farWidth, t = (ratio - 1) / (ratio + 1), aspect = 1080f / 1920f;
            float focalOverDistance = farWidth * aspect * (1 + t) / WorldWidth;
            float cos = screenHeight * (1 - t * t) / (focalOverDistance * length);
            float angle = Mathf.Acos(Mathf.Clamp(cos, .05f, .99f));
            float distance = length * .5f * Mathf.Sin(angle) / t;
            float focal = focalOverDistance * distance;
            BoardCamera.fieldOfView = 2 * Mathf.Atan(.5f / focal) * Mathf.Rad2Deg;
            BoardCamera.aspect = aspect;
            cameraObject.transform.localPosition = new Vector3(0, -distance * Mathf.Sin(angle), -distance * Mathf.Cos(angle));
            cameraObject.transform.LookAt(world.transform.position, Vector3.up);
            var projection = Matrix4x4.Perspective(BoardCamera.fieldOfView, aspect, .1f, 60);
            BoardCamera.projectionMatrix = projection;
            float currentFarY = BoardCamera.WorldToViewportPoint(world.transform.TransformPoint(new Vector3(0, length * .5f, 0))).y;
            float desiredFarY = 1 - 410f / 1920f;
            projection.m12 -= 2 * (desiredFarY - currentFarY);
            BoardCamera.projectionMatrix = projection;

            var shader = Resources.Load<Shader>("Prototype/BoardUnlit");
            material = new Material(shader) { mainTexture = texture, name = "BoardSurfaceMaterial" }; ownedMaterials.Add(material);
            var top = new GameObject("BoardTexturedMesh", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            top.layer = BoardLayer; top.transform.SetParent(world.transform, false);
            mesh = new Mesh { name = "RectangularBoardUV" };
            mesh.vertices = new[] { new Vector3(-5,-length*.5f,0),new Vector3(5,-length*.5f,0),new Vector3(-5,length*.5f,0),new Vector3(5,length*.5f,0) };
            mesh.uv = new[] { new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1) };
            mesh.triangles = new[] { 0,2,1,2,3,1 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            top.GetComponent<MeshFilter>().sharedMesh = mesh; top.GetComponent<MeshRenderer>().sharedMaterial = material;
            surfaceCollider = top.GetComponent<MeshCollider>(); surfaceCollider.sharedMesh = mesh;
            var edge = GameObject.CreatePrimitive(PrimitiveType.Cube); edge.name = "BoardThickness"; edge.layer = BoardLayer;
            edge.transform.SetParent(world.transform, false); edge.transform.localPosition = new Vector3(0,0,.13f);
            edge.transform.localScale = new Vector3(WorldWidth + .035f,length + .035f,.20f);
            Destroy(edge.GetComponent<Collider>());
            edgeMaterial = new Material(shader); edgeMaterial.color = new Color(.12f,.09f,.22f); ownedMaterials.Add(edgeMaterial);
            edge.GetComponent<Renderer>().sharedMaterial = edgeMaterial;

            var cards = new GameObject("CardsOnBoard", typeof(RectTransform), typeof(Canvas)); cards.layer = BoardLayer;
            cards.transform.SetParent(world.transform, false);
            CardSurface = cards.GetComponent<RectTransform>(); CardSurface.sizeDelta = new Vector2(SourceWidth, SourceHeight);
            CardSurface.pivot = new Vector2(.5f,.5f); CardSurface.localScale = Vector3.one * (WorldWidth / SourceWidth);
            CardSurface.localPosition = new Vector3(0,0,-.025f);
            var canvas = cards.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = BoardCamera;
            canvas.sortingOrder = 1;
            ValidateProjection();
        }
        public void Register(Button button)
        {
            slots.Add(button); SetLayer(button.transform);
        }
        static void SetLayer(Transform value)
        {
            value.gameObject.layer = BoardLayer;
            foreach (Transform child in value) SetLayer(child);
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            Button button;
            if(TryGetSlotAtScreen(eventData.position,eventData.pressEventCamera,out button)) button.onClick.Invoke();
        }
        public bool TryGetSlotAtScreen(Vector2 position, Camera eventCamera, out Button button)
        {
            button=null;
            Vector2 local;
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,position,eventCamera,out local)) return false;
            var r = rect.rect;
            if(!r.Contains(local)) return false;
            return TryGetSlotViewport(new Vector2((local.x-r.xMin)/r.width,(local.y-r.yMin)/r.height),out button);
        }
        public bool ClickViewport(Vector2 viewport)
        {
            Button button;
            if(!TryGetSlotViewport(viewport,out button)) return false;
            button.onClick.Invoke(); return true;
        }
        bool TryGetSlotViewport(Vector2 viewport, out Button found)
        {
            found=null;
            RaycastHit hit;
            var ray = BoardCamera.ViewportPointToRay(viewport);
            if (!surfaceCollider.Raycast(ray,out hit,100)) return false;
            Vector2 point = new Vector2((hit.textureCoord.x-.5f)*SourceWidth,(hit.textureCoord.y-.5f)*SourceHeight);
            for (int i=slots.Count-1;i>=0;i--)
            {
                var button=slots[i]; var rect=(RectTransform)button.transform;
                var local=rect.InverseTransformPoint(CardSurface.TransformPoint(point));
                if (rect.rect.Contains(local) && button.interactable) { found=button; return true; }
            }
            return false;
        }
        public Vector2 ScreenPointForSlot(Button button)
        {
            var rect=(RectTransform)button.transform;
            Vector3 projected=BoardCamera.WorldToViewportPoint(rect.TransformPoint(rect.rect.center));
            var display=(RectTransform)transform; var bounds=display.rect;
            return RectTransformUtility.WorldToScreenPoint(null,display.TransformPoint(new Vector3(bounds.xMin+projected.x*bounds.width,bounds.yMin+projected.y*bounds.height,0)));
        }
        public bool ClickSlot(Button button)
        {
            var rect=(RectTransform)button.transform;
            var world=rect.TransformPoint(rect.rect.center);
            Vector3 projected=BoardCamera.WorldToViewportPoint(world);
            bool result=ClickViewport(new Vector2(projected.x,projected.y));
            if (!result) throw new InvalidOperationException("Perspective card raycast missed: "+button.name);
            RaycastChecks++; return result;
        }
        void ValidateProjection()
        {
            // A perspective camera must make equal-width near slots larger than far slots.
            Vector3 far=CardSurface.TransformPoint(new Vector3(0,SourceHeight*.389f,0));
            Vector3 near=CardSurface.TransformPoint(new Vector3(0,-SourceHeight*.383f,0));
            float nearWidth=Mathf.Abs(BoardCamera.WorldToViewportPoint(near+Vector3.right).x-BoardCamera.WorldToViewportPoint(near).x);
            float farWidth=Mathf.Abs(BoardCamera.WorldToViewportPoint(far+Vector3.right).x-BoardCamera.WorldToViewportPoint(far).x);
            if (BoardCamera.orthographic || nearWidth <= farWidth*1.2f) throw new InvalidOperationException("Board perspective calibration failed.");
            Debug.Log("SHENYI_PERSPECTIVE near/far scale="+(nearWidth/farWidth).ToString("F3"));
        }
        void OnDestroy()
        {
            if(worldRoot!=null) Destroy(worldRoot);
            if (target!=null) { target.Release(); Destroy(target); }
            if(mesh!=null) Destroy(mesh);
            foreach(var value in ownedMaterials) if(value!=null) Destroy(value);
        }
    }
}
