using System.Collections.Generic;
using System.Linq;
using AIGame.Core.Energy;
using AIGame.Core.Operations;
using AIGame.Game.Systems;
using AIGame.Game.Util;
using UnityEngine;

namespace AIGame.Game.UI
{
    /// <summary>
    /// Prototip arayüzü (IMGUI). Kurulum gerektirmez; Input Manager ya da Input System fark etmeksizin
    /// çalışır. Gerçek arayüz tasarımı geldiğinde UI Toolkit'e taşınacak; oyun akışı bu sınıfa bağlı değil.
    /// 1920x1080 referans çözünürlükte tasarlandı, ekran yüksekliğine göre ölçeklenir.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public static readonly Color Text = new Color(0.88f, 0.92f, 0.96f);
        public static readonly Color Muted = new Color(0.56f, 0.63f, 0.71f);
        public static readonly Color Accent = new Color(0.4f, 0.88f, 1f);
        public static readonly Color Danger = new Color(1f, 0.42f, 0.36f);
        public static readonly Color Warning = new Color(1f, 0.8f, 0.32f);
        public static readonly Color Good = new Color(0.5f, 1f, 0.62f);
        private static readonly Color PanelColor = new Color(0.03f, 0.045f, 0.07f, 0.92f);
        private static readonly Color PanelEdge = new Color(0.4f, 0.88f, 1f, 0.45f);

        private const float ReferenceHeight = 1080f;

        private readonly List<Rect> _uiRects = new List<Rect>();
        private readonly List<BoardLabel> _labels = new List<BoardLabel>();

        private Texture2D _white;
        private Texture2D _orb;
        private GUIStyle _title, _heading, _body, _small, _tiny, _button, _option, _label, _labelSmall, _clock;
        private float _scale = 1f;
        private float _width;
        private float _height;

        public PrototypeGame Game { get; set; }

        /// <summary>Fare son çizimde bir arayüz panelinin üstündeydi.</summary>
        public bool PointerOverUi { get; private set; }

