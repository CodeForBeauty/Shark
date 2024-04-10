using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using static UnityEditor.PlayerSettings;
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

    public NativeArray<float3> fishPositions;
    public NativeArray<float3> fishVelocities;
    public NativeArray<float3> fishRandoms;
    public TransformAccessArray fishTransforms;


    public NativeParallelMultiHashMap<int3, int> hash;

    public Vector3Int dimentions = new Vector3Int(10, 10, 10);
    public int bucketSize = 5;

    private JobHandle handle;

    private bool isEven = true;


    void Start()
    {
        fishPositions = new(initialFishSpawn, Allocator.Persistent);
        fishVelocities = new(initialFishSpawn, Allocator.Persistent);
        fishRandoms = new(initialFishSpawn, Allocator.Persistent);
        fishTransforms = new TransformAccessArray(initialFishSpawn);

        hash = new NativeParallelMultiHashMap<int3, int>(initialFishSpawn, Allocator.Persistent);
        for (int i = 0; i < initialFishSpawn; i++)
        {
            Vector3 pos = Random.insideUnitSphere * spawnRadius;
            GameObject obj = Instantiate(fishPrefab, pos, Random.rotation);
            fishPositions[i] = (float3)pos;
            fishVelocities[i] = (float3)obj.transform.forward;
            fishTransforms.Add(obj.transform);

            fishes.Add(obj);
        }
    }


    void Update()
    {
        hash.Clear();
        SetSpatialHash spacesUpdater = new SetSpatialHash(fishPositions, fishVelocities, hash.AsParallelWriter(), bucketSize);
        
        JobHandle spaces = spacesUpdater.Schedule(fishTransforms);

        FishJob job = new FishJob(speed, turningSpeed, flockDistance, stearingWeight, flockingWeight, centerWeight, movementRandomness, 
            fishVelocities, fishPositions, hash.AsReadOnly(), fishRandoms, Time.deltaTime, bucketSize, (uint)Random.Range(0, 50000) , isEven);

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
        hash.Dispose();
        fishPositions.Dispose();
        fishVelocities.Dispose();
        fishRandoms.Dispose();
        fishTransforms.Dispose();
    }

    /*private void OnDrawGizmosSelected()
    {
        if (!hash.IsCreated)
            return;
        NativeArray<int3> arr = hash.GetKeyArray(Allocator.Temp);

        foreach (var i in arr)
        {
            Gizmos.color = new Color(hash.CountValuesForKey(i) / 50.0f, 0, 0);
            Gizmos.DrawWireCube((float3)i * bucketSize + (bucketSize / 2), (float3)bucketSize);

            int tempData;
            NativeParallelMultiHashMapIterator<int3> iterator;
            if (hash.TryGetFirstValue(i, out tempData, out iterator))
            {
                //Debug.Log(tempData);
                do
                {
                    Debug.DrawLine((float3)i * bucketSize, fishPositions[tempData], new Color(1, 0, 0));
                } while (hash.TryGetNextValue(out tempData, ref iterator));
            }
        }

        arr.Dispose();
    }*/

    public static int3 GetKey(float3 pos, int bucketSize)
    {
        int3 output = new int3();

        output.x = (int)math.floor(pos.x / bucketSize);
        output.y = (int)math.floor(pos.y / bucketSize);
        output.z = (int)math.floor(pos.z / bucketSize);
        return output;
    }

    [BurstCompile]
    public struct SetSpatialHash : IJobParallelForTransform
    {
        private int _bucketSize;
        [NativeDisableParallelForRestriction] private NativeArray<float3> _fishesPosition;
        [NativeDisableParallelForRestriction] private NativeArray<float3> _fishesVelocity;
        [NativeDisableParallelForRestriction] private NativeParallelMultiHashMap<int3, int>.ParallelWriter _fishesSpace;


        public SetSpatialHash(NativeArray<float3> fishesPosition, NativeArray<float3> fishesVelocity, NativeParallelMultiHashMap<int3, int>.ParallelWriter fishesSpace, int bucketSize)
        {
            _fishesPosition = fishesPosition;
            _fishesVelocity = fishesVelocity;
            _fishesSpace = fishesSpace;

            _bucketSize = bucketSize;
        }

        public void Execute(int idx, TransformAccess transform)
        {
            _fishesPosition[idx] = transform.position;
            _fishesVelocity[idx] = math.forward(transform.rotation);
            int3 key = GetKey(transform.position, _bucketSize);
            _fishesSpace.Add(key, idx);
        }
    }
}
