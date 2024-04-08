using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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


    public GameObject fishPrefab;
    public int initialFishSpawn = 5;
    public float spawnRadius = 10;


    public List<GameObject> fishes;

    private FishJob job;
    public NativeArray<float3> fishPositions;
    public NativeArray<float3> fishVelocities;
    public TransformAccessArray fishTransforms;


    void Start()
    {
        fishPositions = new NativeArray<float3>(initialFishSpawn, Allocator.Persistent);
        fishVelocities = new NativeArray<float3>(initialFishSpawn, Allocator.Persistent);
        fishTransforms = new TransformAccessArray(initialFishSpawn);
        for (int i = 0; i < initialFishSpawn; i++)
        {
            Vector3 pos = Random.insideUnitSphere * spawnRadius;
            GameObject obj = Instantiate(fishPrefab, pos, Random.rotation);
            fishPositions[i] = (float3)pos;
            fishVelocities[i] = (float3)obj.transform.forward;
            fishTransforms.Add(obj.transform);

            fishes.Add(obj);
        }

        job = new FishJob(speed, turningSpeed, flockDistance, stearingWeight, flockingWeight, centerWeight, fishVelocities, fishPositions);

        /*job.deltaTime = Time.deltaTime;
        JobHandle handle = job.Schedule(fishTransforms);

        handle.Complete();*/
    }


    void Update()
    {
        job.deltaTime = Time.deltaTime;
        job._stearingWeight = stearingWeight;
        job._flockingWeight = flockingWeight;
        job._centerWeight = centerWeight;

        JobHandle handle = job.Schedule(fishTransforms);

        handle.Complete();
    }

    private void OnDestroy()
    {
        fishPositions.Dispose();
        fishVelocities.Dispose();
        fishTransforms.Dispose();
    }
}
