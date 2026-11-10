using System.Collections;
using System.Collections.Generic;
using ArenaBreak.Enemy;
using UnityEngine;

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

        private readonly List<Transform> _spawnPointList = new List<Transform>();
        // 구독 해제 대상을 알아야 하므로 스폰한 적의 Health를 들고 있는다
        private readonly List<Health> _trackedEnemies = new List<Health>();

        private WaitForSeconds _nextWaveWait;
        private WaitUntil _waitAllDead;
        private int _aliveCount;

        public event System.Action<int> WaveStarted;
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

        private void OnDisable()
        {
            ReleaseEnemies();
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

                ReleaseEnemies();
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

            for (int n = 0; n < wave.SpawnCount; n++)
            {
                SpawnEnemy(wave.EnemyPrefab);

                if (n < wave.SpawnCount - 1)
                {
                    yield return intervalWait;
                }
            }
        }

        // 풀링 대상: 웨이브마다 생성·파괴가 반복된다
        private void SpawnEnemy(EnemyAI prefab)
        {
            Transform point = _spawnPointList[Random.Range(0, _spawnPointList.Count)];
            EnemyAI enemy = Instantiate(prefab, point.position, point.rotation);
            enemy.SetPlayer(_player);

            Health health = enemy.GetComponent<Health>();
            health.Died += HandleEnemyDied;
            _trackedEnemies.Add(health);
            _aliveCount++;
        }

        private void HandleEnemyDied()
        {
            _aliveCount--;
        }

        private bool AreAllEnemiesDead()
        {
            return _aliveCount == 0;
        }

        private void ReleaseEnemies()
        {
            foreach (Health health in _trackedEnemies)
            {
                if (health != null)
                {
                    health.Died -= HandleEnemyDied;
                }
            }

            _trackedEnemies.Clear();
        }
    }
}
