// SynthMiddleware.cs - Optional middleware components for sound management
using UnityEngine;
using Azimuth.DES;
using Azimuth.Audio;

namespace Azimuth.Audio.Middleware
{
    /// <summary>
    /// Limits the total number of simultaneous sounds to prevent audio overload.
    /// Processes before MasterSynth to filter events.
    /// </summary>
    public class SoundLimiter : EventHandlerBehaviour,
        IEventHandler<SynthTriggerEvent>
    {
        [Header("Sound Limiting")]
        [SerializeField] private int maxSimultaneousSounds = 32;
        [SerializeField] private bool showWarnings = false;
        
        private int _currentSoundCount = 0;
        
        // Higher priority than MasterSynth (processes first)
        public int Priority => -10;
        
        public void OnEvent(SynthTriggerEvent ev)
        {
            if (_currentSoundCount >= maxSimultaneousSounds)
            {
                // Cancel this event to prevent overload
                ev.Cancel();
                
                if (showWarnings)
                {
                    Debug.LogWarning($"[SoundLimiter] Max sounds reached ({maxSimultaneousSounds}), cancelling {ev.PatchName}");
                }
            }
            else
            {
                _currentSoundCount++;
                
                // Schedule cleanup based on duration or default decay time
                float cleanupTime = ev.Duration > 0 ? ev.Duration + 0.5f : 2.0f;
                Invoke(nameof(DecrementCounter), cleanupTime);
            }
        }
        
        private void DecrementCounter()
        {
            _currentSoundCount = Mathf.Max(0, _currentSoundCount - 1);
        }
        
        void OnGUI()
        {
            if (showWarnings)
            {
                GUI.Label(new Rect(10, 100, 300, 20), $"Active Sounds: {_currentSoundCount}/{maxSimultaneousSounds}");
            }
        }
    }

    /// <summary>
    /// Prevents rapid-fire triggering of the same sound (debouncing/cooldown).
    /// </summary>
    public class SoundCooldownManager : EventHandlerBehaviour,
        IEventHandler<SynthTriggerEvent>
    {
        [Header("Cooldown Settings")]
        [SerializeField] private float minTimeBetweenSamePatch = 0.05f;
        [SerializeField] private bool cooldownPerNote = true;
        [SerializeField] private bool showDebugMessages = false;
        
        private System.Collections.Generic.Dictionary<string, float> _lastTriggerTime = 
            new System.Collections.Generic.Dictionary<string, float>();
        
        // Process before MasterSynth
        public int Priority => -10;
        
        public void OnEvent(SynthTriggerEvent ev)
        {
            // Create key based on patch and optionally note
            string key = cooldownPerNote 
                ? $"{ev.PatchName}_{ev.MidiNote}" 
                : ev.PatchName;
            
            if (_lastTriggerTime.TryGetValue(key, out float lastTime))
            {
                float timeSinceLast = Time.time - lastTime;
                if (timeSinceLast < minTimeBetweenSamePatch)
                {
                    // Cancel - too soon
                    ev.Cancel();
                    
                    if (showDebugMessages)
                    {
                        Debug.Log($"[SoundCooldown] Blocked {ev.PatchName} (cooldown: {timeSinceLast:F3}s < {minTimeBetweenSamePatch:F3}s)");
                    }
                    return;
                }
            }
            
            _lastTriggerTime[key] = Time.time;
        }
    }

