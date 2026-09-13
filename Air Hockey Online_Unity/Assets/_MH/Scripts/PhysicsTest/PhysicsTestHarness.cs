using System.Collections.Generic;
using MH.Core;
using MH.GameLogic;
using UnityEngine;

namespace MH.PhysicsTest
{
    /// <summary>
    /// Standalone harness for the shared deterministic simulation (MH.Core / MH.GameLogic).
    ///
    /// It assembles the same primitives <see cref="Match"/> assembles (Puck, Paddle, Wall, BoardConfig,
    /// PuckCollisionResponse) into a closed square board, steps them with <see cref="Entity.Tick"/> in the
    /// same order as <see cref="Match.Tick"/>, and drives one paddle from the mouse. Unity's own physics
    /// (Rigidbody / Collider / Physics2D) is deliberately not used - this tests the real sim.
    ///
    /// Press Play, then HOLD the left mouse button: the paddle follows the cursor and knocks the puck
    /// around the four walls. R resets, Space kicks the puck toward the cursor.
    /// </summary>
    [AddComponentMenu("MH/Physics Test Harness")]
    public sealed class PhysicsTestHarness : MonoBehaviour
    {
        [Header("Board")]
        [Tooltip("The sim's own tuning object: TableWidth/TableLenght, WallThickness, PuckRadius, PaddleRadius, Bounciness, f, PaddlePositionFollow, MaxPuckSpeed...")]
        [SerializeField] private BoardConfig _config = new BoardConfig();

        [Tooltip("Force TableLenght = TableWidth so the four walls form a closed square.")]
        [SerializeField] private bool _squareBoard = true;

        [Header("Run")]
        [Tooltip("Initial puck speed in board units/second when the scene starts or is reset.")]
        [SerializeField] private float _puckStartSpeed = 6f;

        [Tooltip("Starting direction of the puck, measured counter-clockwise from +X.")]
        [SerializeField, Range(0f, 360f)] private float _puckStartAngleDeg = 37f;

        [Tooltip("Sim steps per FixedUpdate. 1 = exactly what the server does; raise it to check sub-stepping against tunnelling.")]
        [SerializeField, Min(1)] private int _subSteps = 1;

        [Tooltip("Off by default: a puck that leaves the box stays where it lands so you can inspect the escape. Turn this on to resync automatically; otherwise press R.")]
        [SerializeField] private bool _autoResetWhenOutOfBounds = false;

        [Header("Input")]
        [Tooltip("Hold the left mouse button and the paddle follows the cursor (mirrors Match.ApplyPaddleTargetFromWorld).")]
        [SerializeField] private bool _followMouseWhileHeld = true;
        [SerializeField] private bool _stopPaddleOnRelease = true;
        [SerializeField] private KeyCode _resetKey = KeyCode.R;
        [SerializeField] private KeyCode _kickKey = KeyCode.Space;

        [Header("View")]
        [SerializeField] private bool _autoFitCamera = true;
        [SerializeField] private float _viewPadding = 1.15f;
        [SerializeField] private Color _backgroundColor = new Color(0.07f, 0.08f, 0.10f, 1f);
        [SerializeField] private Color _wallColor = new Color(0.22f, 0.24f, 0.30f, 1f);
        [SerializeField] private Color _puckColor = new Color(0.95f, 0.95f, 0.98f, 1f);
        [SerializeField] private Color _paddleColor = new Color(0.20f, 0.65f, 1f, 1f);
        [SerializeField] private bool _showHud = true;

        // --- simulation state ---
        private Puck _puck;
        private Paddle _paddle;
        private readonly List<Wall> _walls = new List<Wall>(4);
        private readonly List<string> _wallNames = new List<string>(4);
        private bool _puckVelocityConsumed;

        // --- view state ---
        private Camera _camera;
        private Transform _puckView;
        private Transform _paddleView;
        private readonly List<Transform> _wallViews = new List<Transform>(4);

        private string _status = "ok";

        private void Awake()
        {
            ApplySafeDefaults();

            BuildSimulation();
            BuildViews();
            FitCamera();
            ResetPuck();
        }

