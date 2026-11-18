using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodeBreaker.Level2
{
    // Drop target. While something is being dragged it tints green when the dragged thing would be
    // accepted and red when it would not. Only accepted drops do anything.
    public class L2DropSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public System.Func<string, bool> accepts;
        public System.Action<string> onAccepted;
        public System.Action<string> onRejected;
        public Graphic highlight;

        static readonly Color Good = new Color(0.3f, 0.85f, 0.45f, 1f);
        static readonly Color Bad = new Color(0.95f, 0.3f, 0.28f, 1f);

        Color baseColor;
        bool hover;
        float flash;

        void Awake()
        {
            if (highlight != null) baseColor = highlight.color;
        }

        void Update()
        {
            if (highlight == null) return;
            Color c = baseColor;
            L2Draggable d = L2Draggable.Current;
            if (d != null)
            {
                bool ok = accepts != null && accepts(d.payload);
                c = Color.Lerp(baseColor, ok ? Good : Bad, hover ? 0.85f : 0.3f);
            }
            if (flash > 0f)
            {
                flash -= Time.unscaledDeltaTime;
                c = Color.Lerp(c, Bad, Mathf.Clamp01(flash / 0.5f));
            }
            highlight.color = c;
        }

        public void OnPointerEnter(PointerEventData e) => hover = true;
        public void OnPointerExit(PointerEventData e) => hover = false;

        public void OnDrop(PointerEventData e)
        {
            L2Draggable d = L2Draggable.Current;
            if (d == null) return;
            if (accepts != null && accepts(d.payload))
            {
                d.accepted = true;
                onAccepted?.Invoke(d.payload);
            }
            else
            {
                flash = 0.5f;
                onRejected?.Invoke(d.payload);
            }
        }

        void OnDisable()
        {
            hover = false;
            flash = 0f;
            if (highlight != null) highlight.color = baseColor;
        }
    }
}
