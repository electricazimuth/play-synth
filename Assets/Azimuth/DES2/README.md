# Azimuth Discrete Event System (DES) - Improved Version

A high-performance, thread-safe discrete event system for Unity3D with extensive improvements for production use.

## 🎯 Key Features

- **Object Pooling**: Automatic event pooling for zero-allocation event creation
- **Priority-Based Handling**: Subscribe with priority levels for ordered execution
- **Thread-Safe**: All operations properly locked for multi-threaded environments
- **Performance Safeguards**: Configurable limits on events per frame and processing time
- **Event Chaining**: Chain events to execute in sequence
- **Conditional Execution**: Events can have execution conditions
- **Event Recording**: Record and replay events for debugging
- **Event Filtering**: Filter events at runtime for debugging
- **MonoBehaviour Integration**: Base class for easy Unity integration
- **No Reflection in Hot Path**: Direct typed dispatch for maximum performance

## 📦 Installation

Copy all `.cs` files to your Unity project's `Scripts/` folder (or any folder under `Assets/`).

### Required Files:
- `EventScheduler.cs` - Main scheduler
- `EventScheduler.Event.cs` - Event base class
- `PriorityQueue.cs` - Min-heap priority queue
- `EventTrigger.cs` - Unity Inspector integration
- `EventRecorder.cs` - Event recording/replay
- `EventHandlerBehaviour.cs` - MonoBehaviour base class

### Optional Files:
- `ExampleUsage.cs` - Comprehensive usage examples

## 🚀 Quick Start

### 1. Set Up the Event Scheduler Manager

```csharp
using UnityEngine;
using Azimuth.DES;

public class EventSchedulerManager : MonoBehaviour
{
    private void Update()
    {
        // Process events every frame
        EventScheduler.Tick();
    }

    private void OnDestroy()
    {
        EventScheduler.Clear();
    }
}
```

### 2. Define Your Events

```csharp
public class PlayerDamagedEvent : EventScheduler.Event
{
    public GameObject Player;
    public float Damage;
    public GameObject Attacker;
}
```

### 3. Create Event Handlers

**Option A: Using EventHandlerBehaviour (Recommended)**

```csharp
public class HealthUI : EventHandlerBehaviour, IEventHandler<PlayerDamagedEvent>
{
    public int Priority => 0; // Lower = earlier execution
    
    public void OnEvent(PlayerDamagedEvent ev)
    {
        Debug.Log($"Player took {ev.Damage} damage!");
        UpdateHealthDisplay();
    }
}
```

**Option B: Manual Subscription**

```csharp
public class AudioManager : MonoBehaviour, IEventHandler<PlayerDamagedEvent>
{
    public int Priority => 5;
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
        PlayDamageSound();
    }
}
```

### 4. Dispatch Events

```csharp
// Immediate dispatch
var damageEvent = EventScheduler.New<PlayerDamagedEvent>();
damageEvent.Player = player;
damageEvent.Damage = 25f;
EventScheduler.Dispatch(damageEvent);

// Scheduled dispatch (delayed)
var delayedEvent = EventScheduler.New<PlayerDamagedEvent>();
delayedEvent.Damage = 10f;
EventScheduler.Schedule(delayedEvent, 2.0f); // Execute in 2 seconds
```

## 🎓 Advanced Features

### Event Chaining

Chain events to execute in sequence:

```csharp
var event1 = EventScheduler.New<DamageEvent>();
event1.Damage = 50f;

// Chain another event to happen 1 second later
var event2 = event1.ChainNew<ExplosionEvent>(1.0f);
event2.ExplosionRadius = 5f;

EventScheduler.Schedule(event1);
// event1 executes, then event2 executes 1 second after event1
```

### Conditional Execution

Events only execute if their condition is met:

```csharp
var spawnEvent = EventScheduler.New<EnemySpawnEvent>();
spawnEvent.ExecutionCondition = () => 
{
    return player.IsAlive && enemyCount < maxEnemies;
};
EventScheduler.Schedule(spawnEvent, 5.0f);
// Only spawns if player is alive and under enemy limit when scheduled time arrives
```

