using System.Collections.Generic;
using UnityEngine;

namespace AIGame.Game.UI
{
    /// <summary>
    /// A.I.'ın sesi: balonda daktilo efektiyle sırayla gösterilen cümleler.
    /// A.I.'ın dünyada bir bedeni yok; oyuncunun gördüğü tek temsili bu balon.
    /// </summary>
    public sealed class VoiceQueue
    {
        private readonly Queue<string> _queue = new Queue<string>();
        private string _current;
        private float _visibleChars;
        private float _hold;

        public float CharactersPerSecond = 48f;
        public float MinimumHold = 2.4f;
        public float HoldPerCharacter = 0.035f;

        /// <summary>Şu an gösterilen metin (daktilo ilerlemesine göre kesilmiş).</summary>
        public string Visible =>
            _current == null ? null : _current.Substring(0, Mathf.Min(_current.Length, Mathf.FloorToInt(_visibleChars)));

        public bool IsSpeaking => _current != null && _visibleChars < _current.Length;
        public bool HasLine => _current != null;

        /// <summary>Balon kaybolmadan önce son birkaç saniyede saydamlaşır (0..1).</summary>
        public float Opacity => _current == null ? 0f : Mathf.Clamp01(_hold / 0.6f + (IsSpeaking ? 1f : 0f));

        public void Say(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (_current == line || _queue.Contains(line)) return;
            _queue.Enqueue(line);
            if (_current == null) Next();
        }

        /// <summary>Sıradakileri beklemeden hemen söyler (uyarılar için).</summary>
        public void Interrupt(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _current = line;
            _visibleChars = 0f;
            _hold = MinimumHold + line.Length * HoldPerCharacter;
        }

        public void Clear()
        {
            _queue.Clear();
            _current = null;
        }

        /// <summary>Daktiloyu bitirir ya da bir sonraki cümleye geçer (tıklayınca).</summary>
        public void Skip()
        {
            if (_current == null) return;
            if (IsSpeaking) _visibleChars = _current.Length;
            else Next();
        }

        public void Update(float deltaTime)
        {
            if (_current == null) return;
            if (_visibleChars < _current.Length)
            {
                _visibleChars += deltaTime * CharactersPerSecond;
                return;
            }

            _hold -= deltaTime;
            if (_hold <= 0f) Next();
        }

        private void Next()
        {
            if (_queue.Count == 0)
            {
                _current = null;
                return;
            }

            _current = _queue.Dequeue();
            _visibleChars = 0f;
            _hold = MinimumHold + _current.Length * HoldPerCharacter;
        }
    }
}
