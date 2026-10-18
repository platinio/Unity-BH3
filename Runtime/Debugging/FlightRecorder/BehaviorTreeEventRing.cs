using System;
using System.Collections;
using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    public sealed class BehaviorTreeEventRing : IEnumerable<BehaviorTreeEvent>
    {
        private readonly BehaviorTreeEvent[] events;
        private int nextWriteIndex;
        private int count;

        public BehaviorTreeEventRing(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "A ring needs room for at least one event.");
            }

            events = new BehaviorTreeEvent[capacity];
        }

        public int Capacity => events.Length;

        public int Count => count;

        public int DroppedEventCount { get; private set; }

        public bool IsFull => count == events.Length;

        public void Add(in BehaviorTreeEvent newEvent)
        {
            if (IsFull) DroppedEventCount++;

            events[nextWriteIndex] = newEvent;
            nextWriteIndex = WrapIndex(nextWriteIndex + 1);

            if (count < events.Length) count++;
        }

        /// <summary>Oldest first. Index 0 is the oldest event still held, not the oldest ever recorded.</summary>
        public BehaviorTreeEvent this[int index]
        {
            get
            {
                if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));

                var oldestIndex = IsFull ? nextWriteIndex : 0;

                return events[WrapIndex(oldestIndex + index)];
            }
        }

        public void Clear()
        {
            // Clears the slots as well as the indices so a cleared recording stops pinning its value strings.
            Array.Clear(events, 0, events.Length);
            nextWriteIndex = 0;
            count = 0;
            DroppedEventCount = 0;
        }

        public IEnumerator<BehaviorTreeEvent> GetEnumerator()
        {
            for (int index = 0; index < count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private int WrapIndex(int index) => index < events.Length ? index : index - events.Length;
    }
}