### Event Recording and Replay

Record events for debugging or replay systems:

```csharp
var recorder = new EventRecorder();
EventScheduler.Recorder = recorder;

recorder.StartRecording();
// ... play game ...
recorder.StopRecording();

// Replay all events
recorder.ReplayEvents();

// Export to JSON
string json = recorder.ExportToJson();
System.IO.File.WriteAllText("replay.json", json);
```

### Event Filtering

Filter events at runtime for debugging:

```csharp
// Block all UI update events during performance profiling
EventScheduler.EventFilter = (ev) => 
{
    return !(ev is UIUpdateEvent);
};
```

### Priority-Based Execution

Control execution order with priorities:

```csharp
public class UIHandler : EventHandlerBehaviour, IEventHandler<ScoreEvent>
{
    public int Priority => 0; // Execute first
    public void OnEvent(ScoreEvent ev) { UpdateUI(); }
}

public class AchievementChecker : EventHandlerBehaviour, IEventHandler<ScoreEvent>
{
    public int Priority => 10; // Execute after UI
    public void OnEvent(ScoreEvent ev) { CheckAchievements(); }
}
```

### Performance Configuration

```csharp
// Allow up to 200 events per frame, max 20ms processing time
EventScheduler.SetPerformanceLimits(200, 0.020f);

// Enable statistics tracking
EventScheduler.EnableStatistics = true;

// Enable debug logging
EventScheduler.EnableLogging = true;

// Get statistics
var stats = EventScheduler.GetEventStatistics();
foreach (var kvp in stats)
{
    Debug.Log($"{kvp.Key.Name}: {kvp.Value} events");
}
```

## 🔧 Unity Inspector Integration

Use `EventTrigger` component to trigger events from Unity UI:

```csharp
// In Inspector:
// - Attach EventTrigger component
// - Set Event Type Name: "YourNamespace.YourEvent"
// - Set Delay: 0.5
// - Assign to Button's OnClick()

// Or create specialized triggers:
public class DamageEventTrigger : EventTrigger
{
    [SerializeField] private float damageAmount = 10f;
    
    protected override void ConfigureEvent(EventScheduler.Event ev)
    {
        if (ev is DamageEvent damageEvent)
        {
            damageEvent.Damage = damageAmount;
        }
    }
}
```

## 📊 Performance Characteristics

| Operation | Time Complexity | Notes |
|-----------|----------------|-------|
| New<T>() | O(1) | Constant time with pooling |
| Schedule<T>() | O(log n) | Heap insertion |
| Dispatch<T>() | O(m) | m = number of subscribers |
| Tick() | O(k log n) | k = events processed |
| Subscribe<T>() | O(m log m) | Sorted insertion |

## 🎯 Best Practices

### ✅ DO:

- **Use EventHandlerBehaviour** for MonoBehaviours handling events
- **Schedule events** instead of polling for state changes
- **Use priorities** to control execution order
- **Pool events** by using `New<T>()` and `Schedule()`
- **Set execution conditions** for events that may become invalid
- **Use event chaining** for sequential logic
- **Configure performance limits** based on your frame budget

### ❌ DON'T:

- **Don't create events with `new`** - use `EventScheduler.New<T>()`
- **Don't forget to call Tick()** in your update loop
- **Don't leak subscribers** - always unsubscribe in OnDisable/OnDestroy
- **Don't schedule too many events** - monitor with statistics
- **Don't use events for every frame** - use direct calls for hot paths
- **Don't forget to call Clear()** during scene transitions

## 🐛 Debugging

### View Debug Info

```csharp
Debug.Log(EventScheduler.GetDebugInfo());
// Output:
// EventScheduler Debug Info:
// Queued Events: 15
// Subscriber Types: 8
// Pool Types: 5
// Event Statistics:
//   PlayerDamagedEvent: 42
//   ScoreChangedEvent: 156
```

