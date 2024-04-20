using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;



public struct SpatialHash
{
    public float BucketSize;
    public Vector3Int Dimentions;
    [NativeDisableParallelForRestriction] public NativeParallelMultiHashMap<int3, int> Hash;

    public SpatialHash(float bucketSize, Vector3Int dimentions, int capacity)
    {
        BucketSize = bucketSize;
        Dimentions = dimentions;
        Hash = new(capacity, Allocator.Persistent);
    }


    public bool GetFirst(float3 pos, out int firstElem, out NativeParallelMultiHashMapIterator<int3> iterator)
    {
        return Hash.TryGetFirstValue(getKey(pos, BucketSize), out firstElem, out iterator);
    }

    public bool GetNext(out int elem, ref NativeParallelMultiHashMapIterator<int3> iterator)
    {
        return Hash.TryGetNextValue(out elem, ref iterator);
    }

    public void Insert(float3 pos, int index)
    {
        int3 key = getKey(pos, BucketSize);

        Hash.AsParallelWriter().Add(key, index);
    }

    public void Remove(float3 pos, int index)
    {
        int3 key = getKey(pos, BucketSize);

        Hash.Remove(key, index);
    }

    public void Update(float3 oldPos, float3 newPos, int index)
    {
        int3 key = getKey(oldPos, BucketSize);
        int3 key1 = getKey(newPos, BucketSize);

        bool3 isSame = key == key1;
        if (isSame.x && isSame.y && isSame.z)
            return;

        Remove(key, index);
        Insert(key1, index);
    }

    public void Clear()
    {
        Hash.Clear();
    }


    static public int3 getKey(float3 pos, float bucketSize)
    {
        int3 output = new()
        {
            x = (int)math.floor(pos.x / bucketSize),
            y = (int)math.floor(pos.y / bucketSize),
            z = (int)math.floor(pos.z / bucketSize)
        };
        return output;
    }

    public void Dispose()
    {
        Hash.Dispose();
    }

    public ParallelWriter AsParallelWriter()
    {
        return new ParallelWriter(BucketSize, Dimentions, Hash.AsParallelWriter());
    }

    public struct ParallelWriter
    {
        public float BucketSize;
        public Vector3Int Dimentions;
        [NativeDisableParallelForRestriction] public NativeParallelMultiHashMap<int3, int>.ParallelWriter Hash;

        public ParallelWriter(float bucketSize, Vector3Int dimentions, NativeParallelMultiHashMap<int3, int>.ParallelWriter hash)
        {
            BucketSize = bucketSize;
            Dimentions = dimentions;
            Hash = hash;
        }

        public void Insert(float3 pos, int index)
        {
            int3 key = getKey(pos);

            Hash.Add(key, index);
        }

        private int3 getKey(float3 pos)
        {
            int3 output = new()
            {
                x = (int)math.floor(pos.x / BucketSize),
                y = (int)math.floor(pos.y / BucketSize),
                z = (int)math.floor(pos.z / BucketSize)
            };
            return output;
        }
    }
}
