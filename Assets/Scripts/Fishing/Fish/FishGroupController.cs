using System;
using Unity.Mathematics;
using UnityEngine;

namespace Fishing.Fish
{
    public class FishGroupController : MonoBehaviour
    {   
        [field:SerializeField] public float MinDistanceToFish { get; set; } = 5f;
        [SerializeField] private FishBrain[] m_fishes;
        
        private FishBrain m_selectedFish;
        private Transform m_targetTransform;
        private bool m_foundFish;

        public bool HasFishReachedTarget { get; private set; }
        public FishBrain CurrentFish => m_selectedFish;
        public Action FishReachedTarget;

        private void Start()
        {
            foreach (FishBrain fishBrain in m_fishes)
            {
                fishBrain.ReachedTarget += OnFishTargetReached;
            }
        }

        private void OnDestroy()
        {
            foreach (FishBrain fishBrain in m_fishes)
            {
                fishBrain.ReachedTarget -= OnFishTargetReached;
            }
        }

        private void Update()
        {
            if (m_foundFish == false)
            {
                return;
            }

            m_selectedFish.SwimController.SetTarget(m_targetTransform.position);
            m_selectedFish.SwimController.SetWanderActive(false);
        }
        
        private void OnFishTargetReached(FishBrain fish)
        {
            if (fish == m_selectedFish)
            {
                FishReachedTarget?.Invoke();
                HasFishReachedTarget = true;
            }
        }

        public bool CanFishHere(Transform bobberTransform) => math.distance(transform.position, bobberTransform.position) <= MinDistanceToFish;
    
        public void SelectFishFromDistance(Transform positionToReach)
        {
            Debug.Log("Selecting New Fish.");
            HasFishReachedTarget = false;
            
            int closestFishIndex = -1;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < m_fishes.Length; i++)
            {
                float distance = math.distancesq(positionToReach.position, m_fishes[i].transform.position);
                if (distance < closestDistance)
                {
                    closestFishIndex = i;
                    closestDistance = distance;
                }
            }

            if (closestFishIndex == -1)
            {
                m_foundFish = false;
                return;
            }

            m_targetTransform = positionToReach;
        
            m_selectedFish =  m_fishes[closestFishIndex];
            m_foundFish = true;
        }

        public void ResetFish()
        {
            foreach (var fish in m_fishes)
            {
                fish.SwimController.SetWanderActive(true);
            }
            
            m_selectedFish = null;
            m_foundFish = false;
            HasFishReachedTarget = false;
        }
    }
}
