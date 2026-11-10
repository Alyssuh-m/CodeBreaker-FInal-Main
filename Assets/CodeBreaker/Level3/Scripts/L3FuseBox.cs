using System.Collections;
using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // The wall fuse box with sockets x, y and temp. Starts with fuse 2 in x, fuse 9 in y, temp empty.
    // The player carries one fuse at a time. TEST checks x == 9, y == 2 and temp empty
    // (the state after  temp = x; x = y; y = temp; ).
    public class L3FuseBox : MonoBehaviour
    {
        public string station = "m3";
        public L3FuseSocket[] sockets = new L3FuseSocket[3];   // x, y, temp
        public Transform fuse2, fuse9;
        public TMP_Text display;
        public Light panelLight;
        public Light sparkLight;
        [TextArea(1, 3)] public string hint = "You can't hold two values in one box. Park one in temp first.";
        public float penalty = 10f;
        public L3Reaction[] onSolved = new L3Reaction[0];

        static readonly int[] StartState = { 2, 9, 0 };

        L3Manager M => L3Manager.Instance;
        L2Game G => L2Game.Instance;
        public bool Solved => M != null && M.Solved(station);

        void Start()
        {
            L2Game g = G;
            if (g != null)
            {
                if (fuse2 != null) g.RegisterPrototype(L2Items.Fuse2, fuse2);
                if (fuse9 != null) g.RegisterPrototype(L2Items.Fuse9, fuse9);
            }
            for (int i = 0; i < sockets.Length; i++) if (sockets[i] != null) { sockets[i].box = this; sockets[i].index = i; }
            if (sparkLight != null) sparkLight.enabled = false;
            Layout();
            if (Solved) L3Reaction.RunAll(onSolved, true);
            ShowDisplay();
        }

        public int Get(int i) => G != null ? G.GetInt("fuse_s" + i, StartState[i]) : StartState[i];
        void Set(int i, int v) { if (G != null) G.SetInt("fuse_s" + i, v); }

        public static string IdOf(int v) => v == 2 ? L2Items.Fuse2 : v == 9 ? L2Items.Fuse9 : null;
        static int ValueOf(string id) => id == L2Items.Fuse2 ? 2 : id == L2Items.Fuse9 ? 9 : 0;

        public bool CarryingFuse => G != null && (G.HasItem(L2Items.Fuse2) || G.HasItem(L2Items.Fuse9));

        public void Take(int i)
        {
            L2Game g = G;
            int v = Get(i);
            if (g == null || v == 0 || Solved) return;
            if (CarryingFuse)
            {
                g.Notify("You can only carry one fuse at a time. Put the one you have into an empty socket first.", L2NoteKind.Warning);
                return;
            }
            Set(i, 0);
            string id = IdOf(v);
            g.AddItem(id, false);
            g.Equip(id, false);
            if (g.sfx != null) g.sfx.Pickup();
            g.Notify("You pull fuse " + v + " out of socket " + sockets[i].label + ".", L2NoteKind.Item);
            Layout();
            ShowDisplay();
        }

        public bool Place(int i, string id)
        {
            L2Game g = G;
            if (g == null || Solved) return false;
            if (Get(i) != 0)
            {
                g.Notify("Socket full — that's what temp is for!", L2NoteKind.Warning);
                if (g.sfx != null) g.sfx.Failure();
                return false;
            }
            int v = ValueOf(id);
            if (v == 0) return false;
            g.ReleaseItem(id);
            Set(i, v);
            if (g.sfx != null) g.sfx.Switch(sockets[i].transform.position);
            g.Notify("Fuse " + v + " pushed into socket " + sockets[i].label + ".", L2NoteKind.Info);
            Layout();
            ShowDisplay();
            return true;
        }

        public void Test()
        {
            L2Game g = G;
            if (g == null || M == null) return;
            if (Solved) { g.Notify("The fuse box is already running: x = 9.", L2NoteKind.Info); return; }
            if (Get(0) == 9 && Get(1) == 2 && Get(2) == 0)
            {
                M.Solve(station);
                M.Good();
                g.Notify("TEST OK. The panel lights up: x = 9", L2NoteKind.Success);
                L3Reaction.RunAll(onSolved, false);
                ShowDisplay();
                return;
            }
            M.AddWrong(station);
            M.Penalty(penalty);
            g.Notify("Sparks fly! The fuses don't match the code yet.", L2NoteKind.Warning);
            M.ShowHintIfReady(station, hint);
            StartCoroutine(Sparks());
        }

        IEnumerator Sparks()
        {
            if (sparkLight == null) yield break;
            for (int i = 0; i < 6; i++)
            {
                sparkLight.enabled = i % 2 == 0;
                yield return new WaitForSeconds(0.06f);
            }
            sparkLight.enabled = false;
        }

        void Layout()
        {
            L2Game g = G;
            Place(fuse2, 2, g);
            Place(fuse9, 9, g);
        }

        void Place(Transform fuse, int v, L2Game g)
        {
            if (fuse == null) return;
            for (int i = 0; i < sockets.Length; i++)
            {
                if (Get(i) != v || sockets[i] == null) continue;
                Transform a = sockets[i].anchor != null ? sockets[i].anchor : sockets[i].transform;
                fuse.SetPositionAndRotation(a.position, a.rotation);
                fuse.gameObject.SetActive(true);
                return;
            }
            fuse.gameObject.SetActive(false);   // carried
        }

        void ShowDisplay()
        {
            if (display == null) return;
            if (Solved)
            {
                display.text = "x = 9";
                display.color = new Color(0.4f, 1f, 0.5f);
                if (panelLight != null) panelLight.enabled = true;
                return;
            }
            string s(int v) => v == 0 ? "--" : v.ToString();
            display.text = "x:" + s(Get(0)) + "  y:" + s(Get(1)) + "  temp:" + s(Get(2));
            display.color = new Color(1f, 0.75f, 0.3f);
            if (panelLight != null) panelLight.enabled = false;
        }
    }
}
