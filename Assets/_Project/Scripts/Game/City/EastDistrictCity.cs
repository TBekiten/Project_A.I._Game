using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AIGame.Game.Util;
using UnityEngine;
using Ids = AIGame.Core.Content.EastDistrictScenario.Ids;
using Random = System.Random;

namespace AIGame.Game.City
{
    /// <summary>
    /// Prototip mahallesinin low-poly görünümü. Tamamen koddan, Unity'nin hazır şekilleriyle kurulur;
    /// gerçek modeller geldiğinde aynı bağlantı noktaları (Anchors, güç ayarlayıcıları) korunarak değiştirilir.
    ///
    /// Yerleşim (x doğu, z kuzey): ana yollar x=0 ve z=0'da kesişir, kavşakta trafik ışıkları var.
    /// KD: hastane · GD: konutlar · KB: çarşı · GB: tanımlanmamış bina · Doğu ucu: trafo merkezi.
    /// </summary>
    public sealed class EastDistrictCity : MonoBehaviour
    {
        private readonly Dictionary<string, Action<bool, bool>> _powerSetters = new Dictionary<string, Action<bool, bool>>();
        private readonly Dictionary<string, bool> _powerState = new Dictionary<string, bool>();
        private readonly Dictionary<string, Vector3> _anchors = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, GameObject> _abstractBlocks = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _realModels = new Dictionary<string, GameObject>();
        private readonly Random _random = new Random(7);

        private TrafficLightController _trafficLights;
        private TrafficSystem _traffic;
        private Blinker _faultBeacon;
        private TrafficRoute _ambulanceRoute;

        private Material _asphalt, _sidewalk, _grass, _plot, _marking, _metal, _concrete, _darkGlass;

        public Vector3 HospitalPosition { get; private set; }
        public Vector3 DistrictCenter { get; private set; }

        /// <summary>Şebeke düğümlerinin şehirdeki yerleri (zemin seviyesinde).</summary>
        public IReadOnlyDictionary<string, Vector3> Anchors => _anchors;

        public static EastDistrictCity Build(Transform parent)
        {
            var go = new GameObject("Şehir — Doğu Mahallesi");
            go.transform.SetParent(parent, false);
            var city = go.AddComponent<EastDistrictCity>();
            city.Construct();
            return city;
        }

        // ───────────────────────────── Dış arayüz ─────────────────────────────

        /// <summary>Tüm yüklerin görselini anında günceller.</summary>
        public void ApplyPowerState(Func<string, bool> isPowered)
        {
            foreach (var pair in _powerSetters)
            {
                var powered = isPowered(pair.Key);
                _powerState[pair.Key] = powered;
                pair.Value(powered, false);
            }
        }

        /// <summary>
        /// Değişen yükleri sırayla, titreyerek günceller (sonucun şehirde sahnelenmesi).
        /// Toplam süreyi saniye olarak döndürür.
        /// </summary>
        public float AnimatePowerState(Func<string, bool> isPowered, IEnumerable<string> order, float gapSeconds = 0.75f)
        {
            var changes = order
                .Where(id => _powerSetters.ContainsKey(id) && _powerState.TryGetValue(id, out var current) && current != isPowered(id))
                .ToList();
            StartCoroutine(AnimateChanges(changes, isPowered, gapSeconds));
            return changes.Count * gapSeconds + 0.4f;
        }

        public bool IsShownPowered(string nodeId) => _powerState.TryGetValue(nodeId, out var p) && p;

        public void SetFaultBeacon(bool active)
        {
            if (_faultBeacon != null) _faultBeacon.enabled = active;
        }

        /// <summary>Tanımlanmamış bir binanın soyut bloğunu gerçek modeline dönüştürür.</summary>
        public void Reveal(string nodeId)
        {
            if (!_abstractBlocks.TryGetValue(nodeId, out var block) || !_realModels.TryGetValue(nodeId, out var model)) return;
            if (!block.activeSelf) return;
            StartCoroutine(RevealRoutine(block, model));
        }

        public void SpawnAmbulance()
        {
            var model = BuildAmbulanceModel();
            var vehicle = _traffic.Spawn(model, _ambulanceRoute, 0f, 13f, true);
            vehicle.RemainingParkSeconds = 10f;
        }

        // ───────────────────────────── Kurulum ─────────────────────────────

