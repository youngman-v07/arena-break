using System;
using UnityEngine;

namespace ArenaBreak.Core
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private int _maxHealth = 100;

        // 0이면 무적 없음. 적은 0, 플레이어만 연속 피격 방지용으로 값을 준다
        [SerializeField] private float _invincibleDuration;

        // 프로퍼티는 인스펙터에 안 보이므로, 플레이 중 눈으로 검증하려고 필드를 직렬화한다
        [SerializeField] private int _currentHealth;

        private bool _isDead;
        private float _invincibleUntil;

        public int MaxHealth => _maxHealth;
        public int CurrentHealth => _currentHealth;
        public bool IsDead => _isDead;

        public event Action<int, int> HealthChanged;
        public event Action Died;

        // 풀에서 다시 꺼낼 때 이전 사망 상태가 남지 않도록 Awake가 아니라 활성화마다 초기화한다
        private void OnEnable()
        {
            _currentHealth = _maxHealth;
            _isDead = false;
            _invincibleUntil = 0f;
        }

        public void TakeDamage(int amount)
        {
            // 사망 이벤트가 여러 번 나가면 웨이브 카운트가 중복으로 줄어든다
            if (_isDead || amount <= 0)
            {
                return;
            }

            if (Time.time < _invincibleUntil)
            {
                return;
            }

            _invincibleUntil = Time.time + _invincibleDuration;
            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            HealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_currentHealth == 0)
            {
                _isDead = true;
                Died?.Invoke();
            }
        }
    }
}
