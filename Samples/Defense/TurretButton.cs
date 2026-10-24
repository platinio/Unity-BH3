using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(Button))]
    public sealed class TurretButton : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Text label;
        [SerializeField] private Text cost;
        [SerializeField] private Color restingColor = new(0.16f, 0.2f, 0.26f, 0.9f);
        [SerializeField] private Color placingColor = new(0.3f, 0.45f, 0.7f, 0.95f);
        [SerializeField] private Color unaffordableCostColor = new(1f, 0.5f, 0.5f);

        private Button button;

        public TurretKind Kind { get; private set; }

        private void Awake() => button = GetComponent<Button>();

        public void Show(TurretKind kind, int shortcutKey, Action onClick)
        {
            Kind = kind;
            label.text = kind.Id;
            cost.text = kind.Cost + " points   key " + shortcutKey;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }

        public void ShowPlacing(bool isPlacing) => background.color = isPlacing ? placingColor : restingColor;

        public void ShowAffordable(bool isAffordable)
        {
            button.interactable = isAffordable;
            cost.color = isAffordable ? Color.white : unaffordableCostColor;
        }
    }
}
