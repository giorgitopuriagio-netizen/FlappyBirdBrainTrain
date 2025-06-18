using System.Collections.Generic;
using UnityEngine;

public class EnemySpawn : MonoBehaviour
{
    static public List<GameObject> enemyList;

    [SerializeField] private GameObject[] _enemysPrefabs;
    [SerializeField] private float _interval;
    [SerializeField] private float _heightRange;
    [SerializeField] private float _lowestRange;

    private float timer;

    // Start is called before the first frame update
    void Start()
    {
        Initialization();
        Spawn();
    }

    // Update is called once per frame
    void Update()
    {
        if (timer > _interval)
        {
            Spawn();
            timer = 0;
        }

        timer += Time.deltaTime;
    }

    void Initialization()
    {
        timer = 0;

        _interval = 2.5f;
        _heightRange = -4f;
        _lowestRange = -7f;
        
        enemyList = new List<GameObject>();
    }

    void Spawn()
    {
        Vector3 position;
        GameObject enemies;

        position = transform.position + new Vector3(0, Random.Range(_lowestRange, _heightRange));
        enemies = Instantiate(_enemysPrefabs[Random.Range(0, 3)], position, Quaternion.identity);

        enemyList.Add(enemies);

        Destroy(enemies, 10f);
    }
}