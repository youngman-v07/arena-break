using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaBreak.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset _inputActions;

        [Header("References")]
        [SerializeField] private Transform _cameraTransform;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _jumpHeight = 1.2f;
        [SerializeField] private float _gravity = -20f;
        // isGrounded가 경사·계단에서 깜빡이지 않도록 접지 중에도 살짝 아래로 눌러준다
        [SerializeField] private float _groundedStickVelocity = -2f;

        [Header("Look")]
        [SerializeField] private float _mouseSensitivity = 0.1f;
        [SerializeField] private float _minPitch = -80f;
        [SerializeField] private float _maxPitch = 80f;
        // 커서가 잠기는 첫 프레임에 delta가 크게 튀어 시점이 확 돌아가는 것을 막는다
        [SerializeField] private float _maxLookDeltaPerFrame = 50f;

        [Header("Cursor")]
        [SerializeField] private bool _lockCursor = true;

        private CharacterController _characterController;
        private InputActionMap _playerMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;

        private float _pitch;
        private float _verticalVelocity;
        // 플레이 중 인스펙터에서 _lockCursor를 꺼도, 이 컴포넌트가 잠근 커서는 꺼질 때 되돌려야 한다
        private bool _didLockCursor;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();

            _playerMap = _inputActions.FindActionMap("Player", throwIfNotFound: true);
            _moveAction = _playerMap.FindAction("Move", throwIfNotFound: true);
            _lookAction = _playerMap.FindAction("Look", throwIfNotFound: true);
            _jumpAction = _playerMap.FindAction("Jump", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            _playerMap.Enable();

            if (_lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _didLockCursor = true;
            }
        }

        private void OnDisable()
        {
            _playerMap.Disable();

            if (_didLockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _didLockCursor = false;
            }
        }

        private void Update()
        {
            UpdateLook();
            UpdateMovement();
        }

        private void UpdateLook()
        {
            // Pointer delta는 이미 프레임당 이동량이므로 deltaTime을 곱하지 않는다
            Vector2 lookDelta = Vector2.ClampMagnitude(_lookAction.ReadValue<Vector2>(), _maxLookDeltaPerFrame);
            lookDelta *= _mouseSensitivity;

            transform.Rotate(Vector3.up, lookDelta.x, Space.Self);

            _pitch = Mathf.Clamp(_pitch - lookDelta.y, _minPitch, _maxPitch);
            _cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void UpdateMovement()
        {
            bool isGrounded = _characterController.isGrounded;

            if (isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = _groundedStickVelocity;
            }

            if (isGrounded && _jumpAction.WasPressedThisFrame())
            {
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            }

            _verticalVelocity += _gravity * Time.deltaTime;

            Vector2 moveInput = _moveAction.ReadValue<Vector2>();
            Vector3 horizontal = (transform.right * moveInput.x + transform.forward * moveInput.y) * _moveSpeed;
            Vector3 velocity = horizontal + Vector3.up * _verticalVelocity;

            _characterController.Move(velocity * Time.deltaTime);
        }
    }
}
