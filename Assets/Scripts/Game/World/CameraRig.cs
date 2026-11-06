using UnityEngine;
using UnityEngine.EventSystems;

namespace Ironhorse.Game
{
    public interface ISelectable
    {
        void OnTapped();
    }

    /// <summary>
    /// City-builder camera: one-finger drag pans, pinch zooms (only).
    /// Mouse: left-drag pan, right-drag rotate, wheel zoom. Taps raycast into the world.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }
        public Camera Cam { get; private set; }

        public Vector3 Focus = new Vector3(0, 0, -12);
        public float Distance = 95f;
        public float Yaw = 20f;
        public float Pitch = 54f;
        public float MinDistance = 24f, MaxDistance = 330f;
        public Rect Bounds = new Rect(-275, -280, 550, 980); // every destination, south to the desert and up to Eagle Lake

        /// <summary>Raised on a tap that did not hit a selectable (world position on ground).</summary>
        public System.Action<Vector3> GroundTapped;

        Vector3 _focusTarget;
        float _distTarget, _yawTarget;
        Vector2 _pressPos;
        float _pressTime;
        bool _dragging, _pressOverUI, _multiTouch;
        Vector3 _lastWorldDrag;
        Vector3 _velocity;
        float _lastPinch;
        float _lastTapTime = -1;
        Vector2 _lastTapPos;
        Transform _follow;

        void Awake()
        {
            Instance = this;
            Cam = gameObject.AddComponent<Camera>();
            Cam.fieldOfView = 40; // a little perspective so the world reads as a diorama, not a map
            Cam.nearClipPlane = 1f;
            Cam.farClipPlane = 1400;
            Cam.clearFlags = CameraClearFlags.Skybox;
            Cam.allowMSAA = true;
            Cam.allowHDR = false;
            gameObject.tag = "MainCamera";
            gameObject.AddComponent<AudioListener>();
            _focusTarget = Focus; _distTarget = Distance; _yawTarget = Yaw;
        }

        public void FlyTo(Vector3 focus, float distance = -1)
        {
            _follow = null;
            _focusTarget = new Vector3(focus.x, 0, focus.z);
            if (distance > 0) _distTarget = distance;
        }

        public void Follow(Transform t, float distance = 70) { _follow = t; _distTarget = distance; }

        /// <summary>Zoom by a factor (&lt; 1 zooms in), e.g. from the on-screen +/- buttons.</summary>
        public void ZoomBy(float factor) { _distTarget = Mathf.Clamp(_distTarget * factor, MinDistance, MaxDistance); }

        /// <summary>Jumps between the whole-valley overview and the close town view.</summary>
        public void ToggleOverview()
        {
            _follow = null;
            if (_distTarget < MaxDistance * 0.75f) { _distTarget = MaxDistance; _focusTarget = new Vector3(0, 0, 30); }
            else { _distTarget = 95f; _focusTarget = new Vector3(0, 0, -12); }
        }

