using System;
using Crest;
using Fishing.Fish;
using Unity.Mathematics;
using UnityEngine;
using Utils;

namespace Fishing
{
	public class FishingController : MonoBehaviour
	{
		public enum State
		{
			Idle,
			ChargingCast,
			Cast,
			WaitingForFish,
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
		[SerializeField] private BoatProbes m_boatController;
		[SerializeField] private Animator m_characterAnimator;
		[SerializeField] private Animator m_rodAnimator;
		[Header("Fishing Sequence")] [SerializeField] private GameObject m_splashEffectPrefab;
		[SerializeField] private FishConfig m_testFishConfig;
		[SerializeField] private ObjectShaker m_objectShaker;
		[SerializeField] private float m_reelForce = 0.2f;
		[SerializeField] private float m_sequenceCompleteDistance = 0.5f;

		[Tooltip("How much the rod's health depletes if the player is reeling incorrectly.")] [SerializeField]
		private float m_rodHealthLossPerSecondFromReeling = 0.25f;

		[Tooltip("How much the rod's health depletes if the player is aligned incorrectly.")] [SerializeField]
		private float m_rodHealthLossPerSecondFromAlignment = 0.15f;

		[SerializeField] private float m_rodHealthGainPerSecond = 0.1f;
		[SerializeField] private float m_maxInputDelta = 10f;
		[SerializeField] private float m_minimumInputToFishAlignment = 0.75f;
		[SerializeField] private float m_inputToFishAlignmentDamage = 0f;
		[SerializeField] private float m_maxSequenceStartDuration = 2f;
		[SerializeField] private float m_sequenceStartForce = 2f;

		private FishConfig m_currentFish;
		private GameObject m_splashEffectParent;
		private ParticleSystem m_splashEffect;
		private int m_currentFishStepIndex;
		private float m_currentFishTimer;
		private float m_currentStaminaFishTimer;
		private float m_currentFishStunTimer;
		private float m_rodHealth = 1f;

		private float m_completeDistanceSqr;
		private Vector2 m_currentFishingInput = new();
		private State m_currentState = State.Idle;
		private bool m_hasReachedStartingDistance;
		private FishGroupController[] m_fishGroups;
		private FishGroupController m_activeFishGroup;


		public State CurrentState
		{
			get => m_currentState;
			private set
			{
				if (m_currentState != value)
				{
					Debug.Log($"New State is {value}");
					bool isNotIdle = value is not State.Idle;
					bool isFishing = value is State.WaitingForFish or State.FishingSequence or State.Cast;
					m_rodAnimator.SetBool(WindupStartHash, isNotIdle);
					m_rodAnimator.SetBool(FishingStartHash, isFishing);
					m_boatController.MovementEnabled = value is State.Idle;
				}

				m_currentState = value;
				StateChange?.Invoke(value);
			}
		}

		public EndReason LastCatchResult { get; private set; } = EndReason.Cancelled;
		public FishConfig LastCatchType { get; private set; } = null;

		public float InputToFishAlignment { get; private set; }
		public bool IsInputAlignedWithFish => InputToFishAlignment >= m_minimumInputToFishAlignment;
		public FlipActionBool IsReeling { get; private set; }
		public FlipActionBool IsFishStunned { get; private set; }
		public FishingRodHelper FishingRodHelper => m_rodHelper;
		public Vector2 CurrentInput => m_currentFishingInput;

		public float CurrentRodHealth
		{
			get => m_rodHealth;
			private set => m_rodHealth = math.saturate(value);
		}

		public float CurrentFishStaminaPercent => 1 - math.saturate(m_currentStaminaFishTimer / math.max(0.001f, CurrentFishStep.m_neededPullDuration));


		private int FishingStartHash => Animator.StringToHash("IsFishing");
		private int WindupStartHash => Animator.StringToHash("WindupActive");
		private int FishingVerticalHash => Animator.StringToHash("Vertical");
		private int FishingHorizontalHash => Animator.StringToHash("Horizontal");
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
			IsReeling = new FlipActionBool(false, ReelingFlipDetected);
			IsFishStunned = new FlipActionBool(false, FishStunnedFlipDetected);
			m_fishGroups = FindObjectsByType<FishGroupController>(FindObjectsSortMode.None);

