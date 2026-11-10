using System.Collections;
using ArenaBreak.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaBreak.Player
{
    public class WeaponSystem : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset _inputActions;
        // 에셋에 Reload 액션이 없어서 코드에서 직접 만든다
        [SerializeField] private string _reloadBinding = "<Keyboard>/r";

        [Header("References")]
        [SerializeField] private Transform _cameraTransform;

        [Header("Fire")]
        [SerializeField] private float _fireInterval = 0.15f;
        [SerializeField] private float _range = 50f;
        [SerializeField] private int _damage = 10;

        [Header("Ammo")]
        [SerializeField] private int _magazineSize = 12;
        [SerializeField] private float _reloadDuration = 1.5f;

        [Header("Hit Marker (Debug)")]
        // 벽·표적과 같은 색이면 명중 지점이 보이지 않는다
        [SerializeField] private Color _hitMarkerColor = Color.yellow;
        [SerializeField] private float _hitMarkerSize = 0.1f;
        [SerializeField] private float _hitMarkerLifetime = 0.5f;

        private InputAction _attackAction;
        private InputAction _reloadAction;

        private int _currentAmmo;
        private float _nextFireTime;
        private bool _isReloading;
        private WaitForSeconds _reloadWait;
        // 마커마다 renderer.material로 복제하면 머티리얼이 누수되므로 하나를 공유한다
        private Material _hitMarkerMaterial;

        private void Awake()
        {
            InputActionMap playerMap = _inputActions.FindActionMap("Player", throwIfNotFound: true);
            _attackAction = playerMap.FindAction("Attack", throwIfNotFound: true);
            _reloadAction = new InputAction("Reload", InputActionType.Button, _reloadBinding);

            _currentAmmo = _magazineSize;
            _reloadWait = new WaitForSeconds(_reloadDuration);
        }

        private void OnEnable()
        {
            // 맵 단위로 켜고 끄면 PlayerController의 이동·시점 액션까지 영향을 받으므로 액션 단위로 다룬다
            _attackAction.Enable();
            _reloadAction.Enable();
        }

        private void OnDisable()
        {
            _attackAction.Disable();
            _reloadAction.Disable();

            // 비활성화되면 코루틴이 멈추므로, 재장전 중이었다면 상태를 풀어줘야 발사 불가로 고착되지 않는다
            _isReloading = false;
        }

        private void OnDestroy()
        {
            _reloadAction.Dispose();

            if (_hitMarkerMaterial != null)
            {
                Destroy(_hitMarkerMaterial);
            }
        }

        private void Update()
        {
            if (_reloadAction.WasPressedThisFrame())
            {
                TryReload();
            }

            if (_attackAction.IsPressed())
            {
                TryFire();
            }
        }

        private void TryFire()
        {
            if (_isReloading || _currentAmmo <= 0 || Time.time < _nextFireTime)
            {
                return;
            }

            _nextFireTime = Time.time + _fireInterval;
            _currentAmmo--;

            Ray ray = new Ray(_cameraTransform.position, _cameraTransform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, _range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                // 벽·바닥처럼 IDamageable이 없는 대상은 명중 표시만 하고 넘어간다
                if (hit.collider.TryGetComponent(out IDamageable damageable))
                {
                    damageable.TakeDamage(_damage);
                }

                SpawnHitMarker(hit.point);
            }

            // TODO(3장): HUD 이벤트로 옮긴 뒤 삭제
            Debug.LogFormat("[Weapon] Fire - Ammo {0}/{1}", _currentAmmo, _magazineSize);
        }

        private void TryReload()
        {
            if (_isReloading || _currentAmmo == _magazineSize)
            {
                return;
            }

            StartCoroutine(ReloadRoutine());
        }

        private IEnumerator ReloadRoutine()
        {
            _isReloading = true;
            yield return _reloadWait;

            _currentAmmo = _magazineSize;
            _isReloading = false;

            // TODO(3장): HUD 이벤트로 옮긴 뒤 삭제
            Debug.LogFormat("[Weapon] Reloaded - Ammo {0}/{1}", _currentAmmo, _magazineSize);
        }

        // 풀링 대상: 연사 중 0.15초마다 생성·파괴된다
        private void SpawnHitMarker(Vector3 position)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            // Destroy는 프레임 끝에 처리되므로, 같은 프레임 안의 레이캐스트에도 걸리지 않도록 즉시 제거한다
            DestroyImmediate(marker.GetComponent<Collider>());

            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * _hitMarkerSize;

            Renderer markerRenderer = marker.GetComponent<Renderer>();
            if (_hitMarkerMaterial == null)
            {
                _hitMarkerMaterial = new Material(markerRenderer.sharedMaterial);
            }
            _hitMarkerMaterial.color = _hitMarkerColor;
            markerRenderer.sharedMaterial = _hitMarkerMaterial;

            Destroy(marker, _hitMarkerLifetime);
        }
    }
}
