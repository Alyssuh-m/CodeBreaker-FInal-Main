using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // A simple push button (fuse box TEST, camera desk buttons 1-6). Calls its target on E.
    public class L3Button : L2Interactable
    {
        public string verb = "Press";
        public UnityEvent onPress = new UnityEvent();
        public L3FuseBox fuseBox;          // TEST button
        public L3CameraWall cameraWall;    // camera desk button
        public int cameraIndex;
        public Transform cap;
        public string disabledByFlag;

        public override bool CanInteract => base.CanInteract && (string.IsNullOrEmpty(disabledByFlag) || Game == null || !Game.Flag(disabledByFlag));
        public override string Prompt => "Press E to " + verb + " " + DisplayName;

        public override void Interact()
        {
            L2Game g = Game;
            if (g != null && g.sfx != null) g.sfx.Click();
            if (cap != null) StartCoroutine(Push());
            if (fuseBox != null) fuseBox.Test();
            if (cameraWall != null) cameraWall.Press(cameraIndex);
            onPress.Invoke();
        }

        IEnumerator Push()
        {
            Vector3 s = cap.localScale;
            cap.localScale = s * 0.82f;
            yield return new WaitForSeconds(0.15f);
            cap.localScale = s;
        }
    }
}
