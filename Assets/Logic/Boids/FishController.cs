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
    public TransformAccessArray fishTransforms;

    //public NativeArray<Octree> octree;
    //public NativeArray<SpatialHash> hash;

    public NativeParallelMultiHashMap<int3, int> hash;

    public Vector3Int dimentions = new Vector3Int(10, 10, 10);
    public int bucketSize = 5;


    void Start()
    {
        //octree = new(1, Allocator.Persistent);
        //octree[0] = oct;
        //fishPositions = new(initialFishSpawn, Allocator.Persistent);
        fishPositions = new(initialFishSpawn, Allocator.Persistent);
        fishVelocities = new NativeArray<float3>(initialFishSpawn, Allocator.Persistent);
        fishTransforms = new TransformAccessArray(initialFishSpawn);

        hash = new NativeParallelMultiHashMap<int3, int>(initialFishSpawn, Allocator.Persistent);
        for (int i = 0; i < initialFishSpawn; i++)
        {
            Vector3 pos = Random.insideUnitSphere * spawnRadius;
            GameObject obj = Instantiate(fishPrefab, pos, Random.rotation);
            //fishPositions[i] = (float3)pos;
            fishPositions[i] = (float3)pos;
            fishVelocities[i] = (float3)obj.transform.forward;
            fishTransforms.Add(obj.transform);

            fishes.Add(obj);
        }

        //job = new FishJob(speed, turningSpeed, flockDistance, stearingWeight, flockingWeight, centerWeight, fishVelocities, fishPositions, hash);

        /*job.deltaTime = Time.deltaTime;
        JobHandle handle = job.Schedule(fishTransforms);

        handle.Complete();*/
    }


    void Update()
    {
        hash.Clear();
        SetSpatialHash spacesUpdater = new SetSpatialHash(fishPositions, fishVelocities, hash.AsParallelWriter(), bucketSize);

        JobHandle handle = spacesUpdater.Schedule(fishTransforms);

        FishJob job = new FishJob(speed, turningSpeed, flockDistance, stearingWeight, flockingWeight, centerWeight, movementRandomness, 
            fishVelocities, fishPositions, hash.AsReadOnly(), Time.deltaTime, bucketSize, (uint)Random.Range(0, 50000));

        JobHandle handle1 = job.Schedule(fishTransforms, handle);

        handle1.Complete();
    }

    private void OnDestroy()
    {
        hash.Dispose();
        fishPositions.Dispose();
        fishVelocities.Dispose();
        fishTransforms.Dispose();
    }

    private void OnDrawGizmosSelected()
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
    }

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


    public struct Octree
    {
        public float3 start;
        public float size;
        
        public UnsafeList<Octree> children;
        private UnsafeList<float3> velocities;
        private UnsafeList<float3> positions;
        public bool last;

        public float3 end;

        public float3 test;


        public Octree(float3 _start, float _size, int level)
        {
            test = new();
            start = _start;
            size = _size;
            end = start + size;

            children = new UnsafeList<Octree>(0, Allocator.Temp);
            //children = new List<Octree>();
            positions = new UnsafeList<float3>(0, Allocator.Temp);
            velocities = new UnsafeList<float3>(0, Allocator.Temp);


            if (level == 0)
                last = true;
            else
            {
                float half = size / 2;
                for (int i = 0; i < 8; i++)
                {
                    Octree oct = new Octree(new float3(start.x + (i % 2) * half, start.y + (i / 2 % 2) * half, start.z + (i / 4 % 2) * half), half, level - 1);
                    children.Add(oct);
                }
                last = false;
            }
        }

        public bool Contains(float3 position)
        {
            return (position.x > start.x && position.y > start.y && position.z > start.z) && (position.x < end.x && position.y < end.y && position.z < end.z);
        }

        public bool Search(float3 position, ref UnsafeList<float3> pos, ref UnsafeList<float3> vel)
        {
            if (!Contains(position))
                return false;

            if (last)
            {
                //Debug.Log(positions.Length);
                foreach (float3 val in positions)
                    pos.Add(val);
                foreach (float3 val in velocities)
                    vel.Add(val);

                return true;
            }

            foreach (Octree child in children)
                if (child.Search(position, ref pos, ref vel))
                    return true;

            return false;
        }

        public bool Insert(float3 pos, float3 vel)
        {
            if (!Contains(pos))
                return false;

            if (last)
            {
                test = pos;
                positions.Add(test);
                velocities.Add(vel);
                Debug.Log(positions.Length);
                /*Debug.Log(size);
                Debug.Log(start);*/
                return true;
            }

            foreach (Octree child in children)
                if (child.Insert(pos, vel))
                    return true;

            return false;
        }

        public bool Remove(float3 position)
        {
            if (!Contains(position))
                return false;

            if (last)
            {
                if (positions.Contains(position))
                {
                    int idx = positions.IndexOf(position);
                    velocities.RemoveAt(idx);
                    positions.RemoveAt(idx);

                    return true;
                }
                return false;
            }

            foreach (Octree child in children)
                if (child.Remove(position))
                    return true;

            return false;
        }

        public void Destroy()
        {
            if (last)
            {
                positions.Dispose();
                velocities.Dispose();
                //children.Dispose();
                return;
            }

            foreach (Octree child in children)
                child.Destroy();
            //children.Dispose();
        }

        public void Clear()
        {
            if (last)
            {
                positions.Clear();
                velocities.Clear();
            }
        }
    }
}
