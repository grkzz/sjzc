using UnityEngine;
using UnityEngine.UI;

namespace ShenYi.Prototype
{
    // Decorative feedback never changes the slot's transform, collider or input target.
    public sealed class CardSlotFeedback : MonoBehaviour
    {
        RectTransform frame, tagRect;
        Image[] edges;
        Text actionLabel, primary, secondary;
        RawImage preview;
        CanvasGroup glow;
        bool pulse;
        public bool Legal { get; private set; }
        public bool Ready { get; private set; }
        public void Initialize(Font font)
        {
            frame=Rect("SlotFeedback",transform,0,0,128,174);
            glow=frame.gameObject.AddComponent<CanvasGroup>(); glow.blocksRaycasts=false;
            edges=new Image[4];
            edges[0]=Block(frame,0,0,128,4); edges[1]=Block(frame,0,170,128,4);
            edges[2]=Block(frame,0,0,4,174); edges[3]=Block(frame,124,0,4,174);
            var imageRect=Rect("PlacementPreview",transform,1,1,126,168);
            preview=imageRect.gameObject.AddComponent<RawImage>(); preview.raycastTarget=false; preview.color=new Color(1,1,1,.5f);
            preview.gameObject.SetActive(false);
            tagRect=Rect("ActionTag",transform,-6,-32,140,30);
            Block(tagRect,0,0,140,30).color=new Color(.025f,.07f,.10f,.96f);
            actionLabel=Label(tagRect,font,0,0,140,30,23);
            var badge=Rect("ReadableStatus",transform,-5,176,138,62);
            Block(badge,0,0,138,62).color=new Color(.025f,.035f,.075f,.98f);
            primary=Label(badge,font,0,0,138,34,29);
            primary.supportRichText=true;
            secondary=Label(badge,font,0,34,138,28,22);
        }
        public void Present(bool legal,bool ready,bool attackable,bool hovered,bool invalid,string main,string extra,string action,Texture image)
        {
            Legal=legal; Ready=ready;
            bool active=legal || ready || attackable || hovered;
            frame.gameObject.SetActive(active);
            Color tint=invalid ? new Color(1,.34f,.3f) : hovered && legal ? Color.white : legal ? new Color(.82f,.95f,.26f) : ready ? new Color(.3f,1,.87f) : new Color(1,.8f,.25f);
            foreach(var edge in edges) edge.color=tint;
            pulse=(ready || legal) && !hovered;
            glow.alpha=1;
            primary.text=main; secondary.text=extra;
            primary.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(main));
            primary.color=ready?new Color(.3f,1,.87f):Color.white;
            secondary.color=new Color(.83f,.87f,.95f);
            actionLabel.text=action; actionLabel.color=tint;
            tagRect.gameObject.SetActive(!string.IsNullOrEmpty(action));
            preview.gameObject.SetActive(hovered && legal && image!=null); preview.texture=image;
        }
        void Update()
        {
            if(pulse) { glow.alpha=.66f+.34f*Mathf.Sin(Time.unscaledTime*4); }
            if(Ready && tagRect.gameObject.activeSelf)
                tagRect.anchoredPosition=new Vector2(-6,32+Mathf.Sin(Time.unscaledTime*4)*2);
            else tagRect.anchoredPosition=new Vector2(-6,32);
        }
        static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.layer=parent.gameObject.layer;
            var rect=(RectTransform)go.transform; rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h); return rect;
        }
        static Image Block(Transform parent,float x,float y,float w,float h)
        { var image=Rect("Background",parent,x,y,w,h).gameObject.AddComponent<Image>(); image.raycastTarget=false; return image; }
        static Text Label(Transform parent,Font font,float x,float y,float w,float h,int size)
        {
            var text=Rect("Text",parent,x,y,w,h).gameObject.AddComponent<Text>(); text.font=font; text.fontSize=size;
            text.alignment=TextAnchor.MiddleCenter; text.raycastTarget=false; text.supportRichText=false;
            text.horizontalOverflow=HorizontalWrapMode.Overflow; text.verticalOverflow=VerticalWrapMode.Overflow; return text;
        }
    }
}
