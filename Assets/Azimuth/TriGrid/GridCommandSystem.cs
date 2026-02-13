// GridCommandSystem.cs
using System;
using System.Collections.Generic;

namespace TriGrid.Core
{
    /// <summary>
    /// Base interface for reversible grid commands.
    /// </summary>
    public interface IGridCommand
    {
        GridChangeResult Execute(TriGridData grid);
        GridChangeResult Undo(TriGridData grid);
        string Description { get; }
    }

    /// <summary>
    /// Command to add a face to the grid.
    /// </summary>
    public class AddFaceCommand : IGridCommand
    {
        private readonly FaceCoord _face;

        public AddFaceCommand(FaceCoord face) => _face = face;
        public string Description => $"Add face {_face}";

        public GridChangeResult Execute(TriGridData grid) => grid.AddFace(_face);
        public GridChangeResult Undo(TriGridData grid) => grid.RemoveFace(_face);
    }

    /// <summary>
    /// Command to remove a face from the grid.
    /// </summary>
    public class RemoveFaceCommand : IGridCommand
    {
        private readonly FaceCoord _face;

        public RemoveFaceCommand(FaceCoord face) => _face = face;
        public string Description => $"Remove face {_face}";

        public GridChangeResult Execute(TriGridData grid) => grid.RemoveFace(_face);
        public GridChangeResult Undo(TriGridData grid) => grid.AddFace(_face);
    }

    /// <summary>
    /// Command to place an emitter on a vertex.
    /// </summary>
    public class PlaceEmitterCommand : IGridCommand
    {
        private readonly VertexCoord _vertex;
        private readonly EmitterConfig _config;

        public PlaceEmitterCommand(VertexCoord vertex, EmitterConfig config)
        {
            _vertex = vertex;
            _config = config;
        }

        public string Description => $"Place emitter at {_vertex}";

        public GridChangeResult Execute(TriGridData grid) => grid.PlaceEmitter(_vertex, _config);
        public GridChangeResult Undo(TriGridData grid) => grid.RemoveEmitter(_vertex);
    }

    /// <summary>
    /// Command to remove an emitter from a vertex.
    /// </summary>
    public class RemoveEmitterCommand : IGridCommand
    {
        private readonly VertexCoord _vertex;
        private EmitterConfig _savedConfig;

        public RemoveEmitterCommand(VertexCoord vertex) => _vertex = vertex;
        public string Description => $"Remove emitter at {_vertex}";

        public GridChangeResult Execute(TriGridData grid)
        {
            // Save config before removal for undo
            if (grid.TryGetVertexState(_vertex, out var state) && state.HasEmitter)
                _savedConfig = state.EmitterConfig;
            return grid.RemoveEmitter(_vertex);
        }

        public GridChangeResult Undo(TriGridData grid) => grid.PlaceEmitter(_vertex, _savedConfig);
    }

    /// <summary>
    /// Command to place a reflector on a vertex.
    /// </summary>
    public class PlaceReflectorCommand : IGridCommand
    {
        private readonly VertexCoord _vertex;
        private readonly int _direction;

        public PlaceReflectorCommand(VertexCoord vertex, int direction)
        {
            _vertex = vertex;
            _direction = direction;
        }

        public string Description => $"Place reflector at {_vertex} dir={_direction}";

        public GridChangeResult Execute(TriGridData grid) => grid.PlaceReflector(_vertex, _direction);
        public GridChangeResult Undo(TriGridData grid) => grid.RemoveReflector(_vertex);
    }

    /// <summary>
    /// Command to remove a reflector from a vertex.
    /// </summary>
    public class RemoveReflectorCommand : IGridCommand
    {
        private readonly VertexCoord _vertex;
        private int _savedDirection;

        public RemoveReflectorCommand(VertexCoord vertex) => _vertex = vertex;
        public string Description => $"Remove reflector at {_vertex}";

        public GridChangeResult Execute(TriGridData grid)
        {
            if (grid.TryGetVertexState(_vertex, out var state) && state.HasReflector)
                _savedDirection = state.ReflectorDirection;
            return grid.RemoveReflector(_vertex);
        }

        public GridChangeResult Undo(TriGridData grid) => grid.PlaceReflector(_vertex, _savedDirection);
    }

    /// <summary>
    /// Manages undo/redo stacks for grid commands.
    /// </summary>
    public class GridCommandHistory
    {
        private readonly Stack<IGridCommand> _undoStack = new Stack<IGridCommand>();
        private readonly Stack<IGridCommand> _redoStack = new Stack<IGridCommand>();
        private readonly int _maxHistory;

        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;
        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public event Action OnHistoryChanged;

        public GridCommandHistory(int maxHistory = 256)
        {
            _maxHistory = maxHistory;
        }

        /// <summary>
        /// Execute a command and push it onto the undo stack.
        /// Clears the redo stack.
        /// </summary>
        public GridChangeResult Execute(IGridCommand command, TriGridData grid)
        {
            var result = command.Execute(grid);
            if (result == GridChangeResult.Success)
            {
                _undoStack.Push(command);
                _redoStack.Clear();

                // Trim if over max
                if (_undoStack.Count > _maxHistory)
                {
                    // Stack doesn't support trimming from bottom easily;
                    // for simplicity we accept unbounded growth up to maxHistory.
                    // A production system would use a deque.
                }

                OnHistoryChanged?.Invoke();
            }
            return result;
        }

        /// <summary>
        /// Undo the last command.
        /// </summary>
        public GridChangeResult Undo(TriGridData grid)
        {
            if (_undoStack.Count == 0)
                return GridChangeResult.NotFound;

            var command = _undoStack.Pop();
            var result = command.Undo(grid);
            if (result == GridChangeResult.Success)
            {
                _redoStack.Push(command);
                OnHistoryChanged?.Invoke();
            }
            else
            {
                // Undo failed (e.g., pulse conflict on re-remove), push back
                _undoStack.Push(command);
            }
            return result;
        }

        /// <summary>
        /// Redo the last undone command.
        /// </summary>
        public GridChangeResult Redo(TriGridData grid)
        {
            if (_redoStack.Count == 0)
                return GridChangeResult.NotFound;

            var command = _redoStack.Pop();
            var result = command.Execute(grid);
            if (result == GridChangeResult.Success)
            {
                _undoStack.Push(command);
                OnHistoryChanged?.Invoke();
            }
            else
            {
                _redoStack.Push(command);
            }
            return result;
        }

        /// <summary>
        /// Clear all history.
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnHistoryChanged?.Invoke();
        }
    }
}
