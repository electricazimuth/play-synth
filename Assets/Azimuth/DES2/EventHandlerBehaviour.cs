// EventHandlerBehaviour.cs - Base class for MonoBehaviours that handle events
using UnityEngine;

namespace Azimuth.DES
{
    /// <summary>
    /// Base class for MonoBehaviours that handle events.
    /// Automatically subscribes/unsubscribes and implements IsValid.
    /// </summary>
    public abstract class EventHandlerBehaviour : MonoBehaviour, IEventHandler
    {
        [Header("Event Handler Settings")]
        [Tooltip("Automatically subscribe to events on Enable")]
        [SerializeField] protected bool autoSubscribe = true;

        [Tooltip("Automatically unsubscribe from events on Disable")]
        [SerializeField] protected bool autoUnsubscribe = true;

        // Implement IsValid to check if this MonoBehaviour is still valid
        bool IEventHandler.IsValid => this != null;

        protected virtual void OnEnable()
        {
            if (autoSubscribe)
            {
                EventScheduler.AutoSubscribe(this);
            }
        }

        protected virtual void OnDisable()
        {
            if (autoUnsubscribe)
            {
                EventScheduler.AutoUnsubscribe(this);
            }
        }

        /// <summary>
        /// Manually subscribe to events (useful if autoSubscribe is false)
        /// </summary>
        protected void Subscribe()
        {
            EventScheduler.AutoSubscribe(this);
        }

        /// <summary>
        /// Manually unsubscribe from events (useful if autoUnsubscribe is false)
        /// </summary>
        protected void Unsubscribe()
        {
            EventScheduler.AutoUnsubscribe(this);
        }
    }
}