        private void Construct()
        {
            _asphalt = MaterialFactory.Lit(new Color(0.085f, 0.09f, 0.105f), 0.3f);
            _sidewalk = MaterialFactory.Lit(new Color(0.24f, 0.25f, 0.28f));
            _grass = MaterialFactory.Lit(new Color(0.06f, 0.1f, 0.08f));
            _plot = MaterialFactory.Lit(new Color(0.15f, 0.16f, 0.18f));
            _marking = MaterialFactory.Lit(new Color(0.75f, 0.74f, 0.62f));
            _metal = MaterialFactory.Lit(new Color(0.2f, 0.21f, 0.23f), 0.5f);
            _concrete = MaterialFactory.Lit(new Color(0.5f, 0.51f, 0.53f));
            _darkGlass = MaterialFactory.Lit(new Color(0.06f, 0.08f, 0.11f), 0.8f);

            HospitalPosition = new Vector3(22f, 0f, 22f);
            DistrictCenter = new Vector3(4f, 0f, -2f);

            BuildGround();
            BuildRoads();
            BuildHospital();
            BuildHousing();
            BuildMarket();
            BuildPump();
            BuildSubstation();
            BuildStreetLamps();
            BuildTrafficLights();
            BuildFiller();
            BuildTrees();
            BuildTraffic();

            _anchors[Ids.Substation] = new Vector3(64f, 0f, -12f);
            _anchors[Ids.HousingJunction] = new Vector3(38f, 0f, -30f);
            _anchors[Ids.MarketJunction] = new Vector3(-40f, 0f, 24f);
            _anchors[Ids.Hospital] = HospitalPosition;
            _anchors[Ids.Housing] = new Vector3(20f, 0f, -18f);
            _anchors[Ids.StreetLamps] = new Vector3(-14f, 0f, -9f);
            _anchors[Ids.TrafficLights] = Vector3.zero;
            _anchors[Ids.Pump] = new Vector3(-24f, 0f, -22f);
            _anchors[Ids.Market] = new Vector3(-27f, 0f, 12f);
        }

        private void BuildGround()
        {
            Box("Zemin", transform, new Vector3(0f, -0.1f, 0f), new Vector3(190f, 0.2f, 190f), _grass);
            Box("Arsa — hastane", transform, new Vector3(23f, 0.02f, 23f), new Vector3(34f, 0.04f, 34f), _plot);
            Box("Arsa — konutlar", transform, new Vector3(22f, 0.02f, -18f), new Vector3(36f, 0.04f, 26f), _plot);
            Box("Arsa — çarşı", transform, new Vector3(-27f, 0.02f, 17f), new Vector3(44f, 0.04f, 22f), _plot);
            Box("Arsa — güneybatı", transform, new Vector3(-26f, 0.02f, -22f), new Vector3(30f, 0.04f, 26f), _plot);
        }

        private void BuildRoads()
        {
            var roads = new GameObject("Yollar").transform;
            roads.SetParent(transform, false);

            Box("Ana yol (D-B)", roads, new Vector3(0f, 0.05f, 0f), new Vector3(180f, 0.06f, 9f), _asphalt);
            Box("Ana yol (K-G)", roads, new Vector3(0f, 0.051f, 0f), new Vector3(9f, 0.06f, 180f), _asphalt);
            Box("Konut sokağı", roads, new Vector3(24f, 0.05f, -34f), new Vector3(42f, 0.06f, 7f), _asphalt);
            Box("Hastane yolu", roads, new Vector3(11f, 0.05f, 10f), new Vector3(14f, 0.06f, 6f), _asphalt);

            Box("Kaldırım K", roads, new Vector3(0f, 0.04f, 5.5f), new Vector3(180f, 0.08f, 2f), _sidewalk);
            Box("Kaldırım G", roads, new Vector3(0f, 0.04f, -5.5f), new Vector3(180f, 0.08f, 2f), _sidewalk);
            Box("Kaldırım D", roads, new Vector3(5.5f, 0.041f, 0f), new Vector3(2f, 0.08f, 180f), _sidewalk);
            Box("Kaldırım B", roads, new Vector3(-5.5f, 0.041f, 0f), new Vector3(2f, 0.08f, 180f), _sidewalk);

            for (var x = -86f; x <= 86f; x += 6f)
            {
                if (Mathf.Abs(x) < 8f) continue;
                Box("Şerit", roads, new Vector3(x, 0.09f, 0f), new Vector3(3f, 0.02f, 0.25f), _marking);
                Box("Şerit", roads, new Vector3(0f, 0.09f, x), new Vector3(0.25f, 0.02f, 3f), _marking);
            }

            for (var i = -3; i <= 3; i++)
            {
                Box("Yaya geçidi", roads, new Vector3(-7.5f, 0.09f, i * 1.2f), new Vector3(2.2f, 0.02f, 0.6f), _marking);
                Box("Yaya geçidi", roads, new Vector3(7.5f, 0.09f, i * 1.2f), new Vector3(2.2f, 0.02f, 0.6f), _marking);
                Box("Yaya geçidi", roads, new Vector3(i * 1.2f, 0.09f, -7.5f), new Vector3(0.6f, 0.02f, 2.2f), _marking);
                Box("Yaya geçidi", roads, new Vector3(i * 1.2f, 0.09f, 7.5f), new Vector3(0.6f, 0.02f, 2.2f), _marking);
            }
        }

