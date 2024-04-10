using System.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Collections;
using static UnityEditor.Rendering.CameraUI;
using UnityEngine;


public struct Bucket
{
    public NativeList<int> values;

    public int Length
    {
        get
        {
            return values.Length;
        }
    }

    public Bucket(Allocator alloc)
    {
        values = new(1000, alloc);
    }

    public void Destroy()
    {
        values.Dispose();
    }

    public bool Contains(int val)
    {
        return values.Contains(val);
    }

    public void Add(int val)
    {
        values.Add(val);
    }

    public void Remove(int val)
    {
        values.RemoveAt(values.IndexOf(val));
    }
}

public struct SpatialHash
{
    private int cellSize;

    private UnsafeParallelHashMap<int3, Bucket> values;

    public SpatialHash(int _cellSize, int3 depth)
    {
        cellSize = _cellSize;

        values = new(depth.x * depth.y * depth.z, Allocator.Persistent);

        for (int i = 0; i < depth.x; i++)
        {
            for (int j = 0; j < depth.x; j++)
            {
                for (int k = 0; k < depth.x; k++)
                {
                    int3 pos = new int3(i - depth.x / 2, j - depth.y / 2, k - depth.z / 2);

                    values.Add(pos, new Bucket(Allocator.Persistent));
                }
            }
        }
    }

    public bool Insert(float3 pos, int idx)
    {
        int3 key = GetKey(pos);
        if (!values.ContainsKey(key))
        {
            values.Add(key, new Bucket(Allocator.Persistent));
            return false;
        }
        //Debug.Log(values[key].Length);
        values[key].Add(idx);
        return true;
    }

    public bool Remove(float3 pos, int idx)
    {
        int3 key = GetKey(pos);
        if (!values.ContainsKey(key))
            return false;
        if (!values[key].Contains(idx))
            return false;
        values[key].Remove(idx);
        return true;
    }

    public bool Update(float3 oldPos, float3 pos, int idx)
    {
        if (!Remove(oldPos, idx))
            return false;
        return Insert(pos, idx);
    }

    public bool Search(float3 pos, out Bucket bucket)
    {
        int3 key = GetKey(pos);
        if (!values.ContainsKey(key))
        {
            bucket = values[new int3(0, 0, 0)];
            return false;
        }
        bucket = values[key];
        return true;
    }

    public int3 GetKey(float3 idx)
    {
        int3 output = new int3();

        output.x = (int)math.floor(idx.x / cellSize);
        output.y = (int)math.floor(idx.y / cellSize);
        output.z = (int)math.floor(idx.z / cellSize);
        return output;
    }

    public void Destroy()
    {
        NativeArray<int3> array = values.GetKeyArray(Allocator.Temp);
        foreach (int3 i in array)
        {
            values[i].Destroy();
        }
        array.Dispose();
        values.Dispose();
    }

}

public struct SpatialHashTest
{
    private int cellSize;

    private UnsafeHashMap<int3, int> values;

    public SpatialHashTest(int _cellSize, int3 depth)
    {
        cellSize = _cellSize;

        values = new(depth.x * depth.y * depth.z, Allocator.Persistent);

        for (int i = 0; i < depth.x; i++)
        {
            for (int j = 0; j < depth.x; j++)
            {
                for (int k = 0; k < depth.x; k++)
                {
                    int3 pos = new int3(i - depth.x / 2, j - depth.y / 2, k - depth.z / 2);

                    values.Add(pos, 0);
                }
            }
        }
    }

    public bool Insert(float3 pos, int idx)
    {
        int3 key = GetKey(pos);
        if (!values.ContainsKey(key))
            return false;
        Debug.Log(values[key]);
        values[key] = idx;
        return true;
    }

    public bool Remove(float3 pos, int idx)
    {
        int3 key = GetKey(pos);
        if (!values.ContainsKey(key))
            return false;
        values[key] = idx;
        return true;
    }

    public int3 GetKey(float3 idx)
    {
        int3 output = new int3();

        output.x = (int)math.floor(idx.x / cellSize);
        output.y = (int)math.floor(idx.y / cellSize);
        output.z = (int)math.floor(idx.z / cellSize);
        return output;
    }

    public void Destroy()
    {
        values.Dispose();
    }

}