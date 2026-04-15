using MH.Core;
using MH.GameLogic;
using UnityEngine;

namespace MH.Editor
{
    /// <summary>
    /// Runtime overlay: FPS, server ping (when connected via <see cref="GameRunner"/>), and simulation velocities (puck + paddles). Add to a scene object; optionally assign <see cref="GameRunner"/>.
    /// </summary>
    public class UIDebug : MonoBehaviour
    {
        const float FpsSampleSeconds = 0.5f;

        [SerializeField] GameRunner gameRunner;
        [SerializeField] bool visible = true;
        [SerializeField] int fontSize = 14;
        [SerializeField] Vector2 screenOffset = new Vector2(12f, 12f);

        GUIStyle _style;

        [SerializeField] int playerIdBottom = 0;
        [SerializeField] int playerIdTop = 1;

        float _fpsAccum;
        int _fpsFrameCount;
        float _displayFps = -1f;

        void Awake()
        {
            if (gameRunner == null)
                gameRunner = GameRunner.Instance;
        }

        void Update()
        {
            // Rolling FPS over unscaled time so hitches while paused/timeScale=0 still read sensibly when unscaled.
            _fpsAccum += Time.unscaledDeltaTime;
            _fpsFrameCount++;
            if (_fpsAccum >= FpsSampleSeconds)
            {
                _displayFps = _fpsFrameCount / _fpsAccum;
                _fpsAccum = 0f;
                _fpsFrameCount = 0;
            }
        }

        void OnGUI()
        {
            if (!visible) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    richText = true,
                    normal = { textColor = Color.white }
                };
            }

            var match = gameRunner != null ? gameRunner.CurrentMatch : null;
            int matchLineCount = 0;
            if (match != null)
            {
                if (match.Puck != null) matchLineCount++;
                if (match.GetPlayer(playerIdBottom)?.Paddle != null) matchLineCount++;
                if (match.GetPlayer(playerIdTop)?.Paddle != null) matchLineCount++;
            }

            const int headerLineCount = 2;
            float line = Mathf.Max(_style.CalcHeight(new GUIContent("X"), 400f), _style.lineHeight);
            int totalLines = headerLineCount + matchLineCount;
            var inner = new Rect(screenOffset.x + 6f, screenOffset.y + 6f, 400f, line * totalLines);
            GUI.Box(new Rect(screenOffset.x, screenOffset.y, inner.width + 12f, inner.height + 12f), GUIContent.none);

            float y = inner.y;
            string fpsText = _displayFps >= 0f ? $"{_displayFps:F0} FPS" : "FPS …";
            GUI.Label(new Rect(inner.x, y, 800f, line), fpsText, _style);
            y += line;

            int pingMs = gameRunner != null ? gameRunner.ServerRoundTripPingMs : -1;
            string pingText = pingMs >= 0 ? $"Ping {pingMs} ms" : "Ping —";
            GUI.Label(new Rect(inner.x, y, 800f, line), pingText, _style);
            y += line;

            if (match == null)
                return;

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
