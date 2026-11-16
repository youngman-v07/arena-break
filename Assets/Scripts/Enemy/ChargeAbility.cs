using System.Collections;
using ArenaBreak.Core;
using UnityEngine;
using UnityEngine.AI;

namespace ArenaBreak.Enemy
{
    [RequireComponent(typeof(EnemyAI))]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class ChargeAbility : MonoBehaviour
    {
        [Header("Trigger")]
        // 이보다 가까우면 이미 근접 공격 범위라 돌진할 이유가 없다
        [SerializeField] private float _minTriggerDistance = 3f;
        [SerializeField] private float _maxTriggerDistance = 10f;

        [Header("Charge")]
        [SerializeField] private float _chargeSpeed = 14f;
        [SerializeField] private float _chargeDuration = 0.6f;
        // 돌진이 끝난 뒤부터 센다
        [SerializeField] private float _cooldown = 5f;

        private EnemyAI _enemyAI;
        private NavMeshAgent _agent;
        private Health _health;

        private Coroutine _chargeRoutine;
        private float _nextChargeTime;

        private void Awake()
        {
            _enemyAI = GetComponent<EnemyAI>();
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _health.Died += HandleDied;

            // 풀에서 재사용될 때 이전 생애의 쿨다운이 남지 않게 한다
            _nextChargeTime = 0f;
        }

        private void OnDisable()
        {
            _health.Died -= HandleDied;

            // 비활성화로 코루틴이 멈추면 agent가 꺼진 채 풀에 반납되어 다음 웨이브에서 움직이지 못한다
            _chargeRoutine = null;
            _agent.enabled = true;
        }

        private void Update()
        {
            Transform player = _enemyAI.Player;

            if (_chargeRoutine != null || _health.IsDead || player == null || !_agent.isOnNavMesh || Time.time < _nextChargeTime)
            {
                return;
            }

            float sqrDistance = (player.position - transform.position).sqrMagnitude;
            if (sqrDistance < _minTriggerDistance * _minTriggerDistance || sqrDistance > _maxTriggerDistance * _maxTriggerDistance)
            {
                return;
            }

            _chargeRoutine = StartCoroutine(ChargeRoutine(player));
        }

        private IEnumerator ChargeRoutine(Transform player)
        {
            Vector3 direction = player.position - transform.position;
            direction.y = 0f;
            direction.Normalize();

            // agent를 끄면 EnemyAI.Update가 isOnNavMesh 검사에서 걸려 돌진 중에는 추적·공격 판정을 멈춘다
            _agent.enabled = false;

            float endTime = Time.time + _chargeDuration;
            float sqrMinDistance = _minTriggerDistance * _minTriggerDistance;

            while (Time.time < endTime)
            {
                Vector3 current = transform.position;
                Vector3 next = current + direction * (_chargeSpeed * Time.deltaTime);

                // NavMesh 가장자리(벽·절벽)에 닿으면 멈춘다. 직접 옮기므로 충돌 처리를 대신한다
                if (NavMesh.Raycast(current, next, out NavMeshHit hit, NavMesh.AllAreas))
                {
                    break;
                }

                transform.position = next;

                if ((player.position - next).sqrMagnitude < sqrMinDistance)
                {
                    break;
                }

                yield return null;
            }

            EndCharge();
        }

        private void HandleDied()
        {
            if (_chargeRoutine == null)
            {
                return;
            }

            StopCoroutine(_chargeRoutine);
            EndCharge();

            // EnemyAI.HandleDied는 agent가 꺼져 있어 멈추지 못했으므로, 되살린 agent가 옛 목적지로 가지 않게 여기서 멈춘다
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = true;
            }
        }

        private void EndCharge()
        {
            _agent.enabled = true;
            _agent.Warp(transform.position);

            _chargeRoutine = null;
            _nextChargeTime = Time.time + _cooldown;
        }
    }
}