        private void BuildHospital()
        {
            var root = new GameObject("Hastane").transform;
            root.SetParent(transform, false);
            var group = root.gameObject.AddComponent<PoweredGroup>();

            var wall = MaterialFactory.Lit(new Color(0.82f, 0.84f, 0.86f));
            var trim = MaterialFactory.Lit(new Color(0.55f, 0.6f, 0.66f));
            Box("Ana blok", root, new Vector3(22f, 7f, 23f), new Vector3(22f, 14f, 14f), wall);
            Box("Kanat", root, new Vector3(30f, 3.5f, 34f), new Vector3(10f, 7f, 8f), wall);
            Box("Çatı", root, new Vector3(22f, 14.2f, 23f), new Vector3(22.4f, 0.4f, 14.4f), trim);
            Box("Helipad", root, new Vector3(25f, 14.5f, 23f), new Vector3(7f, 0.2f, 7f), _metal);

            var windows = MaterialFactory.Emissive(new Color(0.05f, 0.06f, 0.08f), Color.black);
            var windowGlow = new Color(1.25f, 1.45f, 1.7f);
            group.AddEmissive(windows, windowGlow);
            Windows(root, new Vector3(22f, 7f, 23f), new Vector3(22f, 14f, 14f), 7, 3, 3.4f, 3.6f, windows);

            var generator = MaterialFactory.Emissive(new Color(0.05f, 0.05f, 0.05f), Color.black);
            group.AddEmissive(generator, new Color(1.3f, 0.55f, 0.15f), inverted: true);
            for (var i = 0; i < 3; i++)
                Box("Jeneratör penceresi", root, new Vector3(13.5f + i * 2.6f, 2.2f, 15.9f), new Vector3(1.4f, 1.4f, 0.12f), generator);

            var cross = MaterialFactory.Emissive(new Color(0.6f, 0.05f, 0.05f), Color.black);
            group.AddEmissive(cross, new Color(3.5f, 0.2f, 0.2f));
            Box("Haç (dikey)", root, new Vector3(30.5f, 11.2f, 15.9f), new Vector3(0.9f, 3f, 0.15f), cross);
            Box("Haç (yatay)", root, new Vector3(30.5f, 11.2f, 15.9f), new Vector3(3f, 0.9f, 0.15f), cross);

            Box("Giriş saçağı", root, new Vector3(16f, 3f, 14.2f), new Vector3(7f, 0.35f, 3.6f), trim);
            Box("Direk", root, new Vector3(13f, 1.5f, 12.8f), new Vector3(0.3f, 3f, 0.3f), trim);
            Box("Direk", root, new Vector3(19f, 1.5f, 12.8f), new Vector3(0.3f, 3f, 0.3f), trim);

            var entranceLight = AddPointLight(root, new Vector3(16f, 2.6f, 13f), new Color(0.85f, 0.92f, 1f), 9f, 2.2f);
            group.AddLight(entranceLight);

            // Hastane trafosu: olayda yanan parça.
            var transformer = new GameObject("Hastane trafosu").transform;
            transformer.SetParent(transform, false);
            Box("Trafo gövdesi", transformer, new Vector3(8.5f, 1.5f, 34f), new Vector3(3f, 3f, 3f), _metal);
            Box("Trafo zemini", transformer, new Vector3(8.5f, 0.05f, 34f), new Vector3(4.5f, 0.1f, 4.5f), _concrete);
            var beaconMat = MaterialFactory.Emissive(new Color(0.2f, 0.02f, 0.02f), Color.black);
            var beacon = Sphere("İkaz lambası", transformer, new Vector3(8.5f, 3.4f, 34f), 0.8f, beaconMat);
            var beaconLight = AddPointLight(transformer, new Vector3(8.5f, 4f, 34f), Color.red, 10f, 3f);
            _faultBeacon = beacon.AddComponent<Blinker>();
            _faultBeacon.Target = beaconMat;
            _faultBeacon.LightSource = beaconLight;
            _faultBeacon.Period = 0.7f;
            _faultBeacon.enabled = false;
            beaconLight.enabled = false;

            Register(Ids.Hospital, group);
        }

