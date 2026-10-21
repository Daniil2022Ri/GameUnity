using Controller.MoveControllers;
using UnityEngine;

namespace Controller.AnimationControllers
{
    /// <summary>
    /// Feeds <see cref="MoveController"/> movement into the player's <see cref="Animator"/>:
    /// idle / walk / sprint, the four strafe directions (forward, backward, right, left) and
    /// the smooth walk -> sprint (acceleration) transition.
    ///
    /// This component never moves anything - movement, gravity and the RTS aiming stay in
    /// <see cref="MoveController"/>. Here only animation reacts to it, so the same player can
    /// be animated by a 2D blend tree, by a state machine, or by both at once.
    ///
    /// Parameter names are resolved against the animator at Start: whatever the controller does
    /// not expose is skipped silently, so a partial (or still empty) animator does not spam
    /// warnings in the console.
    /// </summary>
    [RequireComponent(typeof(MoveController))]
    [DefaultExecutionOrder(50)] // runs after MoveController (order 0), so movement data is fresh
    public class AnimationController : MonoBehaviour
    {
        private const string LogPrefix = nameof(AnimationController) + ": ";

        [Header("References")]
        [Tooltip("Animator to drive. Falls back to the first Animator on this object or its children.")]
        [SerializeField] private Animator animator;
        [Tooltip("Movement source. Falls back to this object or its parents.")]
        [SerializeField] private MoveController moveController;

        [Header("Parameters - Blend Tree")]
        [Tooltip("Normalised speed: 0 = idle, ~0.5 = walk, 1 = sprint. Blend walk -> sprint on this one.")]
        [SerializeField] private string speedParameter = "Speed";
        [Tooltip("Character-local strafe axis: -1 = left, +1 = right.")]
        [SerializeField] private string moveXParameter = "MoveX";
        [Tooltip("Character-local forward axis: -1 = backward, +1 = forward.")]
        [SerializeField] private string moveZParameter = "MoveZ";
        [Tooltip("True while the character actually moves.")]
        [SerializeField] private string isMovingParameter = "IsMoving";
        [Tooltip("True while moving with the sprint key held.")]
        [SerializeField] private string isSprintingParameter = "IsSprinting";

        [Header("Parameters - Direction Flags (optional)")]
        [SerializeField] private string movingForwardParameter = "IsMovingForward";
        [SerializeField] private string movingBackwardParameter = "IsMovingBackward";
        [SerializeField] private string movingRightParameter = "IsMovingRight";
        [SerializeField] private string movingLeftParameter = "IsMovingLeft";
        [Tooltip("Local axis value above which a direction counts as active.")]
        [Range(0.1f, 0.95f)]
        [SerializeField] private float directionThreshold = 0.5f;

        [Header("Smoothing")]
        [Tooltip("Approximate seconds for the parameters to reach a new value. 0 = instant.")]
        [SerializeField] private float smoothingTime = 0.1f;

        [Header("State Machine (optional)")]
        [Tooltip("Cross-fade between the state names below when the movement state changes. " +
                 "Leave off when a blend tree is already driven by Speed / MoveX / MoveZ.")]
        [SerializeField] private bool useStateCrossFade;
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string walkStateName = "Walk";
        [SerializeField] private string sprintStateName = "Sprint";
        [Tooltip("Length of the state cross-fade in seconds.")]
        [SerializeField] private float crossFadeDuration = 0.15f;

        // Animator parameter hashes + whether the controller actually exposes them.
        private int _speedHash, _moveXHash, _moveZHash, _isMovingHash, _isSprintingHash;
        private int _movingForwardHash, _movingBackwardHash, _movingRightHash, _movingLeftHash;
        private bool _hasSpeed, _hasMoveX, _hasMoveZ, _hasIsMoving, _hasIsSprinting;
        private bool _hasMovingForward, _hasMovingBackward, _hasMovingRight, _hasMovingLeft;

