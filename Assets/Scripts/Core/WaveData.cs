using ArenaBreak.Enemy;
using UnityEngine;

namespace ArenaBreak.Core
{
    [CreateAssetMenu(fileName = "WaveData", menuName = "ArenaBreak/Wave Data")]
    public class WaveData : ScriptableObject
    {
        // GameObject가 아니라 EnemyAI로 받아야 스폰 직후 SetPlayer를 바로 호출할 수 있다
        [SerializeField] private EnemyAI _enemyPrefab;
        // 원거리 적까지 포함한 웨이브 전체 수. 기존 에셋의 의미(총 스폰 수)를 바꾸지 않으려고 이렇게 둔다
        [SerializeField] private int _spawnCount = 5;
        [SerializeField] private float _spawnInterval = 1f;

        [Header("Ranged")]
        [SerializeField] private RangedEnemyAI _rangedEnemyPrefab;
        // 위 스폰 수 중 원거리 적이 차지하는 수
        [SerializeField] private int _rangedSpawnCount;

        public EnemyAI EnemyPrefab => _enemyPrefab;
        public int SpawnCount => _spawnCount;
        public float SpawnInterval => _spawnInterval;
        public RangedEnemyAI RangedEnemyPrefab => _rangedEnemyPrefab;

        // 프리팹이 없거나 총 수를 넘기면 그만큼은 근접 적으로 채워 웨이브 총 수가 어긋나지 않게 한다
        public int RangedSpawnCount => _rangedEnemyPrefab == null ? 0 : Mathf.Clamp(_rangedSpawnCount, 0, _spawnCount);
    }
}
