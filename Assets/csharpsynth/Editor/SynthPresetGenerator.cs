using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Reflection;
using System.Globalization;

public class SynthPresetGenerator : EditorWindow
{
    // The path where assets will be created
    private static readonly string OutputPath = "Assets/Audio/Resources/SynthPresets";

    [MenuItem("Tools/Generate Synth Presets")]
    public static void GeneratePresets()
    {
        // 1. Ensure Directory Exists
        if (!Directory.Exists(OutputPath))
        {
            Directory.CreateDirectory(OutputPath);
        }

        // 2. Parse the raw config string
        string[] lines = rawConfigData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        SynthPreset currentPreset = null;
        string currentPresetName = "";
        
        // We need a temporary struct to hold values before assigning to the SO
        SynthParameters tempParams = new SynthParameters(); 
        bool isParsingParams = false;

        int count = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string line in lines)
            {
                string l = line.Trim();
                
                // Skip comments and separators
                if (string.IsNullOrEmpty(l) || l.StartsWith("//") || l.StartsWith("---") || l.StartsWith("=")) 
                    continue;

                // Split key: value
                string[] parts = l.Split(new[] { ':' }, 2);
                if (parts.Length != 2) continue;

                string key = parts[0].Trim();
                string value = parts[1].Trim();

                if (key == "PRESET")
                {
                    // If we were working on a preset, save the parameters to it now
                    if (currentPreset != null)
                    {
                        currentPreset.parameters = tempParams;
                        EditorUtility.SetDirty(currentPreset);
                    }

                    currentPresetName = value;
                    currentPreset = CreateOrLoadAsset(currentPresetName);
                    
                    // Reset temp params for new preset
                    tempParams = new SynthParameters();
                    isParsingParams = false;
                    count++;
                }
                else if (currentPreset != null)
                {
                    // Handle Top-Level Fields
                    if (key == "Priority")
                    {
                        currentPreset.priority = int.Parse(value);
                    }
                    else if (key == "DefaultNote")
                    {
                        // Clean format: "36 (C1)" -> 36
                        string cleanVal = value.Split(' ')[0];
                        currentPreset.defaultMidiNote = int.Parse(cleanVal);
                    }
                    // Handle SynthParameter Struct Fields via Reflection
                    else
                    {
                        isParsingParams = true;
                        ApplyParamToStruct(ref tempParams, key, value);
                    }
                }
            }

            // Apply params to the very last preset found
            if (currentPreset != null)
            {
                currentPreset.parameters = tempParams;
                EditorUtility.SetDirty(currentPreset);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to generate presets: {e.Message}");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"<color=green>Success!</color> Generated/Updated {count} Synth Presets in {OutputPath}");
    }

    private static SynthPreset CreateOrLoadAsset(string name)
    {
        string fullPath = $"{OutputPath}/{name}.asset";
        
        SynthPreset asset = AssetDatabase.LoadAssetAtPath<SynthPreset>(fullPath);
        
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<SynthPreset>();
            asset.patchName = name; // Set internal name
            AssetDatabase.CreateAsset(asset, fullPath);
        }
        else
        {
            asset.patchName = name; // Ensure name is synced
        }

        return asset;
    }

    private static void ApplyParamToStruct(ref SynthParameters parameters, string key, string value)
    {
        // Use reflection to find the field in the struct matching the key
        FieldInfo field = typeof(SynthParameters).GetField(key, BindingFlags.Public | BindingFlags.Instance);

        if (field != null)
        {
            Type fieldType = field.FieldType;

            if (fieldType == typeof(float))
            {
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                {
                    field.SetValueDirect(__makeref(parameters), result);
                }
            }
            else if (fieldType == typeof(int))
            {
                if (int.TryParse(value, out int result))
                {
                    field.SetValueDirect(__makeref(parameters), result);
                }
            }
            else if (fieldType.IsEnum)
            {
                // Format: "Sine (0)" -> take "Sine"
                string enumName = value.Split(' ')[0].Trim();
                try
                {
                    object enumVal = Enum.Parse(fieldType, enumName);
                    field.SetValueDirect(__makeref(parameters), enumVal);
                }
                catch
                {
                    Debug.LogWarning($"Could not parse Enum {enumName} for field {key}");
                }
            }
        }
    }

    // =============================================================================================
    // CONFIG DATA (Embeded directly to ensure zero-setup)
    // =============================================================================================
    private static readonly string rawConfigData = @"
// ============================================================
// DRUMS & PERCUSSION
// ============================================================

PRESET: Kick_808
Priority: 10
DefaultNote: 36 (C1)
---
osc1Level: 1.0
osc2Level: 0.0
noiseLevel: 0.05
osc2Semitones: 0
osc2Detune: 0.0
osc1Waveform: Sine (0)
osc2Waveform: Sine (0)

