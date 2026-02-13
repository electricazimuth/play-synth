// SynthEvents.cs - Event definitions for the synth system
using UnityEngine;
using Azimuth.DES;

namespace Azimuth.Audio
{
    /// <summary>
    /// Trigger a one-shot synth sound with automatic note-off after duration.
    /// Duration of 0 means use the preset's envelope release.
    /// </summary>
    public class SynthTriggerEvent : EventScheduler.Event
    {
        public string PatchName;
        public Vector3 WorldPosition;
        public int MidiNote;
        public float Velocity;
        public float Duration = 0f; // 0 = use envelope, >0 = auto note-off after duration
    }

    /// <summary>
    /// Start a sustained note that requires manual release.
    /// Used for click-and-hold or other manual control scenarios.
    /// </summary>
    public class SynthSustainStartEvent : EventScheduler.Event
    {
        public string PatchName;
        public Vector3 WorldPosition;
        public int MidiNote;
        public float Velocity;
        public string SustainId; // Unique ID to track this sustained note
    }

    /// <summary>
    /// Release a sustained note (send note-off).
    /// </summary>
    public class SynthSustainReleaseEvent : EventScheduler.Event
    {
        public string SustainId; // ID of note to release
    }

    /// <summary>
    /// Stop all active notes immediately (panic button).
    /// </summary>
    public class SynthAllNotesOffEvent : EventScheduler.Event
    {
        // No parameters needed
    }

    /// <summary>
    /// Internal event for auto note-off (scheduled by MasterSynth).
    /// </summary>
    public class SynthAutoNoteOffEvent : EventScheduler.Event
    {
        public string VoiceId; // Unique ID of the voice to release
    }

    /// <summary>
    /// Optional diagnostic event dispatched when voice stealing occurs.
    /// </summary>
    public class SynthVoiceStealEvent : EventScheduler.Event
    {
        public int StolenVoicePriority;
        public int RequestingPriority;
        public string StolenPatchName;
        public string RequestingPatchName;
        public bool WasInRelease;
    }
}