        private void BuildHousing()
        {
            var root = new GameObject("Konutlar").transform;
            root.SetParent(transform, false);
            var group = root.gameObject.AddComponent<PoweredGroup>();

            var windows = MaterialFactory.Emissive(new Color(0.05f, 0.05f, 0.06f), Color.black);
            group.AddEmissive(windows, new Color(1.6f, 1.05f, 0.5f));

            var wallColors = new[]
            {
                new Color(0.62f, 0.55f, 0.47f), new Color(0.5f, 0.56f, 0.6f), new Color(0.66f, 0.62f, 0.52f),
                new Color(0.55f, 0.48f, 0.5f)
            };
            var roof = MaterialFactory.Lit(new Color(0.32f, 0.15f, 0.12f));
            var positions = new[]
            {
                new Vector2(10f, -12f), new Vector2(20f, -12f), new Vector2(30f, -12f),
                new Vector2(10f, -23f), new Vector2(20f, -23f), new Vector2(30f, -23f),
                new Vector2(10f, -44f), new Vector2(21f, -44f)
            };

            for (var i = 0; i < positions.Length; i++)
            {
                var p = positions[i];
                var size = new Vector3(7f, 4.5f, 6f);
                var center = new Vector3(p.x, size.y / 2f, p.y);
                var wall = MaterialFactory.Lit(wallColors[i % wallColors.Length]);
                Box("Ev " + (i + 1), root, center, size, wall);
                var roofSide = size.z / Mathf.Sqrt(2f);
                Box("Çatı " + (i + 1), root, new Vector3(p.x, size.y, p.y), new Vector3(size.x + 0.5f, roofSide, roofSide),
                    roof, Quaternion.Euler(45f, 0f, 0f));
                Windows(root, center, size, 2, 1, 1.6f, 0f, windows);
            }

            Register(Ids.Housing, group);
        }

        private void BuildMarket()
        {
            var root = new GameObject("Çarşı").transform;
            root.SetParent(transform, false);
            var group = root.gameObject.AddComponent<PoweredGroup>();

            var windows = MaterialFactory.Emissive(new Color(0.05f, 0.05f, 0.06f), Color.black);
            group.AddEmissive(windows, new Color(1.8f, 1.4f, 0.8f));

            var signColors = new[]
            {
                new Color(0.2f, 1.6f, 2.4f), new Color(2.6f, 0.4f, 1.4f), new Color(2.4f, 1.6f, 0.2f), new Color(0.4f, 2.4f, 0.8f)
            };
            var awningColors = new[]
            {
                new Color(0.55f, 0.15f, 0.12f), new Color(0.12f, 0.3f, 0.45f), new Color(0.5f, 0.42f, 0.12f), new Color(0.15f, 0.4f, 0.22f)
            };

            for (var i = 0; i < 4; i++)
            {
                var x = -12f - i * 10f;
                var size = new Vector3(8.5f, 5f, 8f);
                var center = new Vector3(x, size.y / 2f, 12f);
                Box("Dükkân " + (i + 1), root, center, size, MaterialFactory.Lit(new Color(0.42f, 0.4f, 0.38f)));
                Box("Vitrin", root, new Vector3(x, 1.6f, 7.9f), new Vector3(6.5f, 2.2f, 0.12f), windows);
                Box("Tente", root, new Vector3(x, 3.1f, 7.4f), new Vector3(7.5f, 0.25f, 1.4f),
                    MaterialFactory.Lit(awningColors[i]), Quaternion.Euler(-12f, 0f, 0f));

                var sign = MaterialFactory.Emissive(new Color(0.1f, 0.1f, 0.1f), Color.black);
                group.AddEmissive(sign, signColors[i]);
                Box("Tabela", root, new Vector3(x, 4.3f, 7.85f), new Vector3(5f, 0.8f, 0.15f), sign);
            }

            group.AddLight(AddPointLight(root, new Vector3(-27f, 3f, 6f), new Color(1f, 0.85f, 0.65f), 16f, 2f));
            Register(Ids.Market, group);
        }

        private void BuildPump()
        {
            var group = new GameObject("Tanımlanmamış bina").AddComponent<PoweredGroup>();
            group.transform.SetParent(transform, false);

            // Soyut blok: A.I. bu binanın ne olduğunu bilmiyor.
            var block = new GameObject("Soyut blok");
            block.transform.SetParent(group.transform, false);
            block.transform.localPosition = new Vector3(-24f, 0f, -22f);
            var grey = MaterialFactory.Lit(new Color(0.42f, 0.44f, 0.48f));
            Box("Blok", block.transform, block.transform.position + new Vector3(0f, 3.5f, 0f), new Vector3(12f, 7f, 10f), grey);
            var scan = MaterialFactory.Emissive(new Color(0.1f, 0.2f, 0.25f), Color.black);
            group.AddEmissive(scan, new Color(0.25f, 0.9f, 1.3f));
            EdgeFrame(block.transform, block.transform.position + new Vector3(0f, 3.5f, 0f), new Vector3(12f, 7f, 10f), 0.12f, scan);

            // Gerçek model: tanındıktan sonra görünür.
            var real = new GameObject("Su pompası");
            real.transform.SetParent(group.transform, false);
            real.transform.localPosition = new Vector3(-24f, 0f, -22f);
            var origin = real.transform.position;
            Box("Pompa binası", real.transform, origin + new Vector3(2f, 2.5f, 1f), new Vector3(9f, 5f, 7f), _concrete);
            var tank = MaterialFactory.Lit(new Color(0.35f, 0.45f, 0.55f), 0.4f);
            Cylinder("Su deposu", real.transform, origin + new Vector3(-6f, 4.5f, -1.5f), 6f, 9f, tank);
            Cylinder("Boru", real.transform, origin + new Vector3(-3.2f, 1f, -1.5f), 0.8f, 2f, _metal);
            var pumpLight = MaterialFactory.Emissive(new Color(0.05f, 0.08f, 0.1f), Color.black);
            group.AddEmissive(pumpLight, new Color(0.3f, 1.2f, 2.2f));
            Box("Pencere", real.transform, origin + new Vector3(0.5f, 2.6f, -2.55f), new Vector3(2.4f, 1.2f, 0.12f), pumpLight);
            real.SetActive(false);

            _abstractBlocks[Ids.Pump] = block;
            _realModels[Ids.Pump] = real;
            Register(Ids.Pump, group);
        }

