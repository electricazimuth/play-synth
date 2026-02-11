using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SynthPlayer : MonoBehaviour
{
    private VoiceManager _voiceManager;
    private float _sampleRate;

    // ============================================================
    // INSPECTOR SETTINGS (Public fields for easy Unity UI)
    // ============================================================
    
    [Header("Synth Configuration")]
    [Range(1, 16)] public int polyphony = 8;
    
    [Header("Master Controls")]
    [Range(0f, 1f)] public float masterVolume = 0.7f;
    [Range(-2f, 2f)] public float pitchBend = 0f;
    
    // We wrap all engine parameters in our Serializable Struct
    // This allows them to show up in Inspector nicely grouped
    [Header("Voice Parameters")]
    public SynthParameters parameters; 

    // ============================================================
    // INTERNAL STATE
    // ============================================================
    
    // Thread-safe parameter copies for Audio Thread
    private float _threadSafeMasterVolume;
    private float _threadSafePitchBend;
    
    // Cache for dirty checking
    private SynthParameters _cachedParameters;

    void Awake()
    {
        _sampleRate = AudioSettings.outputSampleRate;
        _voiceManager = new VoiceManager(polyphony);
        
        // Initialize defaults if struct is empty (all zeros)
        if (parameters.filterCutoff < 1f){
            SetDefaults();
        }
        // Push initial parameters
        UpdateParametersImmediate();
    }

    void Start()
    {
        AudioSource audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.Play();
    }
    
    // ============================================================
    // UPDATE LOOP
    // ============================================================
    
    void Update()
    {
        HandleKeyboardInput();
        
        // Check if Master controls changed Thread-safe parameter copying
        _threadSafeMasterVolume = masterVolume;
        _threadSafePitchBend = pitchBend;

        // Check if Synth Engine parameters changed
        // We compare the struct currently in the Inspector vs our cached version
        if (parameters.HasChanged(ref _cachedParameters))
        {
            UpdateParametersImmediate();
        }
    }

    private void UpdateParametersImmediate()
    {
        // Copy Inspector values to Cache
        _cachedParameters = parameters;
        
        // Pass by reference to VoiceManager (Zero Allocation)
        _voiceManager.UpdateAllVoices(ref _cachedParameters);
    }
    
    // ============================================================
    // AUDIO THREAD
    // ============================================================
    
    void OnAudioFilterRead(float[] data, int channels)
    {
        // Update global parameters safely (Atomic float reads are safe)
        _voiceManager.SetMasterVolume(_threadSafeMasterVolume);
        _voiceManager.SetPitchBend(_threadSafePitchBend);
        
        if (channels == 1)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = _voiceManager.ProcessMono();
        }
        else if (channels == 2)
        {
            for (int i = 0; i < data.Length; i += 2)
            {
                float left, right;
                _voiceManager.ProcessStereo(out left, out right);
                data[i] = left;
                data[i + 1] = right;
            }
        }
    }

    // ============================================================
    // HELPER
    // ============================================================

    private void SetDefaults()
    {
        // Establish sensible defaults so you don't get silence on first run
        parameters.osc1Level = 1.0f;
        parameters.osc2Level = 0.5f;
        parameters.osc2Detune = 0.01f;
        
        parameters.filterCutoff = 2000f;
        parameters.filterResonance = 0.3f;
        parameters.filterEnvAmount = 4000f;
        
        parameters.ampAttack = 0.01f;
        parameters.ampDecay = 0.1f;
        parameters.ampSustain = 0.7f;
        parameters.ampRelease = 0.3f;
        
        parameters.filterAttack = 0.01f;
        parameters.filterDecay = 0.2f;
        parameters.filterSustain = 0.3f;
        parameters.filterRelease = 0.3f;
    }

    private void HandleKeyboardInput()
    {
        // Simple keyboard mapping for testing
        // A S D F G H J K = C D E F G A B C
        
        if (Input.GetKeyDown(KeyCode.A)) _voiceManager.NoteOn(60, 0.8f); // C
        if (Input.GetKeyUp(KeyCode.A)) _voiceManager.NoteOff(60);
        
        if (Input.GetKeyDown(KeyCode.S)) _voiceManager.NoteOn(62, 0.8f); // D
        if (Input.GetKeyUp(KeyCode.S)) _voiceManager.NoteOff(62);
        
        if (Input.GetKeyDown(KeyCode.D)) _voiceManager.NoteOn(64, 0.8f); // E
        if (Input.GetKeyUp(KeyCode.D)) _voiceManager.NoteOff(64);
        
        if (Input.GetKeyDown(KeyCode.F)) _voiceManager.NoteOn(65, 0.8f); // F
        if (Input.GetKeyUp(KeyCode.F)) _voiceManager.NoteOff(65);
        
        if (Input.GetKeyDown(KeyCode.G)) _voiceManager.NoteOn(67, 0.8f); // G
        if (Input.GetKeyUp(KeyCode.G)) _voiceManager.NoteOff(67);
        
        if (Input.GetKeyDown(KeyCode.H)) _voiceManager.NoteOn(69, 0.8f); // A
        if (Input.GetKeyUp(KeyCode.H)) _voiceManager.NoteOff(69);
        
        if (Input.GetKeyDown(KeyCode.J)) _voiceManager.NoteOn(71, 0.8f); // B
        if (Input.GetKeyUp(KeyCode.J)) _voiceManager.NoteOff(71);
        
        if (Input.GetKeyDown(KeyCode.K)) _voiceManager.NoteOn(72, 0.8f); // C
        if (Input.GetKeyUp(KeyCode.K)) _voiceManager.NoteOff(72);
        
        // Panic button
        if (Input.GetKeyDown(KeyCode.Space))
            _voiceManager.AllNotesOff();

    }

    // ============================================================
    // PUBLIC API (for MIDI input, etc.)
    // ============================================================
    
    public void PlayNote(int midiNote, float velocity)
    {
        _voiceManager.NoteOn(midiNote, velocity);
    }
    
    public void StopNote(int midiNote)
    {
        _voiceManager.NoteOff(midiNote);
    }
    
    public void StopAllNotes()
    {
        _voiceManager.AllNotesOff();
    }
}