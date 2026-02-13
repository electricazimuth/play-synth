// BeatClock.cs
using System;
using UnityEngine;

namespace TriGrid.Unity
{
    /// <summary>
    /// Global beat clock that drives emitter timing and pulse quantization.
    /// </summary>
    public class BeatClock : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float _bpm = 120f;
        [SerializeField] private int _beatsPerBar = 4;
        [SerializeField] private int _subdivisionsPerBeat = 4; // 16th notes

        private double _startDspTime;
        private double _lastBeatTime;
        private double _lastSubdivisionTime;
        private int _currentBeat;
        private int _currentSubdivision;
        private int _currentBar;
        private bool _isPlaying;

        // ─── Events ───
        public event Action<int> OnBeat;                  // beat index within bar
        public event Action<int, int> OnSubdivision;      // (beat, subdivision)
        public event Action<int> OnBar;                   // bar number
        public event Action OnStarted;
        public event Action OnStopped;

        // ─── Properties ───
        public float BPM
        {
            get => _bpm;
            set => _bpm = Mathf.Max(1f, value);
        }

        public bool IsPlaying => _isPlaying;
        public int CurrentBeat => _currentBeat;
        public int CurrentBar => _currentBar;
        public double CurrentBeatTime => AudioSettings.dspTime - _startDspTime;

        /// <summary>
        /// Seconds per beat at current BPM.
        /// </summary>
        public double SecondsPerBeat => 60.0 / _bpm;

        /// <summary>
        /// Seconds per subdivision at current BPM.
        /// </summary>
        public double SecondsPerSubdivision => 60.0 / (_bpm * _subdivisionsPerBeat);

        /// <summary>
        /// Get the edge travel duration based on BPM and desired beats per edge.
        /// </summary>
        public float GetEdgeTravelDuration(float beatsPerEdge = 0.5f)
        {
            return (float)(SecondsPerBeat * beatsPerEdge);
        }

        /// <summary>
        /// Snap a timestamp to the nearest beat boundary.
        /// </summary>
        public double SnapToBeat(double time)
        {
            double spb = SecondsPerBeat;
            return Math.Round(time / spb) * spb;
        }

        /// <summary>
        /// Snap a timestamp to the nearest subdivision boundary.
        /// </summary>
        public double SnapToSubdivision(double time)
        {
            double sps = SecondsPerSubdivision;
            return Math.Round(time / sps) * sps;
        }

        public void StartClock()
        {
            _startDspTime = AudioSettings.dspTime;
            _lastBeatTime = _startDspTime;
            _lastSubdivisionTime = _startDspTime;
            _currentBeat = 0;
            _currentSubdivision = 0;
            _currentBar = 0;
            _isPlaying = true;
            OnStarted?.Invoke();
        }

        public void StopClock()
        {
            _isPlaying = false;
            OnStopped?.Invoke();
        }

        public void ResetClock()
        {
            _currentBeat = 0;
            _currentSubdivision = 0;
            _currentBar = 0;
            _startDspTime = AudioSettings.dspTime;
            _lastBeatTime = _startDspTime;
            _lastSubdivisionTime = _startDspTime;
        }

        private void Update()
        {
            if (!_isPlaying) return;

            double currentTime = AudioSettings.dspTime;
            double spb = SecondsPerBeat;
            double sps = SecondsPerSubdivision;

            // Check subdivisions
            while (currentTime >= _lastSubdivisionTime + sps)
            {
                _lastSubdivisionTime += sps;
                _currentSubdivision++;

                if (_currentSubdivision >= _subdivisionsPerBeat)
                {
                    _currentSubdivision = 0;
                }

                OnSubdivision?.Invoke(_currentBeat, _currentSubdivision);
            }

            // Check beats
            while (currentTime >= _lastBeatTime + spb)
            {
                _lastBeatTime += spb;
                _currentBeat++;

                if (_currentBeat >= _beatsPerBar)
                {
                    _currentBeat = 0;
                    _currentBar++;
                    OnBar?.Invoke(_currentBar);
                }

                OnBeat?.Invoke(_currentBeat);
            }
        }
    }
}
