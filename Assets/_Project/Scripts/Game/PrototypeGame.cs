using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AIGame.Core.Content;
using AIGame.Core.Energy;
using AIGame.Core.Narrative;
using AIGame.Core.Operations;
using AIGame.Core.Simulation;
using AIGame.Game.City;
using AIGame.Game.Minigames;
using AIGame.Game.Systems;
using AIGame.Game.UI;
using AIGame.Game.Util;
using UnityEngine;
using UnityEngine.Rendering;
using Ids = AIGame.Core.Content.EastDistrictScenario.Ids;

namespace AIGame.Game
{
    public enum GamePhase
    {
        /// <summary>Şehir yaşıyor, saat akıyor.</summary>
        City,
        /// <summary>Olay oldu, kamera olay yerine dönüyor.</summary>
        EventIntro,
        /// <summary>Oyuncu niyetini seçiyor: kurtar, zarar ver, görmezden gel.</summary>
        EventCard,
        /// <summary>Ekran geçişi (kararma).</summary>
        Transition,
        /// <summary>Ayrı mini oyun bölümü.</summary>
        Minigame,
        /// <summary>Şehre dönüldü, sonuç şehirde sahneleniyor.</summary>
        Reveal,
        /// <summary>Sonuç paneli.</summary>
        Result,
        /// <summary>Olayın ardından: haberler, ambulans, A.I.'ın yorumu.</summary>
        Aftermath
    }

    public struct PublishedNews
    {
        public NewsItem Item;
        public float RealTime;
    }

    /// <summary>
    /// Prototipin giriş noktası ve akışı. Sahnedeki tek bileşen budur; şehir, mini oyun ekranı,
    /// kamera ve arayüz çalışma anında koddan kurulur.
    ///
    /// Akış: şehir (saat akar) → 19:00 olay → olay kartı → [ayrı mini oyun ekranı] →
    /// şehre dönüş, sonucun sahnelenmesi → sonuç paneli → haberler ve ambulans.
    /// </summary>
    public sealed class PrototypeGame : MonoBehaviour
    {
        public static readonly Color NightSky = new Color(0.025f, 0.035f, 0.07f);

        [Tooltip("Şehir ekranında gerçek saniye başına geçen oyun dakikası.")]
        public float MinutesPerSecond = 1f;

        [Tooltip("Olaydan sonra prototipin bitiş mesajına kadar geçen oyun dakikası.")]
        public float AftermathMinutes = 35f;

        private static readonly string[] RevealOrder =
        {
            Ids.Hospital, Ids.Housing, Ids.StreetLamps, Ids.TrafficLights, Ids.Pump, Ids.Market
        };

        private readonly List<PublishedNews> _news = new List<PublishedNews>();
        private readonly HashSet<string> _hintsShown = new HashSet<string>();

        private Transform _root;
        private Hud _hud;
        private bool _eventFired;
        private bool _goalAnnounced;
        private double _aftermathStart;
        private string _hovered;

        public EnergyScenario Scenario { get; private set; }
        public WorldState World { get; private set; }
        public PowerGrid Grid { get; private set; }
        public EnergyOperation Operation { get; private set; }
        public OperationResult LastResult { get; private set; }
        public GamePhase Phase { get; private set; }
        public VoiceQueue Voice { get; } = new VoiceQueue();

        public EastDistrictCity City { get; private set; }
        public CameraRig Rig { get; private set; }
        public GridBoard CityLayer { get; private set; }
        public EnergyMinigameScreen Minigame { get; private set; }

        public bool Paused { get; private set; }
        public bool SystemLayerVisible => CityLayer != null && CityLayer.gameObject.activeSelf;
        public bool ShowEndPrompt { get; private set; }
        public string HoverText { get; private set; }
        public IReadOnlyList<PublishedNews> News => _news;

        public float FadeAlpha { get; private set; }
        public string TitleCard { get; private set; }
        public float TitleCardUntil { get; private set; }

        public string ToastText { get; private set; }
        public Color ToastColor { get; private set; }
        public float ToastUntil { get; private set; }

