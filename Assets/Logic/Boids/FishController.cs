using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using Random = UnityEngine.Random;


public class FishController : MonoBehaviour
{
    public float speed = 0.5f;
    public float turningSpeed = 1.0f;
    public float flockDistance = 1.0f;

    public float stearingWeight = 1.0f;
    public float flockingWeight = 1.0f;
    public float centerWeight = 1.0f;

    public float movementRandomness = 1.0f;


    public GameObject fishPrefab;
    public int initialFishSpawn = 5;
    public float spawnRadius = 10;

    public float size = 500;
    public int levels = 1;


    public List<GameObject> fishes;

    private NativeArray<FishData> fishData;
    public TransformAccessArray fishTransforms;


    private SpatialHash spatialHash;

    public Vector3Int dimentions = new(10, 10, 10);
    public int bucketSize = 5;

    private JobHandle handle;

    private bool isEven = true;


    void Start()
    {
        fishData = new(initialFishSpawn, Allocator.Persistent);
        fishTransforms = new TransformAccessArray(initialFishSpawn);

        spatialHash = new(bucketSize, dimentions, initialFishSpawn);
        for (int i = 0; i < initialFishSpawn; i++)
        {
            Vector3 pos = Random.insideUnitSphere * spawnRadius;
            GameObject obj = Instantiate(fishPrefab, pos, Random.rotation);
            //fishData[i] = new FishData(obj.transform.forward, pos, float3.zero);

            fishTransforms.Add(obj.transform);
            fishes.Add(obj);
        }
    }


    void Update()
    {
        spatialHash.Clear();
        SetSpatialHash spacesUpdater = new SetSpatialHash(fishData, spatialHash.AsParallelWriter());

        JobHandle spaces = spacesUpdater.Schedule(fishTransforms);

        FishJob job = new(speed, turningSpeed, flockDistance, stearingWeight, flockingWeight, centerWeight, movementRandomness, 
            fishData, spatialHash, Time.deltaTime, (uint)Random.Range(0, 50000) , isEven);

        handle = job.Schedule(fishTransforms, spaces);

        isEven = !isEven;
    }

    private void LateUpdate()
    {
        handle.Complete();
    }

    private void OnDestroy()
    {
        handle.Complete();
        spatialHash.Dispose();
        fishData.Dispose();
        fishTransforms.Dispose();
    }

    public struct FishData
    {
        public float3 velocity;
        public float3 position;
        public float3 random;

        public FishData(float3 _velocity, float3 _position, float3 _random)
        {
            velocity = _velocity;
            position = _position;
            random = _random;
        }
    }

    [BurstCompile]
    public struct SetSpatialHash : IJobParallelForTransform
    {
        [NativeDisableParallelForRestriction] private NativeArray<FishData> _fishData;
        [NativeDisableParallelForRestriction] private SpatialHash.ParallelWriter _fishesSpace;


        public SetSpatialHash(NativeArray<FishData> fishData, SpatialHash.ParallelWriter fishesSpace)
        {

            _fishData = fishData;
            _fishesSpace = fishesSpace;
        }

        public void Execute(int idx, TransformAccess transform)
        {
            _fishData[idx] = new FishData(math.forward(transform.rotation), transform.position, _fishData[idx].random);
            _fishesSpace.Insert(transform.position, idx);
        }
    }
}