        private void BuildSubstation()
        {
            var root = new GameObject("Trafo merkezi").transform;
            root.SetParent(transform, false);
            var center = new Vector3(64f, 0f, -12f);
            Box("Avlu", root, center + new Vector3(0f, 0.05f, 0f), new Vector3(22f, 0.1f, 18f), _concrete);
            for (var i = 0; i < 3; i++)
            {
                var p = center + new Vector3(-6f + i * 6f, 0f, 2f);
                Box("Transformatör", root, p + new Vector3(0f, 1.6f, 0f), new Vector3(3.5f, 3.2f, 4f), _metal);
                Cylinder("İzolatör", root, p + new Vector3(-0.8f, 4f, 0f), 0.35f, 1.6f, MaterialFactory.Lit(new Color(0.55f, 0.32f, 0.18f)));
                Cylinder("İzolatör", root, p + new Vector3(0.8f, 4f, 0f), 0.35f, 1.6f, MaterialFactory.Lit(new Color(0.55f, 0.32f, 0.18f)));
            }
            Box("Kontrol binası", root, center + new Vector3(0f, 2f, -6f), new Vector3(10f, 4f, 4f), MaterialFactory.Lit(new Color(0.4f, 0.42f, 0.4f)));
            for (var x = -11f; x <= 11f; x += 2.75f)
            {
                Cylinder("Çit", root, center + new Vector3(x, 1.2f, 9f), 0.15f, 2.4f, _metal);
                Cylinder("Çit", root, center + new Vector3(x, 1.2f, -9f), 0.15f, 2.4f, _metal);
            }
            AddPointLight(root, center + new Vector3(0f, 6f, 0f), new Color(0.9f, 0.95f, 1f), 18f, 2.5f);
        }

        private void BuildStreetLamps()
        {
            var root = new GameObject("Sokak lambaları").transform;
            root.SetParent(transform, false);
            var group = root.gameObject.AddComponent<PoweredGroup>();
            var head = MaterialFactory.Emissive(new Color(0.3f, 0.28f, 0.22f), Color.black);
            group.AddEmissive(head, new Color(3f, 2.4f, 1.5f));

            var spots = new[]
            {
                new Vector3(-40f, 0f, -6.6f), new Vector3(-22f, 0f, 6.6f), new Vector3(22f, 0f, -6.6f), new Vector3(40f, 0f, 6.6f),
                new Vector3(6.6f, 0f, -40f), new Vector3(-6.6f, 0f, -22f), new Vector3(6.6f, 0f, 22f), new Vector3(-6.6f, 0f, 40f)
            };
            foreach (var spot in spots)
            {
                Cylinder("Direk", root, spot + new Vector3(0f, 3f, 0f), 0.25f, 6f, _metal);
                Sphere("Lamba", root, spot + new Vector3(0f, 6.1f, 0f), 0.8f, head);
                group.AddLight(AddPointLight(root, spot + new Vector3(0f, 5.5f, 0f), new Color(1f, 0.82f, 0.55f), 13f, 2.6f));
            }

            Register(Ids.StreetLamps, group);
        }

        private void BuildTrafficLights()
        {
            var root = new GameObject("Trafik ışıkları").transform;
            root.SetParent(transform, false);
            _trafficLights = root.gameObject.AddComponent<TrafficLightController>();

            var poles = new[]
            {
                (new Vector3(-6.5f, 0f, -6.5f), TrafficAxis.EastWest),
                (new Vector3(6.5f, 0f, 6.5f), TrafficAxis.EastWest),
                (new Vector3(6.5f, 0f, -6.5f), TrafficAxis.NorthSouth),
                (new Vector3(-6.5f, 0f, 6.5f), TrafficAxis.NorthSouth)
            };
            foreach (var (position, axis) in poles)
            {
                Cylinder("Direk", root, position + new Vector3(0f, 2.5f, 0f), 0.25f, 5f, _metal);
                Box("Kafa", root, position + new Vector3(0f, 5.4f, 0f), new Vector3(0.9f, 2.4f, 0.9f), _metal);
                var red = MaterialFactory.Emissive(new Color(0.15f, 0.02f, 0.02f), Color.black);
                var yellow = MaterialFactory.Emissive(new Color(0.15f, 0.12f, 0.02f), Color.black);
                var green = MaterialFactory.Emissive(new Color(0.02f, 0.15f, 0.05f), Color.black);
                var face = new Vector3(-0.5f, 0f, -0.5f);
                Sphere("Kırmızı", root, position + face + new Vector3(0f, 6.2f, 0f), 0.55f, red);
                Sphere("Sarı", root, position + face + new Vector3(0f, 5.4f, 0f), 0.55f, yellow);
                Sphere("Yeşil", root, position + face + new Vector3(0f, 4.6f, 0f), 0.55f, green);
                _trafficLights.AddHead(axis, red, yellow, green);
            }

            _powerSetters[Ids.TrafficLights] = (powered, animate) => _trafficLights.SetPowered(powered);
            _powerState[Ids.TrafficLights] = true;
        }

