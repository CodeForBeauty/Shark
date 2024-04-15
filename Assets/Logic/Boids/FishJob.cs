using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Random = Unity.Mathematics.Random;

[BurstCompile]
public struct FishJob : IJobParallelForTransform
{
    private float _speed;
    private float _turningSpeed;
    private float _flockDistance;

    private float _stearingWeight;
    private float _flockingWeight;
    private float _centerWeight;

    private float3 _randomness;

    [NativeDisableParallelForRestriction] private NativeArray<FishController.FishData> _fishData;
    [ReadOnly] private SpatialHash _space;

    private float _deltaTime;


    private Random rng;

    private bool _isEven;


    public FishJob(float speed, float turningSpeed, float flockDistance, float stearingWeight, float flockingWeight, float centerWeight, float randomness,
        NativeArray<FishController.FishData> fishData, SpatialHash space,
        float deltaTime, uint seed, bool isEven)
    {
        _speed = speed;
        _turningSpeed = turningSpeed;
        _flockDistance = flockDistance;

        _stearingWeight = stearingWeight;
        _flockingWeight = flockingWeight;
        _centerWeight = centerWeight;

        _fishData = fishData;

        _deltaTime = deltaTime;

        _space = space;

        rng = new Random(seed);
        _randomness = new float3(randomness, randomness, randomness);

        _isEven = isEven;
    }


    public void Execute(int index, TransformAccess transform)
    {
        if (_isEven == (index % 2 == 0))
            return;
        float3 pos = (float3)transform.position;

        int flockCount = 0;

        float3 averageVelocity = new(), flockCenter = new();

        FishController.FishData data = _fishData[index];

        for (float i = -_flockDistance; i < _flockDistance; i += _space.BucketSize)
        {
            for (float j = -_flockDistance; j < _flockDistance; j += _space.BucketSize)
            {
                for (float k = -_flockDistance; k < _flockDistance; k += _space.BucketSize)
                {
                    float3 bucketPos = new(pos.x + i, pos.y + j, pos.z + k);

                    int tempData;
                    NativeParallelMultiHashMapIterator<int3> iterator;
                    if (_space.GetFirst(bucketPos, out tempData, out iterator))
                    {
                        do
                        {
                            if (tempData == index)
                                continue;
                            float distance = math.distance(pos, _fishData[tempData].position);
                            if (distance < _flockDistance)
                            {

                                data.velocity += math.normalize(pos - _fishData[tempData].position) * (_flockDistance - distance) * _stearingWeight;
                                averageVelocity += _fishData[tempData].velocity;
                                flockCount++;

                                flockCenter += _fishData[tempData].position;
                            }
                        } while (_space.GetNext(out tempData, ref iterator));
                    }
                }
            }
        }


        if (flockCount > 0)
        {
            data.velocity += (averageVelocity / flockCount) * _flockingWeight;
            data.velocity += ((flockCenter / flockCount) - pos) * _centerWeight;
        }

        if (!Equals(_randomness, 0))
        {
            data.random = math.normalize(rng.NextFloat3(-_randomness, _randomness) + data.random);
            data.velocity += data.random * _randomness;
        }

        transform.SetPositionAndRotation(Vector3.Lerp(transform.position,(float3)transform.position + data.velocity, 5f * _deltaTime * _speed),
            Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(data.velocity, math.up()), 5f * _deltaTime * _turningSpeed));

        _fishData[index] = data;
    }
}
