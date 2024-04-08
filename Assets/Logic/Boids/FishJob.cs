using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Jobs;
using Unity.Mathematics;
using Unity.Burst;

[BurstCompile]
public struct FishJob : IJobParallelForTransform
{
    private float _speed;
    private float _turningSpeed;
    private float _flockDistance;

    public float _stearingWeight;
    public float _flockingWeight;
    public float _centerWeight;

    [NativeDisableParallelForRestriction] private NativeArray<float3> _fishesVelocity;
    [NativeDisableParallelForRestriction] private NativeArray<float3> _fishesPosition;

    public float deltaTime;


    public FishJob(float speed, float turningSpeed, float flockDistance, float stearingWeight, float flockingWeight, float centerWeight, 
        NativeArray<float3> fishesVelocity, NativeArray<float3> fishesPosition)
    {
        _speed = speed;
        _turningSpeed = turningSpeed;
        _flockDistance = flockDistance;

        _stearingWeight = stearingWeight;
        _flockingWeight = flockingWeight;
        _centerWeight = centerWeight;

        _fishesVelocity = fishesVelocity;
        _fishesPosition = fishesPosition;

        deltaTime = 0;
    }


    public void Execute(int index, TransformAccess transform)
    {
        //Debug.Log(_fishesPosition[index]);
        _fishesVelocity[index] = _speed * deltaTime * math.forward(transform.rotation);

        float3 pos = (float3)transform.position;

        int flockCount = 0;
        float3 averageVelocity = new(), flockCenter = new();
        for (int i = 0; i < _fishesVelocity.Length; i++)
        {
            float distance = math.distance(_fishesPosition[i], pos);
            if (distance < _flockDistance && distance != 0)
            {
                _fishesVelocity[index] += math.normalize(pos - _fishesPosition[i]) * (_flockDistance - distance) * _stearingWeight;
                averageVelocity += _fishesVelocity[i];
                flockCount++;

                flockCenter += _fishesPosition[i];
            }
        }


        if (flockCount > 0)
        {
            _fishesVelocity[index] += (averageVelocity / flockCount) * _flockingWeight;
            _fishesVelocity[index] += ((flockCenter / flockCount) - pos) * _centerWeight;
        }

        //Debug.Log(_fishesVelocity[index]);
        //transform.position = Vector3.MoveTowards(transform.position, transform.position + (Vector3)_fishesVelocity[index], _speed * deltaTime * 2);

        transform.SetPositionAndRotation(Vector3.MoveTowards(transform.position, (float3)transform.position + _fishesVelocity[index], _speed * deltaTime * 2),
            Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_fishesVelocity[index], math.up()), _turningSpeed * deltaTime));

        _fishesPosition[index] = transform.position;
    }
}
