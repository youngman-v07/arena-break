using System.Collections;
using ArenaBreak.Core;
using UnityEngine;
using UnityEngine.AI;

namespace ArenaBreak.Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class RangedEnemyAI : MonoBehaviour
    {
        private enum State
        {
            Idle,
            Chase,
            Shoot,
            Retreat,
            Dead
        }

        [Header("References")]
        // 프리팹은 씬 오브젝트를 참조할 수 없으므로 비워 두고, 스폰한 쪽이 SetPlayer로 넣는다
        [SerializeField] private Transform _player;

        [Header("Range")]
        // 아레나 대각선(약 57m)보다 커야 먼 구석에 스폰된 적도 움직인다
        [SerializeField] private float _aggroRange = 60f;
        // 이 거리 안이면 멈춰서 쏜다
        [SerializeField] private float _shootDistance = 12f;
        // 사격 거리보다 작아야 "물러나지도 접근하지도 않고 쏘는" 구간이 생긴다
        [SerializeField] private float _retreatDistance = 6f;

        [Header("Retreat")]
        [SerializeField] private float _retreatStep = 6f;
        // 이동 목표가 NavMesh 밖으로 떨어질 때 가까운 NavMesh 지점을 찾는 반경
        [SerializeField] private float _retreatSampleRadius = 2f;

        [Header("Fire")]
        [SerializeField] private Projectile _projectilePrefab;
        [SerializeField] private float _fireInterval = 2f;
        [SerializeField] private int _damage = 10;
        // 자기 콜라이더 안에서 투사체가 시작하지 않도록 몸 앞쪽으로 띄운다 (로컬 좌표)
        [SerializeField] private Vector3 _muzzleOffset = new Vector3(0f, 0f, 1f);
        [SerializeField] private float _turnSpeed = 360f;

        [Header("Death")]
        [SerializeField] private float _deathDelay = 1f;

        private NavMeshAgent _agent;
        private Health _health;
        private WaitForSeconds _deathWait;

        private State _state = State.Idle;
        private float _nextFireTime;

        // 풀이 구독하면 파괴 대신 반납하도록 사망 연출이 끝났음을 알린다
        public event System.Action<RangedEnemyAI> DeathFinished;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _deathWait = new WaitForSeconds(_deathDelay);
        }

        private void OnEnable()
        {
            _health.Died += HandleDied;

            // 풀에서 재사용될 때 이전 생애의 Dead 상태와 발사 쿨다운이 남지 않게 한다
            _state = State.Idle;
            _nextFireTime = 0f;

            // NavMesh에 올라가기 전에는 agent 호출이 에러를 낸다
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = false;
            }
        }

        private void OnDisable()
        {
            _health.Died -= HandleDied;
        }

        // 스폰한 쪽(WaveSpawner)이 프리팹 인스턴스에 플레이어를 넘겨주는 진입점
        public void SetPlayer(Transform player)
        {
            _player = player;
        }

        private void Update()
        {
            // NavMesh 밖에서는 agent 호출이 에러를 내므로 배치될 때까지 기다린다
            if (_state == State.Dead || _player == null || !_agent.isOnNavMesh)
            {
                return;
            }

            float sqrDistance = (_player.position - transform.position).sqrMagnitude;

            if (_state == State.Idle)
            {
                if (sqrDistance <= _aggroRange * _aggroRange)
                {
                    _state = State.Chase;
                }
                return;
            }

            _state = ResolveState(sqrDistance);

            switch (_state)
            {
                case State.Chase:
                    Chase();
                    break;

                case State.Shoot:
                    Shoot();
                    break;

                case State.Retreat:
                    Retreat();
                    break;
            }
        }

        private State ResolveState(float sqrDistance)
        {
            if (sqrDistance < _retreatDistance * _retreatDistance)
            {
                return State.Retreat;
            }

            if (sqrDistance <= _shootDistance * _shootDistance)
            {
                return State.Shoot;
            }

            return State.Chase;
        }

        private void Chase()
        {
            _agent.isStopped = false;
            _agent.SetDestination(_player.position);
        }

        private void Shoot()
        {
            _agent.isStopped = true;
            FacePlayer();
            TryFire();
        }

        private void Retreat()
        {
            if (!TryFindRetreatPoint(out Vector3 point))
            {
                // 구석에 몰려 물러날 곳이 없으면 도망 대신 맞서 쏜다
                Shoot();
                return;
            }

            _agent.isStopped = false;
            _agent.SetDestination(point);
        }

        private bool TryFindRetreatPoint(out Vector3 point)
        {
            Vector3 away = transform.position - _player.position;
            away.y = 0f;

            Vector3 target = transform.position + away.normalized * _retreatStep;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, _retreatSampleRadius, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }

            point = default;
            return false;
        }

        private void FacePlayer()
        {
            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;

            // 영벡터로 LookRotation을 부르면 경고가 나온다
            if (toPlayer.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            Quaternion look = Quaternion.LookRotation(toPlayer);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, _turnSpeed * Time.deltaTime);
        }

        private void TryFire()
        {
            if (Time.time < _nextFireTime)
            {
                return;
            }

            _nextFireTime = Time.time + _fireInterval;

            Vector3 origin = transform.TransformPoint(_muzzleOffset);
            Vector3 direction = (_player.position - origin).normalized;

            // 풀링 대상: 연사하면 생성·파괴가 반복된다
            Projectile projectile = Instantiate(_projectilePrefab, origin, Quaternion.LookRotation(direction));
            projectile.Launch(direction, _player, _damage);
        }

        private void HandleDied()
        {
            _state = State.Dead;

            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = true;
            }

            StartCoroutine(FinishDeathRoutine());
        }

        private IEnumerator FinishDeathRoutine()
        {
            yield return _deathWait;

            // 씬에 직접 둔 적처럼 풀에 속하지 않으면 구독자가 없으므로 파괴한다
            if (DeathFinished != null)
            {
                DeathFinished.Invoke(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
