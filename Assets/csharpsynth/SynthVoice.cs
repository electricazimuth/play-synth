
using UnityEngine;
using System;

/// <summary>
/// Production-ready synthesizer voice with anti-aliased oscillators,
/// exponential envelopes, modulation matrix, and proper gain staging.
/// </summary>
public class SynthVoice
{
    // ============================================================
    // OSCILLATORS
    // ============================================================
    
    private AnalogOscillator _osc1;
    private AnalogOscillator _osc2;
    private NoiseGenerator _noise;
    
    // Oscillator Mix Levels
    public float osc1Level = 1.0f;
    public float osc2Level = 0.5f;
    public float noiseLevel = 0.0f;
    
    // Oscillator Tuning
    public float osc2Detune = 0.01f; // Slight detune for thickness
    public int osc2Semitones = 0;    // Octave/fifth intervals
    
    // ============================================================
    // FILTER
    // ============================================================
    
    private StateVariableFilter _filter;
    private SmoothedParameter _filterCutoffParam;
    private SmoothedParameter _filterResonanceParam;
    
    // Base filter settings
    public float filterCutoff = 2000f;
    public float filterResonance = 0.3f;
    
    // ============================================================
    // ENVELOPES
    // ============================================================
    
    private ExponentialADSR _ampEnvelope;
    private ExponentialADSR _filterEnvelope;
    
    // ============================================================
    // MODULATION
    // ============================================================
    
    private ModulationMatrix _modMatrix;
    private LFO _lfo1;
    private LFO _lfo2;
    
    // Modulation amounts
    public float filterEnvAmount = 4000f;  // Hz
    public float lfo1ToPitch = 0.0f;       // Semitones
    public float lfo1ToFilter = 0.0f;      // Hz
    public float velocityToFilter = 2000f; // Hz
    
    // ============================================================
    // VOICE STATE
    // ============================================================
    
    private int _noteNumber = -1;
    private float _velocity = 1.0f;
    private float _baseFrequency = 440f;
    private bool _isActive = false;

    // ============================================================
    // CONTROL RATE PROCESSING
    // ============================================================
    private const int CONTROL_RATE_INTERVAL = 32;
    private int _controlCounter = 0;
    // Current and Target values for interpolation
    private float _currentCutoff, _targetCutoff;
    private float _cutoffIncrement;


    
    // Stereo
    public float pan = 0.5f; // 0=Left, 0.5=Center, 1=Right
    
    // Voice management
    private float _currentLevel = 0f; // For voice stealing
    private uint _noteOnTime = 0;     // For age-based stealing
    
    // Sample rate
    private double _sampleRate;
    
    // Global pitch bend (set by VoiceManager)
    private float _pitchBendSemitones = 0f;
    
    // ============================================================
    // INITIALIZATION
    // ============================================================
    
    public SynthVoice(double sampleRate)
    {
        _sampleRate = sampleRate;
        
        // Initialize oscillators
        _osc1 = new AnalogOscillator();
        _osc2 = new AnalogOscillator();
        _noise = new NoiseGenerator();
        
        // Initialize filter with smoothing
        _filter = new StateVariableFilter();
        _filterCutoffParam = new SmoothedParameter(filterCutoff, 10f, (float)sampleRate);
        _filterResonanceParam = new SmoothedParameter(filterResonance, 10f, (float)sampleRate);
        
        // Initialize envelopes
        _ampEnvelope = new ExponentialADSR();
        _ampEnvelope.SetSampleRate((float)sampleRate);
        _ampEnvelope.SetAttackTime(0.01f);
        _ampEnvelope.SetDecayTime(0.1f);
        _ampEnvelope.sustainLevel = 0.7f;
        _ampEnvelope.SetReleaseTime(0.3f);
        
        _filterEnvelope = new ExponentialADSR();
        _filterEnvelope.SetSampleRate((float)sampleRate);
        _filterEnvelope.SetAttackTime(0.005f);
        _filterEnvelope.SetDecayTime(0.2f);
        _filterEnvelope.sustainLevel = 0.3f;
        _filterEnvelope.SetReleaseTime(0.3f);
        
        // Initialize LFOs
        _lfo1 = new LFO();
        _lfo1.SetFrequency(5f, (float)sampleRate); // 5 Hz
        _lfo1.waveform = LFO.Waveform.Sine;
        
        _lfo2 = new LFO();
        _lfo2.SetFrequency(0.5f, (float)sampleRate); // 0.5 Hz
        _lfo2.waveform = LFO.Waveform.Triangle;
        
        // Initialize modulation matrix
        _modMatrix = new ModulationMatrix();
        SetupDefaultModulation();
    }

