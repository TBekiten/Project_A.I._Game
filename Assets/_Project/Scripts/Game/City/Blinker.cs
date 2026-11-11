using AIGame.Game.Util;
using UnityEngine;

namespace AIGame.Game.City
{
    /// <summary>İki renk arasında yanıp söner (ambulans tepe lambası, arıza ikaz ışığı).</summary>
    public sealed class Blinker : MonoBehaviour
    {
        public Material Target;
        public Color ColorA = new Color(3f, 0.2f, 0.2f);
        public Color ColorB = Color.black;
        public float Period = 0.5f;
        public Light LightSource;

        private float _timer;
        private bool _phase;

        private void OnEnable()
        {
            Apply();
        }

        private void OnDisable()
        {
            if (Target != null) MaterialFactory.SetEmission(Target, Color.black);
            if (LightSource != null) LightSource.enabled = false;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < Period * 0.5f) return;
            _timer = 0f;
            _phase = !_phase;
            Apply();
        }

        private void Apply()
        {
            var color = _phase ? ColorB : ColorA;
            if (Target != null) MaterialFactory.SetEmission(Target, color);
            if (LightSource != null)
            {
                LightSource.enabled = color.maxColorComponent > 0.01f;
                LightSource.color = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b));
            }
        }
    }
}
