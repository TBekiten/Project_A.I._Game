using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AIGame.Core.Energy;
using AIGame.Game.Util;
using UnityEngine;
using UnityEngine.Rendering;

namespace AIGame.Game.Systems
{
    /// <summary>Şebeke görselinin ayarları. Şehir katmanı ve mini oyun ekranı farklı ayar kullanır.</summary>
    public sealed class BoardStyle
    {
        public float SourceSize = 3f;
        public float JunctionSize = 1.8f;
        public float LoadSize = 2.6f;
        public float LineWidth = 0.7f;
        public float OpenLineWidth = 0.25f;
        public float SwitchSize = 1.5f;

        /// <summary>Düğümden zemine inen ince çizgiler (şehir katmanında binayı gösterir).</summary>
        public bool DropLines;
        public float DropToY;

        public bool ShowLineLabels = true;
    }

    /// <summary>Ekranda gösterilecek bir etiket. Dünya konumundadır; arayüz kameraya göre yerleştirir.</summary>
    public struct BoardLabel
    {
        public Vector3 World;
        public string Text;
        public Color Color;
        public bool Small;
    }

    /// <summary>Tıklanabilir şebeke elemanını işaretler.</summary>
    public sealed class GridPickTarget : MonoBehaviour
    {
        public GridBoard Board;
        public string ElementId;
    }

    /// <summary>
    /// Bir <see cref="PowerGrid"/>'i düğümler (küre/küp) ve hatlar (çizgi) olarak gösterir.
    /// Renkler yük oranını, kalınlık hattın kapalı/açık olduğunu anlatır.
    /// Aynı sınıf hem şehirdeki sistem katmanında hem ayrı mini oyun ekranında kullanılır.
    /// </summary>
    public sealed class GridBoard : MonoBehaviour
    {
        public static readonly Color Calm = new Color(0.25f, 1.4f, 2.0f);
        public static readonly Color Busy = new Color(2.2f, 1.7f, 0.3f);
        public static readonly Color Critical = new Color(2.6f, 0.9f, 0.15f);
        public static readonly Color Danger = new Color(2.8f, 0.25f, 0.2f);
        public static readonly Color OpenColor = new Color(0.32f, 0.36f, 0.42f);
        public static readonly Color DeadColor = new Color(0.18f, 0.22f, 0.3f);
        public static readonly Color FaultColor = new Color(0.55f, 0.06f, 0.06f);
        public static readonly Color PoweredLoad = new Color(2.4f, 1.9f, 0.9f);
        public static readonly Color UnknownLoad = new Color(1.4f, 0.8f, 2.4f);
        public static readonly Color DarkLoad = new Color(0.2f, 0.22f, 0.26f);
        public static readonly Color ShedLoad = new Color(0.55f, 0.12f, 0.12f);

        private sealed class LineVisual
        {
            public GridLine Line;
            public LineRenderer Renderer;
            public Material Material;
            public GameObject Switch;
            public Material SwitchMaterial;
            public Vector3 Middle;
            public Vector3 SwitchScale;
        }

        private sealed class NodeVisual
        {
            public GridNode Node;
            public GameObject Marker;
            public Material Material;
            public Vector3 Position;
            public Vector3 Scale;
            public LineRenderer Drop;
            public Material DropMaterial;
        }

        private readonly Dictionary<string, LineVisual> _lines = new Dictionary<string, LineVisual>();
        private readonly Dictionary<string, NodeVisual> _nodes = new Dictionary<string, NodeVisual>();
        private readonly HashSet<string> _flashing = new HashSet<string>();

        private BoardStyle _style;
        private GridEvaluation _last;
        private string _hovered;

        public PowerGrid Grid { get; private set; }

        public static GridBoard Create(Transform parent, string name, PowerGrid grid,
            IDictionary<string, Vector3> positions, IDictionary<string, Vector3[]> bends, BoardStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var board = go.AddComponent<GridBoard>();
            board.Grid = grid;
            board._style = style ?? new BoardStyle();
            board.Build(positions, bends ?? new Dictionary<string, Vector3[]>());
            return board;
        }

        public Vector3 PositionOf(string nodeId) => _nodes[nodeId].Position;

