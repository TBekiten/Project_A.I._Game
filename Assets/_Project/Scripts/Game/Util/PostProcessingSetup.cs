using UnityEngine;
#if AIGAME_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace AIGame.Game.Util
{
    /// <summary>
    /// URP varsa ışıkların parlaması için bloom ekler. AIGAME_URP sembolü,
    /// URP paketi kuruluysa asmdef'teki versionDefines ile tanımlanır.
    /// </summary>
    public static class PostProcessingSetup
    {
        public static void EnableOn(Camera camera)
        {
#if AIGAME_URP
            var data = camera.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;
#endif
        }

        public static void CreateGlobalVolume(Transform parent)
        {
#if AIGAME_URP
            var go = new GameObject("Global Volume");
            go.transform.SetParent(parent, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.65f);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);

            volume.sharedProfile = profile;
#endif
        }
    }
}
