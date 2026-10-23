using UnityEngine;
using UnityEngine.UI;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(Text))]
    public sealed class DefenseHud : MonoBehaviour
    {
        [SerializeField] private DefenseGame game;

        private Text text;

        private void Awake() => text = GetComponent<Text>();

        private void Update()
        {
            text.text = "Points " + game.Points + "   Lives " + game.Lives + "   Wave " + game.Wave
                      + "\nPick a turret below and click a free pad to build it. Right click cancels.";
        }
    }
}
