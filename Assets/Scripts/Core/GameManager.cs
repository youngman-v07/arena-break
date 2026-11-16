using System;
using System.Collections;
using ArenaBreak.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ArenaBreak.Core
{
    public enum GameState
    {
        Ready,
        Playing,
        GameOver,
        Cleared
    }

    // 같은 오브젝트에 두 개가 붙으면 한쪽은 참조가 비어 OnEnable에서 NullReferenceException을 낸다
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] private float _readyDuration = 3f;

        [Header("References")]
        [SerializeField] private Health _playerHealth;
        [SerializeField] private WaveSpawner _waveSpawner;

        // timeScale = 0은 Update를 멈추지 않으므로, 종료 상태에서는 조작 컴포넌트를 직접 꺼야 한다
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private WeaponSystem _weaponSystem;

        [Header("Input")]
        // InputSystem_Actions 에셋에 없는 액션이라 코드에서 직접 만든다
        [SerializeField] private string _restartBinding = "<Keyboard>/r";
        [SerializeField] private string _quitBinding = "<Keyboard>/escape";

        private InputAction _restartAction;
        private InputAction _quitAction;
        private WaitForSeconds _readyWait;
        private Coroutine _readyRoutine;
        private bool _hasReferences;

        public GameState CurrentState { get; private set; } = GameState.Ready;

        public event Action<GameState> StateChanged;

        private void Awake()
        {
            _restartAction = new InputAction("Restart", InputActionType.Button, _restartBinding);
            _quitAction = new InputAction("Quit", InputActionType.Button, _quitBinding);
            _readyWait = new WaitForSeconds(_readyDuration);

            // 칸이 비어 있으면 이후 모든 콜백이 NullReferenceException을 내므로, 원인을 알리고 스스로 꺼서 연쇄 에러를 막는다
            _hasReferences = _playerHealth != null && _waveSpawner != null && _playerController != null && _weaponSystem != null;
            if (!_hasReferences)
            {
                Debug.LogError("[GameManager] 참조 칸(Player Health / Wave Spawner / Player Controller / Weapon System)이 비어 있어 비활성화한다", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _playerHealth.Died += HandlePlayerDied;
            _waveSpawner.AllWavesCleared += HandleAllWavesCleared;

            _restartAction.Enable();
            _quitAction.Enable();
        }

        private void OnDisable()
        {
            // Awake에서 스스로 꺼진 경우 구독한 적이 없다
            if (!_hasReferences)
            {
                return;
            }

            _playerHealth.Died -= HandlePlayerDied;
            _waveSpawner.AllWavesCleared -= HandleAllWavesCleared;

            _restartAction.Disable();
            _quitAction.Disable();
        }

        private void OnDestroy()
        {
            _restartAction.Dispose();
            _quitAction.Dispose();
        }

        private void Start()
        {
            _readyRoutine = StartCoroutine(ReadyRoutine());
        }

        private void Update()
        {
            if (_quitAction.WasPressedThisFrame())
            {
                Application.Quit();
            }

            bool isEnded = CurrentState == GameState.GameOver || CurrentState == GameState.Cleared;
            if (isEnded && _restartAction.WasPressedThisFrame())
            {
                Restart();
            }
        }

        private IEnumerator ReadyRoutine()
        {
            yield return _readyWait;

            _readyRoutine = null;
            SetState(GameState.Playing);
        }

        private void HandlePlayerDied()
        {
            SetState(GameState.GameOver);
        }

        private void HandleAllWavesCleared()
        {
            SetState(GameState.Cleared);
        }

        private void SetState(GameState next)
        {
            // 사망과 전체 클리어가 같은 프레임에 겹쳐도 먼저 정해진 결과를 뒤집지 않는다
            if (CurrentState == next || CurrentState == GameState.GameOver || CurrentState == GameState.Cleared)
            {
                return;
            }

            CurrentState = next;

            switch (next)
            {
                case GameState.Ready:
                case GameState.Playing:
                    break;

                case GameState.GameOver:
                case GameState.Cleared:
                    EnterEndState();
                    break;
            }

            StateChanged?.Invoke(next);
        }

        private void EnterEndState()
        {
            // 사망으로 먼저 끝났는데 대기 코루틴이 남아 있으면 Playing으로 되돌려 버린다
            if (_readyRoutine != null)
            {
                StopCoroutine(_readyRoutine);
                _readyRoutine = null;
            }

            _playerController.enabled = false;
            _weaponSystem.enabled = false;

            // PlayerController.OnDisable도 커서를 풀지만, _lockCursor가 꺼진 경우에는 풀지 않으므로 직접 보장한다
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Time.timeScale = 0f;
        }

        private void Restart()
        {
            // 0인 채로 로드하면 다음 씬도 멈춘 상태로 시작한다
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