    /// <summary>
    /// Debug logger for all synth events. Useful for tracking audio behavior.
    /// </summary>
    public class SynthDebugger : EventHandlerBehaviour,
        IEventHandler<SynthTriggerEvent>,
        IEventHandler<SynthSustainStartEvent>,
        IEventHandler<SynthSustainReleaseEvent>,
        IEventHandler<SynthVoiceStealEvent>
    {
        [Header("Debug Settings")]
        [SerializeField] private bool logTriggers = true;
        [SerializeField] private bool logSustains = true;
        [SerializeField] private bool logVoiceStealing = true;
        [SerializeField] private bool useRichText = true;
        
        // Execute after sound management but before synth
        public int Priority => -5;
        
        int IEventHandler<SynthTriggerEvent>.Priority => -5;
        int IEventHandler<SynthSustainStartEvent>.Priority => -5;
        int IEventHandler<SynthSustainReleaseEvent>.Priority => -5;
        int IEventHandler<SynthVoiceStealEvent>.Priority => 10; // After synth
        
        public void OnEvent(SynthTriggerEvent ev)
        {
            if (!logTriggers) return;
            
            string msg = useRichText
                ? $"<color=cyan>[Synth Trigger]</color> {ev.PatchName} @ {ev.WorldPosition} | Note:{ev.MidiNote} Vel:{ev.Velocity:F2} Dur:{ev.Duration:F2}s"
                : $"[Synth Trigger] {ev.PatchName} @ {ev.WorldPosition} | Note:{ev.MidiNote} Vel:{ev.Velocity:F2} Dur:{ev.Duration:F2}s";
            
            Debug.Log(msg);
        }
        
        public void OnEvent(SynthSustainStartEvent ev)
        {
            if (!logSustains) return;
            
            string msg = useRichText
                ? $"<color=green>[Synth Sustain Start]</color> {ev.PatchName} ID:{ev.SustainId} Note:{ev.MidiNote}"
                : $"[Synth Sustain Start] {ev.PatchName} ID:{ev.SustainId} Note:{ev.MidiNote}";
            
            Debug.Log(msg);
        }
        
        public void OnEvent(SynthSustainReleaseEvent ev)
        {
            if (!logSustains) return;
            
            string msg = useRichText
                ? $"<color=yellow>[Synth Sustain Release]</color> ID:{ev.SustainId}"
                : $"[Synth Sustain Release] ID:{ev.SustainId}";
            
            Debug.Log(msg);
        }
        
        public void OnEvent(SynthVoiceStealEvent ev)
        {
            if (!logVoiceStealing) return;
            
            string msg = useRichText
                ? $"<color=red>[Voice Steal]</color> {ev.RequestingPatchName}(P:{ev.RequestingPriority}) stole from {ev.StolenPatchName}(P:{ev.StolenVoicePriority}) | Release:{ev.WasInRelease}"
                : $"[Voice Steal] {ev.RequestingPatchName}(P:{ev.RequestingPriority}) stole from {ev.StolenPatchName}(P:{ev.StolenVoicePriority}) | Release:{ev.WasInRelease}";
            
            Debug.LogWarning(msg);
        }
    }

