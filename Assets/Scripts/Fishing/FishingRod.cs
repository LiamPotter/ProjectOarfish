using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class FishingRod : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform fishingBobHolder;
    [SerializeField] private FishingBob fishingBob;
    
    [SerializeField] private Vector2 throwDistanceRange = new Vector2(1f,10f);
    [SerializeField] private Vector2 throwTimeBounds = new Vector2(0, 1.5f);

    private float throwTimer;
    private float throwCompletion;
   
    private void Start()
    {
        fishingBob.Initialize(fishingBobHolder);
    }

    private void Update()
    {
        if (Input.GetMouseButton(0))
        {
            throwTimer+=Time.deltaTime;
            throwCompletion = math.saturate(math.unlerp(throwTimeBounds.x,throwTimeBounds.y,throwTimer));
        }

        if (Input.GetMouseButtonUp(0))
        {
            switch (fishingBob.CurrentState)
            {
                case FishingBob.State.Idle:
                    Vector3 dir = cameraTransform.forward;
                    fishingBob.Throw(dir, math.lerp(throwDistanceRange.x,throwDistanceRange.y,throwCompletion));
                    break;
                case FishingBob.State.InAir:
                case FishingBob.State.Fishing:
                    fishingBob.Return();
                    break;
                case FishingBob.State.Returning:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            throwTimer = 0;
            throwCompletion = 0;
        }
    }
}
