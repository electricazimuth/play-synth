// MasterSynth.cs - Refactored to use event system instead of singleton
using UnityEngine;
using System.Collections.Generic;
using Azimuth.DES;
using Azimuth.Audio;

/// <summary>
/// Centralized synthesizer engine with global voice pool.
/// Event-driven architecture for decoupled audio triggering.
/// Single OnAudioFilterRead for maximum performance.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MasterSynth : EventHandlerBehaviour, 
    IEventHandler<SynthTriggerEvent>,
    IEventHandler<SynthSustainStartEvent>,
    IEventHandler<SynthSustainReleaseEvent>,
    IEventHandler<SynthAllNotesOffEvent>,
    IEventHandler<SynthAutoNoteOffEvent>
{
    // ============================================================
    // EVENT HANDLER PRIORITIES
    // ============================================================
    
    // Audio events should be processed immediately with highest priority
    int IEventHandler<SynthTriggerEvent>.Priority => 0;
    int IEventHandler<SynthSustainStartEvent>.Priority => 0;
    int IEventHandler<SynthSustainReleaseEvent>.Priority => 0;
    int IEventHandler<SynthAllNotesOffEvent>.Priority => 0;
    int IEventHandler<SynthAutoNoteOffEvent>.Priority => 0;
    
    // ============================================================
    // INSPECTOR SETTINGS
    // ============================================================
    
    [Header("Voice Pool Configuration")]
    [Range(16, 128)]
    [Tooltip("Total number of voices available globally")]
    public int totalVoices = 64;
    
    [Header("Preset Library")]
    [Tooltip("Drag SynthPreset assets here to register them")]
    public SynthPreset[] presetLibrary;
    
    [Header("Spatialization Settings")]
    [Range(0f, 2f)]
    [Tooltip("Distance attenuation factor. Higher = faster falloff")]
    public float rolloffFactor = 0.1f;
    
    [Range(0f, 1f)]
    [Tooltip("Stereo panning strength based on X position")]
    public float panStrength = 0.5f;
    
    [Header("Master Controls")]
    [Range(0f, 1f)]
    public float masterVolume = 0.7f;
    
    [Range(0f, 1f)]
    [Tooltip("Extra headroom before soft clipping (lower = safer)")]
    public float headroom = 0.7f;
    
    [Header("Debug")]
    [Tooltip("Dispatch diagnostic events when voice stealing occurs")]
    public bool reportVoiceStealing = false;
    
    // ============================================================
    // INTERNAL STATE
    // ============================================================
    
    private SynthVoice[] _voices;
    private Dictionary<string, SynthPreset> _presetLookup;
    private uint _timestamp = 0;
    
    // Sustained note tracking (by sustain ID, not object reference)
    private Dictionary<string, SynthVoice> _sustainedNotes;
    
    // Auto note-off tracking (for timed notes)
    private Dictionary<string, SynthVoice> _timedNotes;
    private uint _timedNoteCounter = 0;
    
    // Active voice caching for performance
    private SynthVoice[] _activeVoices;
    private int _activeVoiceCount = 0;
    private int _sampleCounter = 0;
    private const int ACTIVE_VOICE_REBUILD_INTERVAL = 1024; // Samples
    
    private float _sampleRate;
    private Transform _listenerTransform;
    
    // Thread-safe parameter copies
    private float _threadSafeMasterVolume;
    private float _threadSafeHeadroom;
    
    // ============================================================
    // INITIALIZATION
    // ============================================================
    
    void Awake()
    {
        _sampleRate = AudioSettings.outputSampleRate;
        
        // Initialize voice pool
        _voices = new SynthVoice[totalVoices];
        _activeVoices = new SynthVoice[totalVoices];
        
        for (int i = 0; i < totalVoices; i++)
        {
            _voices[i] = new SynthVoice(_sampleRate);
        }
        
        // Build preset lookup dictionary
        _presetLookup = new Dictionary<string, SynthPreset>();
        foreach (var preset in presetLibrary)
        {
            if (preset != null)
            {
                _presetLookup[preset.patchName] = preset;
            }
        }
        
        // Initialize note tracking
        _sustainedNotes = new Dictionary<string, SynthVoice>();
        _timedNotes = new Dictionary<string, SynthVoice>();
        
        Debug.Log($"MasterSynth initialized: {totalVoices} voices, {_presetLookup.Count} presets loaded");
    }
    
    void Start()
    {
        // Find audio listener
        AudioListener listener = FindObjectOfType<AudioListener>();
        if (listener != null)
        {
            _listenerTransform = listener.transform;
        }
        else
        {
            _listenerTransform = Camera.main?.transform;
            Debug.LogWarning("No AudioListener found, using Main Camera for spatialization");
        }
        
        // Setup AudioSource
        AudioSource audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f; // Force 2D (we handle spatialization manually)
        audioSource.Play();
    }
    
    void Update()
    {
        // Copy parameters for audio thread (atomic float reads are safe)
        _threadSafeMasterVolume = masterVolume;
        _threadSafeHeadroom = headroom;
    }
    
    // ============================================================
    // EVENT HANDLERS
    // ============================================================
    
    /// <summary>
    /// Handle one-shot synth trigger with optional auto note-off.
    /// </summary>
    public void OnEvent(SynthTriggerEvent ev)
    {
        if (!_presetLookup.TryGetValue(ev.PatchName, out SynthPreset preset))
        {
            Debug.LogWarning($"[MasterSynth] Patch '{ev.PatchName}' not found in preset library");
            return;
        }
        
        // Calculate spatialization
        float gain, pan;
        CalculateSpatialization(ev.WorldPosition, out gain, out pan);
        
        // Find/steal a voice
        SynthVoice voice = StealVoice(preset.priority, ev.PatchName);
        
        if (voice != null)
        {
            // Configure voice with preset
            voice.Configure(preset.parameters);
            voice.SetPriority(preset.priority);
            
            // Trigger note with spatial parameters
            voice.NoteOn(ev.MidiNote, ev.Velocity, gain, pan, _timestamp++);
            
            // Schedule auto note-off if duration is specified
            if (ev.Duration > 0f)
            {
                string voiceId = $"timed_{_timedNoteCounter++}";
                _timedNotes[voiceId] = voice;
                
                // Schedule note-off event
                var noteOffEvent = EventScheduler.New<SynthAutoNoteOffEvent>();
                noteOffEvent.VoiceId = voiceId;
                EventScheduler.Schedule(noteOffEvent, ev.Duration);
            }
        }
    }
    
    /// <summary>
    /// Handle sustained note start (for click-and-hold scenarios).
    /// </summary>
    public void OnEvent(SynthSustainStartEvent ev)
    {
        if (!_presetLookup.TryGetValue(ev.PatchName, out SynthPreset preset))
        {
            Debug.LogWarning($"[MasterSynth] Patch '{ev.PatchName}' not found in preset library");
            return;
        }
        
        // If this sustain ID already has a note, release it first
        if (_sustainedNotes.ContainsKey(ev.SustainId))
        {
            _sustainedNotes[ev.SustainId].NoteOff();
            _sustainedNotes.Remove(ev.SustainId);
        }
        
        // Calculate spatialization
        float gain, pan;
        CalculateSpatialization(ev.WorldPosition, out gain, out pan);
        
        // Find/steal a voice
        SynthVoice voice = StealVoice(preset.priority, ev.PatchName);
        
        if (voice != null)
        {
            // Configure voice with preset
            voice.Configure(preset.parameters);
            voice.SetPriority(preset.priority);
            
            // Trigger note with spatial parameters
            voice.NoteOn(ev.MidiNote, ev.Velocity, gain, pan, _timestamp++);
            
            // Track this sustained note
            _sustainedNotes[ev.SustainId] = voice;
        }
    }
    
    /// <summary>
    /// Handle sustained note release.
    /// </summary>
    public void OnEvent(SynthSustainReleaseEvent ev)
    {
        if (_sustainedNotes.TryGetValue(ev.SustainId, out SynthVoice voice))
        {
            voice.NoteOff();
            _sustainedNotes.Remove(ev.SustainId);
        }
    }
    
    /// <summary>
    /// Handle all notes off (panic button).
    /// </summary>
    public void OnEvent(SynthAllNotesOffEvent ev)
    {
        foreach (var voice in _voices)
        {
            voice.NoteOff();
        }
        
        _sustainedNotes.Clear();
        _timedNotes.Clear();
    }
    
    /// <summary>
    /// Handle auto note-off for timed notes (internal event).
    /// </summary>
    public void OnEvent(SynthAutoNoteOffEvent ev)
    {
        if (_timedNotes.TryGetValue(ev.VoiceId, out SynthVoice voice))
        {
            voice.NoteOff();
            _timedNotes.Remove(ev.VoiceId);
        }
    }
    
    // ============================================================
    // SPATIALIZATION
    // ============================================================
    
    private void CalculateSpatialization(Vector3 worldPosition, out float gain, out float pan)
    {
        if (_listenerTransform == null)
        {
            gain = 1f;
            pan = 0.5f;
            return;
        }
        
        // Transform to listener-local space
        Vector3 localPos = _listenerTransform.InverseTransformPoint(worldPosition);
        
        // Distance-based attenuation (inverse square with offset)
        float distance = localPos.magnitude;
        gain = 1.0f / (1.0f + distance * distance * rolloffFactor);
        
        // Stereo panning based on X-axis position
        // localPos.x: negative = left, positive = right
        pan = Mathf.Clamp01(0.5f + localPos.x * panStrength);
    }
    
    // ============================================================
    // VOICE STEALING
    // ============================================================
    
    private SynthVoice StealVoice(int requestingPriority, string requestingPatchName)
    {
        // Strategy 1: Find inactive voice
        foreach (var voice in _voices)
        {
            if (!voice.IsActive)
            {
                return voice;
            }
        }
        
        // Strategy 2: Steal releasing voice from LOWER priority patch
        SynthVoice candidateRelease = null;
        int lowestReleasePriority = int.MaxValue;
        float lowestReleaseLevel = float.MaxValue;
        
        foreach (var voice in _voices)
        {
            if (voice.IsInRelease && voice.CurrentPriority <= requestingPriority)
            {
                // Prefer lower priority, then quieter voices
                if (voice.CurrentPriority < lowestReleasePriority ||
                    (voice.CurrentPriority == lowestReleasePriority && voice.GetCurrentLevel() < lowestReleaseLevel))
                {
                    lowestReleasePriority = voice.CurrentPriority;
                    lowestReleaseLevel = voice.GetCurrentLevel();
                    candidateRelease = voice;
                }
            }
        }
        
        if (candidateRelease != null)
        {
            ReportVoiceSteal(candidateRelease, requestingPriority, requestingPatchName, true);
            return candidateRelease;
        }
        
        // Strategy 3: Steal oldest voice from same or lower priority
        SynthVoice candidateOldest = null;
        uint oldestTime = uint.MaxValue;
        
        foreach (var voice in _voices)
        {
            if (voice.CurrentPriority <= requestingPriority && voice.GetNoteOnTime() < oldestTime)
            {
                oldestTime = voice.GetNoteOnTime();
                candidateOldest = voice;
            }
        }
        
        if (candidateOldest != null)
        {
            ReportVoiceSteal(candidateOldest, requestingPriority, requestingPatchName, false);
            return candidateOldest;
        }
        
        // Strategy 4: Last resort - steal ANY oldest voice
        oldestTime = uint.MaxValue;
        foreach (var voice in _voices)
        {
            if (voice.GetNoteOnTime() < oldestTime)
            {
                oldestTime = voice.GetNoteOnTime();
                candidateOldest = voice;
            }
        }
        
        if (candidateOldest != null)
        {
            ReportVoiceSteal(candidateOldest, requestingPriority, requestingPatchName, false);
        }
        
        return candidateOldest;
    }
    
    private void ReportVoiceSteal(SynthVoice stolenVoice, int requestingPriority, string requestingPatchName, bool wasInRelease)
    {
        if (!reportVoiceStealing)
            return;
        
        var stealEvent = EventScheduler.New<SynthVoiceStealEvent>();
        stealEvent.StolenVoicePriority = stolenVoice.CurrentPriority;
        stealEvent.RequestingPriority = requestingPriority;
        stealEvent.StolenPatchName = "Unknown"; // Could track this if needed
        stealEvent.RequestingPatchName = requestingPatchName;
        stealEvent.WasInRelease = wasInRelease;
        EventScheduler.Dispatch(stealEvent);
    }
    
    // ============================================================
    // AUDIO THREAD - SINGLE CENTRALIZED DSP LOOP
    // ============================================================
    
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (channels == 2)
        {
            ProcessStereo(data);
        }
        else if (channels == 1)
        {
            ProcessMono(data);
        }
    }
    
    private void ProcessStereo(float[] data)
    {
        int bufferLength = data.Length / 2;
        
        // Rebuild active voice cache periodically
        if (_sampleCounter >= ACTIVE_VOICE_REBUILD_INTERVAL)
        {
            _activeVoiceCount = 0;
            foreach (var voice in _voices)
            {
                if (voice.IsActive)
                {
                    _activeVoices[_activeVoiceCount++] = voice;
                }
            }
            _sampleCounter = 0;
        }
        
        // Energy-based scaling: sqrt(N) for uncorrelated signals
        float energyScale = _threadSafeMasterVolume / Mathf.Sqrt(totalVoices);
        float clipScale = _threadSafeHeadroom;
        
        for (int i = 0; i < data.Length; i += 2)
        {
            float sumL = 0f;
            float sumR = 0f;
            
            // Only process active voices (major optimization)
            for (int v = 0; v < _activeVoiceCount; v++)
            {
                float L, R;
                _activeVoices[v].ProcessStereo(out L, out R);
                sumL += L;
                sumR += R;
            }
            
            // Apply master scaling
            sumL *= energyScale;
            sumR *= energyScale;
            
            // Soft clip with headroom
            data[i] = FastTanh(sumL * clipScale);
            data[i + 1] = FastTanh(sumR * clipScale);
            
            _sampleCounter++;
        }
    }
    
    private void ProcessMono(float[] data)
    {
        // Rebuild active voice cache periodically
        if (_sampleCounter >= ACTIVE_VOICE_REBUILD_INTERVAL)
        {
            _activeVoiceCount = 0;
            foreach (var voice in _voices)
            {
                if (voice.IsActive)
                {
                    _activeVoices[_activeVoiceCount++] = voice;
                }
            }
            _sampleCounter = 0;
        }
        
        float energyScale = _threadSafeMasterVolume / Mathf.Sqrt(totalVoices);
        float clipScale = _threadSafeHeadroom;
        
        for (int i = 0; i < data.Length; i++)
        {
            float sum = 0f;
            
            for (int v = 0; v < _activeVoiceCount; v++)
            {
                sum += _activeVoices[v].Process();
            }
            
            sum *= energyScale;
            data[i] = FastTanh(sum * clipScale);
            
            _sampleCounter++;
        }
    }
    
    // Fast tanh approximation for soft clipping
    private float FastTanh(float x)
    {
        if (x < -3f) return -1f;
        if (x > 3f) return 1f;
        
        float x2 = x * x;
        return x * (27f + x2) / (27f + 9f * x2);
    }
    
    // ============================================================
    // DIAGNOSTICS
    // ============================================================
    
    public int GetActiveVoiceCount()
    {
        int count = 0;
        foreach (var voice in _voices)
        {
            if (voice.IsActive) count++;
        }
        return count;
    }
    
    public string GetDiagnostics()
    {
        return $"Voices: {GetActiveVoiceCount()}/{totalVoices} | Sustained: {_sustainedNotes.Count} | Timed: {_timedNotes.Count} | Presets: {_presetLookup.Count}";
    }
}
