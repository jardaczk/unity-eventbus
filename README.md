# DemonDragon EventBus System

Lightweight, type-safe event bus for Unity with object pooling, automatic dead-handler cleanup, and runtime diagnostics.

## Installation

### Option A – Local package (same machine)

In the target project open `Packages/manifest.json` and add:

```json
{
  "dependencies": {
	"eu.demondragon.eventbus": "file:../../path/to/this/Packages/eu.demondragon.eventbus"
  }
}
```

### Option B – Git URL (recommended for sharing)

Push this repository to GitHub (or any git host), then add the package via **Window → Package Manager → + → Add package from git URL…**:

```
https://github.com/jardaczk/HRTest.git?path=Packages/eu.demondragon.eventbus
```

To pin a specific version use a tag:

```
https://github.com/jardaczk/HRTest.git?path=Packages/eu.demondragon.eventbus#v1.0.0
```

---

## Quick start

```csharp
using DemonDragon.EventBus;

// 1. Define an event
public class PlayerDiedEvent : IEvent
{
	public int PlayerId;
	public void Reset() => PlayerId = 0;
}

// 2. Create a bus (or inject via DI)
var bus = new EventBus();

// 3. Subscribe
bus.Subscribe<PlayerDiedEvent>(e => Debug.Log($"Player {e.PlayerId} died"));

// 4. Publish
bus.Publish(new PlayerDiedEvent { PlayerId = 1 });
```

### Pooled events (zero allocation)

```csharp
var evt = bus.GetPooledEvent<PlayerDiedEvent>();
evt.PlayerId = 42;
bus.Publish(evt); // automatically resets and returns to pool
```

### Manual pool control

```csharp
bus.PrewarmPool<PlayerDiedEvent>(20);
bus.PublishWithoutAutoReturn(evt); // publish without returning to pool
bus.ReturnToPool(evt);             // return manually later
```

### Diagnostics

```csharp
var diag = (IEventBusDiagnostics)bus;
Debug.Log(diag.GetSubscriberCount<PlayerDiedEvent>());
Debug.Log(string.Join(", ", diag.GetSubscribedEventTypes()));
foreach (var kv in diag.GetAllPoolSizes())
	Debug.Log($"{kv.Key}: {kv.Value}");
```

---

## Assembly reference

Add `eu.demondragon.eventbus.runtime` to your assembly definition's references.
# EventBus System

Lightweight, type-safe event bus for Unity with object pooling, automatic dead-handler cleanup, and runtime diagnostics.

## Installation

### Option A – Local package (same machine)

In the target project open `Packages/manifest.json` and add:

```json
{
  "dependencies": {
	"eu.demondragon.eventbus": "file:../../path/to/this/Packages/eu.demondragon.eventbus"
  }
}
```

### Option B – Git URL (recommended for sharing)

Push this repository to GitHub (or any git host), then add the package via **Window → Package Manager → + → Add package from git URL…**:

```
https://github.com/jardaczk/HRTest.git?path=Packages/eu.demondragon.eventbus
```

To pin a specific version use a tag:

```
https://github.com/jardaczk/HRTest.git?path=Packages/eu.demondragon.eventbus#v1.0.0
```

---

## Quick start

```csharp
using EventBusSystem;

// 1. Define an event
public class PlayerDiedEvent : IEvent
{
	public int PlayerId;
	public void Reset() => PlayerId = 0;
}

// 2. Create a bus (or inject via DI)
var bus = new EventBus();

// 3. Subscribe
bus.Subscribe<PlayerDiedEvent>(e => Debug.Log($"Player {e.PlayerId} died"));

// 4. Publish
bus.Publish(new PlayerDiedEvent { PlayerId = 1 });
```

### Pooled events (zero allocation)

```csharp
var evt = bus.GetPooledEvent<PlayerDiedEvent>();
evt.PlayerId = 42;
bus.Publish(evt); // automatically resets and returns to pool
```

### Manual pool control

```csharp
bus.PrewarmPool<PlayerDiedEvent>(20);
bus.PublishWithoutAutoReturn(evt); // publish without returning to pool
bus.ReturnToPool(evt);             // return manually later
```

### Diagnostics

```csharp
var diag = (IEventBusDiagnostics)bus;
Debug.Log(diag.GetSubscriberCount<PlayerDiedEvent>());
Debug.Log(string.Join(", ", diag.GetSubscribedEventTypes()));
foreach (var kv in diag.GetAllPoolSizes())
	Debug.Log($"{kv.Key}: {kv.Value}");
```

---

## Assembly reference

Add `eu.demondragon.eventbus.runtime` to your assembly definition's references.
