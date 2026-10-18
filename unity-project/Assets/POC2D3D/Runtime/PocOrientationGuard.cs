using System.Runtime.InteropServices;
using Halka.Game.Input;
using Halka.Game.UI;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // Phase 4: phones are played in landscape only. On a touch device held upright the page shows
    // its rotation guide ("スマホを横向きにしてください") over the canvas; here the game stops
    // taking input (no step, no A, no menu), AUTO and the play-time clock stop, and the touch
    // controls are switched off. The world clock keeps running, and the player's cell, facing,
    // map, view and camera stay as they are, so turning the phone back simply continues.
    [DefaultExecutionOrder(-120)]
    public sealed class PocOrientationGuard : MonoBehaviour
    {
        [SerializeField] private PocGameplay gameplay;
        [SerializeField] private TouchDpad dpad;
        [SerializeField] private TouchActionButton actionButton;
        [SerializeField] private GameMenuController menu;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int HalkaTouchUiMode();
#endif

        // RC1 feedback: phones are played upright again, as in the 2D game. The RC turns this off;
        // the technical POC scene keeps the Phase 4 landscape-only rule for comparison.
        [SerializeField] private bool landscapeOnly = true;
        public bool LandscapeOnly { get => landscapeOnly; set => landscapeOnly = value; }

        // A phone or tablet (or ?touchControls=1): the same rule as the touch D-pad.
        public bool TouchDevice { get; set; }
        public bool GuideVisible { get; private set; }
        public int GuideShownCount { get; private set; }

        public void Configure(PocGameplay sharedGameplay, TouchDpad touchDpad, TouchActionButton button,
            GameMenuController gameMenu)
        {
            gameplay = sharedGameplay;
            dpad = touchDpad;
            actionButton = button;
            menu = gameMenu;
        }

        private void Awake()
        {
            var browserMode = 0;
#if UNITY_WEBGL && !UNITY_EDITOR
            browserMode = HalkaTouchUiMode();
#endif
            TouchDevice = Application.isMobilePlatform || browserMode > 0;
        }

        private void Update() => Evaluate(Screen.width, Screen.height);

        // Portrait = taller than wide (the canvas fills the window on phones).
        public void Evaluate(int width, int height)
        {
            var portrait = landscapeOnly && TouchDevice && height > width;
            if (portrait != GuideVisible) SetGuide(portrait);
        }

        private void SetGuide(bool visible)
        {
            GuideVisible = visible;
            if (visible) GuideShownCount++;
            if (gameplay != null) gameplay.Suspended = visible;
            if (menu != null)
            {
                if (visible) menu.SetOpen(false, Time.unscaledTime);
                menu.enabled = !visible;
            }
            if (dpad != null) dpad.enabled = !visible;
            if (actionButton != null) actionButton.enabled = !visible;
        }
    }
}