    /// <summary>
    /// Custom spatial processor - can override positions based on zones or effects.
    /// Example: All sounds in a reverb zone get their position adjusted.
    /// </summary>
    public class CustomSpatializer : EventHandlerBehaviour,
        IEventHandler<SynthTriggerEvent>,
        IEventHandler<SynthSustainStartEvent>
    {
        [Header("Spatial Override")]
        [SerializeField] private bool enableReverbZone = false;
        [SerializeField] private Vector3 reverbZoneCenter = Vector3.zero;
        [SerializeField] private float reverbZoneRadius = 10f;
        [SerializeField] private Vector3 reverbPositionOffset = new Vector3(0, 5, 0);
        
        // Process before MasterSynth to modify positions
        public int Priority => -1;
        
        int IEventHandler<SynthTriggerEvent>.Priority => -1;
        int IEventHandler<SynthSustainStartEvent>.Priority => -1;
        
        public void OnEvent(SynthTriggerEvent ev)
        {
            if (enableReverbZone && IsInReverbZone(ev.WorldPosition))
            {
                ev.WorldPosition += reverbPositionOffset;
            }
        }
        
        public void OnEvent(SynthSustainStartEvent ev)
        {
            if (enableReverbZone && IsInReverbZone(ev.WorldPosition))
            {
                ev.WorldPosition += reverbPositionOffset;
            }
        }
        
        private bool IsInReverbZone(Vector3 position)
        {
            return Vector3.Distance(position, reverbZoneCenter) <= reverbZoneRadius;
        }
        
        void OnDrawGizmosSelected()
        {
            if (enableReverbZone)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(reverbZoneCenter, reverbZoneRadius);
            }
        }
    }

    /// <summary>
    /// Velocity remapping - can boost or scale velocities based on gameplay state.
    /// Example: Power-up mode doubles all sound velocities.
    /// </summary>
    public class VelocityModifier : EventHandlerBehaviour,
        IEventHandler<SynthTriggerEvent>,
        IEventHandler<SynthSustainStartEvent>
    {
        [Header("Velocity Modification")]
        [Range(0f, 2f)]
        [SerializeField] private float velocityMultiplier = 1.0f;
        
        [SerializeField] private bool clampToOne = true;
        
        // Process before MasterSynth
        public int Priority => -1;
        
        int IEventHandler<SynthTriggerEvent>.Priority => -1;
        int IEventHandler<SynthSustainStartEvent>.Priority => -1;
        
        public void OnEvent(SynthTriggerEvent ev)
        {
            ev.Velocity *= velocityMultiplier;
            if (clampToOne)
            {
                ev.Velocity = Mathf.Clamp01(ev.Velocity);
            }
        }
        
        public void OnEvent(SynthSustainStartEvent ev)
        {
            ev.Velocity *= velocityMultiplier;
            if (clampToOne)
            {
                ev.Velocity = Mathf.Clamp01(ev.Velocity);
            }
        }
        
        /// <summary>
        /// Enable power-up mode (e.g., from another event)
        /// </summary>
        public void EnablePowerUpMode(float duration)
        {
            velocityMultiplier = 1.5f;
            Invoke(nameof(DisablePowerUpMode), duration);
        }
        
        private void DisablePowerUpMode()
        {
            velocityMultiplier = 1.0f;
        }
    }

    /// <summary>
    /// Statistics tracker for synth usage - useful for performance analysis.
    /// </summary>
    public class SynthStatistics : EventHandlerBehaviour,
        IEventHandler<SynthTriggerEvent>,
        IEventHandler<SynthVoiceStealEvent>
    {
        [Header("Statistics")]
        [SerializeField] private bool displayOnGUI = true;
        
        private int _totalTriggersThisSession = 0;
        private int _totalVoiceSteals = 0;
        private float _sessionStartTime;
        
        private System.Collections.Generic.Dictionary<string, int> _patchUsageCount = 
            new System.Collections.Generic.Dictionary<string, int>();
        
        // Low priority - just for tracking
        public int Priority => 100;
        int IEventHandler<SynthVoiceStealEvent>.Priority => 100;
        
        void Start()
        {
            _sessionStartTime = Time.time;
        }
        
        public void OnEvent(SynthTriggerEvent ev)
        {
            _totalTriggersThisSession++;
            
            if (!_patchUsageCount.ContainsKey(ev.PatchName))
            {
                _patchUsageCount[ev.PatchName] = 0;
            }
            _patchUsageCount[ev.PatchName]++;
        }
        
        public void OnEvent(SynthVoiceStealEvent ev)
        {
            _totalVoiceSteals++;
        }
        
        void OnGUI()
        {
            if (!displayOnGUI) return;
            
            float sessionTime = Time.time - _sessionStartTime;
            float triggersPerSecond = _totalTriggersThisSession / Mathf.Max(1f, sessionTime);
            
            GUILayout.BeginArea(new Rect(10, 130, 400, 300));
            GUILayout.Label($"=== Synth Statistics ===");
            GUILayout.Label($"Total Triggers: {_totalTriggersThisSession}");
            GUILayout.Label($"Triggers/Second: {triggersPerSecond:F2}");
            GUILayout.Label($"Voice Steals: {_totalVoiceSteals}");
            GUILayout.Label($"Session Time: {sessionTime:F1}s");
            
            if (_patchUsageCount.Count > 0)
            {
                GUILayout.Label("--- Patch Usage ---");
                foreach (var kvp in _patchUsageCount)
                {
                    GUILayout.Label($"  {kvp.Key}: {kvp.Value}");
                }
            }
            
            GUILayout.EndArea();
        }
        
        public void ResetStatistics()
        {
            _totalTriggersThisSession = 0;
            _totalVoiceSteals = 0;
            _patchUsageCount.Clear();
            _sessionStartTime = Time.time;
        }
    }
}
