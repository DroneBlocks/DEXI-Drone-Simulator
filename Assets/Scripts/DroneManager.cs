using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum DroneModel
{
    DexiTen,
    DexiFive,
    DexiThree
}

[System.Serializable]
public class DroneType
{
    public DroneModel model;
    public string name;
    public GameObject prefab;
}

public class DroneManager : MonoBehaviour
{
    public static DroneManager Instance { get; private set; }
    public event Action<DroneType> OnDroneSwapped;

    public List<DroneType> drones = new();
    public DroneModel defaultModel;

    public DroneType ActiveDrone => drones.Count > 0 ? drones[currentIndex] : null;

    private int currentIndex = 0;
    private GameObject spawnedDrone;
    private DroneCamera droneCamera;
    private DownwardCamera downwardCamera;
    private Transform spawnPoint;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        int index = drones.FindIndex(d => d.model == defaultModel);
        currentIndex = index != -1 ? index : 0;
        SpawnDrone();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        droneCamera = FindFirstObjectByType<DroneCamera>();
        downwardCamera = FindFirstObjectByType<DownwardCamera>();

        spawnPoint = GameObject.FindWithTag("DroneSpawnpoint")?.transform;

        spawnedDrone = null;

        SpawnDrone();
    }

    public void SwapToDrone(DroneModel model)
    {
        int index = drones.FindIndex(d => d.model == model);
        if (index == -1 || index == currentIndex) return;

        currentIndex = index;

        SpawnDrone();
    }

    void SpawnDrone()
    {
        if (spawnedDrone != null) Destroy(spawnedDrone);
        if (ActiveDrone?.prefab == null) return;

        spawnedDrone = Instantiate(ActiveDrone.prefab, spawnPoint.position, spawnPoint.rotation);

        droneCamera.SetTarget(spawnedDrone.transform);
        downwardCamera.SetTarget(spawnedDrone.transform);

        OnDroneSwapped?.Invoke(ActiveDrone);
    }
}