        private void Build(IDictionary<string, Vector3> positions, IDictionary<string, Vector3[]> bends)
        {
            foreach (var node in Grid.Nodes)
            {
                if (!positions.TryGetValue(node.Id, out var position)) continue;
                var visual = new NodeVisual { Node = node, Position = position };
                var type = node.Kind == GridNodeKind.Source ? PrimitiveType.Cube : PrimitiveType.Sphere;
                var size = node.Kind == GridNodeKind.Source ? _style.SourceSize
                    : node.Kind == GridNodeKind.Junction ? _style.JunctionSize
                    : _style.LoadSize;
                visual.Marker = GameObject.CreatePrimitive(type);
                visual.Marker.name = "Düğüm — " + node.Name;
                visual.Marker.transform.SetParent(transform, false);
                visual.Marker.transform.position = position;
                visual.Scale = Vector3.one * size;
                visual.Marker.transform.localScale = visual.Scale;
                visual.Material = MaterialFactory.Unlit(DarkLoad);
                var renderer = visual.Marker.GetComponent<Renderer>();
                renderer.sharedMaterial = visual.Material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                AddTarget(visual.Marker, node.Id);

                if (_style.DropLines)
                {
                    visual.DropMaterial = MaterialFactory.Unlit(DeadColor);
                    visual.Drop = CreateLine("İniş — " + node.Name, visual.DropMaterial, 0.12f,
                        new[] { position, new Vector3(position.x, _style.DropToY, position.z) });
                }

                _nodes[node.Id] = visual;
            }

            foreach (var line in Grid.Lines)
            {
                if (!_nodes.TryGetValue(line.FromId, out var from) || !_nodes.TryGetValue(line.ToId, out var to)) continue;
                var points = new List<Vector3> { from.Position };
                if (bends.TryGetValue(line.Id, out var extra)) points.AddRange(extra);
                points.Add(to.Position);

                var visual = new LineVisual { Line = line, Material = MaterialFactory.Unlit(OpenColor) };
                visual.Renderer = CreateLine("Hat — " + line.Name, visual.Material, _style.LineWidth, points.ToArray());
                visual.Middle = Midpoint(points);

                if (line.Switchable)
                {
                    visual.Switch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    visual.Switch.name = "Anahtar — " + line.Name;
                    visual.Switch.transform.SetParent(transform, false);
                    visual.Switch.transform.position = visual.Middle + Vector3.up * 0.3f;
                    visual.Switch.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
                    visual.SwitchScale = new Vector3(_style.SwitchSize, _style.SwitchSize * 0.5f, _style.SwitchSize);
                    visual.Switch.transform.localScale = visual.SwitchScale;
                    visual.SwitchMaterial = MaterialFactory.Unlit(OpenColor);
                    var renderer = visual.Switch.GetComponent<Renderer>();
                    renderer.sharedMaterial = visual.SwitchMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    AddTarget(visual.Switch, line.Id);
                }

                _lines[line.Id] = visual;
            }
        }

        private void AddTarget(GameObject go, string elementId)
        {
            var target = go.AddComponent<GridPickTarget>();
            target.Board = this;
            target.ElementId = elementId;
        }

