
using UnityEngine;
using System.Collections.Generic;

public class VoiceManager
{
    private SynthVoice[] _voices;
    private int _maxVoices;
    private uint _timestamp = 0; // For voice age tracking
    
    // Global modulation
    private float _pitchBendSemitones = 0f;
    private float _pitchBendRange = 2f; // ±2 semitones
    
    // Master output processing
    private float _masterVolume = 0.7f;
    
    public VoiceManager(int voiceCount)
    {
        _maxVoices = voiceCount;
        _voices = new SynthVoice[_maxVoices];
        
        double sampleRate = AudioSettings.outputSampleRate;
        
        // Pre-allocate all voices
        for (int i = 0; i < _maxVoices; i++)
        {
            _voices[i] = new SynthVoice(sampleRate);
            
            // Slight pan spread for stereo width
            _voices[i].pan = 0.5f + (i / (float)_maxVoices - 0.5f) * 0.3f;
        }
    }
    
    // ============================================================
    // NOTE CONTROL
    // ============================================================
    
    public void NoteOn(int midiNote, float velocity)
    {
        SynthVoice targetVoice = null;
        
        // Strategy 1: Find inactive voice
        foreach (var voice in _voices)
        {
            if (!voice.IsActive)
            {
                targetVoice = voice;
                break;
            }
        }
        
        // Strategy 2: Steal quietest voice in release stage
        if (targetVoice == null)
        {
            float minLevel = float.MaxValue;
            foreach (var voice in _voices)
            {
                if (voice.IsInRelease && voice.GetCurrentLevel() < minLevel)
                {
                    minLevel = voice.GetCurrentLevel();
                    targetVoice = voice;
                }
            }
        }
        
        // Strategy 3: Steal oldest voice
        if (targetVoice == null)
        {
            uint oldestTime = uint.MaxValue;
            foreach (var voice in _voices)
            {
                if (voice.GetNoteOnTime() < oldestTime)
                {
                    oldestTime = voice.GetNoteOnTime();
                    targetVoice = voice;
                }
            }
        }
        
        // Trigger the voice
        if (targetVoice != null)
        {
            targetVoice.SetPitchBend(_pitchBendSemitones);
            targetVoice.NoteOn(midiNote, velocity, _timestamp++);
        }
    }
    
    public void NoteOff(int midiNote)
    {
        // Release all voices playing this note
        foreach (var voice in _voices)
        {
            if (voice.IsActive && voice.NoteNumber == midiNote)
            {
                voice.NoteOff();
            }
        }
    }
    
    public void AllNotesOff()
    {
        foreach (var voice in _voices)
        {
            voice.NoteOff();
        }
    }
    
    public void AllSoundOff()
    {
        foreach (var voice in _voices)
        {
            voice.NoteOff();
            // Could force immediate silence here if needed
        }
    }
    
    // ============================================================
    // GLOBAL PARAMETERS
    // ============================================================
    
    public void SetPitchBend(float normalizedValue) // -1 to +1
    {
        _pitchBendSemitones = normalizedValue * _pitchBendRange;
        
        // Update all active voices
        foreach (var voice in _voices)
        {
            if (voice.IsActive)
                voice.SetPitchBend(_pitchBendSemitones);
        }
    }
    
    public void SetPitchBendRange(float semitones)
    {
        _pitchBendRange = Mathf.Clamp(semitones, 0f, 12f);
    }
    
    public void SetMasterVolume(float volume)
    {
        _masterVolume = Mathf.Clamp01(volume);
    }
    
    // ============================================================
    // AUDIO PROCESSING
    // ============================================================
    
    public float ProcessMono()
    {
        float sum = 0f;
        
        foreach (var voice in _voices)
        {
            sum += voice.Process();
        }
        
        // Scale by voice count with headroom
        sum *= (0.7f / _maxVoices) * _masterVolume;
        
        // Soft clip with fast tanh
        return FastTanh(sum);
    }
    
    public void ProcessStereo(out float left, out float right)
    {
        left = 0f;
        right = 0f;
        
        foreach (var voice in _voices)
        {
            float L, R;
            voice.ProcessStereo(out L, out R);
            left += L;
            right += R;
        }
        
        // Scale by voice count with headroom
        float scale = (0.7f / _maxVoices) * _masterVolume;
        left *= scale;
        right *= scale;
        
        // Soft clip both channels
        left = FastTanh(left);
        right = FastTanh(right);
    }
    
    // Fast tanh approximation for soft clipping
    private float FastTanh(float x)
    {
        if (x < -3f) return -1f;
        if (x > 3f) return 1f;
        
        float x2 = x * x;
        return x * (27f + x2) / (27f + 9f * x2);
    }
    
    // ============================================================
    // PRESET MANAGEMENT
    // ============================================================

    public void UpdateAllVoices(ref SynthParameters parameters)
    {
        // Standard for-loop avoids creating an Enumerator or Delegate
        for (int i = 0; i < _voices.Length; i++)
        {
            _voices[i].UpdateParameters(ref parameters);
        }
    }
    
    public void SetAllVoiceParameter(System.Action<SynthVoice> setter)
    {
        foreach (var voice in _voices)
        {
            setter(voice);
        }
    }
    
    // Example usage:
    // voiceManager.SetAllVoiceParameter(v => v.SetFilterCutoff(2000f));
    // voiceManager.SetAllVoiceParameter(v => v.osc1Level = 0.8f);
}