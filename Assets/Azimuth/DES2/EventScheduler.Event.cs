// EventScheduler.Event.cs - Enhanced event base class
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Azimuth.DES
{
    public static partial class EventScheduler
    {
        /// <summary>
        /// Base event class with enhanced features
        /// </summary>
        [System.Serializable]
        public abstract class Event : IComparable<Event>, IDisposable
        {
            public float tick { get; internal set; }
            private bool isDisposed = false;
            
            // Optional event metadata
            public string EventId { get; set; } = System.Guid.NewGuid().ToString();
            public object Source { get; set; }
            public bool IsCancelled { get; private set; }
            
            // Event chaining support
            private List<(Event nextEvent, float additionalDelay)> chainedEvents;
            
            // Conditional execution support
            public Func<bool> ExecutionCondition { get; set; }

            public int CompareTo(Event other)
            {
                if (other == null) return 1;
                return tick.CompareTo(other.tick);
            }

            /// <summary>
            /// Cancel this event (prevents processing if not yet executed)
            /// </summary>
            public void Cancel()
            {
                IsCancelled = true;
            }

            /// <summary>
            /// Allows an external (editor) assembly to set the tick time.
            /// This is the public 'doorway' for the editor assembly to set the value.
            /// </summary>
            public void SetScheduleTime(float time)
            {
                this.tick = time;
            }

            /// <summary>
            /// Chain another event to execute after this one
            /// </summary>
            public void Chain<T>(T nextEvent, float additionalDelay = 0f) where T : Event
            {
                if (nextEvent == null)
                {
                    Debug.LogError("Cannot chain null event");
                    return;
                }

                if (chainedEvents == null)
                    chainedEvents = new List<(Event, float)>();
                
                chainedEvents.Add((nextEvent, additionalDelay));
            }

            /// <summary>
            /// Chain a new event of type T to execute after this one
            /// </summary>
            public T ChainNew<T>(float additionalDelay = 0f) where T : Event, new()
            {
                var nextEvent = EventScheduler.New<T>();
                Chain(nextEvent, additionalDelay);
                return nextEvent;
            }

            /// <summary>
            /// Process all chained events (called internally by scheduler)
            /// </summary>
            internal void ProcessChainedEvents()
            {
                if (chainedEvents != null && chainedEvents.Count > 0)
                {
                    foreach (var (nextEvent, delay) in chainedEvents)
                    {
                        if (!nextEvent.IsCancelled && !nextEvent.isDisposed)
                        {
                            EventScheduler.Schedule(nextEvent, delay);
                        }
                    }
                    chainedEvents.Clear();
                }
            }

            /// <summary>
            /// Reset event state for pooling reuse
            /// </summary>
            protected internal virtual void Cleanup()
            {
                Source = null;
                IsCancelled = false;
                tick = 0f;
                ExecutionCondition = null;
                
                if (chainedEvents != null)
                {
                    chainedEvents.Clear();
                }
                
                // Regenerate EventId for new use
                EventId = System.Guid.NewGuid().ToString();
            }

            /// <summary>
            /// Template method for event validation
            /// </summary>
            public virtual bool IsValid()
            {
                return !IsCancelled && !isDisposed;
            }

            /// <summary>
            /// Check if this event should execute (called before dispatch)
            /// </summary>
            public virtual bool ShouldExecute()
            {
                return !IsCancelled && 
                       !isDisposed && 
                       (ExecutionCondition == null || ExecutionCondition());
            }

            /// <summary>
            /// IDisposable implementation for proper cleanup
            /// </summary>
            public void Dispose()
            {
                if (!isDisposed)
                {
                    Cleanup();
                    isDisposed = true;
                }
            }

            public override string ToString()
            {
                return $"{GetType().Name}[{EventId[..8]}] at {tick:F3}";
            }
        }
    }
}
