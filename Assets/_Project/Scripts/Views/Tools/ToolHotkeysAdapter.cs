using System;
using TrafficGame.Presentation.Tools;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrafficGame.Views.Tools
{
    /// <summary>
    /// Optional desktop shortcuts mirroring the toolbar: 1 road, 2 curved road, 3 roundabout, 4 bulldozer, Tab next road type,
    /// Enter confirm, Esc back, Ctrl/Cmd+Z undo, Ctrl/Cmd+Shift+Z or Ctrl+Y redo.
    /// </summary>
    public sealed class ToolHotkeysAdapter : MonoBehaviour, IToolInput
    {
        public event Action<ToolKind> ToolRequested;
        public event Action ConfirmRequested;
        public event Action UndoRequested;
        public event Action RedoRequested;
        public event Action CancelRequested;
        public event Action NextProfileRequested;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame)
                ToolRequested?.Invoke(ToolKind.Road);
            if (keyboard.digit2Key.wasPressedThisFrame)
                ToolRequested?.Invoke(ToolKind.CurvedRoad);
            if (keyboard.digit3Key.wasPressedThisFrame)
                ToolRequested?.Invoke(ToolKind.Roundabout);
            if (keyboard.digit4Key.wasPressedThisFrame)
                ToolRequested?.Invoke(ToolKind.Bulldoze);
            if (keyboard.digit5Key.wasPressedThisFrame)
                ToolRequested?.Invoke(ToolKind.TrafficLights);
            if (keyboard.digit6Key.wasPressedThisFrame)
                ToolRequested?.Invoke(ToolKind.LaneArrows);
            if (keyboard.tabKey.wasPressedThisFrame)
                NextProfileRequested?.Invoke();
            if (keyboard.escapeKey.wasPressedThisFrame)
                CancelRequested?.Invoke();
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                ConfirmRequested?.Invoke();

            var command = keyboard.ctrlKey.isPressed || keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed;
            if (!command)
                return;

            if (keyboard.zKey.wasPressedThisFrame)
            {
                if (keyboard.shiftKey.isPressed)
                    RedoRequested?.Invoke();
                else
                    UndoRequested?.Invoke();
            }

            if (keyboard.yKey.wasPressedThisFrame)
                RedoRequested?.Invoke();
        }
    }
}
