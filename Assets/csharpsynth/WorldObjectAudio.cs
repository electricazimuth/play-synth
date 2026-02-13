// WorldObjectAudio.cs - Refactored to use event system instead of singleton
using UnityEngine;
using Azimuth.DES;
using Azimuth.Audio;

/// <summary>
/// Lightweight audio trigger for game objects.
/// Event-driven architecture for decoupled audio triggering.
/// </summary>
public class WorldObjectAudio : MonoBehaviour
{
    [Header("Sound Configuration")]
    [Tooltip("Name of the SynthPreset to trigger (must match preset in MasterSynth library)")]
    public string patchName = "Default";
    
    [Tooltip("Base MIDI note to play (can be overridden by pitch mapping)")]
    public int baseMidiNote = 60;
    
    [Header("Duration Settings")]
    [Tooltip("Auto note-off duration (0 = use envelope, >0 = auto release after seconds)")]
    public float noteDuration = 0f;
    
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
    
    // Unique ID for this instance's sustained notes
    private string _sustainId;
    
    // Track the currently playing note for mouse sustain
    private int _currentlyPlayingNote = -1;
    private bool _isNoteHeld = false;
    
    // ============================================================
    // INITIALIZATION
    // ============================================================
    
    void Awake()
    {
        // Generate unique sustain ID for this instance
        _sustainId = $"{gameObject.GetInstanceID()}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
    }
    
    // ============================================================
    // COLLISION HANDLING
    // ============================================================
    
    void OnCollisionEnter(Collision collision)
    {
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
        
        // CREATE AND DISPATCH EVENT
        var triggerEvent = EventScheduler.New<SynthTriggerEvent>();
        triggerEvent.PatchName = patchName;
        triggerEvent.WorldPosition = contactPoint;
        triggerEvent.MidiNote = midiNote;
        triggerEvent.Velocity = normalizedVelocity;
        triggerEvent.Duration = noteDuration;
        EventScheduler.Dispatch(triggerEvent);
    }
    
    // ============================================================
    // MOUSE CLICK HANDLING
    // ============================================================
    
    void OnMouseDown()
    {
        if (!triggerOnClick)
            return;
        
        Debug.Log($"<color=green>MouseDown</color> on {name} Synth Preset {patchName}");
        
        if (sustainOnClick)
        {
            // Sustained mode - hold note until mouse release
            _currentlyPlayingNote = baseMidiNote;
            _isNoteHeld = true;
            
            var sustainEvent = EventScheduler.New<SynthSustainStartEvent>();
            sustainEvent.PatchName = patchName;
            sustainEvent.WorldPosition = transform.position;
            sustainEvent.MidiNote = baseMidiNote;
            sustainEvent.Velocity = clickVelocity;
            sustainEvent.SustainId = _sustainId;
            EventScheduler.Dispatch(sustainEvent);
        }
        else
        {
            // One-shot mode - fire and forget
            var triggerEvent = EventScheduler.New<SynthTriggerEvent>();
            triggerEvent.PatchName = patchName;
            triggerEvent.WorldPosition = transform.position;
            triggerEvent.MidiNote = baseMidiNote;
            triggerEvent.Velocity = clickVelocity;
            triggerEvent.Duration = noteDuration;
            EventScheduler.Dispatch(triggerEvent);
        }
    }
    
    void OnMouseUp()
    {
        if (!triggerOnClick)
            return;
        
        if (sustainOnClick && _isNoteHeld)
        {
            // Release the sustained note
            var releaseEvent = EventScheduler.New<SynthSustainReleaseEvent>();
            releaseEvent.SustainId = _sustainId;
            EventScheduler.Dispatch(releaseEvent);
            
            _isNoteHeld = false;
            _currentlyPlayingNote = -1;
        }
    }
    
    // Called when this object is destroyed while note is held
    void OnDestroy()
    {
        if (_isNoteHeld)
        {
            var releaseEvent = EventScheduler.New<SynthSustainReleaseEvent>();
            releaseEvent.SustainId = _sustainId;
            EventScheduler.Dispatch(releaseEvent);
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
        var triggerEvent = EventScheduler.New<SynthTriggerEvent>();
        triggerEvent.PatchName = patchName;
        triggerEvent.WorldPosition = transform.position;
        triggerEvent.MidiNote = baseMidiNote;
        triggerEvent.Velocity = velocity;
        triggerEvent.Duration = noteDuration;
        EventScheduler.Dispatch(triggerEvent);
    }
    
    /// <summary>
    /// Trigger with custom MIDI note
    /// </summary>
    public void TriggerSound(int midiNote, float velocity = 0.8f)
    {
        var triggerEvent = EventScheduler.New<SynthTriggerEvent>();
        triggerEvent.PatchName = patchName;
        triggerEvent.WorldPosition = transform.position;
        triggerEvent.MidiNote = midiNote;
        triggerEvent.Velocity = velocity;
        triggerEvent.Duration = noteDuration;
        EventScheduler.Dispatch(triggerEvent);
    }
    
    /// <summary>
    /// Trigger with full control over all parameters
    /// </summary>
    public void TriggerSound(int midiNote, float velocity, float duration)
    {
        var triggerEvent = EventScheduler.New<SynthTriggerEvent>();
        triggerEvent.PatchName = patchName;
        triggerEvent.WorldPosition = transform.position;
        triggerEvent.MidiNote = midiNote;
        triggerEvent.Velocity = velocity;
        triggerEvent.Duration = duration;
        EventScheduler.Dispatch(triggerEvent);
    }
    
    /// <summary>
    /// Trigger at a specific world position (useful for spawned objects)
    /// </summary>
    public void TriggerSoundAtPosition(Vector3 worldPosition, float velocity = 0.8f)
    {
        var triggerEvent = EventScheduler.New<SynthTriggerEvent>();
        triggerEvent.PatchName = patchName;
        triggerEvent.WorldPosition = worldPosition;
        triggerEvent.MidiNote = baseMidiNote;
        triggerEvent.Velocity = velocity;
        triggerEvent.Duration = noteDuration;
        EventScheduler.Dispatch(triggerEvent);
    }
}
