// EventRecorder.cs - Record and replay events for debugging
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Azimuth.DES
{
    /// <summary>
    /// Records events for debugging, analysis, and replay functionality
    /// </summary>
    public class EventRecorder
    {
        private List<EventRecord> history = new List<EventRecord>();
        private bool isRecording = false;
        private int maxRecordCount = 1000; // Prevent unbounded memory usage

        public bool IsRecording => isRecording;
        public int RecordCount => history.Count;
        public int MaxRecordCount 
        { 
            get => maxRecordCount; 
            set => maxRecordCount = Mathf.Max(1, value); 
        }

        /// <summary>
        /// Start recording events
        /// </summary>
        public void StartRecording()
        {
            isRecording = true;
            Debug.Log("EventRecorder: Started recording");
        }

        /// <summary>
        /// Stop recording events
        /// </summary>
        public void StopRecording()
        {
            isRecording = false;
            Debug.Log($"EventRecorder: Stopped recording. Total events: {history.Count}");
        }

        /// <summary>
        /// Record an event
        /// </summary>
        public void RecordEvent(EventScheduler.Event ev)
        {
            if (!isRecording) return;

            try
            {
                var record = new EventRecord
                {
                    EventType = ev.GetType(),
                    EventTypeName = ev.GetType().FullName,
                    Tick = ev.tick,
                    RealTime = Time.time,
                    EventId = ev.EventId,
                    Source = ev.Source,
                    EventData = SerializeEvent(ev)
                };

                history.Add(record);

                // Enforce max record limit
                if (history.Count > maxRecordCount)
                {
                    history.RemoveAt(0); // Remove oldest record
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"EventRecorder: Failed to record event {ev.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>
        /// Clear all recorded events
        /// </summary>
        public void Clear()
        {
            history.Clear();
            Debug.Log("EventRecorder: Cleared all records");
        }

        /// <summary>
        /// Get all recorded events
        /// </summary>
        public List<EventRecord> GetHistory()
        {
            return new List<EventRecord>(history);
        }

        /// <summary>
        /// Get events of a specific type
        /// </summary>
        public List<EventRecord> GetEventsOfType<T>() where T : EventScheduler.Event
        {
            var results = new List<EventRecord>();
            var targetType = typeof(T);

            for (int i = 0; i < history.Count; i++)
            {
                if (history[i].EventType == targetType)
                {
                    results.Add(history[i]);
                }
            }

            return results;
        }

        /// <summary>
        /// Get events within a time range
        /// </summary>
        public List<EventRecord> GetEventsInTimeRange(float startTick, float endTick)
        {
            var results = new List<EventRecord>();

            for (int i = 0; i < history.Count; i++)
            {
                var record = history[i];
                if (record.Tick >= startTick && record.Tick <= endTick)
                {
                    results.Add(record);
                }
            }

            return results;
        }

        /// <summary>
        /// Replay all recorded events
        /// </summary>
        public void ReplayEvents()
        {
            Debug.Log($"EventRecorder: Replaying {history.Count} events");

            for (int i = 0; i < history.Count; i++)
            {
                var record = history[i];
                try
                {
                    var ev = DeserializeEvent(record);
                    if (ev != null)
                    {
                        EventScheduler.Schedule(ev, record.Tick - Time.time);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"EventRecorder: Failed to replay event {record.EventTypeName}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Replay events of a specific type
        /// </summary>
        public void ReplayEventsOfType<T>() where T : EventScheduler.Event
        {
            var eventsToReplay = GetEventsOfType<T>();
            Debug.Log($"EventRecorder: Replaying {eventsToReplay.Count} events of type {typeof(T).Name}");

            foreach (var record in eventsToReplay)
            {
                try
                {
                    var ev = DeserializeEvent(record);
                    if (ev != null)
                    {
                        EventScheduler.Schedule(ev, record.Tick - Time.time);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"EventRecorder: Failed to replay event {record.EventTypeName}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Export history to JSON string
        /// </summary>
        public string ExportToJson()
        {
            try
            {
                var wrapper = new EventRecordListWrapper { records = history };
                return JsonUtility.ToJson(wrapper, true);
            }
            catch (Exception e)
            {
                Debug.LogError($"EventRecorder: Failed to export to JSON: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Import history from JSON string
        /// </summary>
        public void ImportFromJson(string json)
        {
            try
            {
                var wrapper = JsonUtility.FromJson<EventRecordListWrapper>(json);
                history = wrapper.records ?? new List<EventRecord>();
                Debug.Log($"EventRecorder: Imported {history.Count} event records");
            }
            catch (Exception e)
            {
                Debug.LogError($"EventRecorder: Failed to import from JSON: {e.Message}");
            }
        }

        /// <summary>
        /// Get statistics about recorded events
        /// </summary>
        public Dictionary<Type, int> GetEventStatistics()
        {
            var stats = new Dictionary<Type, int>();

            for (int i = 0; i < history.Count; i++)
            {
                var eventType = history[i].EventType;
                if (eventType != null)
                {
                    stats[eventType] = stats.GetValueOrDefault(eventType, 0) + 1;
                }
            }

            return stats;
        }

        /// <summary>
        /// Serialize event to string (override for custom serialization)
        /// </summary>
        protected virtual string SerializeEvent(EventScheduler.Event ev)
        {
            try
            {
                return JsonUtility.ToJson(ev);
            }
            catch
            {
                // Fallback to simple string representation if JSON fails
                return ev.ToString();
            }
        }

        /// <summary>
        /// Deserialize event from record (override for custom deserialization)
        /// </summary>
        protected virtual EventScheduler.Event DeserializeEvent(EventRecord record)
        {
            try
            {
                if (record.EventType == null)
                {
                    Debug.LogWarning($"EventRecorder: Cannot deserialize event - type is null");
                    return null;
                }

                var newMethod = typeof(EventScheduler).GetMethod(nameof(EventScheduler.New))
                    .MakeGenericMethod(record.EventType);
                var ev = (EventScheduler.Event)newMethod.Invoke(null, null);

                if (!string.IsNullOrEmpty(record.EventData))
                {
                    JsonUtility.FromJsonOverwrite(record.EventData, ev);
                }

                ev.Source = record.Source;
                return ev;
            }
            catch (Exception e)
            {
                Debug.LogError($"EventRecorder: Failed to deserialize event: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Represents a recorded event
    /// </summary>
    [Serializable]
    public class EventRecord
    {
        public Type EventType;
        public string EventTypeName;
        public float Tick;
        public float RealTime;
        public string EventId;
        public object Source;
        public string EventData;

        public override string ToString()
        {
            return $"{EventTypeName} at {Tick:F3} (Real: {RealTime:F3})";
        }
    }

    /// <summary>
    /// Wrapper for JSON serialization of event record list
    /// </summary>
    [Serializable]
    internal class EventRecordListWrapper
    {
        public List<EventRecord> records;
    }
}
