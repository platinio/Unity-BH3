using System;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    [Descriptor(typeof(IBehaviorTreePort))]
    public sealed class BehaviorTreeUnitPortDescriptor : IDescriptor
    {
        public BehaviorTreeUnitPortDescriptor(IBehaviorTreePort target)
        {
            Ensure.That(nameof(target)).IsNotNull(target);

            this.target = target;

            description.portType = target;
        }

        public IBehaviorTreePort target { get; }

        object IDescriptor.target => target;

        public BehaviorTreePortDescription description { get; private set; } = new BehaviorTreePortDescription();

        IDescription IDescriptor.description => description;

        public bool isDirty { get; set; } = true;

        public void Validate()
        {
            if (isDirty)
            {
                isDirty = false;

                description.fallbackLabel = target.key.Filter(symbols: false, punctuation: false).Prettify();

                description.portType = target;

                //target.behaviorTreeNode?.Descriptor<IUnitDescriptor>().DescribePort(target, description);

                // No DescriptionAssignment is run, so we'll just always assume that the description changes.
                DescriptorProvider.instance.TriggerDescriptionChange(target);
            }
        }
    }
}