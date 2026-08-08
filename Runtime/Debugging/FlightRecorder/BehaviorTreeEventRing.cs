using System;
using System.Collections;
using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// A fixed-size window over the most recent events. Writing past the end overwrites the oldest, so a
    /// recorder left on all session costs a constant amount of memory and the buffer always holds the run-up
    /// to whatever just went wrong.
    ///
    /// <para>
    /// The array is allocated once and never grows. Enumerating yields oldest-first, which is the order both
    /// the timeline and the why-inspector read in.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeEventRing : IEnumerable<BehaviorTreeEvent>
    {
        private readonly BehaviorTreeEvent[] events;
        private int next;
        private int count;

        public BehaviorTreeEventRing(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "A ring needs room for at least one event.");

            events = new BehaviorTreeEvent[capacity];
        }

        public int Capacity => events.Length;

        /// <summary>How many events are held, up to <see cref="Capacity"/>.</summary>
        public int Count => count;

        /// <summary>How many events have been dropped off the back. Non-zero means the recording is clipped.</summary>
        public int Dropped { get; private set; }

        public void Add(in BehaviorTreeEvent recorded)
        {
            if (count == events.Length) Dropped++;

            events[next] = recorded;
            next = next + 1 == events.Length ? 0 : next + 1;

            if (count < events.Length) count++;
        }

        /// <summary>Oldest first. Index 0 is the oldest event still held, not the oldest ever recorded.</summary>
        public BehaviorTreeEvent this[int index]
        {
            get
            {
                if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));

                var start = count == events.Length ? next : 0;
                var offset = start + index;

                return events[offset < events.Length ? offset : offset - events.Length];
            }
        }

        public void Clear()
        {
            // Release the value strings so a cleared recording does not pin them; the array itself stays.
            Array.Clear(events, 0, events.Length);
            next = 0;
            count = 0;
            Dropped = 0;
        }

        public IEnumerator<BehaviorTreeEvent> GetEnumerator()
        {
            for (int i = 0; i < count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
