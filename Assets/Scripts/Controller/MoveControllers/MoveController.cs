using SettingsPlayer.PlayerSettings;
using UnityEngine;

namespace Controller.MoveControllers
{
    [RequireComponent(typeof(CharacterController))]
    public class MoveController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 6.5f;
        [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;

        [Header("Gravity / Ground Check")]
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedVelocityY = -2f;
        [SerializeField] private float groundCheckRadius = 0.3f;
        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Aim (RTS - rotate toward cursor)")]
        [Tooltip("When true the character turns to face the point under the mouse cursor.")]
        [SerializeField] private bool aimAtCursor = true;

        [Tooltip("How fast the character rotates toward the cursor (radians/sec feel).")]
        [SerializeField] private float aimTurnSpeed = 12f;

        [Header("Player Settings (stamina)")]
        [Tooltip("Optional link to the player's stats. Sprint is disabled when stamina runs out.")]
        [SerializeField] private PlayerSetting playerSetting;

        private CharacterController _controller;
        private Vector3 _velocity;
        private bool _isGrounded;

        public bool IsGrounded => _isGrounded;
        public bool IsSprinting { get; private set; }

        // Movement data mirrored by other systems (animator, VFX, audio...).
        public bool IsMoving { get; private set; }
        /// <summary>Raw input axes: x = right, y = forward (world space).</summary>
        public Vector2 MoveInput { get; private set; }
        /// <summary>Horizontal speed in m/s the character is currently moving at.</summary>
        public float CurrentSpeed { get; private set; }
        /// <summary>Fastest horizontal speed this controller can reach (sprinting).</summary>
        public float MaxSpeed => Mathf.Max(walkSpeed, sprintSpeed);

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (playerSetting == null)
                playerSetting = GetComponentInParent<PlayerSetting>();
        }

        private void Update()
        {
            CheckGround();
            Move();
            AimAtCursor();
        }

        private void CheckGround()
        {
            if (_controller.isGrounded)
            {
                _isGrounded = true;
                return;
            }

            Vector3 probeCenter = groundCheckPoint != null
                ? groundCheckPoint.position
                : transform.position + _controller.center + Vector3.down * (_controller.height * 0.5f);
            _isGrounded = Physics.CheckSphere(probeCenter, groundCheckRadius, groundMask,
                QueryTriggerInteraction.Ignore);
        }

        private void Move()
        {
            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");

            bool wantsSprint = Input.GetKey(sprintKey);
            bool isMoving = !Mathf.Approximately(x, 0f) || !Mathf.Approximately(z, 0f);

            // Only sprint if there is a link to the player stats, or if sprinting is allowed.
            bool canSprint = playerSetting == null || playerSetting.CanSprint;
            IsSprinting = wantsSprint && isMoving && canSprint;

            float speed = IsSprinting ? sprintSpeed : walkSpeed;

            IsMoving = isMoving;
            MoveInput = new Vector2(x, z);
            CurrentSpeed = isMoving ? speed : 0f;

            _velocity = new Vector3(x, 0f, z) * speed;

            // Keep the player stats informed so stamina drains/regens with real movement.
            if (playerSetting != null)
            {
                playerSetting.SetSprinting(IsSprinting);
                playerSetting.SetMoving(isMoving);
            }

            if (_isGrounded && _velocity.y < 0f)
                _velocity.y = groundedVelocityY;
            else
                _velocity.y += gravity * Time.deltaTime;

            _velocity *= Time.deltaTime;
            _controller.Move(_velocity);
        }

        /// <summary>
        /// RTS-style aim: casts a ray from the camera through the cursor onto a flat
        /// ground plane at the character's height and rotates the character to face it.
        /// </summary>
        private void AimAtCursor()
        {
            if (!aimAtCursor)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, transform.position);

            if (!groundPlane.Raycast(ray, out float enter))
                return;

            Vector3 aimPoint = ray.GetPoint(enter);
            Vector3 direction = aimPoint - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                aimTurnSpeed * Time.deltaTime);
        }
    }
}