        /// <summary>
        /// Keeps the harness usable when values arrive unset (component dropped on a fresh
        /// GameObject, or a scene asset authored by hand): anything zeroed or negative falls back
        /// to a sane default instead of producing an invisible or motionless board.
        /// </summary>
        private void ApplySafeDefaults()
        {
            if (_config == null) _config = new BoardConfig();

            if (_config.TableWidth <= 0f) _config.TableWidth = 9f;
            if (_squareBoard || _config.TableLenght <= 0f) _config.TableLenght = _config.TableWidth;
            if (_config.WallThickness <= 0f) _config.WallThickness = 0.5f;
            if (_config.PuckRadius <= 0f) _config.PuckRadius = 0.5f;
            if (_config.PaddleRadius <= 0f) _config.PaddleRadius = 1f;

            if (_config.PaddlePositionFollow <= 0f) _config.PaddlePositionFollow = 50f;
            if (_config.PaddleMaxSpeed <= 0f) _config.PaddleMaxSpeed = 100f;
            if (_config.MaxPuckSpeed <= 0f) _config.MaxPuckSpeed = 30f;

            if (_puckStartSpeed <= 0f) _puckStartSpeed = 6f;
            if (_puckStartAngleDeg <= 0f) _puckStartAngleDeg = 37f;
            if (_subSteps < 1) _subSteps = 1;
            if (_viewPadding <= 0f) _viewPadding = 1.15f;

            if (_wallColor.a <= 0f) _wallColor = new Color(0.22f, 0.24f, 0.30f, 1f);
            if (_puckColor.a <= 0f) _puckColor = new Color(0.95f, 0.95f, 0.98f, 1f);
            if (_paddleColor.a <= 0f) _paddleColor = new Color(0.20f, 0.65f, 1f, 1f);
            if (_backgroundColor.a <= 0f) _backgroundColor = new Color(0.07f, 0.08f, 0.10f, 1f);
        }

        private void OnDestroy()
        {
            if (_puck != null) _puck.Collider.OnCollision -= OnPuckCollision;
        }

        // ------------------------------------------------------------------ simulation setup

        private void BuildSimulation()
        {
            _walls.Clear();
            _wallNames.Clear();

            _puck = new Puck(_config.PuckRadius);
            _paddle = new Paddle(_config.PaddleRadius);

            float w = _config.TableWidth;
            float h = _config.TableLenght;

            float halfW = _config.TableWidth * 0.5f;
            float halfL = _config.TableLenght * 0.5f;
            
            float t = Mathf.Max(1e-3f, _config.WallThickness);

            // Four walls closing the play area. The horizontal walls span the full width (corners
            // included) and the vertical walls fill the height between them, so there is no gap
            // anywhere - unlike Match's board, which leaves goal openings on the short edges.
            AddWall("Wall_Bottom", w + t, t, 0f, -halfL - t * 0.5f);
            AddWall("Wall_Top", w + t, t, 0f, halfL + t * 0.5f);
            AddWall("Wall_Left", t, h, -halfW - t * 0.5f, 0f);
            AddWall("Wall_Right", t, h, halfW + t * 0.5f, 0f);

            // Only the puck is a moving collider, exactly like Match: its collider tracks the walls
            // and the paddle, and the responses run through PuckCollisionResponse.
            _puck.Collider.OnCollision += OnPuckCollision;
            for (int i = 0; i < _walls.Count; i++) _puck.Collider.TrackOthers.Add(_walls[i].Collider);
            _puck.Collider.TrackOthers.Add(_paddle.GetComponent<CircleCollider>());
        }

        private void AddWall(string wallName, float width, float height, float x, float y)
        {
            var wall = new Wall(width, height);
            wall.GetComponent<Root2D>().Position = new CustomVector2(x, y);
            _walls.Add(wall);
            _wallNames.Add(wallName);
        }

        private void ResetPuck()
        {
            float halfL = _config.TableLenght * 0.5f;
            float angle = _puckStartAngleDeg * Mathf.Deg2Rad;
            var direction = new CustomVector2(Mathf.Cos(angle), Mathf.Sin(angle));

            _puck.GetComponent<Root2D>().Position = CustomVector2.Zero;
            _puck.GetComponent<MoveComponent>().SetVelocity(direction * _puckStartSpeed);

            // Paddle starts on the near edge, like Match's bottom player.
            _paddle.GetComponent<Root2D>().Position = new CustomVector2(0f, -halfL + _config.PaddleRadius);
            _paddle.GetComponent<MoveComponent>().SetVelocity(CustomVector2.Zero);

            _puckVelocityConsumed = false;
            _status = "ok";
        }

        // ------------------------------------------------------------------ stepping

        private void FixedUpdate()
        {
            ReadInput();
            StepSimulation(Time.fixedDeltaTime);
        }

