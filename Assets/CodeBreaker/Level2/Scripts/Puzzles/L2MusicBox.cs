using System.Collections;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // music box (white storage cabinet)
    public class L2MusicBox : L2Interactable
    {
        public Transform lid;
        public Transform lidMirror;
        public Transform[] dancer = new Transform[0];
        public Transform platform;
        public Renderer danceCard;
        public Texture danceCardImage;
        [Tooltip("Hinge line of the lid (world space), filled by the setup tool")]
        public Vector3 hingePoint;
        public Vector3 hingeAxis = Vector3.forward;
        public float openAngle = 75f;

        bool open, playing;
        float angle;   // 0 = closed, openAngle = open

        public override string Prompt => open ? "Press E to Wind Up" : "Press E to Open";

        void Start()
        {
            // The box starts closed (the ballerina folded away) unless it was opened before a checkpoint.
            open = Game != null && Game.Flag("musicbox_open");
            angle = openAngle;
            if (!open) SetLid(0f);
            SetDancerVisible(open);
        }

        void SetLid(float a)
        {
            float delta = a - angle;
            if (Mathf.Abs(delta) < 0.001f) return;
            if (lid != null) lid.RotateAround(hingePoint, hingeAxis, delta);
            if (lidMirror != null) lidMirror.RotateAround(hingePoint, hingeAxis, delta);
            angle = a;
        }

        void SetDancerVisible(bool on)
        {
            foreach (Transform t in dancer) if (t != null) t.gameObject.SetActive(on);
            if (platform != null) platform.gameObject.SetActive(on);
            if (danceCard != null) danceCard.gameObject.SetActive(on);
        }

        public override void Interact()
        {
            if (playing) return;
            StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            playing = true;
            if (!open)
            {
                open = true;
                if (Game != null) Game.SetFlag("musicbox_open");
                SetDancerVisible(true);
                for (float t = 0f; t < 0.6f; t += Time.deltaTime) { SetLid(Mathf.SmoothStep(0f, openAngle, t / 0.6f)); yield return null; }
                SetLid(openAngle);
            }
            if (Game != null && Game.sfx != null) Game.sfx.MusicBox(transform.position);

            Vector3 axisPoint = platform != null ? platform.position : transform.position;
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                float spin = 140f * Time.deltaTime;
                foreach (Transform d in dancer) if (d != null) d.RotateAround(axisPoint, Vector3.up, spin);
                yield return null;
            }

            if (Game != null)
            {
                if (Game.ui != null)
                    Game.ui.ShowDocument("Dance Card", danceCardImage,
                        "Tucked inside the music box, under the dancing ballerina:\n<b>UP  →  RIGHT  →  RIGHT  →  DOWN</b>");
                if (!Game.Flag("dance_seen")) { Game.SetFlag("dance_seen"); Game.RefreshObjective(); }
            }

            // keep turning quietly while the card is read
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                float spin = 140f * Time.deltaTime;
                foreach (Transform d in dancer) if (d != null) d.RotateAround(axisPoint, Vector3.up, spin);
                yield return null;
            }
            playing = false;
        }
    }
}
