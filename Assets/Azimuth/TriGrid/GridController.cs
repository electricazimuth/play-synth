// GridController.cs
using UnityEngine;
using TriGrid.Core;
using TriGrid.Pulse;

namespace TriGrid.Unity
{
    /// <summary>
    /// The central mediator that owns the grid data, coordinates input, visuals, and pulses.
    /// This is the main entry point MonoBehaviour for the grid system.
    /// </summary>
    public class GridController : MonoBehaviour
    {
        [Header("Grid Configuration")]
        [SerializeField] private int _gridMinQ = 0;
        [SerializeField] private int _gridMinR = 0;
        [SerializeField] private int _gridMaxQ = 11;
        [SerializeField] private int _gridMaxR = 11;
        [SerializeField] private Vector3 _gridOrigin = Vector3.zero;

        [Header("Pulse Configuration")]
        [SerializeField] private int _maxPulses = 64;
        [SerializeField] private float _beatsPerEdge = 0.5f;

        [Header("References")]
        [SerializeField] private GridInputHandler _inputHandler;
        [SerializeField] private GridVisualManager _visualManager;
        [SerializeField] private PulseVisualManager _pulseVisualManager;
        [SerializeField] private BeatClock _beatClock;

        // ─── Core Systems ───
        private TriGridData _gridData;
        private PulseSystem _pulseSystem;
        private GridCommandHistory _commandHistory;

        // ─── Current Tool State ───
        private GridTool _currentTool = GridTool.AddFace;
        private EmitterConfig _currentEmitterConfig = EmitterConfig.Default;
        private int _currentReflectorDirection = 0;

        // ─── Properties ───
        public TriGridData GridData => _gridData;
        public PulseSystem PulseSystem => _pulseSystem;
        public GridCommandHistory CommandHistory => _commandHistory;
        public GridTool CurrentTool
        {
            get => _currentTool;
            set => SetTool(value);
        }

        private void Awake()
        {
            InitializeSystems();
        }

        private void OnDestroy()
        {
            UnsubscribeFromInput();
        }

        private void Update()
        {
            HandleKeyboardShortcuts();

            // Update pulse visual scales
            if (_pulseVisualManager != null)
            {
                _pulseVisualManager.UpdatePulseScales();
            }

            // Cleanup destroyed pulses
            _pulseSystem?.CleanupDestroyedPulses();
        }

        // ─────────────────────────────────────────────
        //  INITIALIZATION
        // ─────────────────────────────────────────────

        private void InitializeSystems()
        {
            // Create core data
            _gridData = new TriGridData(_gridMinQ, _gridMinR, _gridMaxQ, _gridMaxR);
            _pulseSystem = new PulseSystem(_gridData, _maxPulses, _gridOrigin);
            _commandHistory = new GridCommandHistory();

            // Initialize visual manager
            if (_visualManager != null)
            {
                _visualManager.Initialize(_gridData, _gridOrigin);
            }

            // Initialize pulse visual manager
            if (_pulseVisualManager != null)
            {
                _pulseVisualManager.Initialize(_pulseSystem, _gridOrigin);
            }

            // Initialize input handler
            if (_inputHandler != null)
            {
                _inputHandler.Initialize(_gridData, _gridOrigin);
                SubscribeToInput();
            }

            // Connect beat clock to pulse system
            if (_beatClock != null)
            {
                _beatClock.OnBeat += HandleBeat;
                UpdateEdgeTravelDuration();
            }
        }

        // ─────────────────────────────────────────────
        //  INPUT HANDLING
        // ─────────────────────────────────────────────

        private void SubscribeToInput()
        {
            _inputHandler.OnFaceClicked += HandleFaceClicked;
            _inputHandler.OnFaceRightClicked += HandleFaceRightClicked;
            _inputHandler.OnVertexClicked += HandleVertexClicked;
            _inputHandler.OnVertexRightClicked += HandleVertexRightClicked;
            _inputHandler.OnFaceHovered += HandleFaceHovered;
            _inputHandler.OnVertexHovered += HandleVertexHovered;
            _inputHandler.OnHoverLost += HandleHoverLost;
        }

