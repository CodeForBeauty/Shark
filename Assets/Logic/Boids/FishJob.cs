using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
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

    [NativeDisableParallelForRestriction] private NativeArray<float3> _fishesVelocity;
    [NativeDisableParallelForRestriction] private NativeArray<float3> _fishesPosition;
    [NativeDisableParallelForRestriction] private NativeArray<float3> _fishesRandoms;
    [ReadOnly] private NativeParallelMultiHashMap<int3, int>.ReadOnly _space;

    private float _deltaTime;
    private int _bucketSize;


    private Random rng;

    private bool _isEven;


    public FishJob(float speed, float turningSpeed, float flockDistance, float stearingWeight, float flockingWeight, float centerWeight, float randomness,
        NativeArray<float3> fishesVelocity, NativeArray<float3> fishesPosition, NativeParallelMultiHashMap<int3, int>.ReadOnly space, NativeArray<float3> fishesRandoms,
        float deltaTime, int bucketSize, uint seed, bool isEven)
    {
        _speed = speed;
        _turningSpeed = turningSpeed;
        _flockDistance = flockDistance;

        _stearingWeight = stearingWeight;
        _flockingWeight = flockingWeight;
        _centerWeight = centerWeight;

        _fishesVelocity = fishesVelocity;
        _fishesPosition = fishesPosition;

        _deltaTime = deltaTime;

        _space = space;
        _bucketSize = bucketSize;

        _fishesRandoms = fishesRandoms;

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

        for (float i = -_flockDistance; i < _flockDistance; i += _bucketSize)
        {
            for (float j = -_flockDistance; j < _flockDistance; j += _bucketSize)
            {
                for (float k = -_flockDistance; k < _flockDistance; k += _bucketSize)
                {
                    int3 bucketPos = FishController.GetKey(new float3(pos.x + i, pos.y + j, pos.z + k), _bucketSize);

                    int tempData;
                    NativeParallelMultiHashMapIterator<int3> iterator;
                    if (_space.TryGetFirstValue(bucketPos, out tempData, out iterator))
                    {
                        do
                        {
                            if (tempData == index)
                                continue;
                            float distance = math.distance(pos, _fishesPosition[tempData]);
                            if (distance < _flockDistance)
                            {

                                _fishesVelocity[index] += math.normalize(pos - _fishesPosition[tempData]) * (_flockDistance - distance) * _stearingWeight;
                                averageVelocity += _fishesVelocity[tempData];
                                flockCount++;

                                flockCenter += _fishesPosition[tempData];
                            }
                        } while (_space.TryGetNextValue(out tempData, ref iterator));
                    }
                }
            }
        }


        if (flockCount > 0)
        {
            _fishesVelocity[index] += (averageVelocity / flockCount) * _flockingWeight;
            _fishesVelocity[index] += ((flockCenter / flockCount) - pos) * _centerWeight;
        }

        _fishesRandoms[index] = math.normalize(rng.NextFloat3(-_randomness, _randomness) + _fishesRandoms[index]);

        /*transform.SetPositionAndRotation(Vector3.Lerp(transform.position, Vector3.MoveTowards(transform.position, (float3)transform.position + _fishesVelocity[index], _speed * 2), 5f * _deltaTime),
            Quaternion.Lerp(transform.rotation, Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_fishesVelocity[index], math.up()), _turningSpeed), 5f * _deltaTime));*/
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position,(float3)transform.position + _fishesVelocity[index], 5f * _deltaTime * _speed),
            Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(_fishesVelocity[index], math.up()), 5f * _deltaTime * _turningSpeed));
    }
}
