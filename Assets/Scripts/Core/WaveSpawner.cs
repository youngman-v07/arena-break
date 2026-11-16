using System.Collections;
using System.Collections.Generic;
using ArenaBreak.Enemy;
using UnityEngine;
using UnityEngine.Pool;

namespace ArenaBreak.Core
{
    public class WaveSpawner : MonoBehaviour
    {
        [Header("Waves")]
        [SerializeField] private List<WaveData> _waves;
        [SerializeField] private float _nextWaveDelay = 3f;

        [Header("References")]
        // 자식 Transform은 Awake에서 모으므로, 배열 대신 부모 하나만 받는다
        [SerializeField] private Transform _spawnPoints;
        [SerializeField] private Transform _player;

        [Header("Pool")]
        [SerializeField] private int _poolDefaultCapacity = 10;
        // 한도를 넘겨 반납된 적은 풀에 쌓이지 않고 파괴된다
        [SerializeField] private int _poolMaxSize = 30;

        private readonly List<Transform> _spawnPointList = new List<Transform>();
        // 웨이브마다 프리팹이 다를 수 있어 프리팹별로 풀을 따로 둔다
        private readonly Dictionary<EnemyAI, ObjectPool<EnemyAI>> _poolByPrefab = new Dictionary<EnemyAI, ObjectPool<EnemyAI>>();
        // 반납 시 어느 풀로 돌려보낼지 알아야 한다
        private readonly Dictionary<EnemyAI, ObjectPool<EnemyAI>> _poolByInstance = new Dictionary<EnemyAI, ObjectPool<EnemyAI>>();
        // 원거리 적은 EnemyAI와 타입이 달라 풀을 따로 둔다
        private readonly Dictionary<RangedEnemyAI, ObjectPool<RangedEnemyAI>> _rangedPoolByPrefab = new Dictionary<RangedEnemyAI, ObjectPool<RangedEnemyAI>>();
        private readonly Dictionary<RangedEnemyAI, ObjectPool<RangedEnemyAI>> _rangedPoolByInstance = new Dictionary<RangedEnemyAI, ObjectPool<RangedEnemyAI>>();
        // 풀 인스턴스는 한 번만 구독하므로, 해제 대상을 알기 위해 Health를 들고 있는다
        private readonly List<Health> _subscribedHealths = new List<Health>();

        private WaitForSeconds _nextWaveWait;
        private WaitUntil _waitAllDead;
        private int _aliveCount;
        private int _killCount;

        public event System.Action<int> WaveStarted;
        public event System.Action<int> KillCountChanged;
        public event System.Action<int> WaveCleared;
        public event System.Action AllWavesCleared;

        private void Awake()
        {
            _nextWaveWait = new WaitForSeconds(_nextWaveDelay);
            _waitAllDead = new WaitUntil(AreAllEnemiesDead);

            if (_spawnPoints == null)
            {
                return;
            }

            foreach (Transform child in _spawnPoints)
            {
                _spawnPointList.Add(child);
            }
        }

        private void Start()
        {
            if (_waves.Count == 0 || _spawnPointList.Count == 0)
            {
                Debug.LogError("[WaveSpawner] 웨이브 또는 스폰 포인트가 비어 있어 시작하지 않는다", this);
                return;
            }

            StartCoroutine(RunWaves());
        }

        // 구독은 풀에 인스턴스를 만들 때 한 번만 하므로 OnDisable이 아니라 OnDestroy에서 해제한다
        private void OnDestroy()
        {
            foreach (Health health in _subscribedHealths)
            {
                if (health != null)
                {
                    health.Died -= HandleEnemyDied;
                }
            }

            foreach (EnemyAI enemy in _poolByInstance.Keys)
            {
                if (enemy != null)
                {
                    enemy.DeathFinished -= HandleEnemyDeathFinished;
                }
            }

            foreach (RangedEnemyAI enemy in _rangedPoolByInstance.Keys)
            {
                if (enemy != null)
                {
                    enemy.DeathFinished -= HandleRangedDeathFinished;
                }
            }

            _subscribedHealths.Clear();
            _poolByInstance.Clear();
            _rangedPoolByInstance.Clear();
        }

        private IEnumerator RunWaves()
        {
            for (int i = 0; i < _waves.Count; i++)
            {
                int waveNumber = i + 1;
                WaveStarted?.Invoke(waveNumber);

                yield return SpawnWave(_waves[i]);

                // 스폰 도중 앞선 적이 다 죽어도 웨이브가 끝난 것으로 보면 안 되므로 스폰이 끝난 뒤에 기다린다
                yield return _waitAllDead;

                WaveCleared?.Invoke(waveNumber);

                if (i < _waves.Count - 1)
                {
                    yield return _nextWaveWait;
                }
            }

            AllWavesCleared?.Invoke();
        }

