using UnityEngine;

/// <summary>
/// ScriptableObject preset for synth patches.
/// Create assets in Project folder for "Kick", "Bass", "Pad", etc.
/// This decouples sound design from scene objects.
/// </summary>
[CreateAssetMenu(fileName = "NewSynthPreset", menuName = "Audio/Synth Preset", order = 1)]
public class SynthPreset : ScriptableObject
{
    [Header("Patch Identity")]
    public string patchName = "Untitled";
    
    [Header("Voice Stealing Priority")]
    [Range(0, 10)]
    [Tooltip("Higher priority = less likely to be stolen. Kick/Snare: 8-10, Bass: 5-7, Pads: 1-3")]
    public int priority = 5;
    
    [Header("Synth Parameters")]
    public SynthParameters parameters;
    
    // Optional: Default MIDI note for this preset
    [Header("Default Playback")]
    public int defaultMidiNote = 60; // Middle C
    
    private void OnValidate()
    {
        // Ensure sensible defaults on creation
        if (parameters.filterCutoff < 1f)
        {
            parameters.osc1Level = 1.0f;
            parameters.osc2Level = 0.5f;
            parameters.osc2Detune = 0.01f;
            
            // Default waveforms
            parameters.osc1Waveform = SynthParameters.WaveformType.Saw;
            parameters.osc2Waveform = SynthParameters.WaveformType.Saw;
            
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
    }
}