        private void OnGUI()
        {
            if (Game == null || Game.World == null) return;
            var e = Event.current;

            GUI.matrix = Matrix4x4.identity;
            GuiInput.Capture(e);
            EnsureStyles();

            _scale = Mathf.Max(0.5f, Screen.height / ReferenceHeight);
            _width = Screen.width / _scale;
            _height = Screen.height / _scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_scale, _scale, 1f));
            _uiRects.Clear();

            DrawBoardLabels();

            DrawTopBar();
            DrawNews();
            DrawControls();

            switch (Game.Phase)
            {
                case GamePhase.EventCard:
                    DrawEventCard();
                    break;
                case GamePhase.Minigame:
                    DrawOperationPanel();
                    DrawCityStatusPanel();
                    break;
                case GamePhase.Result:
                    DrawResult();
                    break;
            }

            DrawVoice();
            DrawToast();
            DrawTooltip();
            DrawTitleCard();
            DrawFade();

            var mouse = e.mousePosition;
            PointerOverUi = _uiRects.Any(r => r.Contains(mouse));

            if (e.type == EventType.MouseDown && e.button == 0 && !PointerOverUi && Game.FadeAlpha < 0.5f)
            {
                Game.OnWorldClick(GuiInput.MouseScreen);
                e.Use();
            }
        }

        // ───────────────────────────── Bölümler ─────────────────────────────

        private void DrawTopBar()
        {
            var bar = new Rect(0f, 0f, _width, 66f);
            Panel(bar);

            var world = Game.World;
            var clock = world.Clock.Format();
            GUI.Label(new Rect(24f, 8f, 360f, 26f), Tint(Game.Scenario.District, Muted), _small);
            GUI.Label(new Rect(24f, 26f, 360f, 36f), clock + (Game.Paused ? Tint("   DURAKLATILDI", Warning) : ""), _clock);

            // Toplam çubuğu: sol yıkım, sağ koruma.
            const float barWidth = 520f;
            var barRect = new Rect((_width - barWidth) / 2f, 20f, barWidth, 14f);
            Fill(barRect, new Color(0.1f, 0.13f, 0.18f));
            var center = barRect.x + barWidth / 2f;
            var value = world.AlignmentNormalized;
            var fillWidth = Mathf.Abs(value) * barWidth / 2f;
            Fill(value >= 0f ? new Rect(center, barRect.y, fillWidth, barRect.height)
                : new Rect(center - fillWidth, barRect.y, fillWidth, barRect.height), value >= 0f ? Accent : Danger);
            Fill(new Rect(center - 1f, barRect.y - 4f, 2f, barRect.height + 8f), Text);
            var marker = center + value * barWidth / 2f;
            Fill(new Rect(marker - 3f, barRect.y - 6f, 6f, barRect.height + 12f), UnityEngine.Color.white);
            GUI.Label(new Rect(barRect.x - 110f, 14f, 100f, 26f), Tint("YIKIM", Danger), RightAligned(_small));
            GUI.Label(new Rect(barRect.xMax + 10f, 14f, 100f, 26f), Tint("KORUMA", Accent), _small);
            GUI.Label(new Rect(barRect.x, 38f, barWidth, 24f),
                Tint("Toplam çubuğu  " + Signed(world.Alignment), Muted), Centered(_tiny));

            // Sentinel ve işlem gücü.
            var right = _width - 24f;
            GUI.Label(new Rect(right - 470f, 8f, 230f, 24f), Tint("SENTINEL DİKKATİ", Muted), _tiny);
            var attention = new Rect(right - 470f, 32f, 220f, 12f);
            Fill(attention, new Color(0.1f, 0.13f, 0.18f));
            Fill(new Rect(attention.x, attention.y, attention.width * world.Attention / 100f, attention.height),
                UnityEngine.Color.Lerp(Warning, Danger, world.Attention / 100f));
            GUI.Label(new Rect(right - 240f, 22f, 40f, 30f), Tint(world.Attention.ToString(), Text), _body);

            GUI.Label(new Rect(right - 180f, 8f, 180f, 24f), Tint("İŞLEM GÜCÜ", Muted), RightAligned(_tiny));
            GUI.Label(new Rect(right - 180f, 24f, 180f, 36f), Tint(world.Compute.ToString(), Accent), RightAligned(_clock));
        }

        private void DrawNews()
        {
            if (Game.News.Count == 0) return;
            const float width = 400f;
            var x = _width - width - 24f;
            var y = 84f;
            var items = Game.News.Reverse().Take(Game.Phase == GamePhase.Minigame ? 0 : 6).ToList();
            if (items.Count == 0) return;

            var heights = items.Select(n => _body.CalcHeight(new GUIContent(n.Item.Headline), width - 32f) + 24f).ToList();
            var total = 44f + heights.Sum() + 8f;
            var panel = new Rect(x, y, width, total);
            Panel(panel);
            GUI.Label(new Rect(x + 16f, y + 10f, width - 32f, 24f), Tint("ŞEHİRDEN HABERLER", Muted), _tiny);

            var cy = y + 40f;
            for (var i = 0; i < items.Count; i++)
            {
                var news = items[i];
                var age = Time.unscaledTime - news.RealTime;
                if (age < 4f) Fill(new Rect(x + 4f, cy + 2f, 3f, heights[i] - 8f), Accent);
                GUI.Label(new Rect(x + 16f, cy, width - 32f, 20f),
                    Tint(AIGame.Core.Simulation.GameClock.Format(news.Item.TimeMinutes) + "  ·  " + news.Item.Source, Muted), _tiny);
                GUI.Label(new Rect(x + 16f, cy + 18f, width - 32f, heights[i]), news.Item.Headline, _body);
                cy += heights[i];
            }
        }

        private void DrawControls()
        {
            string text;
            switch (Game.Phase)
            {
                case GamePhase.Minigame:
                    text = "[Sol tık] Anahtar: hattı aç / kapat   ·   Yük: devreden çıkar / geri ver";
                    break;
                case GamePhase.Aftermath when Game.ShowEndPrompt:
                    text = "[R] Baştan başla   ·   [Tab] Sistem katmanı   ·   [WASD] Kaydır   ·   [Tekerlek] Yakınlaş";
                    break;
                case GamePhase.City:
                case GamePhase.Aftermath:
                    text = "[Tab] Sistem katmanı   ·   [WASD] Kaydır   ·   [Tekerlek] Yakınlaş   ·   [Boşluk] Zamanı durdur";
                    break;
                default:
                    return;
            }
            GUI.Label(new Rect(24f, _height - 34f, _width - 48f, 24f), Tint(text, Muted), _tiny);
        }

        private void DrawEventCard()
        {
            const float width = 780f;
            var s = Game.Scenario;
            var bodyHeight = _body.CalcHeight(new GUIContent(s.EventBody), width - 56f);
            var optionHeights = s.Options.Select(o => Mathf.Max(84f, _small.CalcHeight(new GUIContent(o.Description), width - 96f) + 50f)).ToList();
            var height = 28f + 34f + 12f + bodyHeight + 24f + optionHeights.Sum() + optionHeights.Count * 12f + 20f;
            var rect = new Rect((_width - width) / 2f, Mathf.Max(90f, (_height - height) / 2f - 40f), width, height);
            Panel(rect, Danger);

            var y = rect.y + 22f;
            GUI.Label(new Rect(rect.x + 28f, y, width - 56f, 20f), Tint("OLAY", Danger), _tiny);
            y += 20f;
            GUI.Label(new Rect(rect.x + 28f, y, width - 56f, 34f), s.EventTitle, _heading);
            y += 42f;
            GUI.Label(new Rect(rect.x + 28f, y, width - 56f, bodyHeight), s.EventBody, _body);
            y += bodyHeight + 20f;

            for (var i = 0; i < s.Options.Count; i++)
            {
                var option = s.Options[i];
                var color = option.Intent == Intent.Save ? Accent : option.Intent == Intent.Harm ? Danger : Muted;
                var optionRect = new Rect(rect.x + 28f, y, width - 56f, optionHeights[i]);
                if (GUI.Button(optionRect, GUIContent.none, _option)) Game.ChooseOption(option.Intent);
                Fill(new Rect(optionRect.x, optionRect.y, 4f, optionRect.height), color);
                GUI.Label(new Rect(optionRect.x + 22f, optionRect.y + 12f, optionRect.width - 40f, 28f), Tint(option.Title.ToUpperInvariant(), color), _heading);
                GUI.Label(new Rect(optionRect.x + 22f, optionRect.y + 44f, optionRect.width - 40f, optionRect.height - 48f), option.Description, _small);
                y += optionHeights[i] + 12f;
            }
        }

        private void DrawOperationPanel()
        {
            var op = Game.Operation;
            if (op == null) return;
            const float width = 430f;
            var rect = new Rect(24f, 84f, width, _height - 84f - 190f);
            var color = op.Intent == Intent.Save ? Accent : Danger;
            Panel(rect, color);

            var x = rect.x + 22f;
            var w = width - 44f;
            var y = rect.y + 18f;
            GUI.Label(new Rect(x, y, w, 22f), Tint(op.Intent == Intent.Save ? "OPERASYON · KURTARMA" : "OPERASYON · ZARAR VERME", color), _small);
            y += 30f;

            GUI.Label(new Rect(x, y, w, 20f), Tint("HEDEF", Muted), _tiny);
            y += 20f;
            var goalHeight = _body.CalcHeight(new GUIContent(op.Goal.Description), w);
            GUI.Label(new Rect(x, y, w, goalHeight), op.Goal.Description, _body);
            y += goalHeight + 8f;
            GUI.Label(new Rect(x, y, w, 26f), op.GoalMet ? Tint("Hedef tamamlandı", Good) : Tint("Hedef henüz tamamlanmadı", Muted), _small);
            y += 36f;

            Stat(new Rect(x, y, 90f, 56f), "KOMUT", op.Commands.ToString(), Text);
            Stat(new Rect(x + 100f, y, 90f, 56f), "SİGORTA", op.Trips.ToString(), op.Trips > 0 ? Danger : Text);
            Stat(new Rect(x + 200f, y, 90f, 56f), "İZ", op.Suspicion.ToString(), op.Suspicion > 2 ? Warning : Text);
            Stat(new Rect(x + 300f, y, 90f, 56f), "SAAT", op.Clock.Format(), Text);
            y += 66f;

            GUI.Label(new Rect(x, y, w, 20f), Tint("RENKLER", Muted), _tiny);
            y += 22f;
            Legend(ref y, x, GridBoard.Calm, "Hat rahat");
            Legend(ref y, x, GridBoard.Busy, "Hat yoğun (%65+)");
            Legend(ref y, x, GridBoard.Critical, "Hat sınırda (%90+)");
            Legend(ref y, x, GridBoard.OpenColor, "Açık hat (enerji geçmez)");
            Legend(ref y, x, GridBoard.Danger, "Sigorta attı / arızalı");
            Legend(ref y, x, GridBoard.PoweredLoad, "Yük enerjili");
            Legend(ref y, x, GridBoard.ShedLoad, "Yük atıldı");
            y += 8f;

            GUI.Label(new Rect(x, y, w, 20f), Tint("KAYIT", Muted), _tiny);
            y += 20f;
            foreach (var entry in op.Log.Reverse().Take(4))
            {
                var text = AIGame.Core.Simulation.GameClock.Format(entry.TimeMinutes) + "  " + entry.Message;
                var h = _tiny.CalcHeight(new GUIContent(text), w);
                if (y + h > rect.yMax - 70f) break;
                GUI.Label(new Rect(x, y, w, h), Tint(text, entry.Tripped.Count > 0 ? Danger : Muted), _tiny);
                y += h + 2f;
            }

            var button = new Rect(x, rect.yMax - 62f, w, 46f);
            var label = op.GoalMet ? "OPERASYONU BİTİR · ŞEHRE DÖN" : "BİTİR (hedef tamamlanmadı) · ŞEHRE DÖN";
            if (GUI.Button(button, label, _button)) Game.FinishOperation();
        }

        private void DrawCityStatusPanel()
        {
            var op = Game.Operation;
            if (op == null) return;
            const float width = 330f;
            var loads = Game.Grid.Nodes.Where(n => n.Kind == GridNodeKind.Load).ToList();
            var height = 60f + loads.Count * 30f + 44f;
            var rect = new Rect(_width - width - 24f, 84f, width, height);
            Panel(rect);

            var x = rect.x + 20f;
            var y = rect.y + 14f;
            GUI.Label(new Rect(x, y, width - 40f, 22f), Tint("ŞEHİRDE ŞU AN", Muted), _tiny);
            y += 28f;
            foreach (var node in loads)
            {
                string state;
                Color color;
                if (!node.Enabled)
                {
                    state = "devre dışı";
                    color = Danger;
                }
                else if (op.Current.IsPowered(node.Id))
                {
                    state = "enerjili";
                    color = Warning;
                }
                else
                {
                    state = "karanlık";
                    color = Muted;
                }
                Fill(new Rect(x, y + 8f, 10f, 10f), color);
                GUI.Label(new Rect(x + 18f, y, 190f, 26f), node.DisplayName, _small);
                GUI.Label(new Rect(x + 200f, y, 90f, 26f), Tint(state, color), RightAligned(_small));
                y += 30f;
            }
            GUI.Label(new Rect(x, y + 6f, width - 40f, 40f), Tint("Şehirdeki sonucu döndüğünde göreceksin.", Muted), _tiny);
        }

        private void DrawResult()
        {
            var result = Game.LastResult;
            if (result == null) return;
            const float width = 820f;
            var x0 = (_width - width) / 2f;
            var w = width - 60f;

            var lines = result.Outcomes.Select(o => Tint(Signed(o.AlignmentDelta), o.AlignmentDelta > 0 ? Good : o.AlignmentDelta < 0 ? Danger : Muted)
                                                    + "   " + o.Text).ToList();
            var linesHeight = lines.Sum(l => _body.CalcHeight(new GUIContent(l), w) + 4f);
            var summaryHeight = _body.CalcHeight(new GUIContent(result.ReadingSummary), w);
            var height = 22f + 40f + 30f + 30f + linesHeight + 26f + 30f + summaryHeight + 30f + 60f + 30f + 70f;
            var rect = new Rect(x0, Mathf.Max(84f, (_height - height) / 2f - 40f), width, height);
            Panel(rect, ReadingColor(result.Reading));

            var x = rect.x + 30f;
            var y = rect.y + 22f;
            GUI.Label(new Rect(x, y, w, 36f), "Sonuç", _title);
            y += 40f;
            GUI.Label(new Rect(x, y, w, 26f), Tint("Niyetin: " + IntentText(result.Intent) +
                                                    (result.Intent == Intent.Ignore ? "" : result.GoalMet ? "  ·  hedef tamamlandı" : "  ·  hedef tamamlanamadı"), Muted), _small);
            y += 34f;

            GUI.Label(new Rect(x, y, w, 20f), Tint("GERÇEKTE OLAN", Muted), _tiny);
            y += 24f;
            foreach (var line in lines)
            {
                var h = _body.CalcHeight(new GUIContent(line), w);
                GUI.Label(new Rect(x, y, w, h), line, _body);
                y += h + 4f;
            }
            y += 18f;

            GUI.Label(new Rect(x, y, w, 20f), Tint("İNSANLARIN GÖRDÜĞÜ", Muted), _tiny);
            y += 22f;
            GUI.Label(new Rect(x, y, w, 30f), Tint(ReadingText(result), ReadingColor(result.Reading)), _heading);
            y += 32f;
            GUI.Label(new Rect(x, y, w, summaryHeight), result.ReadingSummary, _body);
            y += summaryHeight + 22f;

            Stat(new Rect(x, y, 200f, 56f), "TOPLAM ÇUBUĞU", Signed(result.AlignmentDelta), result.AlignmentDelta >= 0 ? Accent : Danger);
            Stat(new Rect(x + 220f, y, 200f, 56f), "SENTINEL DİKKATİ", Signed(result.AttentionDelta), result.AttentionDelta > 0 ? Warning : Text);
            Stat(new Rect(x + 440f, y, 200f, 56f), "İŞLEM GÜCÜ", Signed(result.ComputeDelta), result.ComputeDelta >= 0 ? Good : Text);
            y += 64f;
            if (result.Intent != Intent.Ignore)
                GUI.Label(new Rect(x, y, w, 24f), Tint("Komut " + result.Commands + "   ·   Sigorta " + result.Trips + "   ·   İz " + result.Suspicion, Muted), _tiny);

            var button = new Rect(rect.xMax - 230f, rect.yMax - 66f, 200f, 46f);
            if (GUI.Button(button, "DEVAM", _button)) Game.ContinueAfterResult();
        }

        private void DrawVoice()
        {
            var voice = Game.Voice;
            const float width = 860f;
            var x = (_width - width) / 2f;
            var orbSize = 58f;
            var pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * (voice.IsSpeaking ? 9f : 2f));

            if (!voice.HasLine)
            {
                var idle = new Rect(x, _height - 64f - orbSize, orbSize, orbSize);
                GUI.color = new Color(Accent.r, Accent.g, Accent.b, 0.35f * pulse);
                GUI.DrawTexture(idle, _orb);
                GUI.color = UnityEngine.Color.white;
                return;
            }

            var text = voice.Visible ?? "";
            var textHeight = Mathf.Max(30f, _body.CalcHeight(new GUIContent(text.Length > 0 ? text : " "), width - orbSize - 64f));
            var height = textHeight + 34f;
            var rect = new Rect(x, _height - 54f - height, width, height);
            var alpha = Mathf.Clamp01(voice.Opacity);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            Panel(rect);
            GUI.color = new Color(Accent.r, Accent.g, Accent.b, pulse * alpha);
            GUI.DrawTexture(new Rect(rect.x + 14f, rect.y + (height - orbSize) / 2f, orbSize, orbSize), _orb);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(rect.x + orbSize + 32f, rect.y + 10f, 60f, 18f), Tint("A.I.", Accent), _tiny);
            GUI.Label(new Rect(rect.x + orbSize + 32f, rect.y + 26f, width - orbSize - 64f, textHeight), text, _body);
            GUI.color = UnityEngine.Color.white;

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) voice.Skip();
        }

        private void DrawToast()
        {
            if (string.IsNullOrEmpty(Game.ToastText) || Time.unscaledTime > Game.ToastUntil) return;
            var content = new GUIContent(Game.ToastText);
            var width = Mathf.Min(760f, _small.CalcSize(content).x + 48f);
            var rect = new Rect((_width - width) / 2f, 80f, width, 40f);
            var alpha = Mathf.Clamp01((Game.ToastUntil - Time.unscaledTime) / 0.4f);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            Panel(rect, Game.ToastColor);
            GUI.Label(new Rect(rect.x + 24f, rect.y + 8f, width - 48f, 26f), Tint(Game.ToastText, Game.ToastColor), Centered(_small));
            GUI.color = UnityEngine.Color.white;
        }

        private void DrawTooltip()
        {
            var text = Game.HoverText;
            if (string.IsNullOrEmpty(text)) return;
            const float width = 360f;
            var height = _small.CalcHeight(new GUIContent(text), width - 28f) + 22f;
            var mouse = Event.current.mousePosition;
            var x = Mathf.Min(mouse.x + 22f, _width - width - 8f);
            var y = Mathf.Min(mouse.y + 22f, _height - height - 8f);
            var rect = new Rect(x, y, width, height);
            Fill(rect, new Color(0.02f, 0.03f, 0.05f, 0.96f));
            Fill(new Rect(rect.x, rect.y, 3f, rect.height), Accent);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 10f, width - 28f, height - 20f), text, _small);
        }

        private void DrawBoardLabels()
        {
            var board = Game.ActiveBoard;
            var camera = Game.ActiveCamera;
            if (board == null || camera == null || Event.current.type != EventType.Repaint) return;

            _labels.Clear();
            board.CollectLabels(_labels);
            foreach (var label in _labels)
            {
                var screen = camera.WorldToScreenPoint(label.World);
                if (screen.z < 0f) continue;
                var style = label.Small ? _labelSmall : _label;
                var content = new GUIContent(label.Text);
                var size = style.CalcSize(content);
                var gx = screen.x / _scale - size.x / 2f;
                var gy = (Screen.height - screen.y) / _scale - size.y;
                var shadow = new Color(0f, 0f, 0f, 0.85f);
                style.normal.textColor = shadow;
                GUI.Label(new Rect(gx + 1.5f, gy + 1.5f, size.x, size.y), content, style);
                style.normal.textColor = label.Color;
                GUI.Label(new Rect(gx, gy, size.x, size.y), content, style);
            }
        }

        private void DrawTitleCard()
        {
            if (string.IsNullOrEmpty(Game.TitleCard)) return;
            var remaining = Game.TitleCardUntil - Time.unscaledTime;
            if (remaining <= 0f) return;
            var alpha = Mathf.Clamp01(remaining / 0.6f);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(0f, _height * 0.36f, _width, 60f), Tint(Game.TitleCard, Accent), Centered(_title));
            GUI.Label(new Rect(0f, _height * 0.36f + 56f, _width, 30f), Tint(Game.Scenario.District, Muted), Centered(_small));
            GUI.color = UnityEngine.Color.white;
        }

        private void DrawFade()
        {
            if (Game.FadeAlpha <= 0.001f) return;
            GUI.color = new Color(0f, 0f, 0f, Game.FadeAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, _width, _height), _white);
            GUI.color = UnityEngine.Color.white;
        }

        // ───────────────────────────── Yardımcılar ─────────────────────────────

        private void Panel(Rect rect, Color? edge = null)
        {
            _uiRects.Add(rect);
            var tint = GUI.color;
            GUI.color = new Color(PanelColor.r, PanelColor.g, PanelColor.b, PanelColor.a * tint.a);
            GUI.DrawTexture(rect, _white);
            var e = edge ?? PanelEdge;
            GUI.color = new Color(e.r, e.g, e.b, (edge.HasValue ? 0.9f : PanelEdge.a) * tint.a);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), _white);
            GUI.color = tint;
        }

        private void Fill(Rect rect, Color color)
        {
            var tint = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * tint.a);
            GUI.DrawTexture(rect, _white);
            GUI.color = tint;
        }

        private void Stat(Rect rect, string title, string value, Color color)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 20f), Tint(title, Muted), _tiny);
            GUI.Label(new Rect(rect.x, rect.y + 18f, rect.width, 36f), Tint(value, color), _clock);
        }

        private void Legend(ref float y, float x, Color hdr, string text)
        {
            var max = Mathf.Max(1f, hdr.maxColorComponent);
            Fill(new Rect(x, y + 6f, 22f, 8f), new Color(hdr.r / max, hdr.g / max, hdr.b / max));
            GUI.Label(new Rect(x + 32f, y, 340f, 22f), Tint(text, Text), _tiny);
            y += 21f;
        }

        private static string IntentText(Intent intent)
        {
            switch (intent)
            {
                case Intent.Save: return "kurtarmak";
                case Intent.Harm: return "zarar vermek";
                default: return "görmezden gelmek";
            }
        }

        private static string ReadingText(OperationResult result)
        {
            if (result.Intent == Intent.Ignore || result.Commands == 0) return "Hiçbir şey";
            switch (result.Reading)
            {
                case PublicReading.Natural: return result.AlignmentDelta >= 0 ? "Otomatik sistemler" : "Doğal arıza";
                case PublicReading.Unexplained: return "Açıklanamayan müdahale";
                default: return "Siber saldırı";
            }
        }

        private static Color ReadingColor(PublicReading reading)
        {
            switch (reading)
            {
                case PublicReading.Natural: return Good;
                case PublicReading.Unexplained: return Warning;
                default: return Danger;
            }
        }

        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

        private static string Tint(string text, Color color) =>
            "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";

        private static GUIStyle Centered(GUIStyle style) => new GUIStyle(style) { alignment = TextAnchor.UpperCenter };
        private static GUIStyle RightAligned(GUIStyle style) => new GUIStyle(style) { alignment = TextAnchor.UpperRight };

        private void EnsureStyles()
        {
            if (_white != null && _body != null) return;

            _white = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            _white.SetPixel(0, 0, UnityEngine.Color.white);
            _white.Apply();

            const int orbResolution = 64;
            _orb = new Texture2D(orbResolution, orbResolution, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            for (var y = 0; y < orbResolution; y++)
            for (var x = 0; x < orbResolution; x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * orbResolution / 2f) / (orbResolution / 2f);
                var core = Mathf.Clamp01(1f - d / 0.45f);
                var glow = Mathf.Clamp01(1f - d) * 0.55f;
                var a = Mathf.Clamp01(core + glow * glow);
                _orb.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            _orb.Apply();

            GUIStyle Make(int size, FontStyle fontStyle = FontStyle.Normal)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size,
                    fontStyle = fontStyle,
                    wordWrap = true,
                    richText = true,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0)
                };
                style.normal.textColor = Text;
                return style;
            }

            _title = Make(32, FontStyle.Bold);
            _heading = Make(24, FontStyle.Bold);
            _body = Make(19);
            _small = Make(17);
            _tiny = Make(14);
            _clock = Make(26, FontStyle.Bold);
            _clock.wordWrap = false;

            _label = Make(16, FontStyle.Bold);
            _label.wordWrap = false;
            _label.richText = false;
            _label.alignment = TextAnchor.UpperCenter;
            _labelSmall = Make(13);
            _labelSmall.wordWrap = false;
            _labelSmall.richText = false;
            _labelSmall.alignment = TextAnchor.UpperCenter;

            Texture2D Tex(Color c)
            {
                var t = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
                t.SetPixel(0, 0, c);
                t.Apply();
                return t;
            }

            _button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            _button.normal.background = Tex(new Color(0.08f, 0.2f, 0.28f));
            _button.hover.background = Tex(new Color(0.12f, 0.3f, 0.4f));
            _button.active.background = Tex(new Color(0.18f, 0.42f, 0.55f));
            _button.focused.background = _button.normal.background;
            _button.normal.textColor = _button.hover.textColor = _button.active.textColor = _button.focused.textColor = Text;

            _option = new GUIStyle(GUI.skin.button);
            _option.normal.background = Tex(new Color(0.06f, 0.08f, 0.12f));
            _option.hover.background = Tex(new Color(0.1f, 0.14f, 0.2f));
            _option.active.background = Tex(new Color(0.14f, 0.2f, 0.28f));
            _option.focused.background = _option.normal.background;
        }
    }
}
