using ArenaBreak.Core;
using UnityEngine;

namespace ArenaBreak.Enemy
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float _speed = 12f;
        [SerializeField] private float _lifetime = 5f;
        [SerializeField] private float _radius = 0.15f;

        private Vector3 _direction;
        private Transform _target;
        private IDamageable _targetDamageable;
        private int _damage;

        // 발사한 쪽이 겨냥한 대상을 넘겨준다. 레이어로 아군을 가르면 적과 플레이어가 같은 레이어라 구분이 안 된다
        public void Launch(Vector3 direction, Transform target, int damage)
        {
            _direction = direction;
            _target = target;
            _damage = damage;
            target.TryGetComponent(out _targetDamageable);

            Destroy(gameObject, _lifetime);
        }

        private void Update()
        {
            float step = _speed * Time.deltaTime;

            // 프레임당 이동량이 크면 얇은 콜라이더를 통과하므로 이동 구간을 먼저 훑는다
            if (Physics.SphereCast(transform.position, _radius, _direction, out RaycastHit hit, step, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                HandleHit(hit);
                return;
            }

            transform.position += _direction * step;
        }

        private void HandleHit(RaycastHit hit)
        {
            // 겨냥한 대상만 다치게 한다. 벽이나 다른 적에 맞으면 데미지 없이 사라진다
            if (hit.collider.transform.IsChildOf(_target))
            {
                _targetDamageable?.TakeDamage(_damage);
            }

            Destroy(gameObject);
        }
    }
}
