using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaBreak.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class DashSkill : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController _playerController;

        [Header("Dash")]
        [SerializeField] private float _distance = 6f;
        [SerializeField] private float _duration = 0.15f;
        // 대쉬 시작부터 센다. 이 시간이 지나기 전에는 다시 발동하지 않는다
        [SerializeField] private float _cooldown = 3f;

        [Header("Input")]
        // InputSystem_Actions의 Sprint 액션을 쓰면 다른 기능과 겹치므로 코드에서 직접 만든다
        [SerializeField] private string _activateBinding = "<Keyboard>/leftShift";

        private CharacterController _characterController;
        private InputAction _activateAction;
        private bool _isBusy;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _activateAction = new InputAction("Dash", InputActionType.Button, _activateBinding);
        }

        private void OnEnable()
        {
            _activateAction.Enable();
        }

        private void OnDisable()
        {
            _activateAction.Disable();
            // 코루틴이 도중에 멈추면 _isBusy가 true로 남아 다시 켜도 영영 발동하지 못한다
            _isBusy = false;
        }

        private void OnDestroy()
        {
            _activateAction.Dispose();
        }

        private void Update()
        {
            // GameManager가 종료 상태에서 PlayerController를 끄므로, 꺼져 있으면 대쉬도 하지 않는다
            if (!_isBusy && _playerController.isActiveAndEnabled && _activateAction.WasPressedThisFrame())
            {
                StartCoroutine(DashRoutine());
            }
        }

        private IEnumerator DashRoutine()
        {
            _isBusy = true;

            float startTime = Time.time;
            Vector3 velocity = transform.forward * (_distance / _duration);

            // 벽에 막히면 CharacterController가 이동량을 깎아 주므로 따로 충돌을 검사하지 않는다
            while (Time.time - startTime < _duration)
            {
                _characterController.Move(velocity * Time.deltaTime);
                yield return null;
            }

            yield return new WaitForSeconds(Mathf.Max(0f, _cooldown - _duration));

            _isBusy = false;
        }
    }
}
