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

		public enum EndReason
		{
			Cancelled = 0,
			Success = 1,
			Failure = 2
		}

		[SerializeField] private FishingLine m_fishingLine;
		[SerializeField] private FishingRodHelper m_rodHelper;
		[SerializeField] private BoatCamera m_boatCamera;
		[SerializeField] private Animator m_characterAnimator;
		[Header("Fishing Sequence")] [SerializeField] private GameObject m_splashEffectPrefab;
		[SerializeField] private FishConfig m_testFishConfig;
		[SerializeField] private float m_reelForce = 0.2f;
		[SerializeField] private float m_sequenceCompleteDistance = 0.5f;

		[Tooltip("How much the rod's health depletes if the player is performing the sequence incorrectly.")] [SerializeField]
		private float m_rodHealthLossPerSecond = 0.25f;

		[SerializeField] private float m_rodHealthGainPerSecond = 0.1f;
		[SerializeField] private float m_maxInputDelta = 10f;
		[SerializeField] private float m_minimumInputToFishAlignment = 0.75f;


		private FishConfig m_currentFish;
		private GameObject m_splashEffectParent;
		private ParticleSystem m_splashEffect;
		private int m_currentFishStepIndex;
		private float m_currentFishTimer;
		private float m_currentStaminaFishTimer;
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

		public EndReason LastCatchResult { get; private set; } = EndReason.Cancelled;
		public FishConfig LastCatchType { get; private set; } = null;
		
		public float InputToFishAlignment { get; private set; }
		public bool IsInputAlignedWithFish => InputToFishAlignment >= m_minimumInputToFishAlignment;
		public bool IsFishStunned { get; private set; }
		public FishingRodHelper FishingRodHelper => m_rodHelper;
		public Vector2 CurrentInput => m_currentFishingInput;
		public float CurrentRodHealth => m_rodHealth;
		public float CurrentFishStaminaPercent => 1 - math.saturate(m_currentStaminaFishTimer / math.max(0.001f, CurrentFishStep.m_neededPullDuration));


		private int FishingStartHash => Animator.StringToHash("FishingStart");
		private int FishingEndHash => Animator.StringToHash("FishingEnd");
		private FishBehaviourValues.Value CurrentFishStep => m_currentFish.Behaviour.Values[m_currentFishStepIndex];
		private Vector2 CamPositionFlat => new Vector2(m_boatCamera.transform.position.x, m_boatCamera.transform.position.z);
		private Vector2 BobberPositionFlat => new Vector2(m_rodHelper.BobberTransform.position.x, m_rodHelper.BobberTransform.position.z);
		private float BobberDistanceSqr => math.distancesq(CamPositionFlat, BobberPositionFlat);

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
						EnterFishingSequence(m_testFishConfig);
					}
				}

				if (Input.GetMouseButtonDown(1))
				{
					m_rodHelper.ReturnBobber();
					EndFishingSequence(EndReason.Cancelled);
					FishingSequenceEnd?.Invoke();
				}
			}
		}

		private void EnterFishingSequence(FishConfig fish)
		{
			m_currentFish = fish;
			CurrentState = State.FishingSequence;
			m_currentFishTimer = 0f;
			m_currentFishStunTimer = 0f;
			m_rodHealth = 1f;
			m_currentFishStepIndex = 0;
			m_completeDistanceSqr = m_sequenceCompleteDistance * m_sequenceCompleteDistance;

			m_boatCamera.SetCameraMode(BoatCamera.CameraMode.FirstPersonTrack);

			m_fishingLine.tautnessOverride = 1f;
			m_fishingLine.enableBobAnimation = false;
			m_splashEffect.Play(true);
			FishingSequenceStart?.Invoke();
		}

		private void UpdateFishingSequence(float dt)
		{
			m_isReeling = Input.GetMouseButton(0);
			
			if (m_isReeling)
			{
				if (IsFishStunned == false)
				{
					m_rodHealth -= dt * m_rodHealthLossPerSecond;
				}
			}
			else
			{
				m_rodHealth += dt * m_rodHealthGainPerSecond;
			}

			Vector2 mouseDelta = Input.mousePositionDelta;
			Vector2 scaled = new Vector2(mouseDelta.x / m_maxInputDelta, mouseDelta.y / m_maxInputDelta);

			m_currentFishingInput += scaled;
			m_currentFishingInput = math.clamp(m_currentFishingInput, -Vector2.one, Vector2.one);

			Vector2 flatFishInput = new Vector2(m_rodHelper.BobberFloatation.InputVelocity.x, m_rodHelper.BobberFloatation.InputVelocity.z).normalized;
			InputToFishAlignment = math.dot(m_currentFishingInput, flatFishInput);

			if (IsFishStunned == false)
			{
				m_currentFishTimer += dt;
			}

			if (IsInputAlignedWithFish)
			{
				m_currentStaminaFishTimer += dt;
			}

			IsFishStunned = m_currentStaminaFishTimer >= CurrentFishStep.m_neededPullDuration;

			if (IsFishStunned)
			{
				if (m_currentFishStunTimer <= 0.01f)
				{
					m_splashEffect.Stop();
				}

				m_currentFishStunTimer += dt;

				if (m_currentFishStunTimer >= CurrentFishStep.m_stunDuration)
				{
					IncrementStep();
				}
			}

			if (m_currentFishTimer >= CurrentFishStep.m_maxDuration)
			{
				IncrementStep();
			}
			else
			{
				m_splashEffectParent.transform.position = m_rodHelper.BobberTransform.position;
				Vector3 velocity = math.normalize(m_rodHelper.BobberFloatation.Velocity);
				velocity.y = 0;
				m_splashEffectParent.transform.forward = velocity;
			}

			m_rodHelper.UpdateFishingSequence(IsFishStunned, m_isReeling, IsInputAlignedWithFish, m_reelForce, CurrentFishStep);

			if (m_rodHealth <= 0f)
			{
				EndFishingSequence(EndReason.Failure);
				return;
			}

			if (IsFishStunned && BobberDistanceSqr <= m_completeDistanceSqr)
			{
				EndFishingSequence(EndReason.Success);
			}
		}

		private void IncrementStep()
		{
			m_currentFishStepIndex = m_currentFish.Behaviour.IncrementStep(m_currentFishStepIndex);
			m_currentFishTimer = 0;
			m_currentFishStunTimer = 0;
			m_currentStaminaFishTimer = 0;
			InputToFishAlignment = 0;
			m_splashEffect.Play(true);
		}

		private void EndFishingSequence(EndReason reason)
		{
			Debug.Log($"Fishing Sequence End! Reason: {reason}");
			m_rodHelper.ReturnBobber();
			FishingSequenceEnd?.Invoke();
			m_boatCamera.SetCameraMode(BoatCamera.CameraMode.FirstPerson);
			CurrentState = State.Idle;
			m_splashEffect.Stop();
			m_currentFishingInput = Vector2.zero;
			InputToFishAlignment = 0;
			LastCatchType = m_currentFish;
			LastCatchResult = reason;
			m_fishingLine.tautnessOverride = 0f;
			m_fishingLine.enableBobAnimation = true;
		}
	}
}