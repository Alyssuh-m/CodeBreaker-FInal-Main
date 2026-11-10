using UnityEngine;

namespace CodeBreaker.Level2
{
    // Living Room wall clock. The clock interface sets the hour and minute separately; nothing changes
    // until Confirm. Only a confirmed 8:00 satisfies the puzzle. It can be reopened and changed any time.
    public class L2WallClock : L2Interactable
    {
        public Transform hourHand;
        public Transform minuteHand;
        public int targetHour = 8;
        public int targetMinute = 0;

        public int Hour { get; private set; }
        public int Minute { get; private set; }
        public bool IsCorrect => Game != null && Game.Flag("clock_confirmed_8");

        Vector3 normal, center;
        Quaternion hourBase, minuteBase;
        float hourBaseAngle, minuteBaseAngle;

        public override string Prompt => "Press E to Set the Clock";

        void Awake()
        {
            center = hourHand != null ? hourHand.position : transform.position;
            Vector3 n = hourHand != null ? hourHand.position - transform.position : -transform.forward;
            n.y = 0f;
            normal = n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.right;
            if (hourHand != null) { hourBase = hourHand.rotation; hourBaseAngle = HandAngle(hourHand); }
            if (minuteHand != null) { minuteBase = minuteHand.rotation; minuteBaseAngle = HandAngle(minuteHand); }
        }

        void Start()
        {
            if (Game != null && Game.HasInt("clock_h"))
            {
                Hour = Game.GetInt("clock_h", 10);
                Minute = Game.GetInt("clock_m", 10);
            }
            else
            {
                // read the time the clock already shows (rounded to 5 minutes)
                int m = Mathf.RoundToInt(Mathf.Repeat(minuteBaseAngle, 360f) / 6f / 5f) * 5 % 60;
                int h = Mathf.FloorToInt(Mathf.Repeat(hourBaseAngle - m * 0.5f + 1f, 360f) / 30f) % 12;
                Hour = h == 0 ? 12 : h;
                Minute = m;
            }
            ApplyHands();
        }

        // Clockwise angle from 12 o'clock as seen from the front of the clock.
        float HandAngle(Transform hand)
        {
            Renderer r = hand.GetComponent<Renderer>();
            Vector3 dir = (r != null ? r.bounds.center : hand.position + hand.up) - center;
            dir = Vector3.ProjectOnPlane(dir, normal);
            if (dir.sqrMagnitude < 1e-8f) return 0f;
            return Vector3.SignedAngle(Vector3.up, dir, normal);
        }

        void ApplyHands()
        {
            float mAngle = Minute * 6f;
            float hAngle = (Hour % 12) * 30f + Minute * 0.5f;
            if (hourHand != null) hourHand.rotation = Quaternion.AngleAxis(hAngle - hourBaseAngle, normal) * hourBase;
            if (minuteHand != null) minuteHand.rotation = Quaternion.AngleAxis(mAngle - minuteBaseAngle, normal) * minuteBase;
        }

        public override void Interact()
        {
            if (Game != null && Game.ui != null) Game.ui.OpenClock(this);
        }

        // Called by the clock interface's Confirm button.
        public void Confirm(int hour, int minute)
        {
            Hour = hour;
            Minute = minute;
            ApplyHands();
            bool correct = (hour % 12) == (targetHour % 12) && minute == targetMinute;
            if (Game != null)
            {
                Game.SetInt("clock_h", hour);
                Game.SetInt("clock_m", minute);
                Game.SetFlag("clock_confirmed_8", correct);
                if (Game.sfx != null) Game.sfx.Click();
                Game.Notify("The clock now reads " + hour + ":" + minute.ToString("00") + ".", L2NoteKind.Info);
                Game.RefreshObjective();
            }
        }
    }
}
