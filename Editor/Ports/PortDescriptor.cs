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