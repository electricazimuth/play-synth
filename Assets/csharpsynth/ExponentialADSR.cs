using UnityEngine;

// ============================================================
// EXPONENTIAL ADSR ENVELOPE
// ============================================================

public class ExponentialADSR
{
    private enum Stage { Idle, Attack, Decay, Sustain, Release }
    private Stage _stage = Stage.Idle;
    private float _level = 0f;
    
    // Exponential coefficients
    private float _attackCoeff = 0.01f;
    private float _decayCoeff = 0.01f;
    private float _releaseCoeff = 0.01f;
    
    public float sustainLevel = 0.7f;
    
    private float _sampleRate = 44100f;
    
    public void SetSampleRate(float sr)
    {
        _sampleRate = sr;
    }
    
    public void SetAttackTime(float seconds)
    {
        // Coefficient for 99% rise in given time
        _attackCoeff = CalculateCoefficient(seconds);
    }
    
    public void SetDecayTime(float seconds)
    {
        _decayCoeff = CalculateCoefficient(seconds);
    }
    
    public void SetReleaseTime(float seconds)
    {
        _releaseCoeff = CalculateCoefficient(seconds);
    }
    
    private float CalculateCoefficient(float timeSeconds)
    {
        // Exponential time constant
        // Reaches ~99% in timeSeconds
        if (timeSeconds <= 0.0001f)
            return 1f;
        return 1f - Mathf.Exp(-5f / (timeSeconds * _sampleRate));
    }
    
    public void NoteOn()
    {
        _stage = Stage.Attack;
    }
    
    public void NoteOff()
    {
        _stage = Stage.Release;
    }
    
    public float Process()
    {
        switch (_stage)
        {
            case Stage.Idle:
                return 0f;
                
            case Stage.Attack:
                // Exponential approach to 1.0
                _level += (1f - _level) * _attackCoeff;
                if (_level >= 0.999f)
                {
                    _level = 1f;
                    _stage = Stage.Decay;
                }
                break;
                
            case Stage.Decay:
                // Exponential approach to sustain
                _level += (sustainLevel - _level) * _decayCoeff;
                if (Mathf.Abs(_level - sustainLevel) < 0.001f)
                {
                    _level = sustainLevel;
                    _stage = Stage.Sustain;
                }
                break;
                
            case Stage.Sustain:
                _level = sustainLevel;
                break;
                
            case Stage.Release:
                // Exponential decay to 0
                _level += (0f - _level) * _releaseCoeff;
                if (_level < 0.001f)
                {
                    _level = 0f;
                    _stage = Stage.Idle;
                }
                break;
        }
        
        return _level;
    }
    
    public bool IsActive => _stage != Stage.Idle;
    public bool IsInRelease => _stage == Stage.Release;
}
