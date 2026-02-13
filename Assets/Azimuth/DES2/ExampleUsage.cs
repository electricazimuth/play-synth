// ExampleUsage.cs - Demonstrates how to use the improved event system
using UnityEngine;
using Azimuth.DES;

namespace Azimuth.DES.Examples
{
    // ===== EXAMPLE 1: Define Custom Events =====
    
    public class PlayerDamagedEvent : EventScheduler.Event
    {
        public GameObject Player;
        public float Damage;
        public GameObject Attacker;
    }

    public class GameStateChangedEvent : EventScheduler.Event
    {
        public enum State { Menu, Playing, Paused, GameOver }
        public State NewState;
        public State PreviousState;
    }

    public class ScoreChangedEvent : EventScheduler.Event
    {
        public int NewScore;
        public int ScoreDelta;
    }

    // ===== EXAMPLE 2: Event Handler using EventHandlerBehaviour =====
    
    public class PlayerHealthUI : EventHandlerBehaviour, IEventHandler<PlayerDamagedEvent>
    {
        [SerializeField] private UnityEngine.UI.Text healthText;

        // Priority 0 = default, will execute first
        public int Priority => 0;

        public void OnEvent(PlayerDamagedEvent ev)
        {
            Debug.Log($"Player took {ev.Damage} damage from {ev.Attacker?.name ?? "unknown"}");
            UpdateHealthDisplay();
        }

        private void UpdateHealthDisplay()
        {
            // Update UI
            if (healthText != null)
            {
                healthText.text = $"Health: {GetPlayerHealth()}";
            }
        }

        private float GetPlayerHealth()
        {
            // Placeholder - get actual health from player
            return 100f;
        }
    }

    // ===== EXAMPLE 3: Multiple Event Handlers with Priority =====
    
    public class GameManager : EventHandlerBehaviour, 
        IEventHandler<GameStateChangedEvent>,
        IEventHandler<ScoreChangedEvent>
    {
        // Higher priority (executes later) for state changes
        int IEventHandler<GameStateChangedEvent>.Priority => 10;
        
        // Default priority for score
        int IEventHandler<ScoreChangedEvent>.Priority => 0;

        public void OnEvent(GameStateChangedEvent ev)
        {
            Debug.Log($"Game state changed: {ev.PreviousState} -> {ev.NewState}");
            
            switch (ev.NewState)
            {
                case GameStateChangedEvent.State.Playing:
                    Time.timeScale = 1f;
                    break;
                case GameStateChangedEvent.State.Paused:
                    Time.timeScale = 0f;
                    break;
            }
        }

        public void OnEvent(ScoreChangedEvent ev)
        {
            Debug.Log($"Score changed by {ev.ScoreDelta} to {ev.NewScore}");
        }
    }

    // ===== EXAMPLE 4: Using Events in Code =====
    
    public class Player : MonoBehaviour
    {
        [SerializeField] private float health = 100f;

        public void TakeDamage(float damage, GameObject attacker)
        {
            health -= damage;

            // Create and dispatch damage event
            var damageEvent = EventScheduler.New<PlayerDamagedEvent>();
            damageEvent.Player = gameObject;
            damageEvent.Damage = damage;
            damageEvent.Attacker = attacker;
            
            // Dispatch immediately
            EventScheduler.Dispatch(damageEvent);

            // Or schedule for later
            // EventScheduler.Schedule(damageEvent, 0.5f); // 0.5 second delay
        }

        public void Die()
        {
            // Chain multiple events together
            var damageEvent = EventScheduler.New<PlayerDamagedEvent>();
            damageEvent.Player = gameObject;
            damageEvent.Damage = health;
            
            // Chain a game state change to happen 1 second after death
            var stateEvent = damageEvent.ChainNew<GameStateChangedEvent>(1.0f);
            stateEvent.NewState = GameStateChangedEvent.State.GameOver;
            stateEvent.PreviousState = GameStateChangedEvent.State.Playing;
            
            EventScheduler.Schedule(damageEvent);
        }
    }

    // ===== EXAMPLE 5: Conditional Event Execution =====
    
    public class PowerUpSpawner : MonoBehaviour
    {
        public class PowerUpSpawnEvent : EventScheduler.Event
        {
            public Vector3 SpawnPosition;
            public string PowerUpType;
        }

        public void ScheduleConditionalSpawn(Vector3 position, string type)
        {
            var spawnEvent = EventScheduler.New<PowerUpSpawnEvent>();
            spawnEvent.SpawnPosition = position;
            spawnEvent.PowerUpType = type;
            
            // Only execute if game is still playing
            spawnEvent.ExecutionCondition = () => 
            {
                // Check game state, player count, etc.
                return Time.timeScale > 0 && FindObjectOfType<Player>() != null;
            };
            
            EventScheduler.Schedule(spawnEvent, 5.0f);
        }
    }

