using AIGame.Game.Util;
using UnityEngine;

namespace AIGame.Game
{
    /// <summary>
    /// Şehir kamerası: ortografik, izometriğe yakın açı. WASD/oklarla kaydırma, tekerlekle yakınlaşma.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public float PanSpeed = 45f;
        public float MinSize = 18f;
        public float MaxSize = 70f;
        public float Bounds = 80f;

        private Camera _camera;
        private Vector3 _target;
        private float _targetSize;
        private Vector3? _focus;
        private float? _focusSize;

        public Camera Camera => _camera;
        public bool InputEnabled { get; set; } = true;

        public static CameraRig Create(Transform parent, Color background)
        {
            var rigGo = new GameObject("Kamera");
            rigGo.transform.SetParent(parent, false);
            var rig = rigGo.AddComponent<CameraRig>();

            var cameraGo = new GameObject("Şehir kamerası");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(rigGo.transform, false);
            cameraGo.transform.localRotation = Quaternion.Euler(50f, 45f, 0f);
            cameraGo.transform.localPosition = cameraGo.transform.localRotation * new Vector3(0f, 0f, -220f);
            rig._camera = cameraGo.AddComponent<Camera>();
            rig._camera.orthographic = true;
            rig._camera.orthographicSize = 46f;
            rig._camera.clearFlags = CameraClearFlags.SolidColor;
            rig._camera.backgroundColor = background;
            rig._camera.nearClipPlane = 1f;
            rig._camera.farClipPlane = 600f;
            cameraGo.AddComponent<AudioListener>();
            PostProcessingSetup.EnableOn(rig._camera);

            rig._target = Vector3.zero;
            rig._targetSize = rig._camera.orthographicSize;
            return rig;
        }

        /// <summary>Kamerayı yumuşakça bir noktaya ve yakınlığa götürür.</summary>
        public void FocusOn(Vector3 point, float size)
        {
            _focus = new Vector3(point.x, 0f, point.z);
            _focusSize = Mathf.Clamp(size, MinSize, MaxSize);
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;

            if (InputEnabled)
            {
                var input = Vector2.zero;
                if (GuiInput.IsHeld(KeyCode.W) || GuiInput.IsHeld(KeyCode.UpArrow)) input.y += 1f;
                if (GuiInput.IsHeld(KeyCode.S) || GuiInput.IsHeld(KeyCode.DownArrow)) input.y -= 1f;
                if (GuiInput.IsHeld(KeyCode.D) || GuiInput.IsHeld(KeyCode.RightArrow)) input.x += 1f;
                if (GuiInput.IsHeld(KeyCode.A) || GuiInput.IsHeld(KeyCode.LeftArrow)) input.x -= 1f;

                if (input.sqrMagnitude > 0f)
                {
                    _focus = null;
                    _focusSize = null;
                    var yaw = Quaternion.Euler(0f, _camera.transform.eulerAngles.y, 0f);
                    var move = yaw * new Vector3(input.x, 0f, input.y).normalized;
                    _target += move * PanSpeed * dt * (_targetSize / 46f);
                }

                var scroll = GuiInput.ConsumeScroll();
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _focusSize = null;
                    _targetSize = Mathf.Clamp(_targetSize + scroll * 2.2f, MinSize, MaxSize);
                }
            }
            else
            {
                GuiInput.ConsumeScroll();
            }

            if (_focus.HasValue) _target = Vector3.Lerp(_target, _focus.Value, 1f - Mathf.Exp(-4f * dt));
            if (_focusSize.HasValue) _targetSize = Mathf.Lerp(_targetSize, _focusSize.Value, 1f - Mathf.Exp(-4f * dt));

            _target.x = Mathf.Clamp(_target.x, -Bounds, Bounds);
            _target.z = Mathf.Clamp(_target.z, -Bounds, Bounds);
            transform.position = Vector3.Lerp(transform.position, _target, 1f - Mathf.Exp(-10f * dt));
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _targetSize, 1f - Mathf.Exp(-10f * dt));
        }
    }
}
