using ArenaBreak.Enemy;
using UnityEngine;

namespace ArenaBreak.Core
{
    [CreateAssetMenu(fileName = "WaveData", menuName = "ArenaBreak/Wave Data")]
    public class WaveData : ScriptableObject
    {
        // GameObject가 아니라 EnemyAI로 받아야 스폰 직후 SetPlayer를 바로 호출할 수 있다
        [SerializeField] private EnemyAI _enemyPrefab;
        [SerializeField] private int _spawnCount = 5;
        [SerializeField] private float _spawnInterval = 1f;

        public EnemyAI EnemyPrefab => _enemyPrefab;
        public int SpawnCount => _spawnCount;
        public float SpawnInterval => _spawnInterval;
    }
}