    // ===== EXAMPLE 6: Event Filtering for Debugging =====
    
    public class EventDebugger : MonoBehaviour
    {
        [SerializeField] private bool filterSpamEvents = true;

        private void Start()
        {
            if (filterSpamEvents)
            {
                // Filter out frequent events during debugging
                EventScheduler.EventFilter = (ev) =>
                {
                    // Block all score change events
                    if (ev is ScoreChangedEvent)
                        return false;
                    
                    return true;
                };
            }
        }

        private void OnDestroy()
        {
            // Clear filter
            EventScheduler.EventFilter = null;
        }
    }

    // ===== EXAMPLE 7: Event Recording and Replay =====
    
    public class EventRecordingManager : MonoBehaviour
    {
        private EventRecorder recorder;
        
        [SerializeField] private bool recordOnStart = false;
        [SerializeField] private KeyCode recordToggleKey = KeyCode.F9;
        [SerializeField] private KeyCode replayKey = KeyCode.F10;

        private void Start()
        {
            recorder = new EventRecorder();
            EventScheduler.Recorder = recorder;

            if (recordOnStart)
            {
                recorder.StartRecording();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(recordToggleKey))
            {
                if (recorder.IsRecording)
                {
                    recorder.StopRecording();
                    Debug.Log($"Stopped recording. {recorder.RecordCount} events recorded.");
                }
                else
                {
                    recorder.Clear();
                    recorder.StartRecording();
                    Debug.Log("Started recording events.");
                }
            }

            if (Input.GetKeyDown(replayKey))
            {
                Debug.Log("Replaying recorded events...");
                recorder.ReplayEvents();
            }
        }

        private void OnDestroy()
        {
            // Export recorded events to JSON for later analysis
            if (recorder != null && recorder.RecordCount > 0)
            {
                string json = recorder.ExportToJson();
                Debug.Log($"Exported event recording:\n{json}");
                
                // Could save to file here
                // System.IO.File.WriteAllText("event_recording.json", json);
            }
        }
    }

    // ===== EXAMPLE 8: Event Scheduler Manager (update loop) =====
    
    public class EventSchedulerManager : MonoBehaviour
    {
        [Header("Performance Settings")]
        [SerializeField] private int maxEventsPerFrame = 100;
        [SerializeField] private float maxTimePerFrame = 0.016f; // 16ms

        [Header("Debug")]
        [SerializeField] private bool enableLogging = false;
        [SerializeField] private bool enableStatistics = false;
        [SerializeField] private bool showDebugInfo = false;

        private void Awake()
        {
            // Configure event scheduler
            EventScheduler.SetPerformanceLimits(maxEventsPerFrame, maxTimePerFrame);
            EventScheduler.EnableLogging = enableLogging;
            EventScheduler.EnableStatistics = enableStatistics;
        }

        private void Update()
        {
            // Process events every frame
            var result = EventScheduler.Tick();

            if (showDebugInfo && (result.ProcessedCount > 0 || result.RemainingCount > 0))
            {
                Debug.Log($"Event Tick: {result}");
            }

            if (result.HitEventLimit || result.HitTimeLimit)
            {
                Debug.LogWarning($"Event processing limits hit! {result}");
            }
        }

        private void OnGUI()
        {
            if (showDebugInfo)
            {
                GUILayout.BeginArea(new Rect(10, 10, 300, 200));
                GUILayout.Label(EventScheduler.GetDebugInfo());
                GUILayout.EndArea();
            }
        }

        private void OnDestroy()
        {
            // Print statistics before shutdown
            if (enableStatistics)
            {
                var stats = EventScheduler.GetEventStatistics();
                Debug.Log("Event Statistics:");
                foreach (var kvp in stats)
                {
                    Debug.Log($"  {kvp.Key.Name}: {kvp.Value} events");
                }
            }

            // Clear scheduler
            EventScheduler.Clear();
        }
    }

    // ===== EXAMPLE 9: Manual Subscription (without EventHandlerBehaviour) =====
    
    public class AudioManager : MonoBehaviour, IEventHandler<PlayerDamagedEvent>
    {
        [SerializeField] private AudioClip damageSound;
        
        public int Priority => 5; // Execute after UI updates
        
        // Implement IsValid for Unity object lifetime checking
        bool IEventHandler.IsValid => this != null;

        private void OnEnable()
        {
            EventScheduler.Subscribe<PlayerDamagedEvent>(this);
        }

        private void OnDisable()
        {
            EventScheduler.Unsubscribe<PlayerDamagedEvent>(this);
        }

        public void OnEvent(PlayerDamagedEvent ev)
        {
            if (damageSound != null)
            {
                AudioSource.PlayClipAtPoint(damageSound, ev.Player.transform.position);
            }
        }
    }
}