			m_rodHelper.BobberEnteredWater += OnBobberEnteredWater;
		}

		private void OnDestroy()
		{
			m_rodHelper.BobberEnteredWater -= OnBobberEnteredWater;
		}

		private void OnBobberEnteredWater()
		{
			if (m_currentState == State.Idle)
			{
				return;
			}
			
			m_activeFishGroup = SelectClosestFishGroup();

			if (m_activeFishGroup == null)
			{
				return;
			}

			CurrentState = State.WaitingForFish;

			m_activeFishGroup.SelectFishFromDistance(m_rodHelper.BobberTransform);
		}

		private void Update()
		{
			if (CurrentState is State.Idle or State.ChargingCast)
			{
				if (Input.GetMouseButton(0))
				{
					CurrentState = State.ChargingCast;
					m_rodHelper.ChargeThrow(Time.deltaTime);
				}

				if (Input.GetMouseButtonUp(0))
				{
					CurrentState = State.Cast;
				}
			}

			if (CurrentState is State.Cast or State.FishingSequence or State.WaitingForFish)
			{
				if (CurrentState is State.FishingSequence)
				{
					UpdateFishingSequence(Time.deltaTime);
				}
				else
				{
					if (m_activeFishGroup != null && m_activeFishGroup.HasFishReachedTarget && Input.GetMouseButtonDown(0))
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
			CurrentRodHealth = 1f;
			m_currentFishStepIndex = 0;
			m_hasReachedStartingDistance = false;
			m_completeDistanceSqr = m_sequenceCompleteDistance * m_sequenceCompleteDistance;

			m_boatCamera.SetCameraMode(BoatCamera.CameraMode.FirstPersonTrack);

			m_fishingLine.tautnessOverride = 1f;
			m_fishingLine.enableBobAnimation = false;

			Vector3 centeredRodPos = m_rodHelper.InitialLocalPos;
			centeredRodPos.x = 0.1f;
			m_rodHelper.SetRodPosition(centeredRodPos);

			m_rodHelper.CrankController.SetSpeed(25, 0.15f);
			m_splashEffect.Play(true);
			FishingSequenceStart?.Invoke();
		}

		private void UpdateFishingSequence(float dt)
		{
			m_activeFishGroup.CurrentFish.transform.position = m_rodHelper.BobberTransform.position;
			if (m_hasReachedStartingDistance == false)
			{
				m_currentFishTimer += dt;
				float distanceToBobber = math.distance(m_boatCamera.transform.position, m_rodHelper.BobberTransform.position);

				if (distanceToBobber < m_currentFish.Behaviour.WantedStartDistance && m_currentFishTimer < m_maxSequenceStartDuration)
				{
					m_rodHelper.BobberFloatation.AddForce(Vector3.forward * m_sequenceStartForce);
					UpdateSplashEffect();
				}
				else
				{
					Vector3 velocity = m_rodHelper.BobberFloatation.Velocity;
					velocity.y = 0;

					m_rodHelper.BobberFloatation.AddTorqueForce(-velocity * 2, false);
					m_rodHelper.CrankController.SetSpeed(0, 0.5f);
					m_hasReachedStartingDistance = true;
					m_currentFishTimer = 0;
				}

				return;
			}


			Vector2 mouseDelta = Input.mousePositionDelta;
			Vector2 scaled = new Vector2(mouseDelta.x / m_maxInputDelta, mouseDelta.y / m_maxInputDelta);

			m_currentFishingInput += scaled;
			m_currentFishingInput = math.clamp(m_currentFishingInput, -Vector2.one, Vector2.one);

			m_rodAnimator.SetFloat(FishingVerticalHash, m_currentFishingInput.y);
			m_rodAnimator.SetFloat(FishingHorizontalHash, -m_currentFishingInput.x);

			Vector2 flatFishInput = new Vector2(m_rodHelper.BobberFloatation.InputVelocity.x, m_rodHelper.BobberFloatation.InputVelocity.z).normalized;
			InputToFishAlignment = math.dot(m_currentFishingInput, flatFishInput);

			IsReeling.Value = Input.GetMouseButton(0);

			bool isReelingEarly = IsReeling && IsFishStunned == false;
			bool isPoorlyAligned = InputToFishAlignment <= m_inputToFishAlignmentDamage && IsFishStunned == false;

			if (isReelingEarly || isPoorlyAligned)
			{
				float damage = isReelingEarly ? m_rodHealthLossPerSecondFromReeling : m_rodHealthLossPerSecondFromAlignment;
				CurrentRodHealth -= dt * damage;
				m_objectShaker.SetIntensity(1f);
			}
			else
			{
				CurrentRodHealth += dt * m_rodHealthGainPerSecond;
				m_objectShaker.SetIntensity(0f);
			}
			
			m_fishingLine.UpdateHealthColor(CurrentRodHealth);

			if (IsFishStunned == false)
			{
				m_currentFishTimer += dt;
			}

			if (IsInputAlignedWithFish)
			{
				m_currentStaminaFishTimer += dt;
			}

			IsFishStunned.Value = m_currentStaminaFishTimer >= CurrentFishStep.m_neededPullDuration;

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
				UpdateSplashEffect();
			}

			m_rodHelper.UpdateFishingSequence(IsFishStunned, IsReeling, IsInputAlignedWithFish, m_reelForce, CurrentFishStep);


			if (CurrentRodHealth <= 0f)
			{
				EndFishingSequence(EndReason.Failure);
				return;
			}

			if (IsFishStunned && BobberDistanceSqr <= m_completeDistanceSqr)
			{
				EndFishingSequence(EndReason.Success);
			}
		}

		private void UpdateSplashEffect()
		{
			m_splashEffectParent.transform.position = m_rodHelper.BobberTransform.position;
			Vector3 velocity = math.normalize(m_rodHelper.BobberFloatation.Velocity);
			velocity.y = 0;
			m_splashEffectParent.transform.forward = velocity;
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
			m_rodAnimator.SetFloat(FishingVerticalHash, 0);
			m_rodAnimator.SetFloat(FishingHorizontalHash, 0);
			m_rodHelper.ResetRodPosition();
			m_rodHelper.CrankController.SetSpeed(0, 0.5f);
			m_objectShaker.SetIntensity(0f);
			m_fishingLine.UpdateHealthColor(1f);

			foreach (var fishGroup in m_fishGroups)
			{
				fishGroup.ResetFish();
			}
		}

		private void ReelingFlipDetected(bool newValue)
		{
			SetCrankSpeed(newValue, IsFishStunned);
		}

		private void FishStunnedFlipDetected(bool newValue)
		{
			SetCrankSpeed(IsReeling, newValue);
		}

		private void SetCrankSpeed(bool isReeling, bool isFishStunned)
		{
			m_rodHelper.CrankController.SetSpeed(isReeling ? isFishStunned ? -10f : -2.5f : 0f, isFishStunned ? 0.15f : 0.25f);
		}

		private FishGroupController SelectClosestFishGroup()
		{
			int bestFishGroupIndex = -1;
			float closestFishGroupDistance = float.MaxValue;
			for (int i = 0; i < m_fishGroups.Length; i++)
			{
				if (m_fishGroups[i].CanFishHere(m_rodHelper.BobberTransform) == false)
				{
					continue;
				}

				float distance = math.distancesq(m_rodHelper.BobberTransform.position, m_fishGroups[i].transform.position);

				if (distance < closestFishGroupDistance)
				{
					bestFishGroupIndex = i;
					closestFishGroupDistance = distance;
				}
			}

			if (bestFishGroupIndex == -1)
			{
				return null;
			}

			return m_fishGroups[bestFishGroupIndex];
		}
	}
}