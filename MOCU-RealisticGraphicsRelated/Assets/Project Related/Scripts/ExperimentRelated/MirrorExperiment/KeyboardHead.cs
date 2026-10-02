using UnityEngine;
using UnityEngine.InputSystem;


namespace MirrorExperiment
{
    /// Debug only: moves the head with the keyboard, so that the mirror's parallax,
    /// the clear zone and the like can be checked without walking round the room in
    /// the headset - or, on a laptop, without a headset at all.
    ///
    /// While it is active, the headset's own position is ignored and this one is
    /// used instead; the headset's rotation still counts. The car - the mirror, the
    /// clear zone, a fixation point fixed to the body or the mirror - stays where it
    /// is, as it does when a participant moves their head.
    ///
    ///     W S     forward, back - along where the head is looking, kept level
    ///     A D     left, right
    ///     E Q     up, down
    ///     Shift   five times faster
    ///     right mouse button held: look round (for a laptop - a headset turns the
    ///     view by itself)
    ///
    /// Switched on in the Inspector of the MirrorExperiment object while the app
    /// runs, like StarField's AlwaysVisible, and reset with every Play, so it cannot
    /// be left on into a real session. Switching it off puts the head back at its
    /// calibrated place.
    public class KeyboardHead : MonoBehaviour
    {
        [Tooltip("Move the head with WASD/QE (Shift faster), look round with the right mouse button. " +
                 "The headset's own position is ignored meanwhile. Off puts the head back.")]
        public bool Active = false;

        [Tooltip("Meters per second; Shift for five times that.")]
        public float Speed = 0.3f;

        [Tooltip("Degrees of turn per pixel of mouse movement, with the right button held.")]
        public float MouseSensitivity = 0.2f;

        private const float FastFactor = 5f;
        private const float MaxPitch = 89f;

        private Transform _head;

        /// Where the eye is, relative to its calibrated place, in world axes.
        public Vector3 Offset { get; private set; }

        /// The extra turn from the mouse, in degrees. Pitch is positive looking down.
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }


        /// The headset camera, whose heading says which way "forward" is.
        public void SetHead(Transform head)
        {
            _head = head;
        }

        private void Update()
        {
            if (!Active)
            {
                Offset = Vector3.zero;
                Yaw = 0f;
                Pitch = 0f;
                return;
            }

            var mouse = Mouse.current;

            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                Yaw += delta.x * MouseSensitivity;
                Pitch = Mathf.Clamp(Pitch - delta.y * MouseSensitivity, -MaxPitch, MaxPitch);
            }

            var keyboard = Keyboard.current;

            if (keyboard == null)
                return;

            Vector3 input = new Vector3(
                Axis(keyboard.dKey, keyboard.aKey),
                Axis(keyboard.eKey, keyboard.qKey),
                Axis(keyboard.wKey, keyboard.sKey));

            if (input == Vector3.zero)
                return;

            float heading = _head != null ? _head.eulerAngles.y : Yaw;
            float speed = Speed * (keyboard.shiftKey.isPressed ? FastFactor : 1f);

            Offset += Quaternion.Euler(0f, heading, 0f) * input.normalized * speed * Time.unscaledDeltaTime;
        }

        private static float Axis(UnityEngine.InputSystem.Controls.KeyControl positive,
                                  UnityEngine.InputSystem.Controls.KeyControl negative)
        {
            return (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);
        }
    }
}
