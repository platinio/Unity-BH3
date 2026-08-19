using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// A remembered answer about a Unity object, and whether that object has since been destroyed.
    ///
    /// <para>
    /// Every cache in the debugging panels that remembers a <see cref="Object"/> has to tell two situations
    /// apart that look identical, because Unity's overloaded <c>==</c> reports both as null:
    /// <em>the answer was nothing</em> — no asset matched that name, no agent is recording — and <em>the
    /// answer was something that has since been destroyed</em>. They need opposite treatment. The first is a
    /// real answer and reusing it is the entire point of the cache; the second is a dead reference whose
    /// members throw <c>MissingReferenceException</c> the moment a panel touches them, out of <c>OnGUI</c>,
    /// where it becomes a wall of console errors rather than a handled failure.
    /// </para>
    ///
    /// <para>
    /// Recording which one it was at the moment of remembering is the whole of it, and it is written once
    /// here rather than at each cache: this decision was about to be made three times in one change, and a
    /// rule spelled out three times is one that gets half-fixed later.
    /// </para>
    /// </summary>
    public readonly struct Remembered<T> where T : Object
    {
        private readonly T value;

        /// <summary>
        /// Whether there was an object here when it was remembered. Not derivable afterwards, which is the
        /// reason this type exists at all.
        /// </summary>
        private readonly bool hadValue;

        public Remembered(T value)
        {
            this.value = value;
            hadValue = value != null;
        }

        /// <summary>
        /// True once a remembered object has been destroyed or unloaded out from under this entry.
        /// </summary>
        public bool IsStale => hadValue && value == null;

        /// <summary>
        /// The remembered answer, and whether it is still one worth reusing. False means resolve again —
        /// <em>not</em> that the answer was nothing, which is a perfectly reusable <c>true</c> with a null
        /// <paramref name="remembered"/>.
        ///
        /// <para>
        /// The gate and the read are one call so that they cannot come apart. Exposing the value beside a
        /// separate staleness flag would leave every cache spelling out the same two-line pairing, and
        /// nothing would notice the day one of them stopped: reading a destroyed object compiles, and only
        /// fails later, inside <c>OnGUI</c>, as a wall of <c>MissingReferenceException</c>.
        /// </para>
        /// </summary>
        public bool TryReuse(out T remembered)
        {
            remembered = IsStale ? null : value;

            return !IsStale;
        }
    }
}
