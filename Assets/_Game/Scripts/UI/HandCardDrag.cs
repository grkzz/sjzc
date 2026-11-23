using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShenYi.Prototype
{
    // Horizontal gestures browse the hand; gestures toward the table carry a card.
    public sealed class HandCardDrag : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform DragRoot;
        public Func<bool> CanDrag;
        public Action<Vector2> Drop;
        public Action<Vector2> DragStarted, DragMoved;
        public Action DragFinished;
        ScrollRect scroll;
        RectTransform ghost;
        bool browsing, carrying;
        public void OnInitializePotentialDrag(PointerEventData e)
        {
            scroll=GetComponentInParent<ScrollRect>();
            if(scroll!=null) scroll.OnInitializePotentialDrag(e);
        }
        public void OnBeginDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left) return;
            Vector2 delta=e.position-e.pressPosition;
            browsing=Mathf.Abs(delta.x)>Mathf.Abs(delta.y);
            if(browsing) { if(scroll!=null) scroll.OnBeginDrag(e); return; }
            if(DragRoot==null || CanDrag==null || !CanDrag()) return;
            carrying=true; e.eligibleForClick=false;
            var obj=new GameObject("DraggedCard",typeof(RectTransform),typeof(CanvasGroup));
            ghost=obj.GetComponent<RectTransform>(); ghost.SetParent(DragRoot,false);
            ghost.anchorMin=ghost.anchorMax=ghost.pivot=new Vector2(.5f,.5f);
            ghost.sizeDelta=((RectTransform)transform).rect.size;
            ghost.localScale=Vector3.one*1.08f;
            var group=obj.GetComponent<CanvasGroup>(); group.blocksRaycasts=false; group.interactable=false; group.alpha=.9f;
            foreach(Transform child in transform) Instantiate(child.gameObject,ghost,false);
            foreach(var graphic in ghost.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget=false;
            Move(e);
            if(DragStarted!=null) DragStarted(e.position);
        }
        public void OnDrag(PointerEventData e)
        {
            if(browsing) { if(scroll!=null) scroll.OnDrag(e); }
            else if(carrying) { Move(e); if(DragMoved!=null) DragMoved(e.position); }
        }
        void Move(PointerEventData e)
        {
            Vector2 point;
            if(ghost!=null && RectTransformUtility.ScreenPointToLocalPointInRectangle(DragRoot,e.position,e.pressEventCamera,out point))
                ghost.localPosition=new Vector3(point.x,point.y,0);
        }
        public void OnEndDrag(PointerEventData e)
        {
            if(browsing) { if(scroll!=null) scroll.OnEndDrag(e); }
            bool apply=carrying; browsing=carrying=false; ClearGhost();
            if(apply && DragFinished!=null) DragFinished();
            if(apply && CanDrag!=null && CanDrag() && Drop!=null) Drop(e.position);
        }
        void ClearGhost() { if(ghost!=null) { ghost.gameObject.SetActive(false); Destroy(ghost.gameObject); ghost=null; } }
        void OnDisable() { bool cancel=carrying; browsing=carrying=false; ClearGhost(); if(cancel && DragFinished!=null) DragFinished(); }
    }
}
