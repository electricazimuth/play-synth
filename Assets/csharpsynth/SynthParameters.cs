[System.Serializable]
public struct SynthParameters
{
    // Oscillators
    public float osc1Level;
    public float osc2Level;
    public float noiseLevel;
    public int osc2Semitones;
    public float osc2Detune;

    // Waveform Selection
    public WaveformType osc1Waveform;
    public WaveformType osc2Waveform;

    // Filter
    public float filterCutoff;
    public float filterResonance;
    public float filterEnvAmount;

    // Envelopes (Amp)
    public float ampAttack;
    public float ampDecay;
    public float ampSustain;
    public float ampRelease;

    // Envelopes (Filter)
    public float filterAttack;
    public float filterDecay;
    public float filterSustain;
    public float filterRelease;
    
    // Waveform enum (matches AnalogOscillator.Waveform)
    public enum WaveformType
    {
        Sine = 0,
        Saw = 1,
        Square = 2,
        Triangle = 3
    }
    
    // Check if values have changed compared to another struct
    public bool HasChanged(ref SynthParameters other)
    {
        // Simple byte-level comparison isn't possible with floats safely, 
        // but direct comparison is fast enough for ~15 parameters.
        return osc1Level != other.osc1Level ||
               osc2Level != other.osc2Level ||
               noiseLevel != other.noiseLevel ||
               osc2Semitones != other.osc2Semitones ||
               osc2Detune != other.osc2Detune ||
               osc1Waveform != other.osc1Waveform ||
               osc2Waveform != other.osc2Waveform ||
               filterCutoff != other.filterCutoff ||
               filterResonance != other.filterResonance ||
               filterEnvAmount != other.filterEnvAmount ||
               ampAttack != other.ampAttack ||
               ampDecay != other.ampDecay ||
               ampSustain != other.ampSustain ||
               ampRelease != other.ampRelease ||
               filterAttack != other.filterAttack ||
               filterDecay != other.filterDecay ||
               filterSustain != other.filterSustain ||
               filterRelease != other.filterRelease;
    }
}