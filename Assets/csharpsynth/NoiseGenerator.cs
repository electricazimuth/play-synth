using UnityEngine;
// ============================================================
// NOISE GENERATOR
// ============================================================

public class NoiseGenerator
{
    private System.Random _random = new System.Random();
    
    // Pink noise filter state (optional)
    private float _b0 = 0f, _b1 = 0f, _b2 = 0f, _b3 = 0f, _b4 = 0f, _b5 = 0f, _b6 = 0f;
    
    public enum NoiseType { White, Pink }
    public NoiseType type = NoiseType.White;
    
    public float Process()
    {
        if (type == NoiseType.White)
        {
            return (float)(_random.NextDouble() * 2.0 - 1.0);
        }
        else // Pink noise (Paul Kellett's algorithm)
        {
            float white = (float)(_random.NextDouble() * 2.0 - 1.0);
            _b0 = 0.99886f * _b0 + white * 0.0555179f;
            _b1 = 0.99332f * _b1 + white * 0.0750759f;
            _b2 = 0.96900f * _b2 + white * 0.1538520f;
            _b3 = 0.86650f * _b3 + white * 0.3104856f;
            _b4 = 0.55000f * _b4 + white * 0.5329522f;
            _b5 = -0.7616f * _b5 - white * 0.0168980f;
            float pink = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + white * 0.5362f;
            _b6 = white * 0.115926f;
            return pink * 0.11f; // Gain compensation
        }
    }
}