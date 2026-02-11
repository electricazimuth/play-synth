using UnityEngine;
using System;
// ============================================================
// ANALOG OSCILLATOR WITH POLYBLEP ANTI-ALIASING
// ============================================================

public class AnalogOscillator
{
    public enum Waveform { Sine, Saw, Square, Triangle }
    
    private double _phase = 0.0;      // CRITICAL: Double precision
    private double _increment = 0.0;

    //need a class-level variable to store the integrator state
    private double _triangleState = 0.0;
    
    public Waveform waveform = Waveform.Saw;
    public float pulseWidth = 0.5f; // For square wave PWM
    
    public void SetFrequency(float frequency, float sampleRate)
    {
        // pass sample rate as parameter as on audio thread
        
        // Nyquist limit protection
        if (frequency > sampleRate / 2.0f)
            frequency = sampleRate / 2.0f;
        
        _increment = (double)(frequency / sampleRate);
    }
    
    public float Process()
    {
        float output = 0f;
        
        switch (waveform)
        {
            case Waveform.Sine:
                output = (float)Math.Sin(_phase * 2.0 * Math.PI);
                break;
                
            case Waveform.Saw:
                output = (float)(2.0 * _phase - 1.0);
                output -= PolyBLEP(_phase, _increment);
                break;
                
            case Waveform.Square:
                output = _phase < pulseWidth ? 1f : -1f;
                output += PolyBLEP(_phase, _increment);
                output -= PolyBLEP(Fmod(_phase + (1.0 - pulseWidth), 1.0), _increment);
                break;
                
            /*
            case Waveform.Triangle:
                // Integrate square wave for triangle
                output = _phase < 0.5 ? 1f : -1f;
                output += PolyBLEP(_phase, _increment);
                output -= PolyBLEP(Fmod(_phase + 0.5, 1.0), _increment);
                // Leaky integrator
                output = (float)_increment * output + (1f - (float)_increment) * output;
                output *= 4f; // Scale
                break;
            */
            case Waveform.Triangle:
                float square = _phase < 0.5 ? 1f : -1f;
                square += PolyBLEP(_phase, _increment);
                square -= PolyBLEP((_phase + 0.5) % 1.0, _increment);
                
                // Leaky Integration (approximating 1/f filter)
                // 4.0 * _increment scales it to normalize amplitude roughly across pitches
                _triangleState += (4.0 * _increment) * square; 
                
                // Damping to prevent DC drift
                _triangleState *= (1.0 - _increment); 
                
                output = (float)_triangleState;
                break;
        }
        
        // Increment phase
        _phase += _increment;
        
        // Wrap phase
        while (_phase >= 1.0)
            _phase -= 1.0;
        
        return output;
    }
    
    public void Reset()
    {
        _phase = 0.0;
    }
    
    // PolyBLEP anti-aliasing residual
    private float PolyBLEP(double t, double dt)
    {
        // t-dt < t < t+dt
        if (t < dt)
        {
            t /= dt;
            return (float)(t + t - t * t - 1.0);
        }
        else if (t > 1.0 - dt)
        {
            t = (t - 1.0) / dt;
            return (float)(t * t + t + t + 1.0);
        }
        return 0f;
    }
    
    private double Fmod(double a, double b)
    {
        //return a - b * Math.Floor(a / b);
        //Math.Floor is somewhat slow. Since phase is strictly 0.0 to 1.0 and pulseWidth is 0.0 to 1.0, we only need to handle the wrap-around.
        return a - b * PhaseWrap(a / b);
    }

    // Optimized for phase wrapping 0..1
    private double PhaseWrap(double val)
    {
        if (val >= 1.0) return val - 1.0;
        if (val < 0.0) return val + 1.0;
        return val;
    }
}