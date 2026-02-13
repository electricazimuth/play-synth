// EventScheduler.cs - Main scheduler with comprehensive improvements
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Azimuth.DES
{
    // Non-generic base interface
    public interface IEventHandler 
    {
        bool IsValid { get; } // Validity check for destroyed Unity objects
    }

    // Generic interface with priority support
    public interface IEventHandler<T> : IEventHandler where T : EventScheduler.Event
    {
        void OnEvent(T ev);
        int Priority => 0; // Default priority, lower numbers execute first
    }

    public static partial class EventScheduler
    {
        // Core collections
        private static readonly PriorityQueue<Event> eventQueue = new PriorityQueue<Event>();
        private static readonly Dictionary<Type, Stack<Event>> eventPools = new Dictionary<Type, Stack<Event>>();
        private static readonly Dictionary<Type, List<IEventHandler>> subscribers = new Dictionary<Type, List<IEventHandler>>();
        
        // Caching for performance
        private static readonly Dictionary<Type, List<Type>> cachedEventTypes = new Dictionary<Type, List<Type>>();
        
        // Statistics and debugging
        private static readonly Dictionary<Type, int> eventStats = new Dictionary<Type, int>();
        private static bool enableStats = false;
        private static bool enableLogging = false;
        
        // Performance monitoring
        private static int maxEventsPerTick = 100; // Prevent frame drops
        private static float maxTickTime = 0.016f; // 16ms budget
        private const int MAX_POOL_SIZE = 50; // Prevent unbounded pool growth
        
        // Thread safety
        private static readonly object schedulerLock = new object();
        
        // Event filtering and recording
        private static Func<Event, bool> eventFilter = null;
        private static EventRecorder recorder = null;
        
        public static int QueuedEventCount 
        { 
            get 
            { 
                lock (schedulerLock) 
                { 
                    return eventQueue.Count; 
                } 
            } 
        }
        
        public static bool EnableStatistics { get => enableStats; set => enableStats = value; }
        public static bool EnableLogging { get => enableLogging; set => enableLogging = value; }
        public static Func<Event, bool> EventFilter { get => eventFilter; set => eventFilter = value; }
        public static EventRecorder Recorder { get => recorder; set => recorder = value; }

        #region Event Creation and Scheduling
        
        /// <summary>
        /// Creates a new event of type T, using pooling when possible
        /// </summary>
        public static T New<T>() where T : Event, new()
        {
            lock (schedulerLock)
            {
                var eventType = typeof(T);
                
                if (eventPools.TryGetValue(eventType, out var pool) && pool.Count > 0)
                {
                    var pooledEvent = (T)pool.Pop();
                    if (enableLogging)
                        Debug.Log($"Reused pooled event: {eventType.Name}");
                    return pooledEvent;
                }
                
                // Create new event and ensure pool exists
                if (!eventPools.ContainsKey(eventType))
                {
                    eventPools[eventType] = new Stack<Event>();
                }
                
                var newEvent = new T();
                if (enableLogging)
                    Debug.Log($"Created new event: {eventType.Name}");
                return newEvent;
            }
        }

        /// <summary>
        /// Schedules an event to be executed at a specific time
        /// </summary>
        public static void Schedule<T>(T ev, float delay = 0f) where T : Event
        {
            if (ev == null)
            {
                Debug.LogError("Cannot schedule null event");
                return;
            }

            lock (schedulerLock)
            {
                ev.tick = Time.time + delay;
                eventQueue.Push(ev);
                
                if (enableStats)
                {
                    var eventType = ev.GetType();
                    eventStats[eventType] = eventStats.GetValueOrDefault(eventType, 0) + 1;
                }
                
                if (enableLogging)
                    Debug.Log($"Scheduled {ev.GetType().Name} for tick {ev.tick}");
            }
        }

        /// <summary>
        /// Immediately dispatch an event without scheduling
        /// </summary>
        public static void Dispatch<T>(T ev) where T : Event
        {
            if (ev == null)
            {
                Debug.LogError("Cannot dispatch null event");
                return;
            }

            DispatchEventToSubscribers(ev);
            
            // Process chained events
            ev.ProcessChainedEvents();
            
            // Clean up and pool the event
            ev.Cleanup();
            PoolEvent(ev);
        }

        #endregion

        #region Subscription Management

        /// <summary>
        /// Subscribe to events with automatic sorting by priority
        /// </summary>
        public static void Subscribe<T>(IEventHandler<T> listener) where T : Event
        {
            if (listener == null)
            {
                Debug.LogError("Cannot subscribe null listener");
                return;
            }

            lock (schedulerLock)
            {
                var eventType = typeof(T);
                if (!subscribers.ContainsKey(eventType))
                {
                    subscribers[eventType] = new List<IEventHandler>();
                }

                var list = subscribers[eventType];
                if (!list.Contains(listener))
                {
                    list.Add(listener);
                    // Sort by priority (lower numbers first)
                    list.Sort((a, b) => ((IEventHandler<T>)a).Priority.CompareTo(((IEventHandler<T>)b).Priority));
                    
                    if (enableLogging)
                        Debug.Log($"Subscribed {listener.GetType().Name} to {eventType.Name}");
                }
            }
        }

        /// <summary>
        /// Unsubscribe from events
        /// </summary>
        public static void Unsubscribe<T>(IEventHandler<T> listener) where T : Event
        {
            if (listener == null) return;

            lock (schedulerLock)
            {
                var eventType = typeof(T);
                if (subscribers.TryGetValue(eventType, out var list))
                {
                    list.Remove(listener);
                    if (enableLogging)
                        Debug.Log($"Unsubscribed {listener.GetType().Name} from {eventType.Name}");
                }
            }
        }

        /// <summary>
        /// Auto-subscribe MonoBehaviour components that implement IEventHandler (cached for performance)
        /// </summary>
        public static void AutoSubscribe(MonoBehaviour component)
        {
            if (component == null) return;

            var componentType = component.GetType();
            
            lock (schedulerLock)
            {
                if (!cachedEventTypes.TryGetValue(componentType, out var eventTypes))
                {
                    eventTypes = new List<Type>();
                    var interfaces = componentType.GetInterfaces();
                    
                    foreach (var interfaceType in interfaces)
                    {
                        if (interfaceType.IsGenericType && 
                            interfaceType.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                        {
                            eventTypes.Add(interfaceType.GetGenericArguments()[0]);
                        }
                    }
                    
                    cachedEventTypes[componentType] = eventTypes;
                }
                
                foreach (var eventType in eventTypes)
                {
                    var subscribeMethod = typeof(EventScheduler).GetMethod(nameof(Subscribe))
                        .MakeGenericMethod(eventType);
                    subscribeMethod.Invoke(null, new object[] { component });
                }
            }
        }

        /// <summary>
        /// Auto-unsubscribe MonoBehaviour components from all events
        /// </summary>
        public static void AutoUnsubscribe(MonoBehaviour component)
        {
            if (component == null) return;

            var componentType = component.GetType();
            
            lock (schedulerLock)
            {
                if (cachedEventTypes.TryGetValue(componentType, out var eventTypes))
                {
                    foreach (var eventType in eventTypes)
                    {
                        var unsubscribeMethod = typeof(EventScheduler).GetMethod(nameof(Unsubscribe))
                            .MakeGenericMethod(eventType);
                        unsubscribeMethod.Invoke(null, new object[] { component });
                    }
                }
            }
        }

        #endregion

        #region Processing

        /// <summary>
        /// Process events with performance safeguards
        /// </summary>
        public static EventTickResult Tick()
        {
            var startTime = Time.realtimeSinceStartup;
            var currentTime = Time.time;
            var processedCount = 0;

            while (true)
            {
                Event ev = null;
                
                lock (schedulerLock)
                {
                    if (eventQueue.Count == 0 || 
                        eventQueue.Peek().tick > currentTime ||
                        processedCount >= maxEventsPerTick ||
                        (Time.realtimeSinceStartup - startTime) >= maxTickTime)
                    {
                        break;
                    }
                    
                    ev = eventQueue.Pop();
                }

                // Check cancellation
                if (ev.IsCancelled)
                {
                    ev.Cleanup();
                    PoolEvent(ev);
                    continue;
                }

                // Check filter
                if (eventFilter != null && !eventFilter(ev))
                {
                    ev.Cleanup();
                    PoolEvent(ev);
                    continue;
                }

                // Check execution condition
                if (!ev.ShouldExecute())
                {
                    ev.Cleanup();
                    PoolEvent(ev);
                    continue;
                }

                processedCount++;

                try
                {
                    // Record if enabled
                    recorder?.RecordEvent(ev);
                    
                    DispatchEventToSubscribers(ev);
                    
                    // Process chained events
                    ev.ProcessChainedEvents();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error processing event {ev.GetType().Name}: {e}");
                }
                finally
                {
                    // Always clean up and pool, even if dispatch failed
                    ev.Cleanup();
                    PoolEvent(ev);
                }
            }

            // Warn about performance issues
            if (processedCount >= maxEventsPerTick)
            {
                Debug.LogWarning($"Hit max events per tick limit: {maxEventsPerTick}");
            }

            var elapsedTime = Time.realtimeSinceStartup - startTime;
            if (elapsedTime >= maxTickTime)
            {
                Debug.LogWarning($"Event processing took {elapsedTime:F4}s (budget: {maxTickTime:F4}s)");
            }

            int remainingCount;
            lock (schedulerLock)
            {
                remainingCount = eventQueue.Count;
            }

            return new EventTickResult
            {
                ProcessedCount = processedCount,
                RemainingCount = remainingCount,
                ElapsedTime = elapsedTime,
                HitEventLimit = processedCount >= maxEventsPerTick,
                HitTimeLimit = elapsedTime >= maxTickTime
            };
        }

        private static void DispatchEventToSubscribers<T>(T ev) where T : Event
        {
            var eventType = typeof(T);
            List<IEventHandler> listenersCopy = null;

            lock (schedulerLock)
            {
                if (!subscribers.TryGetValue(eventType, out var listeners) || listeners.Count == 0)
                    return;

                // Clean up invalid listeners first
                for (int i = listeners.Count - 1; i >= 0; i--)
                {
                    if (!listeners[i].IsValidHandler())
                    {
                        listeners.RemoveAt(i);
                        if (enableLogging)
                            Debug.Log($"Removed invalid listener from {eventType.Name}");
                    }
                }

                if (listeners.Count == 0)
                    return;

                // Create a copy to avoid holding lock during dispatch
                listenersCopy = new List<IEventHandler>(listeners);
            }

            // Dispatch to valid listeners without holding lock
            for (int i = 0; i < listenersCopy.Count; i++)
            {
                var listener = listenersCopy[i];
                try
                {
                    // Direct typed dispatch - no reflection, no boxing
                    ((IEventHandler<T>)listener).OnEvent(ev);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error dispatching {eventType.Name} to {listener.GetType().Name}: {e}");
                }
            }
        }

        // Non-generic version for dynamic dispatch (used by Dispatch method)
        private static void DispatchEventToSubscribers(Event ev)
        {
            var eventType = ev.GetType();
            List<IEventHandler> listenersCopy = null;

            lock (schedulerLock)
            {
                if (!subscribers.TryGetValue(eventType, out var listeners) || listeners.Count == 0)
                    return;

                // Clean up invalid listeners first
                for (int i = listeners.Count - 1; i >= 0; i--)
                {
                    if (!listeners[i].IsValidHandler())
                    {
                        listeners.RemoveAt(i);
                        if (enableLogging)
                            Debug.Log($"Removed invalid listener from {eventType.Name}");
                    }
                }

                if (listeners.Count == 0)
                    return;

                // Create a copy to avoid holding lock during dispatch
                listenersCopy = new List<IEventHandler>(listeners);
            }

            // Dispatch to valid listeners without holding lock
            for (int i = 0; i < listenersCopy.Count; i++)
            {
                var listener = listenersCopy[i];
                try
                {
                    // Use reflection only for non-generic Dispatch method
                    var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
                    var onEventMethod = handlerType.GetMethod("OnEvent");
                    onEventMethod?.Invoke(listener, new object[] { ev });
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error dispatching {eventType.Name} to {listener.GetType().Name}: {e}");
                }
            }
        }

        #endregion

        #region Pool Management

        private static void PoolEvent(Event ev)
        {
            lock (schedulerLock)
            {
                var eventType = ev.GetType();
                if (eventPools.TryGetValue(eventType, out var pool))
                {
                    if (pool.Count < MAX_POOL_SIZE)
                    {
                        pool.Push(ev);
                    }
                    else
                    {
                        ev.Dispose(); // Let GC handle it
                    }
                }
            }
        }

        #endregion

        #region Management and Debugging

        /// <summary>
        /// Clear all data - call during scene transitions
        /// </summary>
        public static void Clear()
        {
            lock (schedulerLock)
            {
                eventQueue.Clear();
                subscribers.Clear();
                eventStats.Clear();
                cachedEventTypes.Clear();
                
                // Optionally preserve pools across scenes for better performance
                // eventPools.Clear();
                
                if (enableLogging)
                    Debug.Log("EventScheduler cleared");
            }
        }

        /// <summary>
        /// Get statistics about event usage
        /// </summary>
        public static Dictionary<Type, int> GetEventStatistics()
        {
            lock (schedulerLock)
            {
                return new Dictionary<Type, int>(eventStats);
            }
        }

        /// <summary>
        /// Configure performance limits
        /// </summary>
        public static void SetPerformanceLimits(int maxEvents, float maxTime)
        {
            lock (schedulerLock)
            {
                maxEventsPerTick = maxEvents;
                maxTickTime = maxTime;
            }
        }

        /// <summary>
        /// Get debug information about current state
        /// </summary>
        public static string GetDebugInfo()
        {
            lock (schedulerLock)
            {
                var info = $"EventScheduler Debug Info:\n";
                info += $"Queued Events: {eventQueue.Count}\n";
                info += $"Subscriber Types: {subscribers.Count}\n";
                info += $"Pool Types: {eventPools.Count}\n";
                
                if (enableStats && eventStats.Count > 0)
                {
                    info += "Event Statistics:\n";
                    foreach (var kvp in eventStats)
                    {
                        info += $"  {kvp.Key.Name}: {kvp.Value}\n";
                    }
                }
                
                return info;
            }
        }

        #endregion
    }

    /// <summary>
    /// Result of event tick processing
    /// </summary>
    public struct EventTickResult
    {
        public int ProcessedCount;
        public int RemainingCount;
        public float ElapsedTime;
        public bool HitEventLimit;
        public bool HitTimeLimit;

        public override string ToString()
        {
            return $"Processed: {ProcessedCount}, Remaining: {RemainingCount}, Time: {ElapsedTime:F4}s, Limits: {(HitEventLimit ? "Events " : "")}{(HitTimeLimit ? "Time" : "")}";
        }
    }

    /// <summary>
    /// Extension methods for event handler validation
    /// </summary>
    public static class EventHandlerExtensions
    {
        public static bool IsValidHandler(this IEventHandler handler)
        {
            if (handler == null) return false;
            
            // Check if it's a Unity object that might be destroyed
            if (handler is UnityEngine.Object unityObj)
            {
                return unityObj != null; // Uses Unity's null check override
            }
            
            return true;
        }
    }
}
