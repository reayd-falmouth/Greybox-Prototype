using System.Collections.Generic;
using NUnit.Framework;

namespace Runtime.RMC.Backgammon.Tests
{
    [Category("RMC.MyProject")]
    public class BackgammonEventQueueTests
    {
        [Test]
        public void Tick_DispatchesEventsInFifoOrder()
        {
            var received = new List<string>();
            var queue = new BackgammonEventQueue(enableDebugLogs: false);

            queue.Enqueue(new BackgammonPresentationEvent("first", () => received.Add("first"), false, 0f, BackgammonEventClockDomain.ScaledGameplay));
            queue.Enqueue(new BackgammonPresentationEvent("second", () => received.Add("second"), false, 0f, BackgammonEventClockDomain.ScaledGameplay));
            queue.Enqueue(new BackgammonPresentationEvent("third", () => received.Add("third"), false, 0f, BackgammonEventClockDomain.ScaledGameplay));

            queue.Tick(0.016f);
            queue.Tick(0.016f);
            queue.Tick(0.016f);

            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, received);
        }

        [Test]
        public void Tick_BlocksNextEventUntilDelayExpires()
        {
            var received = new List<string>();
            var queue = new BackgammonEventQueue(enableDebugLogs: false);
            queue.SetGameSpeedMultiplier(1f);

            queue.Enqueue(new BackgammonPresentationEvent("block", () => received.Add("block"), true, 0.20f, BackgammonEventClockDomain.ScaledGameplay));
            queue.Enqueue(new BackgammonPresentationEvent("next", () => received.Add("next"), false, 0f, BackgammonEventClockDomain.ScaledGameplay));

            queue.Tick(0.01f); // block dispatched
            Assert.That(received.Count, Is.EqualTo(1));

            queue.Tick(0.10f); // still blocked
            Assert.That(received.Count, Is.EqualTo(1));

            queue.Tick(0.10f); // unblock
            queue.Tick(0.01f); // dispatch next
            CollectionAssert.AreEqual(new[] { "block", "next" }, received);
        }

        [Test]
        public void Tick_GameplayDomainRespondsToSpeedMultiplier()
        {
            var received = new List<string>();
            var queue = new BackgammonEventQueue(enableDebugLogs: false);
            queue.SetGameSpeedMultiplier(2f); // faster gameplay time

            queue.Enqueue(new BackgammonPresentationEvent("a", () => received.Add("a"), true, 0.20f, BackgammonEventClockDomain.ScaledGameplay));
            queue.Enqueue(new BackgammonPresentationEvent("b", () => received.Add("b"), false, 0f, BackgammonEventClockDomain.ScaledGameplay));

            queue.Tick(0.01f); // dispatch a
            queue.Tick(0.06f); // consumes 0.12 gameplay seconds
            Assert.That(received.Count, Is.EqualTo(1));

            queue.Tick(0.05f); // total consumed > 0.20 gameplay seconds
            queue.Tick(0.01f); // next frame can dispatch b
            CollectionAssert.AreEqual(new[] { "a", "b" }, received);
        }

        [Test]
        public void Tick_ChangingSpeedAffectsSubsequentWait()
        {
            var received = new List<string>();
            var queue = new BackgammonEventQueue(enableDebugLogs: false);
            queue.SetGameSpeedMultiplier(1f);

            queue.Enqueue(new BackgammonPresentationEvent("a", () => received.Add("a"), true, 0.30f, BackgammonEventClockDomain.ScaledGameplay));
            queue.Enqueue(new BackgammonPresentationEvent("b", () => received.Add("b"), false, 0f, BackgammonEventClockDomain.ScaledGameplay));

            queue.Tick(0.01f); // dispatch a
            queue.Tick(0.10f); // 0.10 consumed
            Assert.That(received.Count, Is.EqualTo(1));

            queue.SetGameSpeedMultiplier(2f); // speed up remaining wait
            queue.Tick(0.10f); // consumes 0.20 now, enough to unblock
            queue.Tick(0.01f); // dispatch b
            CollectionAssert.AreEqual(new[] { "a", "b" }, received);
        }

        [Test]
        public void Tick_UnscaledDomainIgnoresGameSpeedMultiplier()
        {
            var received = new List<string>();
            var queue = new BackgammonEventQueue(enableDebugLogs: false);
            queue.SetGameSpeedMultiplier(3f);

            queue.Enqueue(new BackgammonPresentationEvent("a", () => received.Add("a"), true, 0.20f, BackgammonEventClockDomain.UnscaledReal));
            queue.Enqueue(new BackgammonPresentationEvent("b", () => received.Add("b"), false, 0f, BackgammonEventClockDomain.UnscaledReal));

            queue.Tick(0.01f); // dispatch a
            queue.Tick(0.10f);
            Assert.That(received.Count, Is.EqualTo(1));

            queue.Tick(0.10f);
            queue.Tick(0.01f);
            CollectionAssert.AreEqual(new[] { "a", "b" }, received);
        }

        [Test]
        public void Tick_OpeningTieStyleSequence_DispatchesWithoutSameFrameOverlap()
        {
            var dispatchTimes = new List<float>();
            float simTime = 0f;
            const float requiredGap = 0.12f;
            var queue = new BackgammonEventQueue(enableDebugLogs: false);
            queue.SetGameSpeedMultiplier(1f);

            queue.Enqueue(new BackgammonPresentationEvent("autodouble", () => dispatchTimes.Add(simTime), true, requiredGap, BackgammonEventClockDomain.ScaledGameplay));
            queue.Enqueue(new BackgammonPresentationEvent("reset-pickup", () => dispatchTimes.Add(simTime), true, requiredGap, BackgammonEventClockDomain.ScaledGameplay));

            queue.Tick(0.01f); // first event dispatch
            simTime += 0.01f;
            queue.Tick(0.06f); // still blocked
            simTime += 0.06f;
            queue.Tick(0.06f); // unblock
            simTime += 0.06f;
            queue.Tick(0.01f); // second event dispatch

            Assert.That(dispatchTimes.Count, Is.EqualTo(2));
            Assert.That(dispatchTimes[1] - dispatchTimes[0], Is.GreaterThanOrEqualTo(requiredGap));
        }
    }
}
