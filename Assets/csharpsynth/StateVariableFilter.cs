using UnityEngine;
// ============================================================
// STATE VARIABLE FILTER
// ============================================================

public class StateVariableFilter
{
    public enum FilterMode { Lowpass, Highpass, Bandpass, Notch }
    
    // State variables
    private float _ic1eq = 0f;
    private float _ic2eq = 0f;
    
    public FilterMode mode = FilterMode.Lowpass;
    
    public float Process(float input, float cutoffHz, float resonance, float sampleRate)
    {       
        // Pre-warp cutoff for stability
        //float g = Mathf.Tan(Mathf.PI * cutoffHz / sampleRate);
        // Optimisation calling Mathf.Tan per sample, per voice. * 16 voices * 44,100 Hz = 705,600 calls to Tan per second
        // replacing with fast approximation
        float g = FastTan(Mathf.PI * cutoffHz / sampleRate);
        
        
        // Resonance damping (0 to 1 maps to stable range)
        float k = 2f * (1f - resonance * 0.99f);
        
        // Coefficient calculation
        float a1 = 1f / (1f + g * (g + k));
        float a2 = g * a1;
        float a3 = g * a2;
        
        // State variable equations
        float v3 = input - _ic2eq;
        float v1 = a1 * _ic1eq + a2 * v3;
        float v2 = _ic2eq + a2 * _ic1eq + a3 * v3;
        
        // Update state
        _ic1eq = 2f * v1 - _ic1eq;
        _ic2eq = 2f * v2 - _ic2eq;
        
        // Select output
        switch (mode)
        {
            case FilterMode.Lowpass:
                return v2;
            case FilterMode.Highpass:
                return input - k * v1 - v2;
            case FilterMode.Bandpass:
                return v1;
            case FilterMode.Notch:
                return input - k * v1;
            default:
                return v2;
        }
    }

    // Fast approximation of Tan(PI * x) for 0 <= x <= 0.5
    // Used for frequency warping in SVF
    private float FastTan(float x)
    {
        // Simple Pade approximation or polynomial
        // For x < 0.25 (11kHz at 44.1), x * PI is a decent approximation
        // Better: x * (3.14159f + x*x...) 
        // Or just strictly clamp cutoff so you don't blow up the filter:
        
        if (x > 0.49f) x = 0.49f; // Tan approaches infinity at 0.5
        
        // 2-term Taylor/Pade usually suffices for audio filters
        float w = x * Mathf.PI;
        return w + (w * w * w) / 3.0f;
    }
    
    public void Reset()
    {
        _ic1eq = 0f;
        _ic2eq = 0f;
    }
}