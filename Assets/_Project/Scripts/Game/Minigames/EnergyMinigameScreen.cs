using System.Collections.Generic;
using AIGame.Core.Energy;
using AIGame.Game.Systems;
using AIGame.Game.Util;
using UnityEngine;
using Ids = AIGame.Core.Content.EastDistrictScenario.Ids;

namespace AIGame.Game.Minigames
{
    /// <summary>
    /// Enerji mini oyununun ayrı ekranı. Şehirden uzakta, kendi kamerasıyla duran bir
    /// "kontrol paneli": şebeke şeması tepeden görünür, oyuncu anahtarlara ve yüklere tıklar.
    /// Oyun akışı açısından ayrı bir bölümdür; ileride ayrı bir Unity sahnesine taşınabilir.
    /// </summary>
    public sealed class EnergyMinigameScreen : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(3000f, 0f, 3000f);

        private Camera _camera;

        public Camera Camera => _camera;
        public GridBoard Board { get; private set; }
        public bool IsOpen => gameObject.activeSelf;

        public static EnergyMinigameScreen Create(Transform parent, PowerGrid grid)
        {
            var go = new GameObject("Mini oyun — Enerji şebekesi");
            go.transform.SetParent(parent, false);
            go.transform.position = Origin;
            var screen = go.AddComponent<EnergyMinigameScreen>();
            screen.Build(grid);
            go.SetActive(false);
            return screen;
        }

        private void Build(PowerGrid grid)
        {
            var cameraGo = new GameObject("Mini oyun kamerası");
            cameraGo.transform.SetParent(transform, false);
            cameraGo.transform.position = Origin + new Vector3(-5.4f, 60f, -5f);
            cameraGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _camera = cameraGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 29f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.015f, 0.03f, 0.05f);
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = 200f;
            PostProcessingSetup.EnableOn(_camera);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name = "Zemin ızgarası";
            Destroy(floor.GetComponent<Collider>());
            floor.transform.SetParent(transform, false);
            floor.transform.position = Origin + new Vector3(0f, -0.5f, 0f);
            floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            floor.transform.localScale = new Vector3(240f, 240f, 1f);
            var floorMaterial = MaterialFactory.Unlit(new Color(0.55f, 0.75f, 1f));
            floorMaterial.mainTexture = MaterialFactory.GridTexture(new Color(0.02f, 0.035f, 0.055f), new Color(0.06f, 0.12f, 0.17f));
            floorMaterial.mainTextureScale = new Vector2(60f, 60f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            Board = GridBoard.Create(transform, "Şebeke şeması", grid, Layout(), Bends(), new BoardStyle
            {
                SourceSize = 3.4f,
                JunctionSize = 2f,
                LoadSize = 2.8f,
                LineWidth = 0.75f,
                OpenLineWidth = 0.28f,
                SwitchSize = 1.7f,
                ShowLineLabels = true
            });
        }

        /// <summary>Şemanın elle yerleştirilmiş düzeni. Hatlar kesişmeyecek şekilde seçildi.</summary>
        private static Dictionary<string, Vector3> Layout()
        {
            Vector3 P(float x, float z) => Origin + new Vector3(x, 0.5f, z);
            return new Dictionary<string, Vector3>
            {
                [Ids.Substation] = P(-24f, 0f),
                [Ids.HousingJunction] = P(-8.5f, -6f),
                [Ids.MarketJunction] = P(-8.5f, 10f),
                [Ids.Hospital] = P(18.5f, 4f),
                [Ids.StreetLamps] = P(-1.7f, 2f),
                [Ids.Pump] = P(-15.3f, 2f),
                [Ids.Housing] = P(6.8f, -12f),
                [Ids.TrafficLights] = P(-1.7f, -15f),
                [Ids.Market] = P(5.1f, 14f)
            };
        }

        private static Dictionary<string, Vector3[]> Bends()
        {
            Vector3 P(float x, float z) => Origin + new Vector3(x, 0.5f, z);
            return new Dictionary<string, Vector3[]>
            {
                [Ids.HospitalLine] = new[] { P(-24f, -21f), P(18.5f, -21f) }
            };
        }

        public void Open(GridEvaluation evaluation)
        {
            gameObject.SetActive(true);
            Board.Refresh(evaluation);
        }

        public void Close()
        {
            Board.SetHover(null);
            gameObject.SetActive(false);
        }
    }
}
