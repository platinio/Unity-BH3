using System.Collections;
using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// A small fixed-size window over the most recent guard traces.
    ///
    /// <para>
    /// Separate from <see cref="BehaviorTreeEventRing"/> rather than folded into it, because the two have
    /// opposite shapes. An event is a flat struct in a pre-allocated array, sized so a busy tree costs a
    /// constant amount; a trace is a variable-length chain plus however many wires a graph happens to have.
    /// Storing one inside the other would either box every event or make the event buffer's memory a
    /// function of graph complexity, and the event buffer's flatness is what keeps recording allocation-free.
    /// </para>
    ///
    /// <para>
    /// Much shorter than the event ring on purpose. Only transitions produce traces, and the traces anyone
    /// reads are the recent ones — the trace for a flip four thousand ticks ago is not what a designer is
    /// looking at when they click the node that just died.
    /// </para>
    /// </summary>
    public sealed class GuardTraceRing : IEnumerable<GuardTrace>
    {
        /// <summary>
        /// Enough to cover the transitions around whatever just went wrong. A guard that flips often enough
        /// to exhaust this is oscillating, which the explanation says out loud from the event buffer anyway.
        /// </summary>
        public const int DefaultCapacity = 64;

        private readonly GuardTrace[] traces;
        private int next;
        private int count;

        public GuardTraceRing(int capacity)
        {
            traces = new GuardTrace[capacity > 0 ? capacity : DefaultCapacity];
        }

        public int Capacity => traces.Length;

        public int Count => count;

        /// <summary>How many traces scrolled off the back.</summary>
        public int Dropped { get; private set; }

        public void Add(GuardTrace trace)
        {
            if (trace == null) return;

            if (count == traces.Length) Dropped++;

            traces[next] = trace;
            next = next + 1 == traces.Length ? 0 : next + 1;

            if (count < traces.Length) count++;
        }

        /// <summary>Oldest first, matching the event ring so the two read the same way round.</summary>
        public GuardTrace this[int index]
        {
            get
            {
                if (index < 0 || index >= count) return null;

                var start = count == traces.Length ? next : 0;
                var offset = start + index;

                return traces[offset < traces.Length ? offset : offset - traces.Length];
            }
        }

        /// <summary>
        /// The trace belonging to a particular event, or null. Newest first, because an explanation is almost
        /// always about something that just happened.
        /// </summary>
        public GuardTrace Find(int tick, int sequence)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                var trace = this[i];
                if (trace != null && trace.Matches(tick, sequence)) return trace;
            }

            return null;
        }

        public void Clear()
        {
            for (int i = 0; i < traces.Length; i++)
            {
                traces[i] = null;
            }

            next = 0;
            count = 0;
            Dropped = 0;
        }

        public IEnumerator<GuardTrace> GetEnumerator()
        {
            for (int i = 0; i < count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
