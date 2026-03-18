using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class FishingBob : MonoBehaviour
{
    public enum State
    {
        Idle,
        InAir,
        Fishing,
        Returning
    }

    [SerializeField] private Rigidbody bobRigidbody;
    [SerializeField] private Transform lineStartPoint;
    [SerializeField] private Transform lineEndPoint;
    [SerializeField] private LineRenderer fishingRodLine;
   
    private Transform fishingRodParent;
    private float currentTimer;
    private Vector3 startPosition;
    private Vector3 endPosition;
    public State CurrentState { get; private set; } = State.Idle;

    
    private void Update()
    {
      switch (CurrentState)
        {
            case State.Idle:
                break;
            case State.InAir:
               // UpdateInAirState();
                break;
            case State.Fishing:
                break;
            case State.Returning:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void LateUpdate()
    {
        if (CurrentState != State.Idle)
        {
            fishingRodLine.SetPosition(0, lineStartPoint.position);
            fishingRodLine.SetPosition(1, lineEndPoint.position);
            fishingRodLine.useWorldSpace = true;
        }
    }


    private void SetIdle()
    {
        transform.SetParent(fishingRodParent);
        transform.localPosition=Vector3.zero;
        transform.localRotation = Quaternion.identity;
        CurrentState = State.Idle;
        bobRigidbody.isKinematic = true;
        fishingRodLine.enabled = false;
    }
    
    public void Initialize(Transform parent)
    {
        fishingRodParent = parent;
    }
    
    public void Throw(Vector3 direction, float distance)
    {
        if (CurrentState is not State.Idle)
        {
            return;
        }

        startPosition = transform.position;
        transform.SetParent(null);
        bobRigidbody.isKinematic = false;
        
        bobRigidbody.AddForce(direction * distance, ForceMode.Impulse);

        fishingRodLine.enabled = true;
        
        CurrentState = State.InAir;
    }

    public void Return()
    {
        CurrentState = State.Returning;
        SetIdle();
    }
}
