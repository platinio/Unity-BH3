using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class Selectable : MonoBehaviour
    {
        public static readonly List<Selectable> All = new();

        [SerializeField] private bool selectsAlone;

        public event Action<bool> SelectionChanged;

        public bool IsSelected { get; private set; }
        public bool SelectsAlone => selectsAlone;

        private void OnEnable() => All.Add(this);

        private void OnDisable()
        {
            All.Remove(this);
            Deselect();
        }

        public void Select()
        {
            if (IsSelected) return;

            IsSelected = true;
            SelectionChanged?.Invoke(true);
        }

        public void Deselect()
        {
            if (!IsSelected) return;

            IsSelected = false;
            SelectionChanged?.Invoke(false);
        }
    }
}
