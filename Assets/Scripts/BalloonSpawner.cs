using UnityEngine;
using System.Collections.Generic;

public class BalloonSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject balloonPrefab;
    [SerializeField] private int balloonCount = 100;
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(10f, 5f, 10f);
    [SerializeField] private float spawnHeight = 2f;
    
    [Header("Balloon Variations")]
    [SerializeField] private float minScale = 0.8f;
    [SerializeField] private float maxScale = 1.2f;
    [SerializeField] private Color[] balloonColors = new Color[] 
    {
        Color.red, Color.blue, Color.green, Color.yellow, 
        Color.magenta, Color.cyan, new Color(1f, 0.5f, 0f)
    };
    
    private List<GameObject> spawnedBalloons = new List<GameObject>();
    
    void Start()
    {
        SpawnBalloons();
    }
    
    public void SpawnBalloons()
    {
        if (balloonPrefab == null)
        {
            Debug.LogError("Balloon prefab not assigned!");
            return;
        }
        
        GameObject balloonContainer = new GameObject("BalloonContainer");
        
        for (int i = 0; i < balloonCount; i++)
        {
            Vector3 randomPosition = new Vector3(
                Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f),
                spawnHeight + Random.Range(0f, spawnAreaSize.y),
                Random.Range(-spawnAreaSize.z / 2f, spawnAreaSize.z / 2f)
            );
            
            GameObject balloon = Instantiate(balloonPrefab, randomPosition, Random.rotation);
            balloon.transform.parent = balloonContainer.transform;
            
            // Apply random scale
            float randomScale = Random.Range(minScale, maxScale);
            balloon.transform.localScale = Vector3.one * randomScale;
            
            // Apply random color
            if (balloonColors.Length > 0)
            {
                Renderer renderer = balloon.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = balloonColors[Random.Range(0, balloonColors.Length)];
                }
            }
            
            // Set balloon layer for collision optimization
            balloon.layer = LayerMask.NameToLayer("Balloon");
            
            spawnedBalloons.Add(balloon);
        }
        
        Debug.Log($"Spawned {balloonCount} balloons");
    }
    
    public void ClearBalloons()
    {
        foreach (GameObject balloon in spawnedBalloons)
        {
            if (balloon != null)
                Destroy(balloon);
        }
        spawnedBalloons.Clear();
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up * spawnHeight, spawnAreaSize);
    }
}