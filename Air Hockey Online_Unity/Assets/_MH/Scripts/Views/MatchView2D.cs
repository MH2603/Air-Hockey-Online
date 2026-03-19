using System.Collections.Generic;
using MH.Core;
using MH.GameLogic;
using UnityEngine;

namespace MH.Views
{
    /// <summary>
    /// Easy scene setup: spawns visuals and binds them to the existing `MH.GameLogic.Match` entities.
    /// </summary>
    public class MatchView2D : MonoBehaviour
    {
        [Header("Spawn Root")]
        [SerializeField] private Transform spawnRoot;

        [Header("Prefabs (optional)")]
        [SerializeField] public GameObject paddlePrefab;
        [SerializeField] public GameObject puckPrefab;
        [SerializeField] public GameObject goalFramePrefab;
        [SerializeField] public GameObject wallPrefab;

        [Header("Which players are bottom/top (for initial visuals)")]
        [SerializeField] private int playerIdBottom = 0;
        [SerializeField] private int playerIdTop = 1;

        private readonly List<GameObject> _spawned = new();
        private static Sprite _whiteSprite;
        private Match _match;

        public void SetMatch(Match match)
        {
            _match = match;
            ClearSpawned();

            if (_match == null) return;

            // Spawn puck.
            if (_match.Puck != null)
            {
                SpawnEntity<PuckView2D>(_match.Puck, puckPrefab, "Puck");
            }

            // Spawn player-side paddles + goal frames.
            var bottom = _match.GetPlayer(playerIdBottom);
            if (bottom != null)
            {
                SpawnEntity<PaddleView2D>(bottom.Paddle, paddlePrefab, $"Paddle_{bottom.Id}");
                SpawnEntity<GoalFrameView2D>(bottom.GoalFrame, goalFramePrefab, $"Goal_{bottom.Id}");
            }

            var top = _match.GetPlayer(playerIdTop);
            if (top != null)
            {
                SpawnEntity<PaddleView2D>(top.Paddle, paddlePrefab, $"Paddle_{top.Id}");
                SpawnEntity<GoalFrameView2D>(top.GoalFrame, goalFramePrefab, $"Goal_{top.Id}");
            }

            // Spawn walls.
            foreach (var wall in _match.Walls)
            {
                SpawnEntity<WallView2D>(wall, wallPrefab, "Wall");
            }
        }

        private void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Destroy(_spawned[i]);
                }
            }
            _spawned.Clear();
        }

        private void SpawnEntity<TView>(Entity entity, GameObject prefab, string name) where TView : EntityView2D
        {
            if (entity == null) return;

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, GetSpawnParent(), false);
                go.name = name;
            }
            else
            {
                go = CreateFallbackVisual(entity, GetSpawnParent(), name);
            }

            var view = go.GetComponent<TView>() ?? go.AddComponent<TView>();
            view.Bind(entity);
            _spawned.Add(go);
        }

        private Transform GetSpawnParent() => spawnRoot != null ? spawnRoot : transform;

        private GameObject CreateFallbackVisual(Entity entity, Transform parent, string name)
        {
            // Minimal fallback: render using `Texture2D.whiteTexture` as a 1x1 sprite.
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite;

            if (entity is Wall)
            {
                sr.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            }
            else if (entity is GoalFrame)
            {
                sr.color = new Color(0.9f, 0.8f, 0.1f, 1f);
            }
            else if (entity is MH.GameLogic.Puck)
            {
                sr.color = new Color(0.2f, 0.7f, 1f, 1f);
            }
            else
            {
                sr.color = new Color(0.9f, 0.2f, 0.2f, 1f);
            }

            // Give the entity view scripts something to scale.
            go.transform.localScale = Vector3.one;
            return go;
        }

        private static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite != null) return _whiteSprite;

                var tex = Texture2D.whiteTexture;
                var rect = new Rect(0, 0, tex.width, tex.height);
                _whiteSprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 1f);
                return _whiteSprite;
            }
        }
    }
}

