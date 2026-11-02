using UnityEngine;
using UnityEngine.EventSystems;

namespace Ironhorse.Game
{
    public interface ISelectable
    {
        void OnTapped();
    }

    /// <summary>
    /// City-builder camera: one-finger drag pans, pinch zooms, two-finger twist rotates.
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
        public float MinDistance = 28f, MaxDistance = 260f;
        public Rect Bounds = new Rect(-240, -170, 480, 420);

        /// <summary>Raised on a tap that did not hit a selectable (world position on ground).</summary>
        public System.Action<Vector3> GroundTapped;

        Vector3 _focusTarget;
        float _distTarget, _yawTarget;
        Vector2 _pressPos;
        float _pressTime;
        bool _dragging, _pressOverUI, _multiTouch;
        Vector3 _lastWorldDrag;
        Vector3 _velocity;
        float _lastPinch, _lastTwist;
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
            // Tilt flatter when zoomed in for a more cinematic view.
            // A high, map-like angle (like a model railway seen from above), flattening a little when zoomed right in.
            Pitch = Mathf.Lerp(36f, 58f, Mathf.InverseLerp(MinDistance, MaxDistance * 0.6f, Distance));

            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            transform.position = Focus + rot * new Vector3(0, 0, -Distance);
            transform.rotation = rot;
            float ground = ValleyTerrain.Height(transform.position.x, transform.position.z) + 4f;
            if (transform.position.y < ground) transform.position = new Vector3(transform.position.x, ground, transform.position.z);
        }

        // ---------------------------------------------------------------- mouse

        void HandleMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _pressOverUI = IsPointerOverUI();
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
            if (Mathf.Abs(wheel) > 0.01f && !IsPointerOverUI()) _distTarget *= Mathf.Pow(0.88f, wheel);
        }

        // ---------------------------------------------------------------- touch

        void HandleTouch()
        {
            if (Input.touchCount == 1 && !_multiTouch)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    _pressOverUI = IsPointerOverUI(t.fingerId);
                    _pressPos = t.position;
                    _pressTime = Time.unscaledTime;
                    _dragging = false;
                    _lastWorldDrag = GroundPoint(t.position);
                    _velocity = Vector3.zero;
                }
                else if (t.phase == TouchPhase.Moved && !_pressOverUI)
                {
                    if (!_dragging && (t.position - _pressPos).magnitude > DragThreshold) { _dragging = true; _follow = null; }
                    if (_dragging) DragTo(t.position);
                }
                else if (t.phase == TouchPhase.Ended)
                {
                    if (!_pressOverUI && !_dragging && Time.unscaledTime - _pressTime < 0.5f) Tap(t.position);
                    _dragging = false;
                }
            }
            else if (Input.touchCount >= 2)
            {
                var a = Input.GetTouch(0); var b = Input.GetTouch(1);
                float pinch = (a.position - b.position).magnitude;
                float twist = Mathf.Atan2(b.position.y - a.position.y, b.position.x - a.position.x) * Mathf.Rad2Deg;
                if (!_multiTouch || a.phase == TouchPhase.Began || b.phase == TouchPhase.Began)
                {
                    _multiTouch = true;
                    _dragging = false;
                    _lastPinch = pinch;
                    _lastTwist = twist;
                    _lastWorldDrag = GroundPoint((a.position + b.position) / 2);
                }
                else
                {
                    if (_lastPinch > 1 && pinch > 1) _distTarget *= _lastPinch / pinch;
                    _yawTarget -= Mathf.DeltaAngle(_lastTwist, twist);
                    _lastPinch = pinch;
                    _lastTwist = twist;
                    var mid = GroundPoint((a.position + b.position) / 2);
                    _focusTarget += _lastWorldDrag - mid;
                    Focus = _focusTarget; // the grabbed ground point stays fixed in the world
                }
            }
            if (Input.touchCount == 0) _multiTouch = false;
        }

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