        private IEnumerator SpawnWave(WaveData wave)
        {
            WaitForSeconds intervalWait = new WaitForSeconds(wave.SpawnInterval);
            int rangedLeft = wave.RangedSpawnCount;
            int chargerLeft = wave.ChargerSpawnCount;
            int meleeLeft = wave.SpawnCount - rangedLeft - chargerLeft;

            while (meleeLeft + rangedLeft + chargerLeft > 0)
            {
                // 남은 수의 비율로 뽑아야 종류가 섞이면서도 웨이브가 정해진 수를 정확히 채운다
                int pick = Random.Range(0, meleeLeft + rangedLeft + chargerLeft);

                if (pick < rangedLeft)
                {
                    SpawnRanged(wave.RangedEnemyPrefab);
                    rangedLeft--;
                }
                else if (pick < rangedLeft + chargerLeft)
                {
                    // 돌진 적도 EnemyAI라 근접 적 스폰 경로와 풀을 그대로 쓴다
                    SpawnMelee(wave.ChargerEnemyPrefab);
                    chargerLeft--;
                }
                else
                {
                    SpawnMelee(wave.EnemyPrefab);
                    meleeLeft--;
                }

                if (meleeLeft + rangedLeft + chargerLeft > 0)
                {
                    yield return intervalWait;
                }
            }
        }

        private void SpawnMelee(EnemyAI prefab)
        {
            Transform point = GetRandomSpawnPoint();
            EnemyAI enemy = GetPool(prefab).Get();

            // 비활성 상태에서 위치를 잡고 켜야 NavMeshAgent가 이전 위치가 아니라 이 위치에 등록된다
            enemy.transform.SetPositionAndRotation(point.position, point.rotation);
            enemy.SetPlayer(_player);
            enemy.gameObject.SetActive(true);

            _aliveCount++;
        }

        private void SpawnRanged(RangedEnemyAI prefab)
        {
            Transform point = GetRandomSpawnPoint();
            RangedEnemyAI enemy = GetRangedPool(prefab).Get();

            enemy.transform.SetPositionAndRotation(point.position, point.rotation);
            enemy.SetPlayer(_player);
            enemy.gameObject.SetActive(true);

            _aliveCount++;
        }

        private Transform GetRandomSpawnPoint()
        {
            return _spawnPointList[Random.Range(0, _spawnPointList.Count)];
        }

        private ObjectPool<EnemyAI> GetPool(EnemyAI prefab)
        {
            if (_poolByPrefab.TryGetValue(prefab, out ObjectPool<EnemyAI> pool))
            {
                return pool;
            }

            pool = new ObjectPool<EnemyAI>(
                createFunc: () => CreateEnemy(prefab),
                actionOnRelease: enemy => enemy.gameObject.SetActive(false),
                actionOnDestroy: enemy => Destroy(enemy.gameObject),
                defaultCapacity: _poolDefaultCapacity,
                maxSize: _poolMaxSize);

            _poolByPrefab.Add(prefab, pool);
            return pool;
        }

        private ObjectPool<RangedEnemyAI> GetRangedPool(RangedEnemyAI prefab)
        {
            if (_rangedPoolByPrefab.TryGetValue(prefab, out ObjectPool<RangedEnemyAI> pool))
            {
                return pool;
            }

            pool = new ObjectPool<RangedEnemyAI>(
                createFunc: () => CreateRangedEnemy(prefab),
                actionOnRelease: enemy => enemy.gameObject.SetActive(false),
                actionOnDestroy: enemy => Destroy(enemy.gameObject),
                defaultCapacity: _poolDefaultCapacity,
                maxSize: _poolMaxSize);

            _rangedPoolByPrefab.Add(prefab, pool);
            return pool;
        }

        private EnemyAI CreateEnemy(EnemyAI prefab)
        {
            EnemyAI enemy = Instantiate(prefab);
            // Get 직후 위치를 잡고 켜기 위해 만들자마자 꺼 둔다
            enemy.gameObject.SetActive(false);

            Health health = enemy.GetComponent<Health>();
            health.Died += HandleEnemyDied;
            _subscribedHealths.Add(health);

            enemy.DeathFinished += HandleEnemyDeathFinished;
            _poolByInstance.Add(enemy, _poolByPrefab[prefab]);

            return enemy;
        }

        private RangedEnemyAI CreateRangedEnemy(RangedEnemyAI prefab)
        {
            RangedEnemyAI enemy = Instantiate(prefab);
            enemy.gameObject.SetActive(false);

            Health health = enemy.GetComponent<Health>();
            health.Died += HandleEnemyDied;
            _subscribedHealths.Add(health);

            enemy.DeathFinished += HandleRangedDeathFinished;
            _rangedPoolByInstance.Add(enemy, _rangedPoolByPrefab[prefab]);

            return enemy;
        }

        private void HandleEnemyDied()
        {
            _aliveCount--;
            _killCount++;
            KillCountChanged?.Invoke(_killCount);
        }

        private void HandleEnemyDeathFinished(EnemyAI enemy)
        {
            _poolByInstance[enemy].Release(enemy);
        }

        private void HandleRangedDeathFinished(RangedEnemyAI enemy)
        {
            _rangedPoolByInstance[enemy].Release(enemy);
        }

        private bool AreAllEnemiesDead()
        {
            return _aliveCount == 0;
        }
    }
}