        private void BuildFiller()
        {
            var root = new GameObject("Diğer mahalleler").transform;
            root.SetParent(transform, false);
            var facade = MaterialFactory.Lit(new Color(0.16f, 0.17f, 0.2f));
            var lit = MaterialFactory.Emissive(new Color(0.05f, 0.05f, 0.06f), new Color(1.1f, 0.85f, 0.5f));
            var litCool = MaterialFactory.Emissive(new Color(0.05f, 0.05f, 0.06f), new Color(0.7f, 0.9f, 1.2f));

            for (var gx = -80f; gx <= 80f; gx += 14f)
            for (var gz = -80f; gz <= 80f; gz += 14f)
            {
                var ring = Mathf.Max(Mathf.Abs(gx), Mathf.Abs(gz));
                if (ring < 50f) continue;
                if (Mathf.Abs(gx) < 9f || Mathf.Abs(gz) < 9f) continue;
                if (gx > 50f && gz > -26f && gz < 2f) continue; // trafo merkezi
                if (_random.NextDouble() < 0.25) continue;

                var height = 6f + (float)_random.NextDouble() * 18f;
                var size = new Vector3(8f + (float)_random.NextDouble() * 3f, height, 8f + (float)_random.NextDouble() * 3f);
                var center = new Vector3(gx, height / 2f, gz);
                Box("Bina", root, center, size, facade);

                var windowCount = _random.Next(2, 7);
                for (var w = 0; w < windowCount; w++)
                {
                    var y = 1.5f + (float)_random.NextDouble() * (height - 3f);
                    var onX = _random.NextDouble() < 0.5;
                    var offset = ((float)_random.NextDouble() - 0.5f) * (onX ? size.z : size.x) * 0.7f;
                    var position = onX
                        ? new Vector3(center.x - size.x / 2f - 0.06f, y, center.z + offset)
                        : new Vector3(center.x + offset, y, center.z - size.z / 2f - 0.06f);
                    Box("Pencere", root, position, onX ? new Vector3(0.12f, 1.2f, 1.4f) : new Vector3(1.4f, 1.2f, 0.12f),
                        _random.NextDouble() < 0.7 ? lit : litCool);
                }
            }
        }

        private void BuildTrees()
        {
            var root = new GameObject("Ağaçlar").transform;
            root.SetParent(transform, false);
            var trunk = MaterialFactory.Lit(new Color(0.22f, 0.15f, 0.1f));
            var leaves = MaterialFactory.Lit(new Color(0.08f, 0.2f, 0.12f));
            var spots = new[]
            {
                new Vector3(40f, 0f, 10f), new Vector3(42f, 0f, 30f), new Vector3(8f, 0f, 40f), new Vector3(36f, 0f, 42f),
                new Vector3(-14f, 0f, -40f), new Vector3(38f, 0f, -16f), new Vector3(36f, 0f, -44f), new Vector3(-12f, 0f, -14f),
                new Vector3(-38f, 0f, -12f), new Vector3(-40f, 0f, -32f), new Vector3(-12f, 0f, -34f), new Vector3(-46f, 0f, 30f),
                new Vector3(-10f, 0f, 26f), new Vector3(-30f, 0f, 30f), new Vector3(16f, 0f, 44f), new Vector3(46f, 0f, -14f)
            };
            foreach (var spot in spots)
            {
                var scale = 0.8f + (float)_random.NextDouble() * 0.6f;
                Cylinder("Gövde", root, spot + new Vector3(0f, 1.2f * scale, 0f), 0.5f * scale, 2.4f * scale, trunk);
                Sphere("Taç", root, spot + new Vector3(0f, 3.4f * scale, 0f), 3.2f * scale, leaves);
            }
        }