filterCutoff: 150
filterResonance: 0.1
filterEnvAmount: 800

ampAttack: 0.001
ampDecay: 0.25
ampSustain: 0.0
ampRelease: 0.05

filterAttack: 0.001
filterDecay: 0.15
filterSustain: 0.0
filterRelease: 0.05
---


PRESET: Snare_Acoustic
Priority: 9
DefaultNote: 38 (D1)
---
osc1Level: 0.3
osc2Level: 0.3
noiseLevel: 0.8
osc2Semitones: 5
osc2Detune: 0.02
osc1Waveform: Triangle (3)
osc2Waveform: Square (2)

filterCutoff: 3500
filterResonance: 0.4
filterEnvAmount: 2000

ampAttack: 0.001
ampDecay: 0.12
ampSustain: 0.0
ampRelease: 0.08

filterAttack: 0.001
filterDecay: 0.08
filterSustain: 0.0
filterRelease: 0.05
---


PRESET: HiHat_Closed
Priority: 7
DefaultNote: 42 (F#1)
---
osc1Level: 0.0
osc2Level: 0.0
noiseLevel: 1.0
osc2Semitones: 0
osc2Detune: 0.0
osc1Waveform: Square (2)
osc2Waveform: Square (2)

filterCutoff: 8000
filterResonance: 0.6
filterEnvAmount: 3000

ampAttack: 0.001
ampDecay: 0.04
ampSustain: 0.0
ampRelease: 0.02

filterAttack: 0.001
filterDecay: 0.03
filterSustain: 0.0
filterRelease: 0.02
---


PRESET: HiHat_Open
Priority: 6
DefaultNote: 46 (A#1)
---
osc1Level: 0.0
osc2Level: 0.0
noiseLevel: 1.0
osc2Semitones: 0
osc2Detune: 0.0
osc1Waveform: Square (2)
osc2Waveform: Square (2)

filterCutoff: 7000
filterResonance: 0.5
filterEnvAmount: 2000

ampAttack: 0.001
ampDecay: 0.15
ampSustain: 0.3
ampRelease: 0.2

filterAttack: 0.001
filterDecay: 0.12
filterSustain: 0.2
filterRelease: 0.15
---


PRESET: Tom_Low
Priority: 8
DefaultNote: 41 (F1)
---
osc1Level: 1.0
osc2Level: 0.4
noiseLevel: 0.15
osc2Semitones: 7
osc2Detune: 0.01
osc1Waveform: Sine (0)
osc2Waveform: Triangle (3)

filterCutoff: 400
filterResonance: 0.3
filterEnvAmount: 1200

ampAttack: 0.001
ampDecay: 0.3
ampSustain: 0.0
ampRelease: 0.1

filterAttack: 0.001
filterDecay: 0.2
filterSustain: 0.0
filterRelease: 0.08
---


PRESET: Clap
Priority: 8
DefaultNote: 39 (D#1)
---
osc1Level: 0.0
osc2Level: 0.0
noiseLevel: 1.0
osc2Semitones: 0
osc2Detune: 0.0
osc1Waveform: Square (2)
osc2Waveform: Square (2)

filterCutoff: 2500
filterResonance: 0.2
filterEnvAmount: 1500

ampAttack: 0.001
ampDecay: 0.08
ampSustain: 0.0
ampRelease: 0.06

filterAttack: 0.001
filterDecay: 0.06
filterSustain: 0.0
filterRelease: 0.04
---


// ============================================================
// BASS SOUNDS
// ============================================================

PRESET: Bass_Sub
Priority: 7
DefaultNote: 36 (C1)
---
osc1Level: 1.0
osc2Level: 0.0
noiseLevel: 0.0
osc2Semitones: 0
osc2Detune: 0.0
osc1Waveform: Sine (0)
osc2Waveform: Sine (0)

filterCutoff: 300
filterResonance: 0.1
filterEnvAmount: 200

ampAttack: 0.01
ampDecay: 0.2
ampSustain: 0.7
ampRelease: 0.3

filterAttack: 0.01
filterDecay: 0.15
filterSustain: 0.3
filterRelease: 0.2
---


PRESET: Bass_Reese
Priority: 6
DefaultNote: 36 (C1)
---
osc1Level: 1.0
osc2Level: 1.0
noiseLevel: 0.0
osc2Semitones: 0
osc2Detune: 0.08
osc1Waveform: Saw (1)
osc2Waveform: Saw (1)

filterCutoff: 600
filterResonance: 0.7
filterEnvAmount: 1500

ampAttack: 0.01
ampDecay: 0.15
ampSustain: 0.8
ampRelease: 0.4

filterAttack: 0.01
filterDecay: 0.3
filterSustain: 0.4
filterRelease: 0.3
---


PRESET: Bass_Acid
Priority: 6
DefaultNote: 36 (C1)
---
osc1Level: 1.0
osc2Level: 0.0
noiseLevel: 0.0
osc2Semitones: 0
osc2Detune: 0.0
osc1Waveform: Saw (1)
osc2Waveform: Saw (1)

filterCutoff: 400
filterResonance: 0.85
filterEnvAmount: 3500

ampAttack: 0.005
ampDecay: 0.1
ampSustain: 0.5
ampRelease: 0.2

filterAttack: 0.01
filterDecay: 0.15
filterSustain: 0.2
filterRelease: 0.15
---


PRESET: Bass_Pluck
Priority: 5
DefaultNote: 48 (C2)
---
osc1Level: 1.0
osc2Level: 0.6
noiseLevel: 0.0
osc2Semitones: 12
osc2Detune: 0.02
osc1Waveform: Triangle (3)
osc2Waveform: Saw (1)

filterCutoff: 1200
filterResonance: 0.4
filterEnvAmount: 2500

ampAttack: 0.001
ampDecay: 0.15
ampSustain: 0.2
ampRelease: 0.15

filterAttack: 0.001
filterDecay: 0.12
filterSustain: 0.1
filterRelease: 0.1
---


// ============================================================
// LEAD SOUNDS
// ============================================================

PRESET: Lead_Square
Priority: 4
DefaultNote: 60 (C3)
---
osc1Level: 1.0
osc2Level: 0.7
noiseLevel: 0.0
osc2Semitones: 0
osc2Detune: 0.015
osc1Waveform: Square (2)
osc2Waveform: Square (2)

filterCutoff: 2500
filterResonance: 0.5
filterEnvAmount: 3000

ampAttack: 0.01
ampDecay: 0.2
ampSustain: 0.7
ampRelease: 0.3

filterAttack: 0.02
filterDecay: 0.3
filterSustain: 0.5
filterRelease: 0.2
---


PRESET: Lead_Saw
Priority: 4
DefaultNote: 60 (C3)
---
osc1Level: 1.0
osc2Level: 0.8
noiseLevel: 0.0
osc2Semitones: -12
osc2Detune: 0.01
osc1Waveform: Saw (1)
osc2Waveform: Saw (1)

filterCutoff: 3000
filterResonance: 0.3
filterEnvAmount: 2500

ampAttack: 0.005
ampDecay: 0.15
ampSustain: 0.8
ampRelease: 0.25

filterAttack: 0.01
filterDecay: 0.25
filterSustain: 0.6
filterRelease: 0.2
---


PRESET: Lead_Sync
Priority: 4
DefaultNote: 60 (C3)
---
osc1Level: 1.0
osc2Level: 0.9
noiseLevel: 0.0
osc2Semitones: 7
osc2Detune: 0.05
osc1Waveform: Saw (1)
osc2Waveform: Square (2)

filterCutoff: 2000
filterResonance: 0.6
filterEnvAmount: 4000

ampAttack: 0.01
ampDecay: 0.1
ampSustain: 0.7
ampRelease: 0.2

filterAttack: 0.005
filterDecay: 0.2
filterSustain: 0.4
filterRelease: 0.15
---


// ============================================================
// PAD SOUNDS
// ============================================================

PRESET: Pad_Warm
Priority: 2
DefaultNote: 48 (C2)
---
osc1Level: 0.8
osc2Level: 0.8
noiseLevel: 0.0
osc2Semitones: 12
osc2Detune: 0.02
osc1Waveform: Saw (1)
osc2Waveform: Triangle (3)

filterCutoff: 1500
filterResonance: 0.2
filterEnvAmount: 1000

ampAttack: 0.5
ampDecay: 0.3
ampSustain: 0.8
ampRelease: 1.0

filterAttack: 0.6
filterDecay: 0.4
filterSustain: 0.6
filterRelease: 0.8
---


PRESET: Pad_Strings
Priority: 2
DefaultNote: 48 (C2)
---
osc1Level: 0.7
osc2Level: 0.7
noiseLevel: 0.05
osc2Semitones: 7
osc2Detune: 0.01
osc1Waveform: Saw (1)
osc2Waveform: Saw (1)

filterCutoff: 2000
filterResonance: 0.3
filterEnvAmount: 800

ampAttack: 0.4
ampDecay: 0.3
ampSustain: 0.9
ampRelease: 0.8

filterAttack: 0.5
filterDecay: 0.4
filterSustain: 0.7
filterRelease: 0.6
---


PRESET: Pad_Sweep
Priority: 2
DefaultNote: 48 (C2)
---
osc1Level: 1.0
osc2Level: 0.8
noiseLevel: 0.1
osc2Semitones: 12
osc2Detune: 0.03
osc1Waveform: Triangle (3)
osc2Waveform: Saw (1)

filterCutoff: 800
filterResonance: 0.6
filterEnvAmount: 5000

ampAttack: 0.8
ampDecay: 0.4
ampSustain: 0.7
ampRelease: 1.2

filterAttack: 1.0
filterDecay: 0.6
filterSustain: 0.3
filterRelease: 0.8
---


// ============================================================
// PLUCK & MALLET SOUNDS
// ============================================================

PRESET: Pluck_Marimba
Priority: 5
DefaultNote: 60 (C3)
---
osc1Level: 1.0
osc2Level: 0.5
noiseLevel: 0.05
osc2Semitones: 12
osc2Detune: 0.0
osc1Waveform: Sine (0)
osc2Waveform: Triangle (3)

filterCutoff: 2500
filterResonance: 0.2
filterEnvAmount: 1500

ampAttack: 0.001
ampDecay: 0.4
ampSustain: 0.0
ampRelease: 0.3

filterAttack: 0.001
filterDecay: 0.3
filterSustain: 0.0
filterRelease: 0.2
---


PRESET: Pluck_Kalimba
Priority: 5
DefaultNote: 72 (C4)
---
osc1Level: 1.0
osc2Level: 0.8
noiseLevel: 0.0
osc2Semitones: 19
osc2Detune: 0.01
osc1Waveform: Triangle (3)
osc2Waveform: Sine (0)

filterCutoff: 3500
filterResonance: 0.4
filterEnvAmount: 2000

ampAttack: 0.001
ampDecay: 0.25
ampSustain: 0.1
ampRelease: 0.2

filterAttack: 0.001
filterDecay: 0.2
filterSustain: 0.05
filterRelease: 0.15
---


PRESET: Pluck_Bell
Priority: 5
DefaultNote: 72 (C4)
---
osc1Level: 0.8
osc2Level: 1.0
noiseLevel: 0.02
osc2Semitones: 24
osc2Detune: 0.005
osc1Waveform: Sine (0)
osc2Waveform: Triangle (3)

filterCutoff: 4500
filterResonance: 0.5
filterEnvAmount: 2500

ampAttack: 0.001
ampDecay: 0.5
ampSustain: 0.2
ampRelease: 0.8

filterAttack: 0.001
filterDecay: 0.4
filterSustain: 0.15
filterRelease: 0.6
---


// ============================================================
// FX & SPECIAL SOUNDS
// ============================================================

PRESET: FX_Zap
Priority: 3
DefaultNote: 60 (C3)
---
osc1Level: 1.0
osc2Level: 0.0
noiseLevel: 0.3
osc2Semitones: 0
osc2Detune: 0.0
osc1Waveform: Square (2)
osc2Waveform: Square (2)

filterCutoff: 8000
filterResonance: 0.8
filterEnvAmount: -6000

ampAttack: 0.001
ampDecay: 0.15
ampSustain: 0.0
ampRelease: 0.05

filterAttack: 0.001
filterDecay: 0.12
filterSustain: 0.0
filterRelease: 0.05
---


PRESET: FX_Sweep
Priority: 3
DefaultNote: 36 (C1)
---
osc1Level: 1.0
osc2Level: 0.5
noiseLevel: 0.2
osc2Semitones: 7
osc2Detune: 0.1
osc1Waveform: Saw (1)
osc2Waveform: Square (2)

filterCutoff: 200
filterResonance: 0.9
filterEnvAmount: 8000

ampAttack: 0.01
ampDecay: 1.5
ampSustain: 0.0
ampRelease: 0.5

filterAttack: 0.01
filterDecay: 1.2
filterSustain: 0.0
filterRelease: 0.4
---


PRESET: FX_Wind
Priority: 1
DefaultNote: 60 (C3)
---
osc1Level: 0.0
osc2Level: 0.0
noiseLevel: 1.0
osc2Semitones: 0
osc2Detune: 0.0
osc1Waveform: Triangle (3)
osc2Waveform: Triangle (3)

filterCutoff: 1500
filterResonance: 0.3
filterEnvAmount: 1000

ampAttack: 0.5
ampDecay: 0.3
ampSustain: 0.8
ampRelease: 1.0

filterAttack: 0.6
filterDecay: 0.4
filterSustain: 0.5
filterRelease: 0.8
---
";
}