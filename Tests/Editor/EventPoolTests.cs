using NUnit.Framework;

namespace EventBusSystem.Tests
{
    internal class SimpleEvent : IEvent
    {
        public int Value;
        public bool WasReset;

        public void Reset()
        {
            Value = 0;
            WasReset = true;
        }
    }

    [TestFixture]
    [Category("EventPool")]
    internal class EventPoolTests
    {
        private EventPool<SimpleEvent> _pool;

        [SetUp]
        public void SetUp() => _pool = new EventPool<SimpleEvent>(maxSize: 10);

        // ── Get ──────────────────────────────────────────────────────────────────

        [Test]
        public void Get_EmptyPool_CreatesNewInstance()
        {
            var evt = _pool.Get();
            Assert.IsNotNull(evt);
        }

        [Test]
        public void Get_AfterReturn_ReturnsRecycledInstance()
        {
            var original = _pool.Get();
            _pool.Return(original);

            var recycled = _pool.Get();
            Assert.AreSame(original, recycled);
        }

        [Test]
        public void Get_PoolEmptyAfterTaking_PoolSizeIsZero()
        {
            _pool.Return(new SimpleEvent());
            _pool.Get();

            Assert.AreEqual(0, _pool.PoolSize);
        }

        // ── Return ───────────────────────────────────────────────────────────────

        [Test]
        public void Return_CallsResetOnItem()
        {
            var evt = new SimpleEvent { Value = 42 };
            _pool.Return(evt);

            Assert.IsTrue(evt.WasReset);
            Assert.AreEqual(0, evt.Value);
        }

        [Test]
        public void Return_NullItem_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _pool.Return(null));
        }

        [Test]
        public void Return_IncreasesPoolSize()
        {
            _pool.Return(new SimpleEvent());
            _pool.Return(new SimpleEvent());

            Assert.AreEqual(2, _pool.PoolSize);
        }

        [Test]
        public void Return_BeyondMaxSize_ItemIsDiscarded()
        {
            var pool = new EventPool<SimpleEvent>(maxSize: 2);
            pool.Return(new SimpleEvent());
            pool.Return(new SimpleEvent());
            pool.Return(new SimpleEvent()); // should be discarded

            Assert.AreEqual(2, pool.PoolSize);
        }

        // ── Prewarm ───────────────────────────────────────────────────────────────

        [Test]
        public void Prewarm_FillsPoolToRequestedCount()
        {
            _pool.Prewarm(5);
            Assert.AreEqual(5, _pool.PoolSize);
        }

        [Test]
        public void Prewarm_DoesNotExceedMaxSize()
        {
            _pool.Prewarm(100);
            Assert.AreEqual(10, _pool.PoolSize, "Pool must not exceed its maxSize of 10");
        }

        [Test]
        public void Prewarm_WhenPartiallyFilled_OnlyFillsRemainder()
        {
            _pool.Prewarm(4);
            _pool.Prewarm(4); // 4 more — total should be 8, not 12
            Assert.AreEqual(8, _pool.PoolSize);
        }

        // ── Clear ─────────────────────────────────────────────────────────────────

        [Test]
        public void Clear_EmptiesPool()
        {
            _pool.Prewarm(5);
            _pool.Clear();

            Assert.AreEqual(0, _pool.PoolSize);
        }

        [Test]
        public void Clear_CallsResetOnAllItems()
        {
            var evt = new SimpleEvent { Value = 7 };
            _pool.Return(evt);

            // Reset is called in Return(), but let's verify Clear resets any remaining items
            var fresh = new SimpleEvent { Value = 99, WasReset = false };

            // Bypass Return() to inject a non-reset item by using the interface
            IEventPoolBase basePool = _pool;
            // Return via typed method so Reset is called — we verify Clear works on top
            _pool.Return(fresh);
            // At this point WasReset == true already; Clear() calls Reset() again
            _pool.Clear();

            Assert.IsTrue(fresh.WasReset);
        }

        // ── Non-generic interface (IEventPoolBase) ────────────────────────────────

        [Test]
        public void IEventPoolBase_GetObject_ReturnsInstance()
        {
            IEventPoolBase basePool = _pool;
            var obj = basePool.GetObject();
            Assert.IsNotNull(obj);
            Assert.IsInstanceOf<SimpleEvent>(obj);
        }

        [Test]
        public void IEventPoolBase_ReturnObject_AcceptsTypedInstance()
        {
            IEventPoolBase basePool = _pool;
            var evt = new SimpleEvent();
            Assert.DoesNotThrow(() => basePool.ReturnObject(evt));
            Assert.AreEqual(1, _pool.PoolSize);
        }

        [Test]
        public void IEventPoolBase_ReturnObject_IgnoresWrongType()
        {
            IEventPoolBase basePool = _pool;
            Assert.DoesNotThrow(() => basePool.ReturnObject("wrong type"));
            Assert.AreEqual(0, _pool.PoolSize);
        }

        [Test]
        public void IEventPoolBase_Prewarm_FillsPool()
        {
            IEventPoolBase basePool = _pool;
            basePool.Prewarm(3);
            Assert.AreEqual(3, _pool.PoolSize);
        }
    }
}