### Monitor Performance

```csharp
var result = EventScheduler.Tick();
if (result.HitEventLimit || result.HitTimeLimit)
{
    Debug.LogWarning($"Performance issue: {result}");
}
```

### Record and Analyze Events

```csharp
recorder.StartRecording();
// ... gameplay ...
recorder.StopRecording();

var damageEvents = recorder.GetEventsOfType<PlayerDamagedEvent>();
Debug.Log($"Player was damaged {damageEvents.Count} times");

var eventsInRange = recorder.GetEventsInTimeRange(10f, 20f);
Debug.Log($"Events between t=10 and t=20: {eventsInRange.Count}");
```

## 🔄 Migration from Old System

### Old Code:
```csharp
// Old HeapQueue with confusing naming
var queue = new HeapQueue<Event>();
queue.Push(event); // Was using SiftDown (actually bubble up)
```

### New Code:
```csharp
// New PriorityQueue with clear naming
var queue = new PriorityQueue<Event>();
queue.Push(event); // Uses BubbleUp (clear intent)
```

### API Changes:
- `HeapQueue<T>` → `PriorityQueue<T>`
- `SiftDown()` → `BubbleUp()` (internal)
- `SiftUp()` → `BubbleDown()` (internal)
- `EventScheduler.Tick()` now returns `EventTickResult` instead of `int`
- Added `AutoUnsubscribe()` method
- Added `EventFilter` property
- Added `Recorder` property

## 📝 Architecture Overview

```
EventScheduler (static class)
├── PriorityQueue<Event>      - Min-heap for scheduled events
├── Event Pools                - Dictionary<Type, Stack<Event>>
├── Subscribers                - Dictionary<Type, List<IEventHandler>>
├── Cached Event Types         - Reflection cache for AutoSubscribe
└── EventRecorder              - Optional recording/replay

Event (abstract base class)
├── Scheduling info (tick, id)
├── Chained events
├── Execution conditions
└── Pooling/cleanup logic

IEventHandler<T>
├── OnEvent(T ev)              - Handle event
├── Priority                   - Execution order
└── IsValid                    - Lifetime checking
```

## 🧪 Testing

The system includes validation methods for testing:

```csharp
[Test]
public void TestPriorityQueue()
{
    var queue = new PriorityQueue<TestEvent>();
    // ... add events ...
    Assert.IsTrue(queue.ValidateHeap());
}

[Test]
public void TestEventChaining()
{
    var event1 = EventScheduler.New<TestEvent>();
    var event2 = event1.ChainNew<TestEvent>(1.0f);
    
    EventScheduler.Schedule(event1);
    // Verify event2 is scheduled 1 second after event1
}
```

## 📄 License

This is production-ready code with comprehensive improvements for the Azimuth project.

## 🤝 Contributing

When adding new features:
1. Maintain thread safety with appropriate locking
2. Add XML documentation for public APIs
3. Update this README with new features
4. Include usage examples
5. Test with performance profiler

## ⚡ Performance Tips

1. **Pool Size**: Monitor pool sizes - the default max is 50 per type
2. **Subscriber Count**: Keep subscriber lists small - use priorities to reduce redundant handlers
3. **Event Frequency**: Don't schedule thousands of events per frame
4. **Reflection**: The system caches reflection calls, but minimize dynamic event creation
5. **Allocations**: Use `New<T>()` for pooled events, avoid creating new event instances
6. **Statistics**: Disable in production builds for better performance

## 🔒 Thread Safety

All public APIs are thread-safe through the `schedulerLock`. Safe to call from:
- Unity main thread
- Async/await operations
- Unity Jobs System callbacks
- Background threads

Note: Event handlers execute on the main thread during `Tick()`.

---

**Version**: 2.0 (Improved)
**Last Updated**: 2026-02-13
**Compatibility**: Unity 2020.3+
