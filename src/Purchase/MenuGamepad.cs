using UnityEngine;

namespace PlayableVehicles
{
    internal static class MenuGamepad
    {
        private const float StickThreshold = 0.5f;
        private const float RepeatDelay = 0.4f;
        private const float RepeatInterval = 0.15f;

        private static Vector2Int _heldDirection;
        private static float _nextRepeat;

        public static bool Confirm => InputGate.JoystickButtonDown(GameActions.Fire);
        public static bool Back => InputGate.JoystickButtonDown(GameActions.TargetCancel);
        public static bool Extra => InputGate.JoystickButtonDown(GameActions.TargetSelect);
        public static bool NextTab => InputGate.JoystickButtonDown(GameActions.Throttle);
        public static bool PreviousTab => InputGate.JoystickButtonDown(GameActions.Throttle, true);

        public static Vector2Int Step()
        {
            float x = InputGate.JoystickAxis(GameActions.MoveLateral, 0f);
            float y = InputGate.JoystickAxis(GameActions.MoveLongitudinal, 0f);
            var direction = Vector2Int.zero;
            if (Mathf.Abs(x) >= Mathf.Abs(y))
            {
                if (x > StickThreshold) direction.x = 1;
                else if (x < -StickThreshold) direction.x = -1;
            }
            else
            {
                if (y > StickThreshold) direction.y = 1;
                else if (y < -StickThreshold) direction.y = -1;
            }

            float now = Time.unscaledTime;
            if (direction == Vector2Int.zero)
            {
                _heldDirection = Vector2Int.zero;
                return Vector2Int.zero;
            }
            if (direction != _heldDirection)
            {
                _heldDirection = direction;
                _nextRepeat = now + RepeatDelay;
                return direction;
            }
            if (now < _nextRepeat) return Vector2Int.zero;
            _nextRepeat = now + RepeatInterval;
            return direction;
        }
    }
}
