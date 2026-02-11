using UnityEngine;

/// <summary>
/// Lightweight audio trigger for game objects.
/// Replaces per-object AudioSource/SynthPlayer approach.
/// Fire-and-forget sound triggering via centralized MasterSynth.
/// </summary>
public class WorldObjectAudio : MonoBehaviour
{
    [Header("Sound Configuration")]
    [Tooltip("Name of the SynthPreset to trigger (must match preset in MasterSynth library)")]
    public string patchName = "Default";
    
    [Tooltip("Base MIDI note to play (can be overridden by pitch mapping)")]
    public int baseMidiNote = 60;
    
    [Header("Collision Response")]
    [Tooltip("Minimum impact velocity to trigger sound")]
    public float velocityThreshold = 0.5f;
    
    [Tooltip("Maximum impact velocity for velocity scaling")]
    public float maxVelocity = 10f;
    
    [Tooltip("Map collision velocity to note pitch (semitones per m/s)")]
    public float velocityToPitch = 0f;
    
    [Header("Click Response")]
    [Tooltip("Trigger sound on mouse click")]
    public bool triggerOnClick = false;
    
    [Range(0f, 1f)]
    [Tooltip("Default velocity for click events")]
    public float clickVelocity = 0.8f;
    
    [Tooltip("Sustain note while mouse is held (requires NoteOff on release)")]
    public bool sustainOnClick = true;
    
    // ============================================================
    // INTERNAL STATE
    // ============================================================
    
    // Track the currently playing note for mouse sustain
    private int _currentlyPlayingNote = -1;
    private bool _isNoteHeld = false;
    
    // ============================================================
    // COLLISION HANDLING
    // ============================================================
    
    void OnCollisionEnter(Collision collision)
    {
        if (MasterSynth.Instance == null)
        {
            Debug.LogWarning("[WorldObjectAudio] MasterSynth not found in scene!");
            return;
        }
        
        // Calculate impact velocity magnitude
        float impactVelocity = collision.relativeVelocity.magnitude;
        
        // Threshold check
        if (impactVelocity < velocityThreshold)
            return;
        
        // Normalize velocity to 0-1 range
        float normalizedVelocity = Mathf.Clamp01(impactVelocity / maxVelocity);
        
        // Optional: Map velocity to pitch
        int midiNote = baseMidiNote;
        if (velocityToPitch != 0f)
        {
            int pitchOffset = Mathf.RoundToInt(impactVelocity * velocityToPitch);
            midiNote = Mathf.Clamp(baseMidiNote + pitchOffset, 0, 127);
        }
        
        // Trigger sound at collision point
        Vector3 contactPoint = collision.contacts.Length > 0 
            ? collision.contacts[0].point 
            : transform.position;
        
        MasterSynth.Instance.TriggerSound(patchName, contactPoint, midiNote, normalizedVelocity);
    }
    
    // ============================================================
    // MOUSE CLICK HANDLING
    // ============================================================
    
    void OnMouseDown()
    {
        Debug.Log($"<color=green>MouseDown</color> on {name} Synth Preset {patchName}");
        if (!triggerOnClick || MasterSynth.Instance == null)
            return;
        
        if (sustainOnClick)
        {
            // Sustained mode - hold note until mouse release
            _currentlyPlayingNote = baseMidiNote;
            _isNoteHeld = true;
            MasterSynth.Instance.TriggerSustainedSound(patchName, transform.position, baseMidiNote, clickVelocity, this);
        }else{
            // One-shot mode - fire and forget
            MasterSynth.Instance.TriggerSound(patchName, transform.position, baseMidiNote, clickVelocity);
        }
    }
    
    void OnMouseUp()
    {
        if (!triggerOnClick || MasterSynth.Instance == null)
            return;
        
        if (sustainOnClick && _isNoteHeld)
        {
            // Release the sustained note
            MasterSynth.Instance.ReleaseSustainedSound(this);
            _isNoteHeld = false;
            _currentlyPlayingNote = -1;
        }
    }
    
    // Called when this object is destroyed while note is held
    void OnDestroy()
    {
        if (_isNoteHeld && MasterSynth.Instance != null)
        {
            MasterSynth.Instance.ReleaseSustainedSound(this);
        }
    }
    
    // ============================================================
    // PUBLIC API (for manual triggering)
    // ============================================================
    
    /// <summary>
    /// Manually trigger this object's sound
    /// </summary>
    public void TriggerSound(float velocity = 0.8f)
    {
        if (MasterSynth.Instance != null)
        {
            MasterSynth.Instance.TriggerSound(patchName, transform.position, baseMidiNote, velocity);
        }
    }
    
    /// <summary>
    /// Trigger with custom MIDI note
    /// </summary>
    public void TriggerSound(int midiNote, float velocity = 0.8f)
    {
        if (MasterSynth.Instance != null)
        {
            MasterSynth.Instance.TriggerSound(patchName, transform.position, midiNote, velocity);
        }
    }
}
