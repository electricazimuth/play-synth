# Play synth

A native C# retro style synthesizer. It's currently fairly basic (waveform -> oscilators + filters).
It's optimised for mobile soundscape - just stereo with a single master audio creator "Centralized Uber-Synth" architecture.

## Synth Setup


TBD



## Grid Setup Instructions

### Dependencies
*   Install **DOTween** (free or Pro) from the Asset Store (used for pulse animation).
*   Ensure you have the **Unity Test Framework** package installed (for the included tests).

### Scene Setup
*   Create an empty GameObject named `GridSystem`.
*   Attach the **`GridController`** component to it.
*   Attach the **`GridInputHandler`** component.
*   Attach the **`GridVisualManager`** component.
*   Attach the **`PulseVisualManager`** component.
*   Attach the **`BeatClock`** component.
*   *(Optional)* Attach **`GridDebugVisualizer`** for seeing the grid lines in the Scene view.


### Wiring
*   In the Inspector for `GridController`, drag the other components (`InputHandler`, `VisualManager`, etc.) into their respective slots.
*   Create **Materials** for Faces, Edges, Vertices, and Pulses (standard URP/Built-in shaders with colors) and assign them to the `GridVisualManager` slots.