        private Vector2 _localMove;     // smoothed: x = right, y = forward
        private float _normalizedSpeed; // smoothed: 0 = idle, 1 = full sprint
        private PlayerAnimationState _state = PlayerAnimationState.Idle;

        /// <summary>Movement state the animator is currently driven with.</summary>
        public PlayerAnimationState State => _state;

        /// <summary>Smoothed character-local movement: x = right, y = forward (-1..1).</summary>
        public Vector2 LocalMove => _localMove;

        /// <summary>Smoothed normalised speed: 0 = idle, 1 = full sprint.</summary>
        public float NormalizedSpeed => _normalizedSpeed;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (moveController == null)
            {
                moveController = GetComponent<MoveController>();
                if (moveController == null)
                    moveController = GetComponentInParent<MoveController>();
            }

            if (moveController == null)
                Debug.LogWarning(LogPrefix + "MoveController not found - the animator will stay idle.", this);
        }

        private void Start()
        {
            // Cached in Start, not Awake: the Animator's parameter list is only guaranteed to
            // be available once the component has been initialised.
            CacheParameters();

            if (!useStateCrossFade || animator == null)
                return;

            // Snap to the state matching the current movement instead of waiting for the first
            // change: the animator's default state may be different (e.g. a T-pose state).
            int stateHash = Animator.StringToHash(GetStateName(_state));
            if (animator.HasState(0, stateHash))
                animator.Play(stateHash, 0, 0f);
        }

        private void Update()
        {
            Vector2 targetMove = moveController != null ? ReadLocalMove() : Vector2.zero;
            float targetSpeed = moveController != null ? ReadNormalizedSpeed() : 0f;

            // Exponential smoothing (frame-rate independent), so start/stop and the
            // walk -> sprint change read as acceleration instead of a snap.
            float blend = smoothingTime <= 0f
                ? 1f
                : 1f - Mathf.Exp(-Time.deltaTime / smoothingTime);

            _localMove = Vector2.Lerp(_localMove, targetMove, blend);
            _normalizedSpeed = Mathf.Lerp(_normalizedSpeed, targetSpeed, blend);

            ApplyParameters();
            UpdateState();
        }

        /// <summary>
        /// Movement input expressed in the character's local space, so the animator can tell
        /// forward/backward from right/left no matter where the character is facing.
        /// </summary>
        private Vector2 ReadLocalMove()
        {
            Vector2 input = moveController.MoveInput;
            if (input.sqrMagnitude < 0.0001f)
                return Vector2.zero;

            Vector3 local = transform.InverseTransformDirection(new Vector3(input.x, 0f, input.y));
            local.y = 0f;

            if (local.sqrMagnitude > 1f)
                local.Normalize();

            return new Vector2(local.x, local.z);
        }

        /// <summary>Current horizontal speed as 0..1, where 1 is full sprint.</summary>
        private float ReadNormalizedSpeed()
        {
            float maxSpeed = moveController.MaxSpeed;
            return maxSpeed > 0.0001f ? Mathf.Clamp01(moveController.CurrentSpeed / maxSpeed) : 0f;
        }

        private void ApplyParameters()
        {
            if (animator == null)
                return;

            if (_hasSpeed)
                animator.SetFloat(_speedHash, _normalizedSpeed);
            if (_hasMoveX)
                animator.SetFloat(_moveXHash, _localMove.x);
            if (_hasMoveZ)
                animator.SetFloat(_moveZHash, _localMove.y);

            bool isMoving = moveController != null && moveController.IsMoving;

            if (_hasIsMoving)
                animator.SetBool(_isMovingHash, isMoving);
            if (_hasIsSprinting)
                animator.SetBool(_isSprintingHash, isMoving && moveController.IsSprinting);

            // Direction flags are evaluated on the smoothed vector, so a single-frame tap does
            // not flicker them and a diagonal press sets two flags at once.
            if (_hasMovingForward)
                animator.SetBool(_movingForwardHash, isMoving && _localMove.y >= directionThreshold);
            if (_hasMovingBackward)
                animator.SetBool(_movingBackwardHash, isMoving && _localMove.y <= -directionThreshold);
            if (_hasMovingRight)
                animator.SetBool(_movingRightHash, isMoving && _localMove.x >= directionThreshold);
            if (_hasMovingLeft)
                animator.SetBool(_movingLeftHash, isMoving && _localMove.x <= -directionThreshold);
        }

