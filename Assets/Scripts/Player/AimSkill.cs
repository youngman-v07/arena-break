using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaBreak.Player
{
    [DisallowMultipleComponent]
    public class AimSkill : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;
        // 십자 조준점을 담은 오브젝트. 비워 두면 확대만 동작한다
        [SerializeField] private CanvasGroup _crosshair;

        [Header("Aim")]
        [SerializeField] private float _zoomMultiplier = 1.5f;
        // 확대·복귀 모두 이 시간 안에 끝난다
        [SerializeField] private float _transitionTime = 0.5f;

        [Header("Input")]
        [SerializeField] private string _aimBinding = "<Mouse>/rightButton";

        private InputAction _aimAction;
        private float _baseFieldOfView;
        private float _aimedFieldOfView;
        private float _weight;

        private void Awake()
        {
            _aimAction = new InputAction("Aim", InputActionType.Button, _aimBinding);

            // 확대 배율은 화면 크기 배율이므로 FOV를 그대로 나누지 않고 탄젠트로 환산한다
            _baseFieldOfView = _camera.fieldOfView;
            float halfRadians = _baseFieldOfView * 0.5f * Mathf.Deg2Rad;
            _aimedFieldOfView = 2f * Mathf.Atan(Mathf.Tan(halfRadians) / _zoomMultiplier) * Mathf.Rad2Deg;
        }

        private void OnEnable()
        {
            _aimAction.Enable();
        }

        private void OnDisable()
        {
            _aimAction.Disable();

            // 조준 도중 꺼지면 확대된 채 남으므로 원래 상태로 되돌린다
            _weight = 0f;
            Apply();
        }

        private void OnDestroy()
        {
            _aimAction.Dispose();
        }

        private void Update()
        {
            float target = _aimAction.IsPressed() ? 1f : 0f;

            if (Mathf.Approximately(_weight, target))
            {
                return;
            }

            _weight = Mathf.MoveTowards(_weight, target, Time.deltaTime / _transitionTime);
            Apply();
        }

        private void Apply()
        {
            // 씬 종료 시 카메라가 먼저 파괴된 채 OnDisable이 불릴 수 있다
            if (_camera == null)
            {
                return;
            }

            float eased = Mathf.SmoothStep(0f, 1f, _weight);
            _camera.fieldOfView = Mathf.Lerp(_baseFieldOfView, _aimedFieldOfView, eased);

            if (_crosshair != null)
            {
                _crosshair.alpha = eased;
            }
        }
    }
}