        private void StepSimulation(float deltaTime)
        {
            int steps = Mathf.Max(1, _subSteps);
            float dt = deltaTime / steps;

            for (int i = 0; i < steps; i++)
            {
                // Same tick order as Match.Tick: paddles integrate -> clamp -> puck move + collision.
                _puckVelocityConsumed = false;
                _paddle.Tick(dt);
                ClampPaddleToBoard();
                _puck.Tick(dt);

                // Nothing resyncs on its own: a puck outside the box stays put until R is pressed,
                // unless auto-resync was explicitly enabled. Report it so the escape is visible.
                if (IsPuckOutOfBounds())
                {
                    if (_autoResetWhenOutOfBounds)
                    {
                        ResetPuck();
                        _status = "puck escaped the box (tunnelling) - resynced";
                        return;
                    }

                    _status = "puck left the box (tunnelling) - press R to reset";
                }
            }
        }

        private void ClampPaddleToBoard()
        {
            var root = _paddle.GetComponent<Root2D>();
            var move = _paddle.GetComponent<MoveComponent>();

            float maxX = _config.TableWidth * 0.5f - _config.PaddleRadius;
            float maxY = _config.TableLenght * 0.5f - _config.PaddleRadius;

            float px = root.Position.x;
            float py = root.Position.y;
            float x = Mathf.Clamp(px, -maxX, maxX);
            float y = Mathf.Clamp(py, -maxY, maxY);
            root.Position = new CustomVector2(x, y);

            // Kill the velocity component that pushed into a wall, so the paddle cannot stick.
            var velocity = move.CurrentVelocity;
            if (Mathf.Abs(px - x) > 1e-4f) velocity.x = 0f;
            if (Mathf.Abs(py - y) > 1e-4f) velocity.y = 0f;
            move.SetVelocity(velocity);
        }

        private bool IsPuckOutOfBounds()
        {
            var p = _puck.GetComponent<Root2D>().Position;
            float limitX = _config.TableWidth * 0.5f + _config.WallThickness + _config.PuckRadius;
            float limitY = _config.TableLenght * 0.5f + _config.WallThickness + _config.PuckRadius;
            return Mathf.Abs(p.x) > limitX || Mathf.Abs(p.y) > limitY;
        }

        private void OnPuckCollision(CollisionInfo info)
        {
            var other = info.Collider1 == _puck.Collider ? info.Collider2 : info.Collider1;

            switch (other.Entity)
            {
                case Paddle paddle:
                    PuckCollisionResponse.ResolvePuckPaddle(
                        _puck,
                        paddle,
                        _config,
                        paddle.GetComponent<MoveComponent>().CurrentVelocity,
                        ref _puckVelocityConsumed);
                    break;

                case Wall wall:
                    PuckCollisionResponse.ResolvePuckWall(_puck, wall, _config, ref _puckVelocityConsumed);
                    break;
            }
        }

        // ------------------------------------------------------------------ input

        private void ReadInput()
        {
            if (Input.GetKeyDown(_resetKey))
            {
                ResetPuck();
                return;
            }

            if (Input.GetKeyDown(_kickKey)) KickPuck();

            if (_followMouseWhileHeld && Input.GetMouseButton(0))
                CommandPaddleTo(ScreenToBoard(Input.mousePosition));
            else if (_stopPaddleOnRelease)
                _paddle.GetComponent<MoveComponent>().SetVelocity(CustomVector2.Zero);
        }

        /// <summary>Same maths as Match.ApplyPaddleTargetFromWorld + SetPaddleVelocity.</summary>
        private void CommandPaddleTo(Vector2 targetWorld)
        {
            var root = _paddle.GetComponent<Root2D>();
            var current = root.Position;

            var delta = new CustomVector2(targetWorld.x - current.x, targetWorld.y - current.y);
            var velocity = delta * _config.PaddlePositionFollow;
            velocity = CustomVector2.ClampMagnitude(velocity, _config.PaddleMaxSpeed);

            _paddle.GetComponent<MoveComponent>().SetVelocity(velocity);
        }

        private void KickPuck()
        {
            var puckPos = _puck.GetComponent<Root2D>().Position;
            Vector2 target = ScreenToBoard(Input.mousePosition);

            var direction = CustomVector2.Normalize(new CustomVector2(target.x - puckPos.x, target.y - puckPos.y));
            if (CustomVector2.SqrMagnitude(direction) < CustomVector2.EpsilonSq)
                direction = new CustomVector2(1f, 0f);

            _puck.GetComponent<MoveComponent>().SetVelocity(direction * (_config.MaxPuckSpeed * 0.6f));
            _status = "puck kicked";
        }

        /// <summary>Screen point -> board plane (z = 0) point, clamped inside the board.</summary>
        private Vector2 ScreenToBoard(Vector3 screenPoint)
        {
            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return Vector2.zero;

            float depth = Mathf.Abs(cam.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, depth));

