// EventTrigger.cs - Generic event trigger for Unity UI/Inspector
using UnityEngine;
using UnityEngine.Events;
using System;
using System.Reflection;

namespace Azimuth.DES
{
    /// <summary>
    /// Generic event trigger that can be configured in the Unity Inspector.
    /// Supports triggering any event type without hard-coded dependencies.
    /// </summary>
    public class EventTrigger : MonoBehaviour
    {
        [Header("Event Configuration")]
        [Tooltip("Full type name of the event to trigger (e.g., 'Azimuth.DES.CowbellEvent')")]
        [SerializeField] private string eventTypeName = "";
        
        [Tooltip("Delay before triggering the event (in seconds)")]
        [SerializeField] private float delay = 0f;
        
        [Tooltip("Should this event repeat automatically?")]
        [SerializeField] private bool repeat = false;
        
        [Tooltip("Interval between repeats (only used if repeat is true)")]
        [SerializeField] private float repeatInterval = 1f;
        
        [Header("Callbacks")]
        [SerializeField] private UnityEvent onEventTriggered;
        
        private Type cachedEventType;
        private MethodInfo cachedNewMethod;
        private MethodInfo cachedScheduleMethod;
        private bool isRepeating = false;

        private void Awake()
        {
            CacheEventType();
        }

        private void OnEnable()
        {
            if (repeat && !isRepeating)
            {
                InvokeRepeating(nameof(TriggerEvent), delay, repeatInterval);
                isRepeating = true;
            }
        }

        private void OnDisable()
        {
            if (isRepeating)
            {
                CancelInvoke(nameof(TriggerEvent));
                isRepeating = false;
            }
        }

        /// <summary>
        /// Cache the event type and methods for better performance
        /// </summary>
        private void CacheEventType()
        {
            if (string.IsNullOrEmpty(eventTypeName))
                return;

            cachedEventType = Type.GetType(eventTypeName);
            
            if (cachedEventType == null)
            {
                Debug.LogError($"EventTrigger: Could not find event type '{eventTypeName}'. Make sure to include the full namespace.");
                return;
            }

            if (!typeof(EventScheduler.Event).IsAssignableFrom(cachedEventType))
            {
                Debug.LogError($"EventTrigger: Type '{eventTypeName}' is not an Event type.");
                cachedEventType = null;
                return;
            }

            // Cache the New<T> method
            cachedNewMethod = typeof(EventScheduler).GetMethod(nameof(EventScheduler.New))
                ?.MakeGenericMethod(cachedEventType);

            // Cache the Schedule<T> method
            cachedScheduleMethod = typeof(EventScheduler).GetMethod(nameof(EventScheduler.Schedule))
                ?.MakeGenericMethod(cachedEventType);

            if (cachedNewMethod == null || cachedScheduleMethod == null)
            {
                Debug.LogError($"EventTrigger: Failed to cache methods for event type '{eventTypeName}'");
                cachedEventType = null;
            }
        }

        /// <summary>
        /// Trigger the configured event. Can be called from Unity Events.
        /// </summary>
        public void TriggerEvent()
        {
            if (cachedEventType == null)
            {
                CacheEventType();
                if (cachedEventType == null)
                    return;
            }

            try
            {
                // Create new event instance
                var ev = (EventScheduler.Event)cachedNewMethod.Invoke(null, null);
                
                // Allow derived classes to configure the event
                ConfigureEvent(ev);
                
                // Schedule the event
                cachedScheduleMethod.Invoke(null, new object[] { ev, delay });
                
                // Invoke Unity Event callback
                onEventTriggered?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"EventTrigger: Error triggering event '{eventTypeName}': {e.Message}");
            }
        }

        /// <summary>
        /// Trigger event with a specific delay override
        /// </summary>
        public void TriggerEventWithDelay(float customDelay)
        {
            float originalDelay = delay;
            delay = customDelay;
            TriggerEvent();
            delay = originalDelay;
        }

        /// <summary>
        /// Override this in derived classes to configure event-specific properties
        /// </summary>
        protected virtual void ConfigureEvent(EventScheduler.Event ev)
        {
            // Base implementation does nothing
            // Derived classes can set event-specific properties here
        }

        /// <summary>
        /// Set the event type name at runtime
        /// </summary>
        public void SetEventType(string typeName)
        {
            eventTypeName = typeName;
            cachedEventType = null;
            CacheEventType();
        }

        /// <summary>
        /// Set the delay at runtime
        /// </summary>
        public void SetDelay(float newDelay)
        {
            delay = newDelay;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Validate event type when changed in inspector
            if (!string.IsNullOrEmpty(eventTypeName))
            {
                var testType = Type.GetType(eventTypeName);
                if (testType == null)
                {
                    Debug.LogWarning($"EventTrigger: Cannot find type '{eventTypeName}'. Make sure to include the full namespace.");
                }
                else if (!typeof(EventScheduler.Event).IsAssignableFrom(testType))
                {
                    Debug.LogWarning($"EventTrigger: Type '{eventTypeName}' is not an Event type.");
                }
            }
        }
#endif
    }


#if PLACEHOLDER_EVENTS
    /// <summary>
    /// Example: Specialized trigger for CowbellEvent
    /// </summary>
    public class CowbellEventTrigger : EventTrigger
    {
        [Header("Cowbell Specific")]
        [SerializeField] private bool useCurrentBeatTime = true;

        protected override void ConfigureEvent(EventScheduler.Event ev)
        {
            // This assumes CowbellEvent has a timeHit property
            // Adjust based on your actual CowbellEvent implementation
            if (useCurrentBeatTime && ev is CowbellEvent cowbellEvent)
            {
                // Only access FmodGameController if it exists
                if (FmodGameController.Instance != null)
                {
                    cowbellEvent.timeHit = FmodGameController.Instance.BeatTime;
                }
            }
        }
    }

// Placeholder for CowbellEvent if not defined elsewhere
// Remove this if you have the actual implementation
    public class CowbellEvent : EventScheduler.Event
    {
        public float timeHit;
    }

    public class FmodGameController : MonoBehaviour
    {
        public static FmodGameController Instance { get; private set; }
        public float BeatTime { get; set; }

        private void Awake()
        {
            Instance = this;
        }
    }
#endif

}
