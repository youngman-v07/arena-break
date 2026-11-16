using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaBreak.Player
{
    public enum SkillState
    {
        Ready,
        Active,
        Cooldown
    }

    [DisallowMultipleComponent]
    public class RapidFireSkill : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WeaponSystem _weaponSystem;

        [Header("Skill")]
        [SerializeField] private float _duration = 5f;
        // 스킬이 끝난 뒤부터 센다
        [SerializeField] private float _cooldown = 10f;

        [Header("Input")]
        // InputSystem_Actions 에셋에 없는 액션이라 코드에서 직접 만든다
        [SerializeField] private string _activateBinding = "<Keyboard>/e";

        [Header("Notify")]
        // 남은 시간을 매 프레임 알리면 HUD가 매번 문자열을 만들므로 표시 단위(0.1초)로 줄인다
        [SerializeField] private float _tickInterval = 0.1f;

        private InputAction _activateAction;
        private WaitForSeconds _tickWait;

        public SkillState CurrentState { get; private set; } = SkillState.Ready;

        public event Action<SkillState> StateChanged;
        public event Action<float> RemainingTimeChanged;

        private void Awake()
        {
            _activateAction = new InputAction("RapidFire", InputActionType.Button, _activateBinding);
            _tickWait = new WaitForSeconds(_tickInterval);

            // 무기가 없으면 Update와 OnDisable이 NullReferenceException을 내므로, 원인을 알리고 스스로 끈다
            if (_weaponSystem == null)
            {
                Debug.LogError("[RapidFireSkill] Weapon System 칸이 비어 있어 비활성화한다", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _activateAction.Enable();
        }

        private void OnDisable()
        {
            _activateAction.Disable();

            // Awake에서 스스로 꺼진 경우에는 되돌릴 무기가 없다
            if (_weaponSystem == null)
            {
                return;
            }

            // 코루틴이 도중에 멈추면 무한 탄약이 켜진 채 남으므로 반드시 되돌린다
            _weaponSystem.InfiniteAmmo = false;
            CurrentState = SkillState.Ready;
        }

        private void OnDestroy()
        {
            _activateAction.Dispose();
        }

        // HUD는 OnEnable에서 구독하므로, 초기 상태는 모든 OnEnable이 끝난 Start에서 알려야 놓치지 않는다
        private void Start()
        {
            StateChanged?.Invoke(CurrentState);
        }

        private void Update()
        {
            // GameManager가 종료 상태에서 WeaponSystem을 끄므로, 꺼져 있으면 스킬도 시작하지 않는다
            if (CurrentState == SkillState.Ready && _weaponSystem.isActiveAndEnabled && _activateAction.WasPressedThisFrame())
            {
                StartCoroutine(SkillRoutine());
            }
        }

        private IEnumerator SkillRoutine()
        {
            SetState(SkillState.Active);
            _weaponSystem.InfiniteAmmo = true;
            yield return CountDown(_duration);

            _weaponSystem.InfiniteAmmo = false;
            SetState(SkillState.Cooldown);
            yield return CountDown(_cooldown);

            SetState(SkillState.Ready);
        }

        private IEnumerator CountDown(float duration)
        {
            float endTime = Time.time + duration;

            while (Time.time < endTime)
            {
                RemainingTimeChanged?.Invoke(endTime - Time.time);
                yield return _tickWait;
            }

            RemainingTimeChanged?.Invoke(0f);
        }

        private void SetState(SkillState next)
        {
            CurrentState = next;
            StateChanged?.Invoke(next);
        }
    }
}
