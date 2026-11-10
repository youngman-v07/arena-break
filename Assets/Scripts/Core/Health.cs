using System;
using UnityEngine;

namespace ArenaBreak.Core
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private int _maxHealth = 100;

        // 프로퍼티는 인스펙터에 안 보이므로, 플레이 중 눈으로 검증하려고 필드를 직렬화한다
        [SerializeField] private int _currentHealth;

        private bool _isDead;

        public int MaxHealth => _maxHealth;
        public int CurrentHealth => _currentHealth;
        public bool IsDead => _isDead;

        public event Action<int, int> HealthChanged;
        public event Action Died;

        private void Awake()
        {
            _currentHealth = _maxHealth;
        }

        public void TakeDamage(int amount)
        {
            // 사망 이벤트가 여러 번 나가면 웨이브 카운트가 중복으로 줄어든다
            if (_isDead || amount <= 0)
            {
                return;
            }

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
