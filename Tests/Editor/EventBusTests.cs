using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DemonDragon.EventBus.Tests
{
    /// <summary>
    /// Thin wrapper that calls UnityEngine.TestTools.LogAssert.Expect via reflection,
    /// so this file compiles under VS/MSBuild without a hard dependency on UnityEngine.TestRunner.dll
    /// while still registering the expectation with Unity's test runner at runtime.
    /// </summary>
    internal static class LogExpect
    {
        private static readonly System.Reflection.MethodInfo _expectMethod;

        static LogExpect()
        {
            var assembly = System.Reflection.Assembly.Load("UnityEngine.TestRunner");
            var type = assembly.GetType("UnityEngine.TestTools.LogAssert");
            _expectMethod = type.GetMethod("Expect",
                new[] { typeof(LogType), typeof(System.Text.RegularExpressions.Regex) });
        }

        public static void Expect(LogType type, string regexPattern)
        {
            _expectMethod.Invoke(null, new object[]
            {
                type,
                new System.Text.RegularExpressions.Regex(regexPattern)
            });
        }
    }

    // ─── Log capture helper ──────────────────────────────────────────────────────

    /// <summary>
    /// Captures Unity log messages during a test so they can be asserted on
    /// without a dependency on UnityEngine.TestTools.LogAssert.
    /// Dispose (or call Stop) to detach the listener.
    /// </summary>
    internal sealed class LogCapture : IDisposable
    {
        private readonly List<(LogType type, string message)> _logs = new List<(LogType, string)>();

        public LogCapture()
        {
            Application.logMessageReceived += OnLog;
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            _logs.Add((type, message));
        }

        public bool HasLog(LogType type, string substring) =>
            _logs.Exists(l => l.type == type &&
                              l.message.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0);

        public void Dispose()
        {
            Application.logMessageReceived -= OnLog;
        }
    }

    // ─── Shared test fixtures ────────────────────────────────────────────────────

    internal class PlayerDiedEvent : IEvent
    {
        public int PlayerId;
        public string Reason;

        public void Reset()
        {
            PlayerId = 0;
            Reason = null;
        }
    }

    internal class ScoreChangedEvent : IEvent
    {
        public int Delta;

        public void Reset() => Delta = 0;
    }

    // ─── EventBus core tests ─────────────────────────────────────────────────────

    [TestFixture]
    [Category("EventBus")]
    internal class EventBusSubscribeTests
    {
        private EventBus _bus;

        [SetUp]
        public void SetUp() => _bus = new EventBus();

        // ── Subscribe / Publish ──────────────────────────────────────────────────

        [Test]
        public void Subscribe_And_Publish_HandlerIsInvoked()
        {
            bool invoked = false;
            _bus.Subscribe<PlayerDiedEvent>(_ => invoked = true);

            _bus.Publish(new PlayerDiedEvent());

            Assert.IsTrue(invoked);
        }

        [Test]
        public void Publish_PassesCorrectEventData_ToHandler()
        {
            PlayerDiedEvent received = null;
            _bus.Subscribe<PlayerDiedEvent>(e => received = e);

            var evt = new PlayerDiedEvent { PlayerId = 42, Reason = "headshot" };
            _bus.Publish(evt);

            // received points to same instance (not pooled, so no Reset was called)
            Assert.IsNotNull(received);
            Assert.AreEqual(42, received.PlayerId);
            Assert.AreEqual("headshot", received.Reason);
        }

        [Test]
        public void Subscribe_MultipleHandlers_AllAreInvoked()
        {
            int count = 0;
            _bus.Subscribe<PlayerDiedEvent>(_ => count++);
            _bus.Subscribe<PlayerDiedEvent>(_ => count++);
            _bus.Subscribe<PlayerDiedEvent>(_ => count++);

            _bus.Publish(new PlayerDiedEvent());

            Assert.AreEqual(3, count);
        }

        [Test]
        public void Subscribe_SameHandlerTwice_IsInvokedTwice()
        {
            int count = 0;
            Action<PlayerDiedEvent> handler = _ => count++;
            _bus.Subscribe(handler);
            _bus.Subscribe(handler);

            _bus.Publish(new PlayerDiedEvent());

            Assert.AreEqual(2, count);
        }

        [Test]
        public void Subscribe_NullHandler_LogsWarningAndDoesNotThrow()
        {
            using var logs = new LogCapture();

            Assert.DoesNotThrow(() => _bus.Subscribe<PlayerDiedEvent>(null));
            Assert.IsTrue(logs.HasLog(LogType.Warning, "null handler"),
                "Expected a warning about null handler");
        }

        [Test]
        public void Publish_NoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bus.Publish(new PlayerDiedEvent()));
        }

        [Test]
        public void Publish_DifferentEventTypes_OnlyMatchingHandlerInvoked()
        {
            bool playerDiedInvoked = false;
            bool scoreChangedInvoked = false;

            _bus.Subscribe<PlayerDiedEvent>(_ => playerDiedInvoked = true);
            _bus.Subscribe<ScoreChangedEvent>(_ => scoreChangedInvoked = true);

            _bus.Publish(new PlayerDiedEvent());

            Assert.IsTrue(playerDiedInvoked);
            Assert.IsFalse(scoreChangedInvoked);
        }

        // ── Unsubscribe ──────────────────────────────────────────────────────────

        [Test]
        public void Unsubscribe_HandlerIsNoLongerInvoked()
        {
            int count = 0;
            Action<PlayerDiedEvent> handler = _ => count++;
            _bus.Subscribe(handler);
            _bus.Unsubscribe(handler);

            _bus.Publish(new PlayerDiedEvent());

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Unsubscribe_OneOfMultipleHandlers_OthersStillInvoked()
        {
            int countA = 0, countB = 0;
            Action<PlayerDiedEvent> handlerA = _ => countA++;
            Action<PlayerDiedEvent> handlerB = _ => countB++;

            _bus.Subscribe(handlerA);
            _bus.Subscribe(handlerB);
            _bus.Unsubscribe(handlerA);

            _bus.Publish(new PlayerDiedEvent());

            Assert.AreEqual(0, countA);
            Assert.AreEqual(1, countB);
        }

        [Test]
        public void Unsubscribe_LastHandler_RemovesEntryFromSubscriptions()
        {
            Action<PlayerDiedEvent> handler = _ => { };
            _bus.Subscribe(handler);
            _bus.Unsubscribe(handler);

            // After removing last handler, GetSubscriberCount must be 0
            Assert.AreEqual(0, _bus.GetSubscriberCount<PlayerDiedEvent>());
        }

        [Test]
        public void Unsubscribe_NotSubscribedHandler_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bus.Unsubscribe<PlayerDiedEvent>(_ => { }));
        }

        [Test]
        public void Unsubscribe_NullHandler_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bus.Unsubscribe<PlayerDiedEvent>(null));
        }

        // ── Handler exceptions ───────────────────────────────────────────────────

        [Test]
        public void Publish_HandlerThrows_OtherHandlersStillInvoked()
        {
            bool secondHandlerInvoked = false;

            _bus.Subscribe<PlayerDiedEvent>(_ => throw new Exception("boom"));
            _bus.Subscribe<PlayerDiedEvent>(_ => secondHandlerInvoked = true);

            LogExpect.Expect(LogType.Error, ".*boom.*");
            _bus.Publish(new PlayerDiedEvent());

            Assert.IsTrue(secondHandlerInvoked);
        }

        // ── ClearAllSubscriptions ────────────────────────────────────────────────

        [Test]
        public void ClearAllSubscriptions_NoHandlersInvokedAfterClear()
        {
            bool invoked = false;
            _bus.Subscribe<PlayerDiedEvent>(_ => invoked = true);
            _bus.ClearAllSubscriptions();

            _bus.Publish(new PlayerDiedEvent());

            Assert.IsFalse(invoked);
        }

        [Test]
        public void ClearAllSubscriptions_SubscriberCountBecomesZero()
        {
            _bus.Subscribe<PlayerDiedEvent>(_ => { });
            _bus.Subscribe<ScoreChangedEvent>(_ => { });
            _bus.ClearAllSubscriptions();

            Assert.AreEqual(0, _bus.GetSubscriberCount<PlayerDiedEvent>());
            Assert.AreEqual(0, _bus.GetSubscriberCount<ScoreChangedEvent>());
        }
    }

    // ─── EventBus pool integration tests ────────────────────────────────────────

    [TestFixture]
    [Category("EventBus")]
    [Category("EventBus.Pool")]
    internal class EventBusPoolTests
    {
        private EventBus _bus;

        [SetUp]
        public void SetUp() => _bus = new EventBus();

        [Test]
        public void GetPooledEvent_ReturnsNonNullInstance()
        {
            var evt = _bus.GetPooledEvent<PlayerDiedEvent>();
            Assert.IsNotNull(evt);
        }

        [Test]
        public void GetPooledEvent_ThenPublish_InstanceIsReturnedToPool()
        {
            _bus.Subscribe<PlayerDiedEvent>(_ => { });

            var evt = _bus.GetPooledEvent<PlayerDiedEvent>();
            _bus.Publish(evt);

            // After Publish the pool should have exactly one recycled instance
            Assert.AreEqual(1, _bus.GetPoolSize<PlayerDiedEvent>());
        }

        [Test]
        public void Publish_PooledEvent_ResetsEventBeforeReturning()
        {
            PlayerDiedEvent captured = null;
            _bus.Subscribe<PlayerDiedEvent>(e => captured = e);

            var evt = _bus.GetPooledEvent<PlayerDiedEvent>();
            evt.PlayerId = 99;
            evt.Reason = "test";
            _bus.Publish(evt);

            // Get the same recycled instance back from the pool
            var recycled = _bus.GetPooledEvent<PlayerDiedEvent>();

            Assert.AreEqual(0, recycled.PlayerId, "Reset() should have zeroed PlayerId");
            Assert.IsNull(recycled.Reason, "Reset() should have cleared Reason");
        }

        [Test]
        public void Publish_NonPooledEvent_DoesNotAddToPool()
        {
            _bus.Subscribe<PlayerDiedEvent>(_ => { });
            _bus.Publish(new PlayerDiedEvent());

            Assert.AreEqual(0, _bus.GetPoolSize<PlayerDiedEvent>());
        }

        [Test]
        public void Publish_PooledEvent_NoDoubleReturn()
        {
            _bus.Subscribe<PlayerDiedEvent>(_ => { });

            var evt = _bus.GetPooledEvent<PlayerDiedEvent>();
            _bus.Publish(evt);           // returns to pool
            _bus.Publish(evt);           // second publish — must NOT double-return

            Assert.AreEqual(1, _bus.GetPoolSize<PlayerDiedEvent>());
        }

        [Test]
        public void ReturnToPool_ManualReturn_AfterPublishWithoutAutoReturn()
        {
            _bus.Subscribe<PlayerDiedEvent>(_ => { });

            var evt = _bus.GetPooledEvent<PlayerDiedEvent>();
            _bus.PublishWithoutAutoReturn(evt);

            Assert.AreEqual(0, _bus.GetPoolSize<PlayerDiedEvent>(), "Pool should still be empty before manual return");

            _bus.ReturnToPool(evt);

            Assert.AreEqual(1, _bus.GetPoolSize<PlayerDiedEvent>());
        }

        [Test]
        public void GetPooledEvent_AfterReturn_ReusesSameInstance()
        {
            _bus.Subscribe<PlayerDiedEvent>(_ => { });

            var first = _bus.GetPooledEvent<PlayerDiedEvent>();
            _bus.Publish(first);

            var second = _bus.GetPooledEvent<PlayerDiedEvent>();

            Assert.AreSame(first, second, "Pool should hand back the recycled instance");
        }

        [Test]
        public void PrewarmPool_FillsPoolWithCorrectCount()
        {
            _bus.PrewarmPool<PlayerDiedEvent>(5);

            Assert.AreEqual(5, _bus.GetPoolSize<PlayerDiedEvent>());
        }

        [Test]
        public void PrewarmPool_DoesNotExceedMaxPoolSize()
        {
            // Default max is 50; requesting 100 should cap at 50
            _bus.PrewarmPool<PlayerDiedEvent>(100);

            Assert.LessOrEqual(_bus.GetPoolSize<PlayerDiedEvent>(), 50);
        }

        [Test]
        public void ClearAllPools_EmptiesPool()
        {
            _bus.PrewarmPool<PlayerDiedEvent>(10);
            _bus.ClearAllPools();

            Assert.AreEqual(0, _bus.GetPoolSize<PlayerDiedEvent>());
        }

        [Test]
        public void ClearAllPools_PooledInstanceTrackingIsReset()
        {
            // Obtain a pooled event, clear pools, then publish the stale reference — must not throw or double-return
            _bus.Subscribe<PlayerDiedEvent>(_ => { });
            var evt = _bus.GetPooledEvent<PlayerDiedEvent>();
            _bus.ClearAllPools();

            Assert.DoesNotThrow(() => _bus.Publish(evt));
        }

        [Test]
        public void PublishWithoutAutoReturn_EventIsNotReturnedToPool()
        {
            _bus.Subscribe<PlayerDiedEvent>(_ => { });
            var evt = _bus.GetPooledEvent<PlayerDiedEvent>();

            _bus.PublishWithoutAutoReturn(evt);

            Assert.AreEqual(0, _bus.GetPoolSize<PlayerDiedEvent>());
        }
    }

    // ─── IEventBusDiagnostics tests ──────────────────────────────────────────────

    [TestFixture]
    [Category("EventBus")]
    [Category("EventBus.Diagnostics")]
    internal class EventBusDiagnosticsTests
    {
        private EventBus _bus;

        [SetUp]
        public void SetUp() => _bus = new EventBus();

        [Test]
        public void GetSubscriberCount_ReflectsCurrentSubscriptions()
        {
            Assert.AreEqual(0, _bus.GetSubscriberCount<PlayerDiedEvent>());

            Action<PlayerDiedEvent> h1 = _ => { };
            Action<PlayerDiedEvent> h2 = _ => { };
            _bus.Subscribe(h1);
            Assert.AreEqual(1, _bus.GetSubscriberCount<PlayerDiedEvent>());

            _bus.Subscribe(h2);
            Assert.AreEqual(2, _bus.GetSubscriberCount<PlayerDiedEvent>());

            _bus.Unsubscribe(h1);
            Assert.AreEqual(1, _bus.GetSubscriberCount<PlayerDiedEvent>());
        }

        [Test]
        public void GetSubscribedEventTypes_ReturnsOnlyTypesWithActiveSubscribers()
        {
            _bus.Subscribe<PlayerDiedEvent>(_ => { });
            _bus.Subscribe<ScoreChangedEvent>(_ => { });

            var types = new List<Type>(_bus.GetSubscribedEventTypes());

            Assert.Contains(typeof(PlayerDiedEvent), types);
            Assert.Contains(typeof(ScoreChangedEvent), types);
            Assert.AreEqual(2, types.Count);
        }

        [Test]
        public void GetAllPoolSizes_ReturnsCorrectSizes()
        {
            _bus.PrewarmPool<PlayerDiedEvent>(3);
            _bus.PrewarmPool<ScoreChangedEvent>(7);

            var sizes = _bus.GetAllPoolSizes();

            Assert.AreEqual(3, sizes[nameof(PlayerDiedEvent)]);
            Assert.AreEqual(7, sizes[nameof(ScoreChangedEvent)]);
        }

        [Test]
        public void ToString_ContainsSubscribedEventTypeAndPoolInfo()
        {
            _bus.Subscribe<PlayerDiedEvent>(_ => { });
            _bus.PrewarmPool<PlayerDiedEvent>(2);

            var str = _bus.ToString();

            StringAssert.Contains("PlayerDiedEvent", str);
            StringAssert.Contains("2", str);
        }
    }

    // ─── Dead handler cleanup tests ───────────────────────────────────────────────

    [TestFixture]
    [Category("EventBus")]
    [Category("EventBus.DeadHandlers")]
    internal class EventBusDeadHandlerTests
    {
        private EventBus _bus;

        [SetUp]
        public void SetUp() => _bus = new EventBus();

        /// <summary>
        /// Helper MonoBehaviour whose handler we can register, then destroy.
        /// </summary>
        private class HandlerBehaviour : MonoBehaviour
        {
            public int InvokeCount;
            public void OnPlayerDied(PlayerDiedEvent _) => InvokeCount++;
        }

        [Test]
        public void Publish_AfterHandlerOwnerDestroyed_SkipsDeadHandlerAndLogs()
        {
            var go = new GameObject("TestHandler");
            var mb = go.AddComponent<HandlerBehaviour>();

            _bus.Subscribe<PlayerDiedEvent>(mb.OnPlayerDied);

            // Destroy the MonoBehaviour owner
            UnityEngine.Object.DestroyImmediate(go);

            using var logs = new LogCapture();
            Assert.DoesNotThrow(() => _bus.Publish(new PlayerDiedEvent()));

            Assert.IsTrue(logs.HasLog(LogType.Warning, "Cleaned up destroyed handlers"),
                "Expected cleanup warning after dead handler detected");
            // After cleanup the dead handler must be gone
            Assert.AreEqual(0, _bus.GetSubscriberCount<PlayerDiedEvent>());
        }

        [Test]
        public void Publish_MixedLiveAndDeadHandlers_OnlyLiveHandlerInvoked()
        {
            var go = new GameObject("DeadHandler");
            var dead = go.AddComponent<HandlerBehaviour>();
            _bus.Subscribe<PlayerDiedEvent>(dead.OnPlayerDied);

            int liveCount = 0;
            _bus.Subscribe<PlayerDiedEvent>(_ => liveCount++);

            UnityEngine.Object.DestroyImmediate(go);

            using var logs = new LogCapture();
            _bus.Publish(new PlayerDiedEvent());

            Assert.IsTrue(logs.HasLog(LogType.Warning, "Cleaned up destroyed handlers"),
                "Expected cleanup warning after dead handler detected");

            Assert.AreEqual(0, dead.InvokeCount);
            Assert.AreEqual(1, liveCount);
        }
    }
}
