using System.Collections.Generic;
using UnityEngine;

namespace AIGame.Game.Util
{
    /// <summary>
    /// Fare ve klavye girdisini IMGUI olaylarından toplar. Böylece proje eski Input Manager'ı
    /// ya da yeni Input System'i kullansa da girdi aynı şekilde çalışır.
    /// <see cref="Capture"/> her OnGUI çağrısının başında, GUI.matrix birim matrisken çağrılmalı.
    /// </summary>
    public static class GuiInput
    {
        private static readonly HashSet<KeyCode> Held = new HashSet<KeyCode>();
        private static readonly HashSet<KeyCode> PressedThisFrame = new HashSet<KeyCode>();
        private static float _scroll;
        private static int _pressedFrame = -1;

        /// <summary>Farenin ekran konumu, piksel, sol alt köşe orijin (Camera.ScreenPointToRay ile uyumlu).</summary>
        public static Vector2 MouseScreen { get; private set; }

        public static bool HasMouse { get; private set; }

        public static void Capture(Event e)
        {
            if (e == null) return;
            MouseScreen = new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);
            HasMouse = true;

            switch (e.type)
            {
                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.None) break;
                    if (!Held.Contains(e.keyCode))
                    {
                        if (_pressedFrame != Time.frameCount)
                        {
                            PressedThisFrame.Clear();
                            _pressedFrame = Time.frameCount;
                        }
                        PressedThisFrame.Add(e.keyCode);
                    }
                    Held.Add(e.keyCode);
                    break;
                case EventType.KeyUp:
                    Held.Remove(e.keyCode);
                    break;
                case EventType.ScrollWheel:
                    _scroll += e.delta.y;
                    break;
            }
        }

        public static bool IsHeld(KeyCode key) => Held.Contains(key);

        /// <summary>Tuş bu karede ya da bir önceki karede basıldıysa bir kez true döner.</summary>
        public static bool ConsumePress(KeyCode key)
        {
            if (_pressedFrame < Time.frameCount - 1)
            {
                PressedThisFrame.Clear();
                return false;
            }
            return PressedThisFrame.Remove(key);
        }

        public static float ConsumeScroll()
        {
            var value = _scroll;
            _scroll = 0f;
            return value;
        }

        public static void ClearHeld()
        {
            Held.Clear();
            PressedThisFrame.Clear();
            _scroll = 0f;
        }
    }
}
