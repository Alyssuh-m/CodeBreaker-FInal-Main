using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Control room system diagram. Each node shows the state of an existing puzzle flag (read only):
    // red flashing = failed, green = online. It never changes game state.
    public class L3SystemDiagram : MonoBehaviour
    {
        [System.Serializable]
        public class Node
        {
            public string label;
            public string flag;          // L2Game flag that means "online" (e.g. solved_gen); empty = use intKey
            public string intKey;        // optional: online when GetInt(intKey) == intValue
            public int intValue;
            public Renderer lamp;
            public TMP_Text text;
        }

        public Node[] nodes = new Node[0];
        public TMP_Text statusLine;
        public string allOnlineFlag = "solved_c3";
        public Light glow;
        public Color green = new Color(0.25f, 1f, 0.4f), red = new Color(1f, 0.12f, 0.08f), off = new Color(0.08f, 0.08f, 0.09f);
        MaterialPropertyBlock block;
        float check;

        void Awake() { block = new MaterialPropertyBlock(); }

        void Update()
        {
            L2Game g = L2Game.Instance;
            bool blink = Mathf.FloorToInt(Time.time * 2.2f) % 2 == 0;
            int online = 0;
            foreach (Node n in nodes)
            {
                bool ok = g != null && (!string.IsNullOrEmpty(n.flag) ? g.Flag(n.flag) : (!string.IsNullOrEmpty(n.intKey) && g.GetInt(n.intKey, -1) == n.intValue));
                if (ok) online++;
                Color c = ok ? green : (blink ? red : off);
                if (n.lamp != null)
                {
                    n.lamp.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", ok ? c * 1.3f : c * 1.6f);
                    block.SetColor("_EmissionColor", c * (ok ? 2.2f : 2.8f));
                    n.lamp.SetPropertyBlock(block);
                }
                if (n.text != null) n.text.color = ok ? green : new Color(1f, 0.35f, 0.3f);
            }
            check -= Time.deltaTime;
            if (check > 0f) return;
            check = 0.25f;
            bool all = g != null && g.Flag(allOnlineFlag);
            if (statusLine != null)
                statusLine.text = all ? "<color=#5FD383>SYSTEM ONLINE  ·  REBOOT COMPLETE</color>"
                                      : "<color=#FF4A3D>SYSTEM FAILURE</color>  <color=#9FB3BF>" + online + " / " + nodes.Length + " subsystems online</color>";
            if (glow != null) glow.color = all ? green : Color.Lerp(red, green, 0.25f);
        }
    }
}