    public void UpdateParameters(ref SynthParameters p)
    {
        this.osc1Level = p.osc1Level;
        this.osc2Level = p.osc2Level;
        this.noiseLevel = p.noiseLevel;
        this.osc2Semitones = p.osc2Semitones;
        this.osc2Detune = p.osc2Detune;
        
        // Using the setters we defined earlier to clamp values
        this.SetFilterCutoff(p.filterCutoff);
        this.SetFilterResonance(p.filterResonance);
        this.filterEnvAmount = p.filterEnvAmount;

        // Map Envelope settings 
        // (Assuming you exposed setters in ExponentialADSR as discussed previously)
        _ampEnvelope.SetAttackTime(p.ampAttack);
        _ampEnvelope.SetDecayTime(p.ampDecay);
        _ampEnvelope.sustainLevel = p.ampSustain;
        _ampEnvelope.SetReleaseTime(p.ampRelease);
        
        _filterEnvelope.SetAttackTime(p.filterAttack);
        _filterEnvelope.SetDecayTime(p.filterDecay);
        _filterEnvelope.sustainLevel = p.filterSustain;
        _filterEnvelope.SetReleaseTime(p.filterRelease);
    }
    
    private void SetupDefaultModulation()
    {
        // Default routing: Filter envelope to cutoff
        _modMatrix.AddRoute(
            ModulationMatrix.Source.FilterEnv,
            ModulationMatrix.Destination.FilterCutoff,
            1.0f // Will be scaled by filterEnvAmount
        );
        
        // Velocity to filter cutoff
        _modMatrix.AddRoute(
            ModulationMatrix.Source.Velocity,
            ModulationMatrix.Destination.FilterCutoff,
            1.0f // Will be scaled by velocityToFilter
        );
    }
    
    // ============================================================
    // NOTE CONTROL
    // ============================================================
    
    public void NoteOn(int midiNote, float velocity, uint timestamp)
    {
        _noteNumber = midiNote;
        _velocity = Mathf.Clamp01(velocity);
        _noteOnTime = timestamp;
        
        // Calculate base frequency
        _baseFrequency = MidiNoteToFrequency(midiNote);
        
        // Update oscillator frequencies
        UpdateOscillatorFrequencies();
        
        // Reset oscillator phases for consistent attack
        _osc1.Reset();
        _osc2.Reset();
        
        // Trigger envelopes
        _ampEnvelope.NoteOn();
        _filterEnvelope.NoteOn();
        
        // Set modulation sources
        _modMatrix.SetSource(ModulationMatrix.Source.Velocity, _velocity);
        
        // Reset LFOs (optional - comment out for free-running LFOs)
        // _lfo1.Reset();
        // _lfo2.Reset();
        
        _isActive = true;
    }
    
    public void NoteOff()
    {
        _ampEnvelope.NoteOff();
        _filterEnvelope.NoteOff();
    }
    
    private void UpdateOscillatorFrequencies()
    {
        // Calculate pitch with bend
        float totalPitchOffset = _pitchBendSemitones;
        
        // Add LFO modulation
        float lfoModulation = _modMatrix.GetDestination(ModulationMatrix.Destination.Pitch);
        totalPitchOffset += lfoModulation;
        
        float pitchMultiplier = SemitonesToRatio(totalPitchOffset);
        
        // Osc 1
        _osc1.SetFrequency(_baseFrequency * pitchMultiplier, (float)_sampleRate);
        
        // Osc 2 with detune and semitone offset
        float osc2Offset = osc2Semitones + (osc2Detune * 100f); // Detune in cents
        float osc2Multiplier = SemitonesToRatio(osc2Offset / 100f);
        _osc2.SetFrequency(_baseFrequency * pitchMultiplier * osc2Multiplier, (float)_sampleRate);
    }
    