        private void BuildTraffic()
        {
            var root = new GameObject("Trafik");
            root.transform.SetParent(transform, false);
            _traffic = root.AddComponent<TrafficSystem>();
            _traffic.Lights = _trafficLights;

            const float edge = 90f;
            const float stop = edge - 7f;
            var routes = new[]
            {
                new TrafficRoute(new[] { new Vector3(-edge, 0f, -2.2f), new Vector3(edge, 0f, -2.2f) }, TrafficAxis.EastWest, stop, true),
                new TrafficRoute(new[] { new Vector3(edge, 0f, 2.2f), new Vector3(-edge, 0f, 2.2f) }, TrafficAxis.EastWest, stop, true),
                new TrafficRoute(new[] { new Vector3(2.2f, 0f, -edge), new Vector3(2.2f, 0f, edge) }, TrafficAxis.NorthSouth, stop, true),
                new TrafficRoute(new[] { new Vector3(-2.2f, 0f, edge), new Vector3(-2.2f, 0f, -edge) }, TrafficAxis.NorthSouth, stop, true)
            };

            var carColors = new[]
            {
                new Color(0.55f, 0.1f, 0.1f), new Color(0.15f, 0.25f, 0.45f), new Color(0.7f, 0.7f, 0.72f),
                new Color(0.12f, 0.12f, 0.13f), new Color(0.35f, 0.4f, 0.2f), new Color(0.6f, 0.45f, 0.15f)
            };
            var index = 0;
            foreach (var route in routes)
            {
                for (var i = 0; i < 4; i++)
                {
                    var model = BuildCarModel(carColors[index++ % carColors.Length]);
                    _traffic.Spawn(model, route, 10f + i * 42f + (float)_random.NextDouble() * 8f, 8f + (float)_random.NextDouble() * 3f, false);
                }
            }

            _ambulanceRoute = new TrafficRoute(new[]
            {
                new Vector3(-edge, 0f, -2.2f), new Vector3(2.2f, 0f, -2.2f), new Vector3(2.2f, 0f, 10f), new Vector3(15f, 0f, 10f)
            }, TrafficAxis.EastWest, stop, false);
        }

        private GameObject BuildCarModel(Color color)
        {
            var car = new GameObject("Araba");
            var body = MaterialFactory.Lit(color, 0.6f);
            Box("Gövde", car.transform, new Vector3(0f, 0.65f, 0f), new Vector3(2f, 0.9f, 4.2f), body, null, true);
            Box("Kabin", car.transform, new Vector3(0f, 1.35f, -0.2f), new Vector3(1.8f, 0.6f, 2.2f), _darkGlass, null, true);
            var head = MaterialFactory.Emissive(Color.white, new Color(2.2f, 2.1f, 1.8f));
            var tail = MaterialFactory.Emissive(new Color(0.3f, 0f, 0f), new Color(2f, 0.1f, 0.1f));
            Box("Far", car.transform, new Vector3(-0.6f, 0.7f, 2.11f), new Vector3(0.4f, 0.2f, 0.05f), head, null, true);
            Box("Far", car.transform, new Vector3(0.6f, 0.7f, 2.11f), new Vector3(0.4f, 0.2f, 0.05f), head, null, true);
            Box("Stop", car.transform, new Vector3(-0.6f, 0.7f, -2.11f), new Vector3(0.4f, 0.2f, 0.05f), tail, null, true);
            Box("Stop", car.transform, new Vector3(0.6f, 0.7f, -2.11f), new Vector3(0.4f, 0.2f, 0.05f), tail, null, true);
            return car;
        }

        private GameObject BuildAmbulanceModel()
        {
            var ambulance = new GameObject("Ambulans");
            var white = MaterialFactory.Lit(new Color(0.9f, 0.9f, 0.92f), 0.5f);
            var red = MaterialFactory.Lit(new Color(0.7f, 0.08f, 0.08f));
            Box("Gövde", ambulance.transform, new Vector3(0f, 1.2f, -0.4f), new Vector3(2.3f, 1.9f, 4f), white, null, true);
            Box("Kabin", ambulance.transform, new Vector3(0f, 0.9f, 2.1f), new Vector3(2.2f, 1.3f, 1.4f), white, null, true);
            Box("Şerit", ambulance.transform, new Vector3(0f, 1.1f, -0.4f), new Vector3(2.34f, 0.35f, 4.02f), red, null, true);

            var barMat = MaterialFactory.Emissive(new Color(0.1f, 0.1f, 0.3f), Color.black);
            Box("Tepe lambası", ambulance.transform, new Vector3(0f, 2.3f, 1.2f), new Vector3(1.4f, 0.3f, 0.5f), barMat, null, true);
            var light = AddPointLight(ambulance.transform, new Vector3(0f, 3f, 1.2f), Color.blue, 12f, 3f, true);
            var blinker = ambulance.AddComponent<Blinker>();
            blinker.Target = barMat;
            blinker.ColorA = new Color(0.3f, 0.6f, 4f);
            blinker.ColorB = new Color(4f, 0.3f, 0.3f);
            blinker.Period = 0.45f;
            blinker.LightSource = light;
            return ambulance;
        }

        // ───────────────────────────── Animasyonlar ─────────────────────────────