        private LineRenderer CreateLine(string name, Material material, float width, Vector3[] points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.positionCount = points.Length;
            renderer.SetPositions(points);
            renderer.startWidth = width;
            renderer.endWidth = width;
            renderer.numCapVertices = 3;
            renderer.numCornerVertices = 3;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static Vector3 Midpoint(IReadOnlyList<Vector3> points)
        {
            var total = 0f;
            for (var i = 1; i < points.Count; i++) total += Vector3.Distance(points[i - 1], points[i]);
            var half = total / 2f;
            for (var i = 1; i < points.Count; i++)
            {
                var segment = Vector3.Distance(points[i - 1], points[i]);
                if (half <= segment) return Vector3.Lerp(points[i - 1], points[i], segment <= 0f ? 0f : half / segment);
                half -= segment;
            }
            return points[points.Count - 1];
        }

        // ───────────────────────────── Durum ─────────────────────────────

        public void Refresh(GridEvaluation evaluation)
        {
            _last = evaluation;

            foreach (var visual in _lines.Values)
            {
                if (_flashing.Contains(visual.Line.Id)) continue;
                var color = LineColor(visual.Line, evaluation);
                var width = visual.Line.IsLive && evaluation.IsEnergized(visual.Line.FromId) && evaluation.IsEnergized(visual.Line.ToId)
                    ? _style.LineWidth
                    : _style.OpenLineWidth;
                MaterialFactory.SetBaseColor(visual.Material, color);
                visual.Renderer.startWidth = width;
                visual.Renderer.endWidth = width;
                if (visual.SwitchMaterial != null)
                    MaterialFactory.SetBaseColor(visual.SwitchMaterial, visual.Line.Closed ? color : visual.Line.Tripped ? Danger : OpenColor * 1.6f);
            }

            foreach (var visual in _nodes.Values)
            {
                var color = NodeColor(visual.Node, evaluation);
                MaterialFactory.SetBaseColor(visual.Material, color);
                if (visual.DropMaterial != null)
                    MaterialFactory.SetBaseColor(visual.DropMaterial, evaluation.IsPowered(visual.Node.Id) ? color * 0.35f : DeadColor);
            }
        }

        public static Color LineColor(GridLine line, GridEvaluation evaluation)
        {
            if (line.Faulted) return FaultColor;
            if (line.Tripped) return Danger;
            if (!line.Closed) return OpenColor;
            if (!evaluation.IsEnergized(line.FromId) || !evaluation.IsEnergized(line.ToId)) return DeadColor;
            var ratio = evaluation.LoadRatio(line.Id);
            if (ratio > 1f) return Danger;
            if (ratio > 0.9f) return Critical;
            if (ratio > 0.65f) return Busy;
            return Calm;
        }

        public static Color NodeColor(GridNode node, GridEvaluation evaluation)
        {
            switch (node.Kind)
            {
                case GridNodeKind.Source:
                    return new Color(2.2f, 2.2f, 2.4f);
                case GridNodeKind.Junction:
                    return evaluation.IsEnergized(node.Id) ? Calm : DeadColor;
                default:
                    if (!node.Enabled) return ShedLoad;
                    if (!evaluation.IsPowered(node.Id)) return DarkLoad;
                    return node.Identified ? PoweredLoad : UnknownLoad;
            }
        }

        // ───────────────────────────── Etkileşim ─────────────────────────────

        public bool TryPick(Camera camera, Vector2 screenPosition, out string elementId)
        {
            elementId = null;
            if (camera == null || !isActiveAndEnabled) return false;
            var ray = camera.ScreenPointToRay(screenPosition);
            var hits = Physics.RaycastAll(ray, 5000f);
            var bestDistance = float.MaxValue;
            foreach (var hit in hits)
            {
                var target = hit.collider.GetComponent<GridPickTarget>();
                if (target == null || target.Board != this || hit.distance >= bestDistance) continue;
                bestDistance = hit.distance;
                elementId = target.ElementId;
            }
            return elementId != null;
        }

        public void SetHover(string elementId)
        {
            if (_hovered == elementId) return;
            SetHighlight(_hovered, false);
            _hovered = elementId;
            SetHighlight(_hovered, true);
        }

        private void SetHighlight(string elementId, bool on)
        {
            if (elementId == null) return;
            if (_nodes.TryGetValue(elementId, out var node))
                node.Marker.transform.localScale = on ? node.Scale * 1.35f : node.Scale;
            else if (_lines.TryGetValue(elementId, out var line) && line.Switch != null)
                line.Switch.transform.localScale = on ? line.SwitchScale * 1.45f : line.SwitchScale;
        }

        /// <summary>Sigortası atan hatları kısa bir süre kırmızı-beyaz yakıp söndürür.</summary>
        public void Flash(IEnumerable<string> lineIds)
        {
            var ids = lineIds.Where(_lines.ContainsKey).ToList();
            if (ids.Count == 0 || !isActiveAndEnabled) return;
            StartCoroutine(FlashRoutine(ids));
        }

        private IEnumerator FlashRoutine(List<string> ids)
        {
            foreach (var id in ids) _flashing.Add(id);
            for (var i = 0; i < 8; i++)
            {
                foreach (var id in ids)
                {
                    var visual = _lines[id];
                    MaterialFactory.SetBaseColor(visual.Material, i % 2 == 0 ? new Color(4f, 4f, 4f) : Danger);
                    visual.Renderer.startWidth = visual.Renderer.endWidth = _style.LineWidth * 1.4f;
                }
                yield return new WaitForSeconds(0.12f);
            }
            foreach (var id in ids) _flashing.Remove(id);
            if (_last != null) Refresh(_last);
        }

        // ───────────────────────────── Etiketler ─────────────────────────────

        public void CollectLabels(List<BoardLabel> labels)
        {
            if (_last == null) return;
            foreach (var visual in _nodes.Values)
            {
                var node = visual.Node;
                string text;
                switch (node.Kind)
                {
                    case GridNodeKind.Load:
                        text = node.DisplayName + "  " + node.Demand;
                        break;
                    default:
                        text = node.Name;
                        break;
                }
                labels.Add(new BoardLabel
                {
                    World = visual.Position + Vector3.forward * (visual.Scale.z * 0.5f + 0.4f),
                    Text = text,
                    Color = LabelColor(NodeColor(node, _last)),
                    Small = node.Kind == GridNodeKind.Junction
                });
            }

            if (!_style.ShowLineLabels) return;
            foreach (var visual in _lines.Values)
            {
                var line = visual.Line;
                if (!line.Switchable && !line.Faulted) continue;
                string state;
                if (line.Faulted) state = "ARIZALI";
                else if (line.Tripped) state = "SİGORTA ATTI";
                else if (!line.Closed) state = "açık  " + line.Capacity;
                else state = _last.FlowOf(line.Id) + " / " + line.Capacity;
                labels.Add(new BoardLabel
                {
                    World = visual.Middle + Vector3.back * (_style.SwitchSize * 0.5f + 0.6f),
                    Text = line.Name + "\n" + state,
                    Color = LabelColor(LineColor(line, _last)),
                    Small = true
                });
            }
        }

        private static Color LabelColor(Color hdr)
        {
            var max = Mathf.Max(1f, hdr.maxColorComponent);
            var c = new Color(hdr.r / max, hdr.g / max, hdr.b / max, 1f);
            return Color.Lerp(c, Color.white, 0.35f);
        }
    }
}
