using System.Collections.Generic;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class TurretBar : MonoBehaviour
    {
        [SerializeField] private DefenseGame game;
        [SerializeField] private TurretBuilder builder;
        [SerializeField] private TurretButton buttonPrefab;

        private readonly List<TurretButton> shown = new();

        private void Start()
        {
            for (int i = 0; i < builder.Kinds.Length; i++) Add(builder.Kinds[i], shortcutKey: i + 1);
        }

        private void Update()
        {
            foreach (TurretButton button in shown)
            {
                button.ShowPlacing(button.Kind == builder.Placing);
                button.ShowAffordable(game.CanAfford(button.Kind.Cost));
            }
        }

        private void Add(TurretKind kind, int shortcutKey)
        {
            TurretButton button = Instantiate(buttonPrefab, transform);
            button.Show(kind, shortcutKey, () => builder.BeginPlacing(kind));
            shown.Add(button);
        }
    }
}