            float maxX = _config.TableWidth * 0.5f - _config.PaddleRadius;
            float maxY = _config.TableLenght * 0.5f - _config.PaddleRadius;
            return new Vector2(Mathf.Clamp(world.x, -maxX, maxX), Mathf.Clamp(world.y, -maxY, maxY));
        }

        // ------------------------------------------------------------------ view

        private void BuildViews()
        {
            Sprite circle = CreateCircleSprite();
            Sprite square = CreateSquareSprite();

            _puckView = CreateView("Puck", circle, _puckColor, 3, _config.PuckRadius * 2f);
            _paddleView = CreateView("Paddle", circle, _paddleColor, 4, _config.PaddleRadius * 2f);

            _wallViews.Clear();
            for (int i = 0; i < _walls.Count; i++)
            {
                RectCollider rect = _walls[i].Collider;
                Transform view = CreateView(_wallNames[i], square, _wallColor, 2, 0f);
                view.localScale = new Vector3(rect.Width, rect.Height, 1f);
                _wallViews.Add(view);
            }

            SyncViews();
        }

        private Transform CreateView(string viewName, Sprite sprite, Color color, int sortingOrder, float unitSize)
        {
            var go = new GameObject(viewName);
            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            if (unitSize > 0f) go.transform.localScale = new Vector3(unitSize, unitSize, 1f);
            return go.transform;
        }

        private void LateUpdate()
        {
            SyncViews();
        }

        private void SyncViews()
        {
            SyncView(_puckView, _puck.GetComponent<Root2D>());
            SyncView(_paddleView, _paddle.GetComponent<Root2D>());

            for (int i = 0; i < _walls.Count && i < _wallViews.Count; i++)
                SyncView(_wallViews[i], _walls[i].GetComponent<Root2D>());
        }

        private static void SyncView(Transform view, Root2D root)
        {
            if (view == null || root == null) return;
            var p = root.Position;
            view.localPosition = new Vector3(p.x, p.y, 0f);
        }

        private void FitCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Physics Test Camera");
                _camera = go.AddComponent<Camera>();
            }

            Transform camTransform = _camera.transform;
            camTransform.position = new Vector3(0f, 0f, -10f);
            camTransform.rotation = Quaternion.identity;

            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = _backgroundColor;

            if (!_autoFitCamera) return;

            float halfHeight = _config.TableLenght * 0.5f + _config.WallThickness;
            float halfWidth = _config.TableWidth * 0.5f + _config.WallThickness;
            float aspect = Mathf.Max(0.05f, _camera.aspect);
            _camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / aspect) * _viewPadding;
        }

        // 1 world unit sprites generated at runtime: no sprite/material assets to reference.
        private static Sprite CreateSquareSprite(int size = 8)
        {
            Texture2D texture = NewTexture(size);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateCircleSprite(int size = 128)
        {
            Texture2D texture = NewTexture(size);
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = (byte)(Mathf.Clamp01(radius - distance) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        // ------------------------------------------------------------------ HUD

        private void OnGUI()
        {
            if (!_showHud) return;

            var puckPosition = _puck.GetComponent<Root2D>().Position;
            var puckVelocity = _puck.GetComponent<MoveComponent>().CurrentVelocity;
            float speed = CustomVector2.Magnitude(puckVelocity);

            int steps = Mathf.Max(1, _subSteps);
            float dt = Time.fixedDeltaTime / steps;
            float stepTravel = speed * dt;
            bool tunnellingRisk = stepTravel > _config.WallThickness;

            GUI.Box(new Rect(10f, 10f, 360f, 186f), GUIContent.none);
            GUILayout.BeginArea(new Rect(18f, 16f, 344f, 174f));
            GUILayout.Label("MH.Core physics test - custom sim, NOT Unity physics");
            GUILayout.Label($"puck     : pos {puckPosition}  vel {puckVelocity}  |v| {speed:0.00}");
            GUILayout.Label($"paddle   : pos {_paddle.GetComponent<Root2D>().Position}");
            GUILayout.Label($"step     : {steps} sub-step(s), dt {dt:0.0000}s, travel {stepTravel:0.000} (wall {_config.WallThickness:0.00})");
            GUILayout.Label($"tunnel   : {(tunnellingRisk ? "RISK - step travel exceeds wall thickness" : "ok")}");
            GUILayout.Label($"bounds   : {(IsPuckOutOfBounds() ? "OUT OF BOX - press R to reset" : "inside")}");
            GUILayout.Label($"status   : {_status}");
            GUILayout.Label("HOLD left mouse = move paddle | R = reset | Space = kick");
            GUILayout.EndArea();
        }
    }
}
