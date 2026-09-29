using Halka.Game.Core;
using UnityEngine;

namespace Halka.Game.UI
{
    public sealed class GameHud : MonoBehaviour
    {
        private GUIStyle style;

        private void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 16,
                    normal = { textColor = new Color(0.18f, 0.19f, 0.15f) }
                };
            }
            GUI.Label(new Rect(Screen.width - 110f, 10f, 100f, 28f), GameVersion.Label, style);
        }
    }
}