        public bool IsPointerOverUI(int pointerId = -1)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return pointerId >= 0 ? es.IsPointerOverGameObject(pointerId) : es.IsPointerOverGameObject();
        }

        void Update()
        {
            if (Input.touchSupported && Input.touchCount > 0) HandleTouch();
            else HandleMouse();

            if (_follow != null)
            {
                if (!_follow.gameObject.activeInHierarchy) _follow = null;
                else _focusTarget = new Vector3(_follow.position.x, 0, _follow.position.z);
            }

            if (!_dragging && _velocity.sqrMagnitude > 0.01f)
            {
                _focusTarget += _velocity * Time.deltaTime;
                _velocity = Vector3.Lerp(_velocity, Vector3.zero, Time.deltaTime * 4f);
            }
            _focusTarget.x = Mathf.Clamp(_focusTarget.x, Bounds.xMin, Bounds.xMax);
            _focusTarget.z = Mathf.Clamp(_focusTarget.z, Bounds.yMin, Bounds.yMax);
            _distTarget = Mathf.Clamp(_distTarget, MinDistance, MaxDistance);

            float k = 1 - Mathf.Exp(-Time.deltaTime * 10f);
            Focus = Vector3.Lerp(Focus, _focusTarget, k);
            Distance = Mathf.Lerp(Distance, _distTarget, k);
            Yaw = Mathf.LerpAngle(Yaw, _yawTarget, k);
            // Looking across the land rather than straight down, so hills, mesas and towns read in depth even
            // zoomed all the way out; a little flatter still when zoomed right in.
            Pitch = Mathf.Lerp(36f, 46f, Mathf.InverseLerp(MinDistance, MaxDistance * 0.6f, Distance));

            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            transform.position = Focus + rot * new Vector3(0, 0, -Distance);
            transform.rotation = rot;
            float ground = ValleyTerrain.Height(transform.position.x, transform.position.z) + 4f;
            if (transform.position.y < ground) transform.position = new Vector3(transform.position.x, ground, transform.position.z);
            if (_shakeLeft > 0)
            {
                _shakeLeft -= Time.unscaledDeltaTime;
                float a = _shakeAmp * Mathf.Clamp01(_shakeLeft / _shakeTime);
                float t = Time.unscaledTime * 32;
                transform.position += transform.right * (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2 * a + transform.up * (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2 * a;
            }
        }

        float _shakeLeft, _shakeTime = 1, _shakeAmp;

        /// <summary>A short rumble that dies away (world units at its start). Stronger shakes win over weaker ones.</summary>
        public void Shake(float amplitude, float seconds)
        {
            if (_shakeLeft > 0 && _shakeAmp * (_shakeLeft / _shakeTime) > amplitude) return;
            _shakeAmp = amplitude;
            _shakeTime = _shakeLeft = Mathf.Max(0.05f, seconds);
        }

        // ---------------------------------------------------------------- mouse

        void HandleMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _pressOverUI = UIUnder(Input.mousePosition);
                _pressPos = Input.mousePosition;
                _pressTime = Time.unscaledTime;
                _dragging = false;
                _lastWorldDrag = GroundPoint(Input.mousePosition);
                _velocity = Vector3.zero;
            }
            if (Input.GetMouseButton(0) && !_pressOverUI)
            {
                if (!_dragging && ((Vector2)Input.mousePosition - _pressPos).magnitude > DragThreshold) { _dragging = true; _follow = null; }
                if (_dragging) DragTo(Input.mousePosition);
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (!_pressOverUI && !_dragging && Time.unscaledTime - _pressTime < 0.5f) Tap(Input.mousePosition);
                _dragging = false;
            }
            if (Input.GetMouseButton(1)) _yawTarget += Input.GetAxis("Mouse X") * 4f;
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && !IsPointerOverUI()) _distTarget *= Mathf.Pow(0.8f, wheel);
        }

        // ---------------------------------------------------------------- touch

        int _panFinger = -1;

        void HandleTouch()
        {
            if (Input.touchCount >= 2)
            {
                // Pinch zooms, nothing else: the view doesn't slide or twist under two fingers.
                var a = Input.GetTouch(0); var b = Input.GetTouch(1);
                float pinch = (a.position - b.position).magnitude;
                if (!_multiTouch || a.phase == TouchPhase.Began || b.phase == TouchPhase.Began)
                {
                    _multiTouch = true;
                    _dragging = false;
                    _velocity = Vector3.zero;
                    _lastPinch = pinch;
                }
                else
                {
                    if (_lastPinch > 1 && pinch > 1) _distTarget *= Mathf.Pow(_lastPinch / pinch, 1.6f); // a bit faster than 1:1 to cross the valley quickly
                    _lastPinch = pinch;
                }
                _panFinger = -1;
                return;
            }
            if (Input.touchCount == 0) { _multiTouch = false; _panFinger = -1; return; }

            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began || t.fingerId != _panFinger)
            {
                // A new finger, or the finger left over after a pinch: grab the ground under it.
                bool fresh = t.phase == TouchPhase.Began && !_multiTouch;
                _panFinger = t.fingerId;
                _pressOverUI = fresh && UIUnder(t.position);
                _pressPos = t.position;
                _pressTime = fresh ? Time.unscaledTime : -10f; // a finger left over from a pinch never taps
                _dragging = !fresh && !_pressOverUI;
                _lastWorldDrag = GroundPoint(t.position);
                _velocity = Vector3.zero;
                _multiTouch = false;
                if (t.phase == TouchPhase.Began) return;
            }
            if ((t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) && !_pressOverUI)
            {
                if (!_dragging && (t.position - _pressPos).magnitude > DragThreshold) { _dragging = true; _follow = null; _lastWorldDrag = GroundPoint(t.position); }
                if (_dragging) DragTo(t.position);
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                if (t.phase == TouchPhase.Ended && !_pressOverUI && !_dragging && Time.unscaledTime - _pressTime < 0.5f)
                {
                    // Double-tap toggles the whole-valley overview; a single tap selects.
                    if (Time.unscaledTime - _lastTapTime < 0.3f && (t.position - _lastTapPos).magnitude < 80f) { ToggleOverview(); _lastTapTime = -1; }
                    else { Tap(t.position); _lastTapTime = Time.unscaledTime; _lastTapPos = t.position; }
                }
                _dragging = false;
                _panFinger = -1;
            }
        }

        /// <summary>True when a UI element that takes input sits at this screen point (explicit raycast, any finger).</summary>
        bool UIUnder(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var ped = new PointerEventData(es) { position = screen };
            _uiHits.Clear();
            es.RaycastAll(ped, _uiHits);
            return _uiHits.Count > 0;
        }

        readonly System.Collections.Generic.List<RaycastResult> _uiHits = new System.Collections.Generic.List<RaycastResult>();

        float DragThreshold { get { return Mathf.Max(8f, Screen.dpi > 0 ? Screen.dpi * 0.06f : 10f); } }

        void DragTo(Vector2 screen)
        {
            var now = GroundPoint(screen);
            var delta = _lastWorldDrag - now;
            delta.y = 0;
            _focusTarget += delta;
            Focus = _focusTarget; // no smoothing while dragging, so the grabbed point tracks the finger
            _velocity = Vector3.Lerp(_velocity, delta / Mathf.Max(Time.deltaTime, 0.001f), 0.3f);
            // Re-project after moving so the grabbed point stays under the finger.
            _lastWorldDrag = now + delta;
        }

        Vector3 GroundPoint(Vector2 screen)
        {
            var ray = Cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            float d;
            if (plane.Raycast(ray, out d)) return ray.GetPoint(Mathf.Min(d, 2000));
            return Focus;
        }

        void Tap(Vector2 screen)
        {
            var ray = Cam.ScreenPointToRay(screen);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 3000f))
            {
                var sel = hit.collider.GetComponentInParent<ISelectable>();
                if (sel != null) { sel.OnTapped(); return; }
                GroundTapped?.Invoke(hit.point);
                return;
            }
            GroundTapped?.Invoke(GroundPoint(screen));
        }
    }
}
