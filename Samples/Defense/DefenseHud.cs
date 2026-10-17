using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(Text))]
    public sealed class DefenseHud : MonoBehaviour
    {
        [SerializeField] private DefenseGame game;
        [SerializeField] private TurretBuilder builder;

        private Text text;
        private readonly StringBuilder lines = new();

        private void Awake() => text = GetComponent<Text>();

        private void Update()
        {
            lines.Clear();
            lines.Append("Points ").Append(game.Points).Append("   Lives ").Append(game.Lives).Append("   Wave ").Append(game.Wave).Append('\n');
            for (int i = 0; i < builder.Kinds.Length; i++)
            {
                TurretKind kind = builder.Kinds[i];
                lines.Append(kind == builder.Selected ? "> " : "  ").Append('[').Append(i + 1).Append("] ").Append(kind.Id).Append(' ').Append(kind.Cost).Append("   ");
            }
            lines.Append("\nClick a pad to build the selected turret.");
            text.text = lines.ToString();
        }
    }
}
