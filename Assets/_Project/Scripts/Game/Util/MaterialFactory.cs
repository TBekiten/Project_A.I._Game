using UnityEngine;

namespace AIGame.Game.Util
{
    /// <summary>
    /// Çalışma anında malzeme üretir. Önce URP shader'larını dener; bulamazsa yerleşik
    /// shader'lara düşer, böylece kod URP dışında da çalışır.
    /// </summary>
    public static class MaterialFactory
    {
        private static Shader _lit;
        private static Shader _unlit;

        private static Shader LitShader
        {
            get
            {
                if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit");
                if (_lit == null) _lit = Shader.Find("Standard");
                return _lit;
            }
        }

        private static Shader UnlitShader
        {
            get
            {
                if (_unlit == null) _unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (_unlit == null) _unlit = Shader.Find("Unlit/Color");
                return _unlit;
            }
        }

        public static Material Lit(Color color, float smoothness = 0.15f)
        {
            var material = new Material(LitShader);
            SetBaseColor(material, color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        /// <summary>Işık saçan malzeme (pencere, lamba). Emisyon rengi sonradan değiştirilebilir.</summary>
        public static Material Emissive(Color baseColor, Color emission)
        {
            var material = Lit(baseColor, 0.6f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SetEmission(material, emission);
            return material;
        }

        /// <summary>Işıktan etkilenmeyen düz renk. HDR (1'den büyük) değerler bloom ile parlar.</summary>
        public static Material Unlit(Color color)
        {
            var material = new Material(UnlitShader);
            SetBaseColor(material, color);
            return material;
        }

        public static void SetBaseColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        public static void SetEmission(Material material, Color emission)
        {
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
        }

        /// <summary>Tek renkli, tekrarlanabilir ızgara dokusu (mini oyun zemini için).</summary>
        public static Texture2D GridTexture(Color background, Color line, int size = 64, int lineWidth = 1)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = x < lineWidth || y < lineWidth ? line : background;
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
