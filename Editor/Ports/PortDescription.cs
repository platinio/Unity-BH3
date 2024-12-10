using System;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public sealed class PortDescription : IDescription
    {
        private string _label;

        private bool _isLabelVisible = true;

        internal IPort portType;

        public EditorTexture icon
        {
            get => _icon;
            set => _icon = value;
        }

        private EditorTexture _icon;

        public string fallbackLabel { get; set; }

        public string label
        {
            get => _label ?? fallbackLabel;
            set => _label = value;
        }

        public bool showLabel
        {
            get => !BoltFlow.Configuration.hidePortLabels || _isLabelVisible;
            set => _isLabelVisible = value;
        }

        string IDescription.title => label;

        public string summary { get; set; }

        public Func<Metadata, Metadata> getMetadata { get; set; }

        public void CopyFrom(PortDescription other)
        {
            _label = other._label;
            _isLabelVisible = other._isLabelVisible;
            summary = other.summary;
            portType = other.portType ?? portType;
            getMetadata = other.getMetadata ?? getMetadata;
        }
    }
}
