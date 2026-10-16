using System;
using TrafficGame.Presentation.Cameras;
using TrafficGame.Views.UI;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace TrafficGame.Views.Cameras
{
    /// <summary>
    /// Turns mouse and touch input into camera gestures. Any button or a single finger pans once the press has
    /// moved past <see cref="PointerGestures.DragSlop"/> (so short clicks and taps stay with the tools);
    /// the wheel zooms at the cursor; two fingers pan and pinch-zoom. Presses that start on the UI are ignored.
    /// </summary>
    public sealed class CameraInputAdapter : MonoBehaviour, ICameraInput
    {
        private float _scrollZoomSensitivity = 0.008f;
        private IUiHitTest _ui;

        private bool _mouseDown;
        private bool _mouseDragging;
        private float2 _mousePress;
        private float2 _lastMousePosition;

        private int _lastTouchCount;
        private bool _touchStartedOnUi;
        private bool _touchDragging;
        private float2 _touchPress;
        private float2 _lastTouchCenter;
        private float _lastTouchSpan;

        public event Action<float2> Panned;
        public event Action PanEnded;
        public event Action<float2, float> Zoomed;
        public event Action<float2, float> ZoomStepped;

        /// <param name="scrollZoomSensitivity">Zoom exponent per unit of wheel scroll.</param>
        public void Configure(float scrollZoomSensitivity) => _scrollZoomSensitivity = scrollZoomSensitivity;

        public void SetUiHitTest(IUiHitTest ui) => _ui = ui;

        private void Update()
        {
            // Browsers may also emit mouse events for touches, so touches take priority.
            if (!ReadTouches())
                ReadMouse();
        }

        private bool ReadTouches()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null)
                return false;

            TouchControl first = null, second = null;
            var count = 0;
            foreach (var touch in touchscreen.touches)
            {
                if (!touch.press.isPressed)
                    continue;

                if (count == 0)
                    first = touch;
                else if (count == 1)
                    second = touch;
                count++;
            }

            if (count == 0)
            {
                if (_lastTouchCount > 0 && _touchDragging)
                    PanEnded?.Invoke();
                _touchDragging = false;
                _lastTouchCount = 0;
                return false;
            }

            float2 center = first.position.ReadValue();
            var span = 0f;
            if (second != null)
            {
                float2 other = second.position.ReadValue();
                span = math.distance(center, other);
                center = (center + other) * 0.5f;
            }

            if (_lastTouchCount == 0)
            {
                _touchStartedOnUi = IsOverUi(center);
                _touchDragging = false;
                _touchPress = center;
            }

            // Only continue a gesture while the finger count is unchanged; otherwise the center would jump.
            if (!_touchStartedOnUi && count == _lastTouchCount)
            {
                _touchDragging |= count >= 2 || math.distance(center, _touchPress) > PointerGestures.DragSlop;
                if (_touchDragging)
                    RaisePanned(center - _lastTouchCenter);
                if (count >= 2 && _lastTouchSpan > 0f && span > 0f)
                    Zoomed?.Invoke(center, span / _lastTouchSpan);
            }

            _lastTouchCount = count;
            _lastTouchCenter = center;
            _lastTouchSpan = span;
            return true;
        }

        private void ReadMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            float2 position = mouse.position.ReadValue();
            var pressed = mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed;

            if (pressed && !_mouseDown && !IsOverUi(position))
            {
                _mouseDown = true;
                _mouseDragging = false;
                _mousePress = position;
            }
            else if (!pressed && _mouseDown)
            {
                _mouseDown = false;
                if (_mouseDragging)
                    PanEnded?.Invoke();
            }

            if (_mouseDown)
            {
                _mouseDragging |= math.distance(position, _mousePress) > PointerGestures.DragSlop;
                if (_mouseDragging)
                    RaisePanned(position - _lastMousePosition);
            }

            _lastMousePosition = position;

            var scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f && !IsOverUi(position))
                ZoomStepped?.Invoke(position, math.exp(scroll * _scrollZoomSensitivity));
        }

        private bool IsOverUi(float2 position) => _ui != null && _ui.IsOverUi(position);

        private void RaisePanned(float2 delta)
        {
            if (math.any(delta != float2.zero))
                Panned?.Invoke(delta);
        }
    }
}
