using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(Button))]
    public sealed class ActionButton : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Text cost;

        private Button button;
        private int woodCost;
        private int goldCost;

        private void Awake() => button = GetComponent<Button>();

        public void Show(string what, int wood, int gold, Action onClick)
        {
            woodCost = wood;
            goldCost = gold;
            label.text = what;
            cost.text = Describe(wood, gold);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }

        public void ShowAffordable(bool affordable)
        {
            button.interactable = affordable;
            cost.color = affordable ? Color.white : new Color(1f, 0.5f, 0.5f);
        }

        public bool IsAffordableFrom(Treasury treasury) => treasury.CanAfford(woodCost, goldCost);

        private static string Describe(int wood, int gold)
        {
            if (wood > 0 && gold > 0) return wood + " wood  " + gold + " gold";
            if (wood > 0) return wood + " wood";
            return gold > 0 ? gold + " gold" : "free";
        }
    }
}
