// GridSaveData.cs
using System;
using System.Collections.Generic;

namespace TriGrid.Core
{
    /// <summary>
    /// Serializable snapshot of the grid state for save/load.
    /// </summary>
    [Serializable]
    public class GridSaveData
    {
        [Serializable]
        public struct FaceEntry
        {
            public int Q;
            public int R;
            public int Orient; // 0=L, 1=R

            public FaceEntry(FaceCoord f)
            {
                Q = f.Q;
                R = f.R;
                Orient = (int)f.Orient;
            }

            public FaceCoord ToFaceCoord() => new FaceCoord(Q, R, (Orientation)Orient);
        }

        [Serializable]
        public struct EmitterEntry
        {
            public int U;
            public int V;
            public EmitterConfig Config;

            public EmitterEntry(VertexCoord v, EmitterConfig config)
            {
                U = v.U;
                V = v.V;
                Config = config;
            }
        }

        [Serializable]
        public struct ReflectorEntry
        {
            public int U;
            public int V;
            public int Direction;

            public ReflectorEntry(VertexCoord v, int direction)
            {
                U = v.U;
                V = v.V;
                Direction = direction;
            }
        }

        [Serializable]
        public struct NoteEntry
        {
            public int U;
            public int V;
            public int NoteIndex;

            public NoteEntry(VertexCoord v, int note)
            {
                U = v.U;
                V = v.V;
                NoteIndex = note;
            }
        }

        public int MinQ;
        public int MinR;
        public int MaxQ;
        public int MaxR;
        public List<FaceEntry> Faces = new List<FaceEntry>();
        public List<EmitterEntry> Emitters = new List<EmitterEntry>();
        public List<ReflectorEntry> Reflectors = new List<ReflectorEntry>();
        public List<NoteEntry> Notes = new List<NoteEntry>();

        /// <summary>
        /// Create a save snapshot from the current grid state.
        /// </summary>
        public static GridSaveData FromGrid(TriGridData grid)
        {
            var data = new GridSaveData
            {
                MinQ = grid.MinQ,
                MinR = grid.MinR,
                MaxQ = grid.MaxQ,
                MaxR = grid.MaxR
            };

            foreach (var face in grid.ActiveFaces)
            {
                data.Faces.Add(new FaceEntry(face));
            }

            foreach (var kvp in grid.AllVertices)
            {
                var v = kvp.Key;
                var state = kvp.Value;

                if (state.HasEmitter)
                    data.Emitters.Add(new EmitterEntry(v, state.EmitterConfig));

                if (state.HasReflector)
                    data.Reflectors.Add(new ReflectorEntry(v, state.ReflectorDirection));

                if (state.AssignedNote >= 0)
                    data.Notes.Add(new NoteEntry(v, state.AssignedNote));
            }

            return data;
        }

        /// <summary>
        /// Restore grid state from a save snapshot.
        /// </summary>
        public static TriGridData ToGrid(GridSaveData data)
        {
            var grid = new TriGridData(data.MinQ, data.MinR, data.MaxQ, data.MaxR);

            foreach (var faceEntry in data.Faces)
            {
                grid.AddFace(faceEntry.ToFaceCoord());
            }

            foreach (var emitter in data.Emitters)
            {
                grid.PlaceEmitter(new VertexCoord(emitter.U, emitter.V), emitter.Config);
            }

            foreach (var reflector in data.Reflectors)
            {
                grid.PlaceReflector(new VertexCoord(reflector.U, reflector.V), reflector.Direction);
            }

            foreach (var note in data.Notes)
            {
                grid.AssignNote(new VertexCoord(note.U, note.V), note.NoteIndex);
            }

            return grid;
        }
    }
}