        /// <summary>Etiketlerin ve ipuçlarının çizileceği şebeke görseli ve kamerası.</summary>
        public GridBoard ActiveBoard
        {
            get
            {
                if (Phase == GamePhase.Minigame && Minigame != null) return Minigame.Board;
                if ((Phase == GamePhase.City || Phase == GamePhase.Aftermath) && SystemLayerVisible) return CityLayer;
                return null;
            }
        }

        public Camera ActiveCamera => Phase == GamePhase.Minigame && Minigame != null ? Minigame.Camera : Rig != null ? Rig.Camera : null;

        // ───────────────────────────── Kurulum ─────────────────────────────

        private void Awake()
        {
            _hud = GetComponent<Hud>();
            if (_hud == null) _hud = gameObject.AddComponent<Hud>();
            _hud.Game = this;
            Build();
        }

        private void Build()
        {
            _root = new GameObject("Prototip").transform;
            _news.Clear();
            _hintsShown.Clear();
            _eventFired = false;
            _goalAnnounced = false;
            _hovered = null;
            Operation = null;
            LastResult = null;
            Paused = false;
            ShowEndPrompt = false;
            FadeAlpha = 0f;
            TitleCard = null;
            Voice.Clear();

            Scenario = EastDistrictScenario.Create();
            Grid = Scenario.BuildGrid();
            World = new WorldState(Scenario.StartMinutes, Scenario.StartCompute);

            SetupLighting();
            City = EastDistrictCity.Build(_root);
            Rig = CameraRig.Create(_root, NightSky);
            Rig.FocusOn(City.DistrictCenter, 46f);

            var positions = City.Anchors.ToDictionary(pair => pair.Key, pair => pair.Value + Vector3.up * 24f);
            CityLayer = GridBoard.Create(_root, "Sistem katmanı", Grid, positions, null, new BoardStyle
            {
                SourceSize = 3.2f,
                JunctionSize = 2f,
                LoadSize = 3f,
                LineWidth = 0.8f,
                OpenLineWidth = 0.3f,
                SwitchSize = 1.6f,
                DropLines = true,
                DropToY = 0.5f,
                ShowLineLabels = false
            });
            CityLayer.Refresh(Grid.Evaluate());
            CityLayer.gameObject.SetActive(false);

            Minigame = EnergyMinigameScreen.Create(_root, Grid);
            PostProcessingSetup.CreateGlobalVolume(_root);

            City.ApplyPowerState(Grid.Evaluate().IsPowered);
            foreach (var line in Scenario.IntroVoice) Voice.Say(line);
            Phase = GamePhase.City;
        }

