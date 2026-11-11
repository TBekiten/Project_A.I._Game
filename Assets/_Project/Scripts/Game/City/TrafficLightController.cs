using System.Collections.Generic;
using AIGame.Game.Util;
using UnityEngine;

namespace AIGame.Game.City
{
    public enum TrafficAxis
    {
        EastWest,
        NorthSouth
    }

    /// <summary>
    /// Ana kavşağın ışıkları. Elektrik varsa iki eksen sırayla yeşil yanar;
    /// elektrik yoksa tüm lambalar söner ve sürücüler kavşağa sırayla, yavaşça girer.
    /// </summary>
    public sealed class TrafficLightController : MonoBehaviour
    {
        private sealed class SignalHead
        {
            public TrafficAxis Axis;
            public Material Red;
            public Material Yellow;
            public Material Green;
        }

        private static readonly Color RedOn = new Color(3.2f, 0.25f, 0.2f);
        private static readonly Color YellowOn = new Color(3.0f, 2.0f, 0.2f);
        private static readonly Color GreenOn = new Color(0.3f, 3.0f, 0.8f);

        private readonly List<SignalHead> _heads = new List<SignalHead>();

        public float GreenSeconds = 6f;
        public float YellowSeconds = 1.5f;

        private float _timer;
        private TrafficAxis _greenAxis = TrafficAxis.EastWest;
        private bool _yellow;

        public bool Powered { get; private set; } = true;

        public void AddHead(TrafficAxis axis, Material red, Material yellow, Material green)
        {
            _heads.Add(new SignalHead { Axis = axis, Red = red, Yellow = yellow, Green = green });
            Refresh();
        }

        public void SetPowered(bool powered)
        {
            Powered = powered;
            Refresh();
        }

        /// <summary>Bu eksendeki araçlar kavşağa girebilir mi?</summary>
        public bool IsGreen(TrafficAxis axis) => Powered && !_yellow && _greenAxis == axis;

        private void Update()
        {
            if (!Powered) return;
            _timer += Time.deltaTime;
            if (!_yellow && _timer >= GreenSeconds)
            {
                _yellow = true;
                _timer = 0f;
                Refresh();
            }
            else if (_yellow && _timer >= YellowSeconds)
            {
                _yellow = false;
                _timer = 0f;
                _greenAxis = _greenAxis == TrafficAxis.EastWest ? TrafficAxis.NorthSouth : TrafficAxis.EastWest;
                Refresh();
            }
        }

        private void Refresh()
        {
            foreach (var head in _heads)
            {
                var active = Powered && head.Axis == _greenAxis;
                MaterialFactory.SetEmission(head.Green, active && !_yellow ? GreenOn : Color.black);
                MaterialFactory.SetEmission(head.Yellow, active && _yellow ? YellowOn : Color.black);
                MaterialFactory.SetEmission(head.Red, Powered && !active ? RedOn : Color.black);
            }
        }
    }
}
