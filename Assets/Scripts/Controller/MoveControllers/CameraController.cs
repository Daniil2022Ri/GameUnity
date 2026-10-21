namespace Controller.MoveControllers
{
    using UI.Settings;
    using UnityEngine;

    /// <summary>
    /// RTS-style follow camera. Smoothly trails the player and pans the view
    /// toward the mouse cursor (look-ahead) so the aim direction is visible while
    /// the player itself always stays on screen.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Follow")]
        [Tooltip("Base camera offset relative to the target.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -8f);
        [Tooltip("Higher = tighter follow.")]
        [SerializeField] private float followLerp = 8f;
        [Tooltip("If true the offset rotates with the target's yaw; false keeps a fixed world (RTS) angle.")]
        [SerializeField] private bool followTargetRotation = false;

        [Header("Cursor Look-Ahead (RTS aim)")]
        [Tooltip("Shift the view toward the mouse cursor.")]
        [SerializeField] private bool lookAtCursor = true;
        [Tooltip("Maximum horizontal distance the view can drift toward the cursor.")]
        [SerializeField] private float maxLookAhead = 6f;
        [Tooltip("Portion of the look-ahead applied to the camera position (physical pan).")]
        [Range(0f, 1f)]
        [SerializeField] private float positionLookAheadWeight = 0.5f;
        [Tooltip("Portion of the look-ahead applied to the look point (how much the view turns).")]
        [Range(0f, 1f)]
        [SerializeField] private float lookPointLookAheadWeight = 1f;
        [Tooltip("Higher = the look-ahead reacts faster to the cursor.")]
        [SerializeField] private float lookAheadLerp = 6f;
        [Tooltip("Scale the look-ahead response with GameSettings.MouseSensitivity " +
                 "(1 = neutral, so nothing changes until the player moves the slider).")]
        [SerializeField] private bool useSettingsSensitivity = true;

        private Camera _camera;
        private Vector3 _currentLookAhead;
        private Vector3 _smoothedLookPoint;

        private void Awake()
        {
            // Prefer the camera on this object; fall back to the main camera.
            _camera = TryGetComponent(out Camera cam) ? cam : Camera.main;
        }

        private void Start()
        {
            // Snap the camera into place on the first frame to avoid an initial glide-in.
            if (target == null)
                return;

            _currentLookAhead = Vector3.zero;
            _smoothedLookPoint = target.position;
            transform.position = target.position + offset;
            transform.LookAt(_smoothedLookPoint + Vector3.up * 0.5f);
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 desiredLookAhead = ComputeCursorLookAhead();

            // Frame-rate independent smoothing of the look-ahead vector. The settings slider
            // scales the response, so a higher mouse sensitivity pans the view faster.
            float lookSpeed = lookAheadLerp * SensitivityMultiplier;
            _currentLookAhead = Vector3.Lerp(_currentLookAhead, desiredLookAhead,
                1f - Mathf.Exp(-lookSpeed * Time.deltaTime));

            Vector3 baseOffset = followTargetRotation
                ? Quaternion.Euler(0f, target.eulerAngles.y, 0f) * offset
                : offset;

            Vector3 desiredPosition = target.position + baseOffset;
            // Only pan horizontally toward the cursor; keep the configured height.
            desiredPosition += new Vector3(_currentLookAhead.x, 0f, _currentLookAhead.z)
                * positionLookAheadWeight;

            transform.position = Vector3.Lerp(transform.position, desiredPosition,
                1f - Mathf.Exp(-followLerp * Time.deltaTime));

            Vector3 desiredLookPoint = target.position + _currentLookAhead * lookPointLookAheadWeight;
            _smoothedLookPoint = Vector3.Lerp(_smoothedLookPoint, desiredLookPoint,
                1f - Mathf.Exp(-followLerp * Time.deltaTime));

            transform.LookAt(_smoothedLookPoint + Vector3.up * 0.5f);
        }

        /// <summary>Mouse sensitivity from the settings menu (1 when settings are not loaded).</summary>
        private float SensitivityMultiplier =>
            useSettingsSensitivity && GameSettings.Exists ? GameSettings.Instance.MouseSensitivity : 1f;

        /// <summary>
        /// Projects the cursor onto a flat ground plane at the target's height and
        /// returns a clamped horizontal vector pointing from the target toward the cursor.
        /// </summary>
        private Vector3 ComputeCursorLookAhead()
        {
            if (!lookAtCursor || _camera == null)
                return Vector3.zero;

            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, target.position);

            if (!groundPlane.Raycast(ray, out float enter))
                return Vector3.zero;

            Vector3 cursorGround = ray.GetPoint(enter);
            Vector3 delta = cursorGround - target.position;
            delta.y = 0f;

            float distance = delta.magnitude;
            if (distance < 0.001f)
                return Vector3.zero;

            delta.Normalize();
            return delta * Mathf.Min(distance, maxLookAhead);
        }
    }
}
