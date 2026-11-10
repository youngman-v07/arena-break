using System.Collections;
using ArenaBreak.Core;
using UnityEngine;
using UnityEngine.AI;

namespace ArenaBreak.Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        private enum State
        {
            Idle,
            Chase,
            Attack,
            Dead
        }

        [Header("References")]
        // 프리팹은 씬 오브젝트를 참조할 수 없으므로 비워 두고, 스폰한 쪽이 SetPlayer로 넣는다
        [SerializeField] private Transform _player;

        [Header("Range")]
        // 아레나 대각선(약 57m)보다 커야 먼 구석에 스폰된 적도 움직인다
        [SerializeField] private float _chaseRange = 60f;
        // NavMeshAgent의 Stopping Distance보다 커야 멈춘 자리에서 공격이 닿는다
        [SerializeField] private float _attackRange = 2f;

        [Header("Attack")]
        [SerializeField] private float _attackInterval = 2f;
        [SerializeField] private int _damage = 10;

        [Header("Death")]
        [SerializeField] private float _destroyDelay = 1f;

        private NavMeshAgent _agent;
        private Health _health;
        private IDamageable _playerDamageable;
        private WaitForSeconds _destroyWait;

        private State _state = State.Idle;
        private float _nextAttackTime;

        // 풀이 구독하면 파괴 대신 반납하도록 사망 연출이 끝났음을 알린다
        public event System.Action<EnemyAI> DeathFinished;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _destroyWait = new WaitForSeconds(_destroyDelay);

            CachePlayerDamageable();
        }

        private void OnEnable()
        {
            _health.Died += HandleDied;

            // 풀에서 재사용될 때 이전 생애의 Dead 상태와 공격 쿨다운이 남지 않게 한다
            _state = State.Idle;
            _nextAttackTime = 0f;

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
            CachePlayerDamageable();
        }

        private void Update()
        {
            // NavMesh 밖에서는 agent 호출이 에러를 내므로 배치될 때까지 기다린다
            if (_state == State.Dead || _player == null || !_agent.isOnNavMesh)
            {
                return;
            }

            float sqrDistance = (_player.position - transform.position).sqrMagnitude;

            switch (_state)
            {
                case State.Idle:
                    if (sqrDistance <= _chaseRange * _chaseRange)
                    {
                        _state = State.Chase;
                    }
                    break;

                case State.Chase:
                    if (sqrDistance <= _attackRange * _attackRange)
                    {
                        _state = State.Attack;
                        _agent.isStopped = true;
                        break;
                    }

                    _agent.SetDestination(_player.position);
                    break;

                case State.Attack:
                    if (sqrDistance > _attackRange * _attackRange)
                    {
                        _state = State.Chase;
                        _agent.isStopped = false;
                        break;
                    }

                    TryAttack();
                    break;
            }
        }

        private void TryAttack()
        {
            if (Time.time < _nextAttackTime)
            {
                return;
            }

            _nextAttackTime = Time.time + _attackInterval;

            // 플레이어에게 Health가 생기기 전(3장 이전)에는 로그로만 공격을 확인한다
            _playerDamageable?.TakeDamage(_damage);

            // TODO(3장): 플레이어 Health가 붙으면 삭제
            Debug.LogFormat("[Enemy] {0} attacks (damage {1})", name, _damage);
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
            yield return _destroyWait;

            // 씬에 직접 둔 적처럼 풀에 속하지 않으면 구독자가 없으므로 기존대로 파괴한다
            if (DeathFinished != null)
            {
                DeathFinished.Invoke(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void CachePlayerDamageable()
        {
            _playerDamageable = null;

            if (_player != null)
            {
                _player.TryGetComponent(out _playerDamageable);
            }
        }
    }
}
