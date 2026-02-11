using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Centralized synthesizer engine with global voice pool.
/// Replaces per-object SynthPlayer architecture.
/// Single OnAudioFilterRead for maximum performance.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MasterSynth : MonoBehaviour
{
    public static MasterSynth Instance { get; private set; }
    
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
    
    // ============================================================
    // INTERNAL STATE
    // ============================================================
    
    private SynthVoice[] _voices;
    private Dictionary<string, SynthPreset> _presetLookup;
    private uint _timestamp = 0;
    
    // Sustained note tracking (for click-and-hold gameplay)
    private Dictionary<WorldObjectAudio, SynthVoice> _sustainedNotes;
    
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
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
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
        
        // Initialize sustained note tracking
        _sustainedNotes = new Dictionary<WorldObjectAudio, SynthVoice>();
        
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
    // PUBLIC API - TRIGGER SOUNDS
    // ============================================================
    
    /// <summary>
    /// Trigger a synth sound from a world position.
    /// This is the main entry point called by WorldObjectAudio components.
    /// </summary>
    public void TriggerSound(string patchName, Vector3 worldPosition, int midiNote, float velocity)
    {
        if (!_presetLookup.TryGetValue(patchName, out SynthPreset preset))
        {
            Debug.LogWarning($"[MasterSynth] Patch '{patchName}' not found in preset library");
            return;
        }
        
        // Calculate spatialization
        float gain, pan;
        CalculateSpatialization(worldPosition, out gain, out pan);
        
        // Find/steal a voice
        SynthVoice voice = StealVoice(preset.priority);
        
        if (voice != null)
        {
            // Configure voice with preset (only resets state if inactive)
            voice.Configure(preset.parameters);
            
            // Trigger note with spatial parameters
            voice.NoteOn(midiNote, velocity, gain, pan, _timestamp++);
        }
    }
    
    /// <summary>
    /// Trigger using preset's default MIDI note
    /// </summary>
    public void TriggerSound(string patchName, Vector3 worldPosition, float velocity)
    {
        if (_presetLookup.TryGetValue(patchName, out SynthPreset preset))
        {
            TriggerSound(patchName, worldPosition, preset.defaultMidiNote, velocity);
        }
    }
    
    /// <summary>
    /// Trigger a sustained sound that can be released later (for click-and-hold)
    /// Returns the voice so the caller can release it
    /// </summary>
    public void TriggerSustainedSound(string patchName, Vector3 worldPosition, int midiNote, float velocity, WorldObjectAudio sourceObject)
    {
        if (!_presetLookup.TryGetValue(patchName, out SynthPreset preset))
        {
            Debug.LogWarning($"[MasterSynth] Patch '{patchName}' not found in preset library");
            return;
        }
        
        // If this object already has a sustained note, release it first
        if (_sustainedNotes.ContainsKey(sourceObject))
        {
            ReleaseSustainedSound(sourceObject);
        }
        
        // Calculate spatialization
        float gain, pan;
        CalculateSpatialization(worldPosition, out gain, out pan);
        
        // Find/steal a voice
        SynthVoice voice = StealVoice(preset.priority);
        
        if (voice != null)
        {
            // Configure voice with preset
            voice.Configure(preset.parameters);
            
            // Trigger note with spatial parameters
            voice.NoteOn(midiNote, velocity, gain, pan, _timestamp++);
            
            // Track this sustained note
            _sustainedNotes[sourceObject] = voice;
        }
    }
    
    /// <summary>
    /// Release a sustained sound (send NoteOff to the voice)
    /// </summary>
    public void ReleaseSustainedSound(WorldObjectAudio sourceObject)
    {
        if (_sustainedNotes.TryGetValue(sourceObject, out SynthVoice voice))
        {
            voice.NoteOff();
            _sustainedNotes.Remove(sourceObject);
        }
    }
    
    /// <summary>
    /// Stop all active notes (panic button)
    /// </summary>
    public void AllNotesOff()
    {
        foreach (var voice in _voices)
        {
            voice.NoteOff();
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
    
    private SynthVoice StealVoice(int requestingPriority)
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
            return candidateRelease;
        
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
            return candidateOldest;
        
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
        
        return candidateOldest;
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
        return $"Voices: {GetActiveVoiceCount()}/{totalVoices} | Presets: {_presetLookup.Count}";
    }
}
