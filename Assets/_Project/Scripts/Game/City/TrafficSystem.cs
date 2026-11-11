using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIGame.Game.City
{
    /// <summary>Araçların izlediği çizgi. İsteğe bağlı bir kavşak durma çizgisi içerir.</summary>
    public sealed class TrafficRoute
    {
        private readonly Vector3[] _points;
        private readonly float[] _cumulative;

        public TrafficRoute(Vector3[] points, TrafficAxis axis, float stopDistance, bool loops)
        {
            if (points == null || points.Length < 2) throw new ArgumentException("Rota en az iki nokta ister.");
            _points = points;
            _cumulative = new float[points.Length];
            for (var i = 1; i < points.Length; i++)
                _cumulative[i] = _cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            Axis = axis;
            StopDistance = stopDistance;
            Loops = loops;
        }

        public TrafficAxis Axis { get; }

        /// <summary>Kavşak durma çizgisinin rota başından uzaklığı. Negatifse durma çizgisi yok.</summary>
        public float StopDistance { get; }

        /// <summary>Rota bitince araç başa döner mi?</summary>
        public bool Loops { get; }

        public float Length => _cumulative[_cumulative.Length - 1];

        public Vector3 Evaluate(float distance, out Vector3 forward)
        {
            distance = Mathf.Clamp(distance, 0f, Length);
            for (var i = 1; i < _points.Length; i++)
            {
                if (distance > _cumulative[i] && i < _points.Length - 1) continue;
                var segment = _cumulative[i] - _cumulative[i - 1];
                var t = segment <= 0f ? 0f : (distance - _cumulative[i - 1]) / segment;
                forward = (_points[i] - _points[i - 1]).normalized;
                return Vector3.Lerp(_points[i - 1], _points[i], t);
            }
            forward = Vector3.forward;
            return _points[_points.Length - 1];
        }
    }

    public sealed class Vehicle : MonoBehaviour
    {
        public TrafficRoute Route;
        public float Distance;
        public float Speed;
        public float CruiseSpeed = 9f;
        public bool IsAmbulance;
        public bool PassedStop;
        public float WaitTimer;
        public bool Arrived;
        public float ParkedTime;

        public float RemainingParkSeconds = 8f;
    }

    /// <summary>
    /// Basit trafik: araçlar rotalarını izler, öndeki araca mesafe bırakır ve kavşakta
    /// ışıklara uyar. Işıklar sönükse her araç tam durur ve kavşağa tek tek girer;
    /// ambulans da bu kuyrukta bekler. Zincirleme sonucun görünen hâli budur.
    /// </summary>
    public sealed class TrafficSystem : MonoBehaviour
    {
        private readonly List<Vehicle> _vehicles = new List<Vehicle>();

        public TrafficLightController Lights;
        public float MinGap = 6.5f;
        public float UnpoweredWaitSeconds = 2.2f;
        public float CrossingSeconds = 1.6f;
        public float Acceleration = 10f;
        public float Braking = 24f;

        private float _intersectionFreeAt;

        public IReadOnlyList<Vehicle> Vehicles => _vehicles;

        public Vehicle Spawn(GameObject model, TrafficRoute route, float distance, float cruiseSpeed, bool ambulance)
        {
            var vehicle = model.AddComponent<Vehicle>();
            vehicle.Route = route;
            vehicle.Distance = distance;
            vehicle.CruiseSpeed = cruiseSpeed;
            vehicle.Speed = cruiseSpeed;
            vehicle.IsAmbulance = ambulance;
            vehicle.PassedStop = route.StopDistance < 0f || distance > route.StopDistance;
            model.transform.SetParent(transform, true);
            Place(vehicle);
            _vehicles.Add(vehicle);
            return vehicle;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var now = Time.time;

            for (var i = _vehicles.Count - 1; i >= 0; i--)
            {
                var v = _vehicles[i];
                if (v == null)
                {
                    _vehicles.RemoveAt(i);
                    continue;
                }

                if (v.Arrived)
                {
                    v.ParkedTime += dt;
                    if (v.ParkedTime >= v.RemainingParkSeconds)
                    {
                        _vehicles.RemoveAt(i);
                        Destroy(v.gameObject);
                    }
                    continue;
                }

                var target = v.CruiseSpeed;

                var gap = GapAhead(v);
                if (gap < MinGap) target = 0f;
                else if (gap < MinGap * 2f) target *= (gap - MinGap) / MinGap;

                var holdAtLine = false;
                if (!v.PassedStop && v.Route.StopDistance >= 0f)
                {
                    var toStop = v.Route.StopDistance - v.Distance;
                    if (toStop <= 0.05f)
                    {
                        if (TryEnterIntersection(v, now, dt))
                        {
                            v.PassedStop = true;
                        }
                        else
                        {
                            holdAtLine = true;
                            target = 0f;
                        }
                    }
                    else if (toStop < 10f && !CanCrossWithoutStopping(v))
                    {
                        target = Mathf.Min(target, Mathf.Max(1.5f, toStop * 1.2f));
                    }
                }

                var rate = target > v.Speed ? Acceleration : Braking;
                v.Speed = Mathf.MoveTowards(v.Speed, target, rate * dt);
                v.Distance += v.Speed * dt;

                if ((holdAtLine || !v.PassedStop) && v.Route.StopDistance >= 0f && v.Distance > v.Route.StopDistance)
                {
                    v.Distance = v.Route.StopDistance;
                    if (holdAtLine) v.Speed = 0f;
                }

                if (v.Distance >= v.Route.Length)
                {
                    if (v.Route.Loops)
                    {
                        v.Distance -= v.Route.Length;
                        v.PassedStop = v.Route.StopDistance < 0f;
                        v.WaitTimer = 0f;
                    }
                    else
                    {
                        v.Distance = v.Route.Length;
                        v.Arrived = true;
                        v.Speed = 0f;
                    }
                }

                Place(v);
            }
        }

        /// <summary>
        /// Aynı yönde, aynı şeritte öndeki araca olan mesafe. Dünya konumuyla ölçülür;
        /// böylece farklı rotalardaki araçlar (ör. ambulans ve arabalar) aynı şeritte sıraya girer.
        /// </summary>
        private float GapAhead(Vehicle v)
        {
            var best = float.MaxValue;
            var forward = v.transform.forward;
            var position = v.transform.position;
            foreach (var other in _vehicles)
            {
                if (other == null || other == v || other.Arrived) continue;
                if (Vector3.Dot(forward, other.transform.forward) < 0.7f) continue;
                var delta = other.transform.position - position;
                var along = Vector3.Dot(delta, forward);
                if (along <= 0f) continue;
                var lateral = (delta - forward * along).magnitude;
                if (lateral > 1.6f) continue;
                if (along < best) best = along;
            }
            return best;
        }

        private bool CanCrossWithoutStopping(Vehicle v)
        {
            if (Lights == null) return true;
            if (!Lights.Powered) return false;
            return v.IsAmbulance || Lights.IsGreen(v.Route.Axis);
        }

        private bool TryEnterIntersection(Vehicle v, float now, float dt)
        {
            if (Lights == null) return true;
            if (Lights.Powered)
            {
                v.WaitTimer = 0f;
                return v.IsAmbulance || Lights.IsGreen(v.Route.Axis);
            }

            // Işıklar sönük: tam dur, bekle, kavşak boşsa gir.
            v.WaitTimer += dt;
            if (v.WaitTimer < UnpoweredWaitSeconds || now < _intersectionFreeAt) return false;
            _intersectionFreeAt = now + CrossingSeconds;
            v.WaitTimer = 0f;
            return true;
        }

        private static void Place(Vehicle v)
        {
            var position = v.Route.Evaluate(v.Distance, out var forward);
            v.transform.position = position;
            if (forward.sqrMagnitude > 0.0001f) v.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
