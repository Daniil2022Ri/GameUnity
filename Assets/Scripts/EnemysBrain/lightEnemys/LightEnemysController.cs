using Combat;
using UnityEngine;

namespace EnemysBrain.lightEnemys
{
    /// <summary>
    /// Drives a "light" enemy: chases the player and attacks when within attack range.
    /// Kept allocation-free and cache-based: the player transform and its damageable
    /// target are resolved once (retried on a timer only while missing), and all combat
    /// values come from <see cref="EnemyLightSettings"/>.
    /// </summary>
    [RequireComponent(typeof(EnemyLightSettings))]
    public class LightEnemysController : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Player transform. Auto-found by tag when left empty.")]
        [SerializeField] private Transform player;
        [SerializeField] private string playerTag = "Player";

        [Header("Behavior")]
        [SerializeField] private bool rotateToPlayer = true;
        [SerializeField] private float rotationSpeed = 10f;
        [Tooltip("Seconds between re-attempts to locate the player while it is missing.")]
        [SerializeField] private float targetAcquireRetryTime = 0.5f;

        private EnemyLightSettings _stats;
        private IDamageable _target;
        private float _attackTimer;
        private float _acquireTimer;

        private void Awake()
        {
            _stats = GetComponent<EnemyLightSettings>();
        }

        private void Start()
        {
            TryAcquireTarget();
        }

        private void Update()
        {
            if (_stats.IsDead)
                return;

            if (_target == null)
            {
                // Throttle the search so we don't scan the scene every frame.
                _acquireTimer -= Time.deltaTime;
                if (_acquireTimer > 0f || !TryAcquireTarget())
                    return;
            }
            else if (_target.IsDead)
            {
                // No point chasing a corpse; wait for a new target.
                return;
            }

            // Flat (ground-plane) vector toward the player.
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float sqrDistance = toPlayer.sqrMagnitude;

            float attackRange = _stats.AttackRange;
            if (sqrDistance <= attackRange * attackRange)
            {
                if (rotateToPlayer && sqrDistance > 0.0001f)
                    RotateTowards(toPlayer);

                TryAttack();
                return;
            }

            // Chase: only compute a square root when actually moving.
            float distance = Mathf.Sqrt(sqrDistance);
            Vector3 step = toPlayer / distance * (_stats.MoveSpeed * Time.deltaTime);
            transform.position += step;

            if (rotateToPlayer)
                RotateTowards(toPlayer);
        }

        private void TryAttack()
        {
            if (_target == null || _target.IsDead)
                return;

            _attackTimer -= Time.deltaTime;
            if (_attackTimer > 0f)
                return;

            _target.TakeDamage(_stats.AttackDamage);
            _attackTimer = _stats.AttackInterval;
        }

        private void RotateTowards(Vector3 flatDirection)
        {
            Quaternion targetRotation = Quaternion.LookRotation(flatDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                rotationSpeed * Time.deltaTime);
        }

        /// <summary>Resolves the player transform and its damageable target, caching both.</summary>
        private bool TryAcquireTarget()
        {
            _acquireTimer = targetAcquireRetryTime;

            if (player == null)
            {
                GameObject found = GameObject.FindGameObjectWithTag(playerTag);
                if (found == null)
                    return false;

                player = found.transform;
            }

            _target ??= player.GetComponentInChildren<IDamageable>();

            return _target != null;
        }
    }
}