        /// <summary>Raises the idle -> walk -> sprint transition when the movement state changes.</summary>
        private void UpdateState()
        {
            PlayerAnimationState next = ResolveState();
            if (next == _state)
                return;

            _state = next;

            if (!useStateCrossFade || animator == null)
                return;

            int stateHash = Animator.StringToHash(GetStateName(next));
            if (!animator.HasState(0, stateHash))
                return;

            // Fixed-time cross-fade: walk -> sprint always takes the same time, so the
            // acceleration has a predictable duration.
            animator.CrossFadeInFixedTime(stateHash, crossFadeDuration);
        }

        private PlayerAnimationState ResolveState()
        {
            if (moveController == null || !moveController.IsMoving)
                return PlayerAnimationState.Idle;

            return moveController.IsSprinting ? PlayerAnimationState.Sprint : PlayerAnimationState.Walk;
        }

        private string GetStateName(PlayerAnimationState state) => state switch
        {
            PlayerAnimationState.Walk => walkStateName,
            PlayerAnimationState.Sprint => sprintStateName,
            _ => idleStateName
        };

        /// <summary>
        /// Picks up the animator's parameter hashes once, at Start. Missing parameters are just
        /// disabled instead of being reported every frame by the Animator.
        /// </summary>
        private void CacheParameters()
        {
            if (animator == null)
            {
                Debug.LogWarning(LogPrefix + "no Animator found - animations are not driven.", this);
                return;
            }

            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning(LogPrefix + "the Animator has no controller assigned.", this);
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;

            _hasSpeed = TryGetParameter(parameters, speedParameter, AnimatorControllerParameterType.Float, out _speedHash);
            _hasMoveX = TryGetParameter(parameters, moveXParameter, AnimatorControllerParameterType.Float, out _moveXHash);
            _hasMoveZ = TryGetParameter(parameters, moveZParameter, AnimatorControllerParameterType.Float, out _moveZHash);
            _hasIsMoving = TryGetParameter(parameters, isMovingParameter, AnimatorControllerParameterType.Bool, out _isMovingHash);
            _hasIsSprinting = TryGetParameter(parameters, isSprintingParameter, AnimatorControllerParameterType.Bool, out _isSprintingHash);

            _hasMovingForward = TryGetParameter(parameters, movingForwardParameter, AnimatorControllerParameterType.Bool, out _movingForwardHash);
            _hasMovingBackward = TryGetParameter(parameters, movingBackwardParameter, AnimatorControllerParameterType.Bool, out _movingBackwardHash);
            _hasMovingRight = TryGetParameter(parameters, movingRightParameter, AnimatorControllerParameterType.Bool, out _movingRightHash);
            _hasMovingLeft = TryGetParameter(parameters, movingLeftParameter, AnimatorControllerParameterType.Bool, out _movingLeftHash);

            if (!_hasSpeed && !_hasMoveX && !_hasMoveZ && !_hasIsMoving && !_hasIsSprinting)
                Debug.LogWarning(LogPrefix + "none of the animator parameters were found - check the names in the Inspector.", this);
        }

        private static bool TryGetParameter(AnimatorControllerParameter[] parameters, string name,
            AnimatorControllerParameterType type, out int hash)
        {
            hash = 0;

            if (string.IsNullOrWhiteSpace(name))
                return false;

            foreach (AnimatorControllerParameter parameter in parameters)
            {
                if (parameter.type == type && parameter.name == name)
                {
                    hash = parameter.nameHash;
                    return true;
                }
            }

            return false;
        }
    }
}
