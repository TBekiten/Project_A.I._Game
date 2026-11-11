using System.Collections;
using System.Collections.Generic;
using AIGame.Game.Util;
using UnityEngine;

namespace AIGame.Game.City
{
    /// <summary>
    /// Bir binanın ya da yükün enerjiye bağlı görselleri: pencereler, tabelalar, lambalar.
    /// "Ters" malzemeler yalnızca elektrik yokken yanar (ör. hastanenin jeneratör ışıkları).
    /// </summary>
    public sealed class PoweredGroup : MonoBehaviour
    {
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Color> _emissions = new List<Color>();
        private readonly List<bool> _inverted = new List<bool>();
        private readonly List<Light> _lights = new List<Light>();
        private Coroutine _flicker;

        public bool Powered { get; private set; } = true;

        public void AddEmissive(Material material, Color emission, bool inverted = false)
        {
            _materials.Add(material);
            _emissions.Add(emission);
            _inverted.Add(inverted);
            ApplyVisual(Powered);
        }

        public void AddLight(Light light)
        {
            _lights.Add(light);
            light.enabled = Powered;
        }

        public void SetPowered(bool powered)
        {
            if (_flicker != null)
            {
                StopCoroutine(_flicker);
                _flicker = null;
            }
            Powered = powered;
            ApplyVisual(powered);
        }

        /// <summary>Elektrik gelip giderken birkaç kez titreyip yeni duruma oturur.</summary>
        public void FlickerTo(bool powered)
        {
            if (_flicker != null) StopCoroutine(_flicker);
            Powered = powered;
            if (!isActiveAndEnabled)
            {
                ApplyVisual(powered);
                return;
            }
            _flicker = StartCoroutine(Flicker(powered));
        }

        private IEnumerator Flicker(bool target)
        {
            var steps = Random.Range(3, 6);
            for (var i = 0; i < steps; i++)
            {
                ApplyVisual(i % 2 == 0 ? !target : target);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
            }
            ApplyVisual(target);
            _flicker = null;
        }

        private void ApplyVisual(bool powered)
        {
            for (var i = 0; i < _materials.Count; i++)
            {
                var lit = _inverted[i] ? !powered : powered;
                MaterialFactory.SetEmission(_materials[i], lit ? _emissions[i] : Color.black);
            }
            foreach (var light in _lights)
                if (light != null) light.enabled = powered;
        }
    }
}
