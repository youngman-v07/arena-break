using System.Collections;
using ArenaBreak.Core;
using UnityEngine;

namespace ArenaBreak.Enemy
{
    [RequireComponent(typeof(Health))]
    public class HitFlash : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private Color _flashColor = Color.white;
        [SerializeField] private float _flashDuration = 0.05f;

        private Health _health;
        private MaterialPropertyBlock _propertyBlock;
        private WaitForSeconds _flashWait;
        private Coroutine _flashRoutine;

        private void Awake()
        {
            _health = GetComponent<Health>();
            // renderer.material은 적마다 머티리얼을 복제해 누수되므로 PropertyBlock으로 색만 덮어쓴다
            _propertyBlock = new MaterialPropertyBlock();
            _flashWait = new WaitForSeconds(_flashDuration);
        }

        private void OnEnable()
        {
            _health.HealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            _health.HealthChanged -= HandleHealthChanged;

            // 코루틴은 비활성화로 멈추지만 PropertyBlock은 남으므로, 풀에서 다시 꺼낼 때 흰색으로 시작하지 않게 되돌린다
            _flashRoutine = null;
            _renderer.SetPropertyBlock(null);
        }

        private void HandleHealthChanged(int current, int max)
        {
            // 연속 피격 시 앞선 복구가 뒤 플래시를 일찍 끊지 않도록 타이머를 다시 시작한다
            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
            }

            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            _propertyBlock.SetColor(BaseColorId, _flashColor);
            _renderer.SetPropertyBlock(_propertyBlock);

            yield return _flashWait;

            _renderer.SetPropertyBlock(null);
            _flashRoutine = null;
        }
    }
}