        private void UnsubscribeFromInput()
        {
            if (_inputHandler == null) return;
            _inputHandler.OnFaceClicked -= HandleFaceClicked;
            _inputHandler.OnFaceRightClicked -= HandleFaceRightClicked;
            _inputHandler.OnVertexClicked -= HandleVertexClicked;
            _inputHandler.OnVertexRightClicked -= HandleVertexRightClicked;
            _inputHandler.OnFaceHovered -= HandleFaceHovered;
            _inputHandler.OnVertexHovered -= HandleVertexHovered;
            _inputHandler.OnHoverLost -= HandleHoverLost;
        }

        private void HandleFaceClicked(FaceCoord face)
        {
            switch (_currentTool)
            {
                case GridTool.AddFace:
                    _commandHistory.Execute(new AddFaceCommand(face), _gridData);
                    break;
                case GridTool.RemoveFace:
                    _commandHistory.Execute(new RemoveFaceCommand(face), _gridData);
                    break;
            }
        }

        private void HandleFaceRightClicked(FaceCoord face)
        {
            // Right-click always removes
            _commandHistory.Execute(new RemoveFaceCommand(face), _gridData);
        }

        private void HandleVertexClicked(VertexCoord vertex)
        {
            switch (_currentTool)
            {
                case GridTool.PlaceEmitter:
                    _commandHistory.Execute(
                        new PlaceEmitterCommand(vertex, _currentEmitterConfig), _gridData);
                    break;
                case GridTool.PlaceReflector:
                    _commandHistory.Execute(
                        new PlaceReflectorCommand(vertex, _currentReflectorDirection), _gridData);
                    break;
            }
        }

        private void HandleVertexRightClicked(VertexCoord vertex)
        {
            // Right-click removes whatever is on the vertex
            if (_gridData.HasEmitter(vertex))
            {
                _commandHistory.Execute(new RemoveEmitterCommand(vertex), _gridData);
            }
            else if (_gridData.HasReflector(vertex))
            {
                _commandHistory.Execute(new RemoveReflectorCommand(vertex), _gridData);
            }
        }

        private void HandleFaceHovered(FaceCoord face)
        {
            if (_visualManager != null && (_currentTool == GridTool.AddFace || _currentTool == GridTool.RemoveFace))
            {
                _visualManager.ShowFacePreview(face);
            }
        }

        private void HandleVertexHovered(VertexCoord vertex)
        {
            if (_visualManager != null &&
                (_currentTool == GridTool.PlaceEmitter || _currentTool == GridTool.PlaceReflector))
            {
                _visualManager.ShowVertexPreview(vertex);
            }
        }

        private void HandleHoverLost()
        {
            _visualManager?.HidePreview();
        }

        // ─────────────────────────────────────────────
        //  TOOL MANAGEMENT
        // ─────────────────────────────────────────────

        private void SetTool(GridTool tool)
        {
            _currentTool = tool;

            // Update input handler mode based on tool
            if (_inputHandler != null)
            {
                switch (tool)
                {
                    case GridTool.AddFace:
                    case GridTool.RemoveFace:
                        _inputHandler.InteractionMode = GridInteractionMode.Face;
                        break;
                    case GridTool.PlaceEmitter:
                    case GridTool.PlaceReflector:
                        _inputHandler.InteractionMode = GridInteractionMode.Vertex;
                        break;
                }
            }

            _visualManager?.HidePreview();
        }

        public void SetEmitterConfig(EmitterConfig config)
        {
            _currentEmitterConfig = config;
        }

        public void SetReflectorDirection(int direction)
        {
            _currentReflectorDirection = ((direction % 6) + 6) % 6;
        }

        // ─────────────────────────────────────────────
        //  BEAT CLOCK INTEGRATION
        // ─────────────────────────────────────────────

