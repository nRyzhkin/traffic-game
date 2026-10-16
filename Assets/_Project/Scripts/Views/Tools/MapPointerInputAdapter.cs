using System;
using TrafficGame.Presentation.Tools;
using TrafficGame.Views.UI;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace TrafficGame.Views.Tools
{
    /// <summary>
    /// Reports pointer movement and clicks on the map. A mouse click is a press and release without dragging
    /// (dragging pans the camera). On touch screens a tap moves the pointer and is reported as a tap — most tools
    /// confirm with the ✓ button — and the second tap of a double tap is a click. Presses that start on the UI never
    /// reach the map.
    /// </summary>
    public sealed class MapPointerInputAdapter : MonoBehaviour, IMapPointerInput
    {
        // Browsers emulate mouse events right after touches; ignore the mouse for a moment so taps are not doubled.
        private const float MouseIgnoreAfterTouchSeconds = 0.5f;

        private IUiHitTest _ui;
        private float2 _lastPosition = new(float.NaN);
        private float2? _primaryPress;
        private float2? _secondaryPress;

        private bool _touchActive;
        private bool _touchIsTap;
        private float2 _touchPress;
        private float2 _touchPosition;
        private float _lastTouchTime = float.NegativeInfinity;
        private float _lastTapTime = float.NegativeInfinity;
        private float2 _lastTapPosition;

        public event Action<float2> PointerMoved;
        public event Action<float2> Tapped;
        public event Action<float2> PrimaryClicked;
        public event Action SecondaryClicked;

        public void SetUiHitTest(IUiHitTest ui) => _ui = ui;

        private void Update()
        {
            if (!ReadTouch())
                ReadMouse();
        }

        private bool ReadTouch()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null)
                return false;

            TouchControl first = null;
            var count = 0;
            foreach (var touch in touchscreen.touches)
            {
                if (!touch.press.isPressed)
                    continue;
                first ??= touch;
                count++;
            }

            if (count == 0)
            {
                if (!_touchActive)
                    return false;

                _touchActive = false;
                if (_touchIsTap)
                    OnTap(_touchPosition);
                return true;
            }

            _lastTouchTime = Time.unscaledTime;
            _touchPosition = first.position.ReadValue();
            if (!_touchActive)
            {
                _touchActive = true;
                _touchPress = _touchPosition;
                _touchIsTap = !IsOverUi(_touchPosition);
            }

            _touchIsTap &= count == 1 && math.distance(_touchPosition, _touchPress) <= PointerGestures.DragSlop;
            return true;
        }

        private void OnTap(float2 position)
        {
            Move(position);

            var isDoubleTap = Time.unscaledTime - _lastTapTime <= PointerGestures.DoubleTapSeconds &&
                              math.distance(position, _lastTapPosition) <= PointerGestures.DoubleTapDistance;
            _lastTapTime = isDoubleTap ? float.NegativeInfinity : Time.unscaledTime;
            _lastTapPosition = position;

            if (isDoubleTap)
                PrimaryClicked?.Invoke(position);
            else
                Tapped?.Invoke(position);
        }

        private void ReadMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null || Time.unscaledTime - _lastTouchTime < MouseIgnoreAfterTouchSeconds)
                return;

            float2 position = mouse.position.ReadValue();
            if (!IsOverUi(position))
                Move(position);

            if (IsClick(mouse.leftButton, position, ref _primaryPress))
                PrimaryClicked?.Invoke(position);
            if (IsClick(mouse.rightButton, position, ref _secondaryPress))
                SecondaryClicked?.Invoke();
        }

        private bool IsClick(ButtonControl button, float2 position, ref float2? press)
        {
            if (button.wasPressedThisFrame)
                press = IsOverUi(position) ? null : position;

            if (!button.wasReleasedThisFrame || !press.HasValue)
                return false;

            var isClick = math.distance(position, press.Value) <= PointerGestures.DragSlop;
            press = null;
            return isClick;
        }

        private bool IsOverUi(float2 position) => _ui != null && _ui.IsOverUi(position);

        private void Move(float2 position)
        {
            if (math.all(position == _lastPosition))
                return;

            _lastPosition = position;
            PointerMoved?.Invoke(position);
        }
    }
}
