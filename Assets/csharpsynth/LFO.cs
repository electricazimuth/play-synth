using UnityEngine;
using System;
// ============================================================
// LFO (LOW FREQUENCY OSCILLATOR)
// ============================================================

public class LFO
{
    public enum Waveform { Sine, Triangle, Saw, Square, SampleAndHold }
    
    private double _phase = 0.0;
    private double _increment = 0.0;
    
    public Waveform waveform = Waveform.Sine;
    
    // For sample & hold
    private float _sampleHoldValue = 0f;
    private System.Random _random = new System.Random();
    
    public void SetFrequency(float frequency, float sampleRate)
    {
        _increment = frequency / sampleRate;
    }
    
    public float Process()
    {
        float output = 0f;
        
        switch (waveform)
        {
            case Waveform.Sine:
                output = (float)Math.Sin(_phase * 2.0 * Math.PI);
                break;
                
            case Waveform.Triangle:
                if (_phase < 0.5)
                    output = (float)(4.0 * _phase - 1.0);
                else
                    output = (float)(-4.0 * _phase + 3.0);
                break;
                
            case Waveform.Saw:
                output = (float)(2.0 * _phase - 1.0);
                break;
                
            case Waveform.Square:
                output = _phase < 0.5 ? 1f : -1f;
                break;
                
            case Waveform.SampleAndHold:
                if (_phase < _increment) // New cycle
                    _sampleHoldValue = (float)(_random.NextDouble() * 2.0 - 1.0);
                output = _sampleHoldValue;
                break;
        }
        
        _phase += _increment;
        while (_phase >= 1.0)
            _phase -= 1.0;
        
        return output;
    }
    
    public void Reset()
    {
        _phase = 0.0;
    }
}