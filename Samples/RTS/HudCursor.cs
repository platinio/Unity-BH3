using UnityEngine.EventSystems;

namespace ArcaneOnyx.BH3.Samples
{
    public static class HudCursor
    {
        public static bool IsOverTheHud => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
