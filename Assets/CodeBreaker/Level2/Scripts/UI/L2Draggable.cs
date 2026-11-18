using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodeBreaker.Level2
{
    // Drag source for inventory items and photo pieces. A ghost copy follows the mouse; the source
    // stays where it is, so a rejected drop never loses or consumes anything.
    public class L2Draggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public static L2Draggable Current { get; private set; }

        public string payload;
        [Tooltip("Parent for the ghost (the canvas root)")]
        public RectTransform dragLayer;
        public bool dragEnabled = true;

        [System.NonSerialized] public bool accepted;
        RectTransform ghost;

        public void OnBeginDrag(PointerEventData e)
        {
            if (!dragEnabled || dragLayer == null) return;
            Current = this;
            accepted = false;

            Graphic src = GetComponent<Graphic>();
            var go = new GameObject("Drag Ghost", typeof(RectTransform), typeof(CanvasGroup));
            ghost = (RectTransform)go.transform;
            ghost.SetParent(dragLayer, false);
            ghost.sizeDelta = ((RectTransform)transform).rect.size * 0.9f;
            go.GetComponent<CanvasGroup>().blocksRaycasts = false;
            go.GetComponent<CanvasGroup>().alpha = 0.85f;

            if (src is RawImage ri)
            {
                var r = go.AddComponent<RawImage>();
                r.texture = ri.texture;
                r.uvRect = ri.uvRect;
            }
            else
            {
                Image icon = FindIcon();
                var im = go.AddComponent<Image>();
                im.sprite = icon != null ? icon.sprite : null;
                im.preserveAspect = true;
                if (icon == null) im.color = new Color(1f, 1f, 1f, 0.4f);
            }
            ghost.position = e.position;
        }

        Image FindIcon()
        {
            foreach (Image i in GetComponentsInChildren<Image>())
                if (i.gameObject != gameObject && i.sprite != null && i.name == "Icon") return i;
            return null;
        }

        public void OnDrag(PointerEventData e)
        {
            if (ghost != null) ghost.position = e.position;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
            if (Current == this) Current = null;
        }

        void OnDisable()
        {
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
            if (Current == this) Current = null;
        }
    }
}
