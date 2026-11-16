using ArenaBreak.Core;
using ArenaBreak.Player;
using UnityEngine;
using UnityEngine.UI;

namespace ArenaBreak.UI
{
    [DisallowMultipleComponent]
    public class HUDController : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private Health _playerHealth;
        [SerializeField] private WeaponSystem _weaponSystem;
        [SerializeField] private RapidFireSkill _rapidFireSkill;
        [SerializeField] private WaveSpawner _waveSpawner;
        [SerializeField] private GameManager _gameManager;

        [Header("Health")]
        // fillAmount는 스프라이트가 있어야 동작하므로 앵커 폭으로 줄인다. 앵커는 좌우 스트레치(0~1), 오프셋 0이어야 한다
        [SerializeField] private RectTransform _healthFill;

        [Header("Texts")]
        [SerializeField] private Text _ammoText;
        [SerializeField] private Text _waveText;
        [SerializeField] private Text _killText;
        [SerializeField] private string _ammoFormat = "{0} / {1}";
        [SerializeField] private string _waveFormat = "WAVE {0}";
        [SerializeField] private string _killFormat = "KILLS {0}";

        [Header("Skill")]
        [SerializeField] private Text _skillText;
        [SerializeField] private string _skillReadyMessage = "RAPID FIRE [E] READY";
        [SerializeField] private string _skillActiveFormat = "RAPID FIRE {0:0.0}s";
        [SerializeField] private string _skillCooldownFormat = "COOLDOWN {0:0.0}s";
        [SerializeField] private Color _skillReadyColor = Color.yellow;
        [SerializeField] private Color _skillActiveColor = Color.white;
        [SerializeField] private Color _skillCooldownColor = Color.gray;

        [Header("Result")]
        // 이 컴포넌트를 결과 패널 자신에 붙이면 패널을 끄는 순간 구독도 끊기므로, 패널 바깥에 둔다
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private Text _resultText;
        [SerializeField] private string _gameOverMessage = "GAME OVER";
        [SerializeField] private string _clearedMessage = "CLEARED!";

        // 남은 시간 이벤트가 어느 상태의 시간인지 알아야 문구 형식을 고른다
        private SkillState _skillState;
        private bool _hasReferences;

        private void Awake()
        {
            // 칸이 비어 있으면 이후 모든 콜백이 NullReferenceException을 내므로, 원인을 알리고 스스로 꺼서 연쇄 에러를 막는다
            _hasReferences = HasAllReferences();
            if (!_hasReferences)
            {
                Debug.LogError("[HUDController] 참조 칸 중 비어 있는 것이 있어 비활성화한다. 인스펙터의 None 칸을 확인할 것", this);
                enabled = false;
                return;
            }

            _resultPanel.SetActive(false);
        }

        private bool HasAllReferences()
        {
            return _playerHealth != null && _weaponSystem != null && _rapidFireSkill != null
                && _waveSpawner != null && _gameManager != null && _healthFill != null
                && _ammoText != null && _waveText != null && _killText != null && _skillText != null
                && _resultPanel != null && _resultText != null;
        }

        private void OnEnable()
        {
            _playerHealth.HealthChanged += HandleHealthChanged;
            _weaponSystem.AmmoChanged += HandleAmmoChanged;
            _rapidFireSkill.StateChanged += HandleSkillStateChanged;
            _rapidFireSkill.RemainingTimeChanged += HandleSkillRemainingTimeChanged;
            _waveSpawner.WaveStarted += HandleWaveStarted;
            _waveSpawner.KillCountChanged += HandleKillCountChanged;
            _gameManager.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            // Awake에서 스스로 꺼진 경우 구독한 적이 없다
            if (!_hasReferences)
            {
                return;
            }

            _playerHealth.HealthChanged -= HandleHealthChanged;
            _weaponSystem.AmmoChanged -= HandleAmmoChanged;
            _rapidFireSkill.StateChanged -= HandleSkillStateChanged;
            _rapidFireSkill.RemainingTimeChanged -= HandleSkillRemainingTimeChanged;
            _waveSpawner.WaveStarted -= HandleWaveStarted;
            _waveSpawner.KillCountChanged -= HandleKillCountChanged;
            _gameManager.StateChanged -= HandleStateChanged;
        }

        // Health는 OnEnable에서 체력을 채우고 이벤트는 내보내지 않으므로, 모든 OnEnable이 끝난 Start에서 초기값을 읽는다
        private void Start()
        {
            HandleHealthChanged(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
            HandleKillCountChanged(0);
        }

        private void HandleHealthChanged(int current, int max)
        {
            float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

            Vector2 anchorMax = _healthFill.anchorMax;
            anchorMax.x = ratio;
            _healthFill.anchorMax = anchorMax;
        }

        private void HandleAmmoChanged(int current, int max)
        {
            _ammoText.text = string.Format(_ammoFormat, current, max);
        }

        private void HandleSkillStateChanged(SkillState state)
        {
            _skillState = state;

            switch (state)
            {
                case SkillState.Ready:
                    _skillText.text = _skillReadyMessage;
                    _skillText.color = _skillReadyColor;
                    break;

                case SkillState.Active:
                    _skillText.color = _skillActiveColor;
                    break;

                case SkillState.Cooldown:
                    _skillText.color = _skillCooldownColor;
                    break;
            }
        }

        private void HandleSkillRemainingTimeChanged(float remaining)
        {
            switch (_skillState)
            {
                case SkillState.Active:
                    _skillText.text = string.Format(_skillActiveFormat, remaining);
                    break;

                case SkillState.Cooldown:
                    _skillText.text = string.Format(_skillCooldownFormat, remaining);
                    break;
            }
        }

        private void HandleWaveStarted(int waveNumber)
        {
            _waveText.text = string.Format(_waveFormat, waveNumber);
        }

        private void HandleKillCountChanged(int killCount)
        {
            _killText.text = string.Format(_killFormat, killCount);
        }

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.GameOver:
                    _resultText.text = _gameOverMessage;
                    _resultPanel.SetActive(true);
                    break;

                case GameState.Cleared:
                    _resultText.text = _clearedMessage;
                    _resultPanel.SetActive(true);
                    break;

                default:
                    _resultPanel.SetActive(false);
                    break;
            }
        }
    }
}
