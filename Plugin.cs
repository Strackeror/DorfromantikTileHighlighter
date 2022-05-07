using BepInEx;
using System.Linq;
using Dorfromantik;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using System.Collections.Generic;

namespace BepInEx5.PluginTemplate
{
    public static class ObjectExtension
    {

        public static void GetField<T, U>(this T obj, string fieldName, out U? output)
        {
            output = (U?)typeof(T).GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(obj);
        }
    }

    [BepInPlugin("ed00382f-1fad-4b65-96fb-a19b216cd810", "TileHighlight", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        MatchingTileEdgeHighlighter? hoverHighlighter;
        Tile? currentTile;

        Dictionary<Vector2Int, MatchingTileEdgeHighlighter> placedHighlighters = new Dictionary<Vector2Int, MatchingTileEdgeHighlighter>();

        InputRouter? router;

        void Awake()
        {

        }

        private Tile? GetHoveredTile()
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Pointer.current.position.ReadValue();
            List<RaycastResult> raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, raycastResults);
            foreach (RaycastResult raycastResult in raycastResults)
            {
                if (raycastResult.gameObject.layer == 5)
                    return null;
                if (raycastResult.gameObject.layer == 10 && (bool)(UnityEngine.Object)raycastResult.gameObject.GetComponent<Tile>())
                    return raycastResult.gameObject.GetComponent<Tile>();
            }
            return null;
        }

        private void updateTileHighlighter(Tile tile, MatchingTileEdgeHighlighter edgeHl, bool animate = false)
        {
            edgeHl.gameObject.SetActive(true);

            edgeHl.transform.SetParent(tile.transform, false);
            foreach (var dir in Enumerable.Range(0, 6))
            {
                var neighbour = tile.GetNeighbor(dir, Space.World);
                if (neighbour == null || neighbour.State != TileState.placed)
                {
                    Logger.LogDebug($"tile {tile.GridPos} dir:{dir} no edge");
                    edgeHl.HighlightEdge(dir, TileEdgeState.Undefined, animate);
                    continue;
                }

                var neighbourDir = (dir + 3) % 6;

                var tileEdgeTypes = tile.GetEdgeTypes(dir, Space.World);
                var neighbourEdgeTypes = neighbour.GetEdgeTypes(neighbourDir, Space.World);
                if (tileEdgeTypes.Count == 0 && neighbourEdgeTypes.Count == 0)
                {
                    Logger.LogDebug($"tile {tile.GridPos} dir:{dir} good edge");
                    edgeHl.HighlightEdge(dir, TileEdgeState.Perfect, animate);
                    continue;
                }
                if (tileEdgeTypes.Any(t => neighbourEdgeTypes.Contains(t)))
                {
                    Logger.LogDebug($"tile {tile.GridPos} dir:{dir} good edge");
                    edgeHl.HighlightEdge(dir, TileEdgeState.Perfect, animate);
                    continue;
                }

                if (tile.GetHybridEdges(dir, Space.World).Any() && neighbourEdgeTypes.Count() == 0)
                {
                    Logger.LogDebug($"tile {tile.GridPos} dir:{dir} good edge");
                    edgeHl.HighlightEdge(dir, TileEdgeState.Perfect, animate);
                    continue;
                }
                if (neighbour.GetHybridEdges(neighbourDir, Space.World).Any() && tileEdgeTypes.Count() == 0)
                {
                    Logger.LogDebug($"tile {tile.GridPos} dir:{dir} good edge");
                    edgeHl.HighlightEdge(dir, TileEdgeState.Perfect, animate);
                    continue;
                }

                Logger.LogDebug($"tile {tile.GridPos} dir:{dir} bad edge");
                edgeHl.HighlightEdge(dir, TileEdgeState.Imperfect, animate);
            }

        }

        private void refreshTile(Tile tile)
        {
            Logger.LogInfo($"tile {tile.GridPos} refreshed");
            if (!placedHighlighters.Keys.Contains(tile.GridPos))
            {
                return;
            }
            updateTileHighlighter(tile, placedHighlighters[tile.GridPos]);
        }

        IEnumerator<int> UpdateTileNextFrame(Tile tile, MatchingTileEdgeHighlighter edgeHl)
        {
            yield return 0;
            updateTileHighlighter(tile, edgeHl, true);
            yield break;
        }

        private void Update()
        {
            if (router == null)
            {

                Singleton<InputManager>.Instance.GetField("inputRouter", out router);
                if (router != null)
                {
                    Logger.LogMessage("Found router");
                }
            }

            if (hoverHighlighter == null)
            {
                var highlighters = FindObjectsOfType(typeof(MatchingTileEdgeHighlighter), true) as MatchingTileEdgeHighlighter[] ?? new MatchingTileEdgeHighlighter[] { };
                if (highlighters.Length == 0)
                {
                    return;
                }
                var obj = Instantiate(highlighters[0].gameObject);
                hoverHighlighter = obj.GetComponent<MatchingTileEdgeHighlighter>();
                hoverHighlighter.name = "MatchingModHighlighter";
                Logger.LogInfo("Instantiated highlighter");
            }

            var tile = GetHoveredTile();
            if (tile?.State != TileState.placed)
            {
                tile = null;
            }

            if (router?.ActiveTool != ToolId.None)
            {
                tile = null;
            }

            if (tile != currentTile)
            {
                currentTile = tile;
                if (tile == null)
                {
                    Logger.LogInfo("Stopped highligting");
                    hoverHighlighter.gameObject.SetActive(false);
                }
                else
                {
                    Logger.LogInfo(("Highlighted:", tile.GridPos));
                    updateTileHighlighter(tile, hoverHighlighter);
                }
            }

            if (Input.GetMouseButtonDown(0) && currentTile != null)
            {
                var refreshEvent = (int _, Tile tile) => refreshTile(tile);
                if (placedHighlighters.Keys.Contains(currentTile.GridPos))
                {
                    Logger.LogInfo($"Remove highlighter from {currentTile.GridPos}");
                    Destroy(placedHighlighters[currentTile.GridPos].gameObject);
                    placedHighlighters.Remove(currentTile.GridPos);
                    currentTile.OnNeighborTilePlaced -= refreshEvent;
                }
                else
                {
                    Logger.LogInfo($"Add highlighter to {currentTile.GridPos}");
                    var newHighlighter = Instantiate(hoverHighlighter).GetComponent<MatchingTileEdgeHighlighter>();
                    if (newHighlighter == null)
                    {
                        Logger.LogError("Failed to instantiate new highlighter");
                        return;
                    }
                    newHighlighter.name = $"MatchingModHighligter{currentTile.GridPos}";
                    newHighlighter.gameObject.SetActive(true);
                    newHighlighter.transform.SetParent(currentTile.transform, false);
                    hoverHighlighter.gameObject.SetActive(false);
                    currentTile.OnNeighborTilePlaced += refreshEvent;
                    placedHighlighters.Add(currentTile.GridPos, newHighlighter);
                    StartCoroutine(UpdateTileNextFrame(currentTile, newHighlighter));
                }
            }
        }
    }
}