        private void HandleBeat(int beatIndex)
        {
            _pulseSystem.EmitFromAllEmitters();

            // Start movement for any stationary pulses
            foreach (var pulse in _pulseSystem.ActivePulses)
            {
                if (pulse.IsAlive && !pulse.IsMoving)
                {
                    _pulseSystem.BeginPulseMovement(pulse);
                }
            }
        }

        private void UpdateEdgeTravelDuration()
        {
            if (_beatClock != null && _pulseVisualManager != null)
            {
                _pulseVisualManager.EdgeTravelDuration = _beatClock.GetEdgeTravelDuration(_beatsPerEdge);
            }
        }

        // ─────────────────────────────────────────────
        //  KEYBOARD SHORTCUTS
        // ─────────────────────────────────────────────

        private void HandleKeyboardShortcuts()
        {
            // Tool switching
            if (Input.GetKeyDown(KeyCode.Alpha1)) CurrentTool = GridTool.AddFace;
            if (Input.GetKeyDown(KeyCode.Alpha2)) CurrentTool = GridTool.RemoveFace;
            if (Input.GetKeyDown(KeyCode.Alpha3)) CurrentTool = GridTool.PlaceEmitter;
            if (Input.GetKeyDown(KeyCode.Alpha4)) CurrentTool = GridTool.PlaceReflector;

            // Undo / Redo
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftCommand))
            {
                if (Input.GetKeyDown(KeyCode.Z))
                {
                    if (Input.GetKey(KeyCode.LeftShift))
                        _commandHistory.Redo(_gridData);
                    else
                        _commandHistory.Undo(_gridData);
                }
            }

            // Reflector direction rotation
            if (_currentTool == GridTool.PlaceReflector)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                    SetReflectorDirection(_currentReflectorDirection - 1);
                if (Input.GetKeyDown(KeyCode.E))
                    SetReflectorDirection(_currentReflectorDirection + 1);
            }

            // Play / Stop
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (_beatClock != null)
                {
                    if (_beatClock.IsPlaying)
                    {
                        _beatClock.StopClock();
                        _pulseSystem.DestroyAllPulses();
                    }
                    else
                    {
                        _beatClock.StartClock();
                    }
                }
            }
        }

        // ─────────────────────────────────────────────
        //  SAVE / LOAD
        // ─────────────────────────────────────────────

        /// <summary>
        /// Save the current grid state to a JSON string.
        /// </summary>
        public string SaveToJson()
        {
            var saveData = GridSaveData.FromGrid(_gridData);
            return JsonUtility.ToJson(saveData, true);
        }

        /// <summary>
        /// Load grid state from a JSON string. Destroys all current state.
        /// </summary>
        public void LoadFromJson(string json)
        {
            // Stop pulses first
            _pulseSystem?.DestroyAllPulses();

            var saveData = JsonUtility.FromJson<GridSaveData>(json);
            var newGrid = GridSaveData.ToGrid(saveData);

            // We need to swap the grid data reference.
            // Since visual manager is subscribed to the old grid, we need to reinitialize.
            _gridData.Clear();

            // Re-add all faces from save data
            foreach (var faceEntry in saveData.Faces)
            {
                _gridData.AddFace(faceEntry.ToFaceCoord());
            }

            foreach (var emitter in saveData.Emitters)
            {
                _gridData.PlaceEmitter(new VertexCoord(emitter.U, emitter.V), emitter.Config);
            }

            foreach (var reflector in saveData.Reflectors)
            {
                _gridData.PlaceReflector(new VertexCoord(reflector.U, reflector.V), reflector.Direction);
            }

            foreach (var note in saveData.Notes)
            {
                _gridData.AssignNote(new VertexCoord(note.U, note.V), note.NoteIndex);
            }

            _commandHistory.Clear();
        }
    }

    /// <summary>
    /// Available tools for grid interaction.
    /// </summary>
    public enum GridTool
    {
        AddFace,
        RemoveFace,
        PlaceEmitter,
        PlaceReflector
    }
}