    // ============================================================
    // AUDIO PROCESSING
    // ============================================================
    public float Process()
    {
        if (!_isActive)
            return 0f;

        // =========================================================
        // 1. AUDIO RATE MODULATION GENERATION
        // (We run these every sample to keep phase/timing accurate)
        // =========================================================
        
        float lfo1Value = _lfo1.Process();
        float lfo2Value = _lfo2.Process();
        float filterEnvValue = _filterEnvelope.Process();
        float ampEnvValue = _ampEnvelope.Process();

        // Update Matrix Sources (Lightweight array assignments)
        _modMatrix.SetSource(ModulationMatrix.Source.LFO1, lfo1Value);
        _modMatrix.SetSource(ModulationMatrix.Source.LFO2, lfo2Value);
        _modMatrix.SetSource(ModulationMatrix.Source.FilterEnv, filterEnvValue);
        _modMatrix.SetSource(ModulationMatrix.Source.AmpEnv, ampEnvValue);

        // =========================================================
        // 2. CONTROL RATE LOGIC (Expensive Math)
        // =========================================================
        
        // Only run the heavy routing and frequency math every CONTROL_RATE_INTERVAL samples
        if (_controlCounter <= 0)
        {
            // A. Process Routing Logic
            _modMatrix.Process();

            // B. Calculate Target Pitch (Heavy Math.Pow inside here)
            // This updates the target frequency for the oscillators
            UpdateOscillatorFrequencies(); 

            // C. Calculate Target Cutoff
            float filterCutoffMod = _modMatrix.GetDestination(ModulationMatrix.Destination.FilterCutoff);
            
            float modulatedCutoff = filterCutoff + 
                                    (filterEnvValue * filterEnvAmount) +
                                    (filterCutoffMod * lfo1ToFilter) +
                                    (_velocity * velocityToFilter);

            // Clamp
            modulatedCutoff = Mathf.Clamp(modulatedCutoff, 20f, (float)_sampleRate * 0.45f);

            // D. Update Filter Smoother Targets
            // The smoother will interpolate to this new value over the next 16 samples
            _filterCutoffParam.SetTarget(modulatedCutoff);
            _filterResonanceParam.SetTarget(filterResonance);

            // Reset Counter
            _controlCounter = CONTROL_RATE_INTERVAL;
        }
        
        _controlCounter--;

        // =========================================================
        // 3. AUDIO RATE SIGNAL PROCESSING
        // =========================================================

        // Generate samples using the updated frequencies
        // Note: Osc frequencies were updated in step 2B
        float osc1Sample = _osc1.Process() * osc1Level;
        float osc2Sample = _osc2.Process() * osc2Level;
        float noiseSample = _noise.Process() * noiseLevel;

        float oscMix = osc1Sample + osc2Sample + noiseSample;

        // Process Filter
        // GetNextValue() performs the linear interpolation from the control rate steps
        float filtered = _filter.Process(
            oscMix,
            _filterCutoffParam.GetNextValue(),
            _filterResonanceParam.GetNextValue(), 
            (float)_sampleRate
        );

        // Apply Amp Envelope & Velocity
        float output = filtered * ampEnvValue * _velocity;

        // =========================================================
        // 4. HOUSEKEEPING
        // =========================================================
        
        _currentLevel = Mathf.Abs(output);

        if (!_ampEnvelope.IsActive)
        {
            _isActive = false;
            _noteNumber = -1;
            _currentLevel = 0f;
        }

        return output;
    }
    /*
    public float OLDSLOWProcess()
    {
        if (!_isActive)
            return 0f;
        
        // Process LFOs
        float lfo1Value = _lfo1.Process();
        float lfo2Value = _lfo2.Process();
        
        // Update modulation matrix sources
        _modMatrix.SetSource(ModulationMatrix.Source.LFO1, lfo1Value);
        _modMatrix.SetSource(ModulationMatrix.Source.LFO2, lfo2Value);
        
        float filterEnvValue = _filterEnvelope.Process();
        _modMatrix.SetSource(ModulationMatrix.Source.FilterEnv, filterEnvValue);
        
        float ampEnvValue = _ampEnvelope.Process();
        _modMatrix.SetSource(ModulationMatrix.Source.AmpEnv, ampEnvValue);
        
        // Process modulation matrix
        _modMatrix.Process();
        
        // Update oscillator frequencies (for pitch modulation)
        UpdateOscillatorFrequencies();
        
        // Generate oscillator samples
        float osc1Sample = _osc1.Process() * osc1Level;
        float osc2Sample = _osc2.Process() * osc2Level;
        float noiseSample = _noise.Process() * noiseLevel;
        
        // Mix oscillators
        float oscMix = osc1Sample + osc2Sample + noiseSample;
        
        // Calculate modulated filter parameters
        float filterCutoffMod = _modMatrix.GetDestination(ModulationMatrix.Destination.FilterCutoff);
        float modulatedCutoff = filterCutoff + 
                                (filterEnvValue * filterEnvAmount) +
                                (filterCutoffMod * lfo1ToFilter) +
                                (_velocity * velocityToFilter);
        
        // Clamp cutoff to valid range
        modulatedCutoff = Mathf.Clamp(modulatedCutoff, 20f, (float)_sampleRate * 0.45f);
        
        // Smooth filter parameters
        _filterCutoffParam.SetTarget(modulatedCutoff);
        _filterResonanceParam.SetTarget(filterResonance);
        
        // Process through filter
        float filtered = _filter.Process(
            oscMix,
            _filterCutoffParam.GetNextValue(),
            _filterResonanceParam.GetNextValue(),
            (float)_sampleRate
        );
        
        // Apply amplitude envelope
        float output = filtered * ampEnvValue * _velocity;
        
        // Store current level for voice stealing
        _currentLevel = Mathf.Abs(output);
        
        // Check if voice is finished
        if (!_ampEnvelope.IsActive)
        {
            _isActive = false;
            _noteNumber = -1;
            _currentLevel = 0f;
        }
        
        return output;
    }
    */
    // Process with stereo output
    public void ProcessStereo(out float left, out float right)
    {
        float mono = Process();
        
        // Constant power panning
        float panAngle = pan * Mathf.PI * 0.5f;
        left = mono * Mathf.Cos(panAngle);
        right = mono * Mathf.Sin(panAngle);
    }
    
    // ============================================================
    // VOICE STATE QUERIES
    // ============================================================
    
    public bool IsActive => _isActive;
    public int NoteNumber => _noteNumber;
    public bool IsInRelease => _ampEnvelope.IsInRelease;
    public float GetCurrentLevel() => _currentLevel;
    public uint GetNoteOnTime() => _noteOnTime;
    
    // ============================================================
    // PARAMETER SETTERS
    // ============================================================
    
    public void SetPitchBend(float semitones)
    {
        _pitchBendSemitones = semitones;
    }
    
    public void SetFilterCutoff(float hz)
    {
        filterCutoff = Mathf.Clamp(hz, 20f, (float)_sampleRate * 0.45f);
    }
    
    public void SetFilterResonance(float res)
    {
        filterResonance = Mathf.Clamp01(res);
    }
    
    // ============================================================
    // UTILITY FUNCTIONS
    // ============================================================
    
    private float MidiNoteToFrequency(int midiNote)
    {
        return 440f * Mathf.Pow(2f, (midiNote - 69) / 12f);
    }
    
    private float SemitonesToRatio(float semitones)
    {
        return Mathf.Pow(2f, semitones / 12f);
    }
}