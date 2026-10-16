using System;
using System.Collections.Generic;

namespace TrafficGame.Core.Commands
{
    /// <summary>Executes commands and keeps a bounded undo/redo history.</summary>
    public sealed class CommandHistory
    {
        private readonly List<ICommand> _undo = new();
        private readonly List<ICommand> _redo = new();
        private readonly int _capacity;

        public CommandHistory(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "History must hold at least one command.");
            _capacity = capacity;
        }

        public event Action Changed;

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public void Execute(ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            command.Execute();
            _undo.Add(command);
            if (_undo.Count > _capacity)
                _undo.RemoveAt(0);
            _redo.Clear();
            Changed?.Invoke();
        }

        public bool Undo()
        {
            if (!CanUndo)
                return false;

            var command = Pop(_undo);
            command.Undo();
            _redo.Add(command);
            Changed?.Invoke();
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo)
                return false;

            var command = Pop(_redo);
            command.Execute();
            _undo.Add(command);
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            Changed?.Invoke();
        }

        private static ICommand Pop(List<ICommand> stack)
        {
            var command = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            return command;
        }
    }
}
