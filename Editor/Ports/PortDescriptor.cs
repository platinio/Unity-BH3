using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [Descriptor(typeof(IPort))]
    public sealed class PortDescriptor : IDescriptor
    {
        public PortDescriptor(IPort target)
        {
            Ensure.That(nameof(target)).IsNotNull(target);

            this.target = target;

            description.portType = target;

            // How a port widget reaches its own metadata, and through that the inline value field a port with
            // a default draws. Assigned here rather than in Validate because Validate only runs while isDirty,
            // so a descriptor that has already been validated would never pick it up — and a port widget whose
            // FetchMetadata returns null never gets CacheMetadata called at all, leaving it with no inspector
            // and nothing drawn. Visual Scripting's UnitDescriptor uses this same universal form for ports it
            // cannot reach through a declaring member.
            description.getMetadata = unitMetadata => unitMetadata.StaticObject(target);
        }

        public IPort target { get; }

        object IDescriptor.target => target;

        public PortDescription description { get; private set; } = new PortDescription();

        IDescription IDescriptor.description => description;

        public bool isDirty { get; set; } = true;

        public void Validate()
        {
            if (isDirty)
            {
                isDirty = false;

                description.fallbackLabel = target.key.Filter(symbols: false, punctuation: false).Prettify();
                description.portType = target;

                DescriptorProvider.instance.TriggerDescriptionChange(target);
            }
        }
    }
}