using UnityEngine;
// ============================================================
// SMOOTHED PARAMETER (Anti-Click)
// ============================================================

public class SmoothedParameter
{
    private float _currentValue;
    private float _targetValue;
    private float _coefficient;
    
    public SmoothedParameter(float initialValue, float smoothingTimeMs, float sampleRate)
    {
        _currentValue = _targetValue = initialValue;
        SetSmoothingTime(smoothingTimeMs, sampleRate);
    }
    
    public void SetSmoothingTime(float timeMs, float sampleRate)
    {
        // One-pole lowpass coefficient
        _coefficient = 1f - Mathf.Exp(-1f / (timeMs * 0.001f * sampleRate));
    }
    
    public void SetTarget(float target)
    {
        _targetValue = target;
    }
    
    public float GetNextValue()
    {
        _currentValue += (_targetValue - _currentValue) * _coefficient;
        return _currentValue;
    }
    
    public float CurrentValue => _currentValue;
    
    public void SetImmediate(float value)
    {
        _currentValue = _targetValue = value;
    }
}