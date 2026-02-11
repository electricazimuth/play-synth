using UnityEngine;

// ============================================================
// MODULATION MATRIX
// ============================================================

public class ModulationMatrix
{
    public enum Source
    {
        Velocity,
        LFO1,
        LFO2,
        FilterEnv,
        AmpEnv,
        ModWheel,
        Aftertouch
    }
    
    public enum Destination
    {
        Pitch,          // In semitones
        FilterCutoff,   // In Hz
        FilterRes,      // 0 to 1
        Osc2Pitch,      // In semitones
        PWM,            // Pulse width 0 to 1
        Amplitude       // 0 to 1
    }
    
    private const int MAX_SOURCES = 7;
    private const int MAX_DESTINATIONS = 6;
    private const int MAX_ROUTES = 32;
    
    private float[] _sourceValues = new float[MAX_SOURCES];
    private float[] _destValues = new float[MAX_DESTINATIONS];
    
    private struct ModRoute
    {
        public Source source;
        public Destination dest;
        public float amount;
        public bool active;
    }
    
    private ModRoute[] _routes = new ModRoute[MAX_ROUTES];
    private int _routeCount = 0;
    
    public void AddRoute(Source src, Destination dst, float amount)
    {
        if (_routeCount < MAX_ROUTES)
        {
            _routes[_routeCount] = new ModRoute
            {
                source = src,
                dest = dst,
                amount = amount,
                active = true
            };
            _routeCount++;
        }
    }
    
    public void SetSource(Source src, float value)
    {
        _sourceValues[(int)src] = value;
    }
    
    public void Process()
    {
        // Clear destinations
        for (int i = 0; i < MAX_DESTINATIONS; i++)
            _destValues[i] = 0f;
        
        // Apply all active routes
        for (int i = 0; i < _routeCount; i++)
        {
            if (_routes[i].active)
            {
                float srcValue = _sourceValues[(int)_routes[i].source];
                _destValues[(int)_routes[i].dest] += srcValue * _routes[i].amount;
            }
        }
    }
    
    public float GetDestination(Destination dest)
    {
        return _destValues[(int)dest];
    }
    
    public void ClearRoutes()
    {
        _routeCount = 0;
    }
}