        private IEnumerator AnimateChanges(List<string> ids, Func<string, bool> isPowered, float gap)
        {
            foreach (var id in ids)
            {
                var powered = isPowered(id);
                _powerState[id] = powered;
                _powerSetters[id](powered, true);
                yield return new WaitForSeconds(gap);
            }
        }

        private static IEnumerator RevealRoutine(GameObject block, GameObject model)
        {
            const float duration = 0.6f;
            var start = block.transform.localScale;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                block.transform.localScale = new Vector3(start.x, Mathf.Lerp(start.y, 0.01f, t / duration), start.z);
                yield return null;
            }
            block.SetActive(false);

            model.SetActive(true);
            var target = model.transform.localScale;
            for (var t = 0f; t < duration; t += Time.deltaTime)
            {
                model.transform.localScale = new Vector3(target.x, Mathf.Lerp(0.01f, target.y, t / duration), target.z);
                yield return null;
            }
            model.transform.localScale = target;
        }

        // ───────────────────────────── Yardımcılar ─────────────────────────────

        private void Register(string nodeId, PoweredGroup group)
        {
            _powerSetters[nodeId] = (powered, animate) =>
            {
                if (animate) group.FlickerTo(powered);
                else group.SetPowered(powered);
            };
            _powerState[nodeId] = true;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material,
            Quaternion? rotation = null, bool local = false)
        {
            var go = Primitive(PrimitiveType.Cube, name, parent, material);
            if (local)
            {
                go.transform.localPosition = position;
                go.transform.localRotation = rotation ?? Quaternion.identity;
            }
            else
            {
                go.transform.position = position;
                go.transform.rotation = rotation ?? Quaternion.identity;
            }
            go.transform.localScale = size;
            return go;
        }

        private static GameObject Cylinder(string name, Transform parent, Vector3 position, float diameter, float height, Material material)
        {
            var go = Primitive(PrimitiveType.Cylinder, name, parent, material);
            go.transform.position = position;
            go.transform.localScale = new Vector3(diameter, height / 2f, diameter);
            return go;
        }

        private static GameObject Sphere(string name, Transform parent, Vector3 position, float diameter, Material material)
        {
            var go = Primitive(PrimitiveType.Sphere, name, parent, material);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * diameter;
            return go;
        }

        private static Light AddPointLight(Transform parent, Vector3 position, Color color, float range, float intensity, bool local = false)
        {
            var go = new GameObject("Işık");
            go.transform.SetParent(parent, false);
            if (local) go.transform.localPosition = position;
            else go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>Kameraya bakan iki cepheye (batı ve güney) pencere dizer.</summary>
        private static void Windows(Transform parent, Vector3 center, Vector3 size, int columns, int rows,
            float floorHeight, float firstFloorOffset, Material material)
        {
            var bottom = center.y - size.y / 2f + 1.6f + firstFloorOffset;
            for (var r = 0; r < rows; r++)
            {
                var y = bottom + r * floorHeight;
                if (y > center.y + size.y / 2f - 0.8f) break;
                for (var c = 0; c < columns; c++)
                {
                    var tx = (c + 0.5f) / columns - 0.5f;
                    Box("Pencere", parent, new Vector3(center.x + tx * size.x * 0.85f, y, center.z - size.z / 2f - 0.06f),
                        new Vector3(Mathf.Min(1.4f, size.x / columns * 0.55f), 1.3f, 0.12f), material);
                }
                var sideColumns = Mathf.Max(1, Mathf.RoundToInt(columns * size.z / size.x));
                for (var c = 0; c < sideColumns; c++)
                {
                    var tz = (c + 0.5f) / sideColumns - 0.5f;
                    Box("Pencere", parent, new Vector3(center.x - size.x / 2f - 0.06f, y, center.z + tz * size.z * 0.85f),
                        new Vector3(0.12f, 1.3f, Mathf.Min(1.4f, size.z / sideColumns * 0.55f)), material);
                }
            }
        }

        /// <summary>Bir kutunun 12 kenarına ince ışıklı çubuk koyar ("taranıyor" görünümü).</summary>
        private static void EdgeFrame(Transform parent, Vector3 center, Vector3 size, float thickness, Material material)
        {
            var h = size / 2f;
            for (var sy = -1; sy <= 1; sy += 2)
            for (var sz = -1; sz <= 1; sz += 2)
                Box("Kenar", parent, center + new Vector3(0f, sy * h.y, sz * h.z), new Vector3(size.x, thickness, thickness), material);
            for (var sx = -1; sx <= 1; sx += 2)
            for (var sz = -1; sz <= 1; sz += 2)
                Box("Kenar", parent, center + new Vector3(sx * h.x, 0f, sz * h.z), new Vector3(thickness, size.y, thickness), material);
            for (var sx = -1; sx <= 1; sx += 2)
            for (var sy = -1; sy <= 1; sy += 2)
                Box("Kenar", parent, center + new Vector3(sx * h.x, sy * h.y, 0f), new Vector3(thickness, thickness, size.z), material);
        }
    }
}
