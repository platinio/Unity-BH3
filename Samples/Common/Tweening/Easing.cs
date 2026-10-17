namespace ArcaneOnyx.BH3.Samples
{
    public static class Easing
    {
        private const float BackOvershoot = 1.70158f;
        private const float BounceLengthInFalls = 2.75f;

        public static float Linear(float progress) => progress;

        public static float InQuad(float progress) => progress * progress;

        public static float OutQuad(float progress) => 1f - InQuad(1f - progress);

        public static float InOutQuad(float progress) =>
            progress < 0.5f ? InQuad(progress * 2f) / 2f : 0.5f + OutQuad(progress * 2f - 1f) / 2f;

        public static float OutBack(float progress)
        {
            float progressFromEnd = progress - 1f;
            float squared = progressFromEnd * progressFromEnd;
            float cubed = squared * progressFromEnd;

            return 1f + (BackOvershoot + 1f) * cubed + BackOvershoot * squared;
        }

        public static float OutBounce(float progress)
        {
            float timeInFalls = progress * BounceLengthInFalls;

            if (timeInFalls < 1f) return timeInFalls * timeInFalls;
            if (timeInFalls < 2f) return Hop(timeInFalls, 1f, 2f);
            if (timeInFalls < 2.5f) return Hop(timeInFalls, 2f, 2.5f);
            return Hop(timeInFalls, 2.5f, BounceLengthInFalls);
        }

        private static float Hop(float timeInFalls, float hopStart, float hopEnd)
        {
            float hopMiddle = (hopStart + hopEnd) / 2f;
            float halfHopLength = (hopEnd - hopStart) / 2f;
            float timeFromMiddle = timeInFalls - hopMiddle;
            float heightAboveGround = halfHopLength * halfHopLength - timeFromMiddle * timeFromMiddle;

            return 1f - heightAboveGround;
        }
    }
}
