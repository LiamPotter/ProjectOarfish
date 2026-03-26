using System;
using Fishing.Fish;
using Unity.Mathematics;
using UnityEngine;

namespace Fishing
{
    public class FishingController : MonoBehaviour
    {
        public enum State
        {
            Idle,
            Cast,
            FishingSequence
        }
        [SerializeField] private FishingRodHelper m_rodHelper;
        [SerializeField] private BoatCamera m_boatCamera;
        [SerializeField] private Animator m_characterAnimator;
        [Header("Fishing Sequence")]
        [SerializeField] private GameObject m_splashEffectPrefab;
        [SerializeField] private FishConfig m_testFishConfig;
        [SerializeField] private float m_reelForce = 0.2f;
        [SerializeField] private float m_sequenceCompleteDistance = 0.5f;
        [Tooltip("How much the rod's health depletes if the player is performing the sequence incorrectly.")]
        [SerializeField] private float m_rodHealthLossPerSecond = 0.25f;
        [SerializeField] private float m_rodHealthGainPerSecond = 0.1f;
        [SerializeField] private float m_maxInputDelta = 10f;


        private GameObject m_splashEffectParent;
        private ParticleSystem m_splashEffect;
        private int m_currentFishStepIndex;
        private float m_currentFishTimer;
        private float m_currentFishStunTimer;
        private float m_rodHealth = 1f;
        private bool m_isReeling;
        private float m_completeDistanceSqr;
        private Vector2 m_currentFishingInput = new();
        private State m_currentState = State.Idle;
        
        public State CurrentState
        {
            get => m_currentState;
            private set
            {
                m_currentState = value;
                StateChange?.Invoke(value);
            }
        }

        public FishingRodHelper FishingRodHelper => m_rodHelper;
        
        private int FishingStartHash => Animator.StringToHash("FishingStart");
        private int FishingEndHash => Animator.StringToHash("FishingEnd");
        private FishBehaviourValues.Value CurrentFishStep => m_testFishConfig.Behaviour.Values[m_currentFishStepIndex];
        private bool FishStunned => m_currentFishTimer >= CurrentFishStep.m_duration && m_currentFishStunTimer < CurrentFishStep.m_stunDuration;
        private Vector2 PositionFlat => new Vector2(transform.position.x, transform.position.z);
        private Vector2 BobberPositionFlat => new Vector2(m_rodHelper.BobberTransform.position.x, m_rodHelper.BobberTransform.position.z);
        private float BobberDistanceSqr => math.distancesq(PositionFlat,BobberPositionFlat);

        public Action FishingSequenceStart;
        public Action FishingSequenceEnd;
        public Action<State> StateChange;

        private void Start()
        {
            m_splashEffectParent = Instantiate(m_splashEffectPrefab);
            m_splashEffect = m_splashEffectParent.GetComponentInChildren<ParticleSystem>();
        }
        
        private void Update()
        {
            if (CurrentState is State.Idle)
            {
                if (Input.GetMouseButton(0))
                {
                    m_rodHelper.ChargeThrow(Time.deltaTime);
                }

                if (Input.GetMouseButtonUp(0))
                {
                    CurrentState = State.Cast;
                    m_rodHelper.ThrowBobber();
                }
            }

            if (CurrentState is State.Cast or State.FishingSequence)
            {
                if (CurrentState is State.FishingSequence)
                {
                    UpdateFishingSequence(Time.deltaTime);
                }
                else
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        EnterFishingSequence();
                    }
                }
                if (Input.GetMouseButtonDown(1))
                {
                    m_rodHelper.ReturnBobber();
                    EndFishingSequence(false);
                    FishingSequenceEnd?.Invoke();
                }
            }
        }

        private void EnterFishingSequence()
        {
            CurrentState = State.FishingSequence;
            m_currentFishTimer = 0f;
            m_currentFishStunTimer = 0f;
            m_rodHealth = 1f;
            m_currentFishStepIndex = 0;
            m_completeDistanceSqr = m_sequenceCompleteDistance*m_sequenceCompleteDistance;
            
            m_boatCamera.SetCameraMode(BoatCamera.CameraMode.FirstPersonTrack);
            
            m_splashEffect.Play(true);
            FishingSequenceStart?.Invoke();
        }

        private void UpdateFishingSequence(float dt)
        {
            m_isReeling = Input.GetMouseButton(0);

            Vector2 mouseDelta = Input.mousePositionDelta;
            Vector2 scaled = new Vector2(mouseDelta.x/m_maxInputDelta, mouseDelta.y/m_maxInputDelta);
            
            m_currentFishingInput += scaled;
            m_currentFishingInput = math.clamp(m_currentFishingInput,-Vector2.one,Vector2.one);
            
            Debug.Log($"Mouse Delta: {mouseDelta} (scaled:  {scaled}). Current Input: {m_currentFishingInput}");
            
            m_currentFishTimer += dt;
            if (m_currentFishTimer >= CurrentFishStep.m_duration)
            {
                if (m_currentFishStunTimer <= 0.01f)
                {
                    m_splashEffect.Stop();
                }
                m_currentFishStunTimer += dt;

                if (m_currentFishStunTimer >= CurrentFishStep.m_stunDuration)
                {
                    m_currentFishStepIndex = m_testFishConfig.Behaviour.IncrementStep(m_currentFishStepIndex);
                    m_currentFishTimer = 0;
                    m_currentFishStunTimer = 0;
                    m_splashEffect.Play(true);
                }
            }
            else
            {
                if (m_isReeling)
                {
                    m_rodHealth -= dt * m_rodHealthLossPerSecond;
                }
                else
                {
                    m_rodHealth += dt * m_rodHealthGainPerSecond;
                }

                m_splashEffectParent.transform.position = m_rodHelper.BobberTransform.position;
                Vector3 velocity = math.normalize(m_rodHelper.BobberFloatation.Velocity);
                velocity.y = 0;
                m_splashEffectParent.transform.forward = velocity;
            }
            
            m_rodHelper.UpdateFishingSequence(FishStunned,m_isReeling,m_reelForce, CurrentFishStep);

            if (m_rodHealth <= 0f)
            {
                EndFishingSequence(false);
                return;
            }
            
            if (BobberDistanceSqr <= m_completeDistanceSqr)
            {
                EndFishingSequence(true);
            }
        }

        private void EndFishingSequence(bool success)
        {
            Debug.Log($"Fishing Sequence End! Success? {success}");
            m_rodHelper.ReturnBobber();
            FishingSequenceEnd?.Invoke();
            m_boatCamera.SetCameraMode(BoatCamera.CameraMode.FirstPerson);
            CurrentState = State.Idle;
            m_splashEffect.Stop();
        }
    }
}