        private void SetupLighting()
        {
            var moon = new GameObject("Ay ışığı");
            moon.transform.SetParent(_root, false);
            moon.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            var light = moon.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.65f, 0.95f);
            light.intensity = 0.35f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.17f, 0.25f);
            RenderSettings.fog = false;
        }

        public void Restart()
        {
            StopAllCoroutines();
            if (_root != null) Destroy(_root.gameObject);
            Build();
        }

        // ───────────────────────────── Döngü ─────────────────────────────

        private void Update()
        {
            var dt = Time.deltaTime;
            Voice.Update(dt);
            HandleKeys();

            if ((Phase == GamePhase.City || Phase == GamePhase.Aftermath) && !Paused)
                World.Clock.Advance(dt * MinutesPerSecond);

            PublishDueNews();

            if (Phase == GamePhase.City && !_eventFired && World.Clock.TotalMinutes >= Scenario.EventMinutes)
            {
                _eventFired = true;
                StartCoroutine(EventSequence());
            }

            if (Phase == GamePhase.Aftermath && !ShowEndPrompt && World.Clock.TotalMinutes - _aftermathStart >= AftermathMinutes)
            {
                ShowEndPrompt = true;
                Voice.Say("Prototip burada bitiyor. [R] ile baştan başlayabilir, başka bir yol deneyebilirsin.");
            }

            if ((Phase == GamePhase.City || Phase == GamePhase.Aftermath) && SystemLayerVisible)
                CityLayer.Refresh(Grid.Evaluate());

            UpdateHover();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) GuiInput.ClearHeld();
        }

        private void HandleKeys()
        {
            var cityControls = Phase == GamePhase.City || Phase == GamePhase.Aftermath;
            Rig.InputEnabled = cityControls || Phase == GamePhase.EventCard || Phase == GamePhase.Result;

            if (GuiInput.ConsumePress(KeyCode.Tab) && cityControls)
            {
                CityLayer.gameObject.SetActive(!CityLayer.gameObject.activeSelf);
                if (SystemLayerVisible) CityLayer.Refresh(Grid.Evaluate());
            }

            if (GuiInput.ConsumePress(KeyCode.Space) && cityControls) Paused = !Paused;

            if (GuiInput.ConsumePress(KeyCode.R) && Phase == GamePhase.Aftermath) Restart();
        }

        private void PublishDueNews()
        {
            foreach (var item in World.News.PublishDue(World.Clock.TotalMinutes))
                _news.Add(new PublishedNews { Item = item, RealTime = Time.unscaledTime });
        }

        private void UpdateHover()
        {
            var board = ActiveBoard;
            var camera = ActiveCamera;
            string id = null;
            if (board != null && GuiInput.HasMouse && !_hud.PointerOverUi)
                board.TryPick(camera, GuiInput.MouseScreen, out id);

            if (board != null) board.SetHover(id);
            if (id == null)
            {
                HoverText = null;
                _hovered = null;
                return;
            }

            var interactive = Phase == GamePhase.Minigame;
            var evaluation = interactive && Operation != null ? Operation.Current : Grid.Evaluate();
            HoverText = GridText.Describe(Grid, evaluation, id, interactive);

            if (interactive && id != _hovered && !_hintsShown.Contains(id) && Scenario.Hints.TryGetValue(id, out var hint))
            {
                _hintsShown.Add(id);
                Voice.Interrupt(hint);
            }
            _hovered = id;
        }

        // ───────────────────────────── Olay ─────────────────────────────

        private IEnumerator EventSequence()
        {
            Phase = GamePhase.EventIntro;
            Grid.SetFault(Scenario.FaultLineId);
            var evaluation = Grid.Evaluate();
            City.SetFaultBeacon(true);
            City.AnimatePowerState(evaluation.IsPowered, RevealOrder, 0.2f);
            CityLayer.Refresh(evaluation);

            if (Scenario.EventNews != null)
            {
                World.News.Schedule(new NewsItem(World.Clock.TotalMinutes, Scenario.EventNews.Source, Scenario.EventNews.Headline));
                PublishDueNews();
            }

            Voice.Interrupt(Scenario.EventVoice);
            Rig.FocusOn(City.HospitalPosition, 28f);
            yield return new WaitForSeconds(2.8f);
            Phase = GamePhase.EventCard;
        }

        /// <summary>Olay kartından çağrılır.</summary>
        public void ChooseOption(Intent intent)
        {
            if (Phase != GamePhase.EventCard) return;

            if (intent == Intent.Ignore)
            {
                LastResult = EnergyOutcomeEvaluator.EvaluateIgnore(Grid, Scenario, World.Clock.TotalMinutes);
                World.Apply(LastResult);
                Phase = GamePhase.Result;
                return;
            }

            StartCoroutine(EnterMinigame(intent));
        }

        // ───────────────────────────── Mini oyun ─────────────────────────────

        private IEnumerator EnterMinigame(Intent intent)
        {
            Phase = GamePhase.Transition;
            CityLayer.SetHover(null);
            yield return Fade(1f, 0.35f);

            Operation = new EnergyOperation(Grid, intent, Scenario.GoalFor(intent), World.Clock, Scenario.Rules);
            _goalAnnounced = false;
            Rig.Camera.enabled = false;
            Minigame.Open(Operation.Current);
            TitleCard = "ENERJİ ŞEBEKESİ";
            TitleCardUntil = Time.unscaledTime + 2.2f;
            Phase = GamePhase.Minigame;

            yield return Fade(0f, 0.45f);
            Voice.Interrupt(intent == Intent.Save ? Scenario.SaveStartVoice : Scenario.HarmStartVoice);
        }

        /// <summary>Mini oyunda bir dünya noktasına tıklandı.</summary>
        public void OnWorldClick(Vector2 screenPosition)
        {
            if (Phase != GamePhase.Minigame || Operation == null) return;
            if (!Minigame.Board.TryPick(Minigame.Camera, screenPosition, out var id)) return;

            var result = Operation.Toggle(id);
            if (!result.Accepted)
            {
                ShowToast(result.Message, Hud.Warning);
                return;
            }

            Minigame.Board.Refresh(Operation.Current);
            if (result.Tripped.Count > 0)
            {
                Minigame.Board.Flash(result.Tripped.Select(line => line.Id));
                ShowToast(result.Message, Hud.Danger);
                Voice.Interrupt("Sigorta attı. Alarm düştü; bu kayıtlara geçti.");
            }
            else
            {
                ShowToast(result.Message, Hud.Accent);
            }

            if (Operation.GoalMet && !_goalAnnounced)
            {
                _goalAnnounced = true;
                Voice.Say(Operation.Intent == Intent.Save
                    ? "Hastaneye enerji gidiyor. İstersem burada bırakabilirim."
                    : "Bölge karanlıkta. Çıkabilirim.");
            }
        }

        /// <summary>"Operasyonu bitir" düğmesi.</summary>
        public void FinishOperation()
        {
            if (Phase != GamePhase.Minigame || Operation == null) return;
            LastResult = EnergyOutcomeEvaluator.Evaluate(Operation, Scenario);
            World.Apply(LastResult);
            StartCoroutine(ReturnToCity());
        }

        private IEnumerator ReturnToCity()
        {
            Phase = GamePhase.Transition;
            yield return Fade(1f, 0.35f);

            Minigame.Close();
            Rig.Camera.enabled = true;
            Rig.FocusOn(City.DistrictCenter, 40f);
            Phase = GamePhase.Reveal;
            yield return Fade(0f, 0.45f);
            yield return new WaitForSeconds(0.5f);

            var evaluation = Grid.Evaluate();
            var duration = City.AnimatePowerState(evaluation.IsPowered, RevealOrder);
            yield return new WaitForSeconds(duration);

            if (LastResult.RevealedNodeIds.Count > 0)
            {
                foreach (var id in LastResult.RevealedNodeIds) City.Reveal(id);
                yield return new WaitForSeconds(1.5f);
            }

            CityLayer.Refresh(evaluation);
            yield return new WaitForSeconds(0.6f);
            Phase = GamePhase.Result;
        }

        /// <summary>Sonuç panelindeki "Devam" düğmesi.</summary>
        public void ContinueAfterResult()
        {
            if (Phase != GamePhase.Result) return;
            Phase = GamePhase.Aftermath;
            _aftermathStart = World.Clock.TotalMinutes;
            foreach (var line in LastResult.VoiceLines) Voice.Say(line);
            StartCoroutine(AmbulanceSequence());
        }

        private IEnumerator AmbulanceSequence()
        {
            yield return new WaitForSeconds(2f);
            Rig.FocusOn(new Vector3(-8f, 0f, -4f), 34f);
            City.SpawnAmbulance();
            Voice.Say(City.IsShownPowered(Ids.TrafficLights)
                ? "Bir ambulans hastaneye geliyor."
                : "Bir ambulans hastaneye geliyor. Kavşağın ışıkları sönük.");
        }

        // ───────────────────────────── Yardımcılar ─────────────────────────────

        public void ShowToast(string text, Color color)
        {
            ToastText = text;
            ToastColor = color;
            ToastUntil = Time.unscaledTime + 2.6f;
        }

        private IEnumerator Fade(float target, float duration)
        {
            var start = FadeAlpha;
            for (var t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                FadeAlpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            FadeAlpha = target;
        }
    }
}
