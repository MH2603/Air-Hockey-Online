using MH.Core;
using MH.GameLogic;
using UnityEngine;

namespace MH.Editor
{
    /// <summary>
    /// Runtime overlay for simulation velocities (puck + paddles). Add to a scene object; optionally assign <see cref="GameRunner"/>.
    /// </summary>
    public class UIDebug : MonoBehaviour
    {
        [SerializeField] GameRunner gameRunner;
        [SerializeField] bool visible = true;
        [SerializeField] int fontSize = 14;
        [SerializeField] Vector2 screenOffset = new Vector2(12f, 12f);

        GUIStyle _style;

        [SerializeField] int playerIdBottom = 0;
        [SerializeField] int playerIdTop = 1;

        void Awake()
        {
            if (gameRunner == null)
                gameRunner = GameRunner.Instance;
        }

        void OnGUI()
        {
            if (!visible) return;

            var match = gameRunner != null ? gameRunner.CurrentMatch : null;
            if (match == null) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    richText = true,
                    normal = { textColor = Color.white }
                };
            }

            float line = Mathf.Max(_style.CalcHeight(new GUIContent("X"), 400f), _style.lineHeight);
            var inner = new Rect(screenOffset.x + 6f, screenOffset.y + 6f, 400f, line * 4f);
            GUI.Box(new Rect(screenOffset.x, screenOffset.y, inner.width + 12f, inner.height + 12f), GUIContent.none);

            float y = inner.y;
            DrawRow(_style, inner.x, ref y, line, "Puck", match.Puck);
            DrawPlayerRow(_style, inner.x, ref y, line, match, playerIdBottom);
            DrawPlayerRow(_style, inner.x, ref y, line, match, playerIdTop);
        }

        static void DrawRow(GUIStyle style, float x, ref float y, float lineHeight, string title, Paddle paddle)
        {
            if (paddle == null) return;
            var v = paddle.GetComponent<MoveComponent>().CurrentVelocity;
            DrawVelLine(style, x, ref y, lineHeight, title, v);
        }

        static void DrawRow(GUIStyle style, float x, ref float y, float lineHeight, string title, Puck puck)
        {
            if (puck == null) return;
            var v = puck.GetComponent<MoveComponent>().CurrentVelocity;
            DrawVelLine(style, x, ref y, lineHeight, title, v);
        }

        static void DrawPlayerRow(GUIStyle style, float x, ref float y, float lineHeight, Match match, int playerId)
        {
            var p = match.GetPlayer(playerId);
            if (p == null) return;
            DrawRow(style, x, ref y, lineHeight, $"Paddle (id {playerId})", p.Paddle);
        }

        static void DrawVelLine(GUIStyle style, float x, ref float y, float lineHeight, string title, CustomVector2 v)
        {
            float speed = CustomVector2.Magnitude(v);
            string text = $"{title}  vel ({v.x:F3}, {v.y:F3})  speed {speed:F3}";
            GUI.Label(new Rect(x, y, 800f, lineHeight), text, style);
            y += lineHeight;
        }
    }
}
