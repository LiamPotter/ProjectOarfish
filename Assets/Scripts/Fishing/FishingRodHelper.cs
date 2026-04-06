using System;
using Fishing.Fish;
using Unity.Mathematics;
using UnityEngine;

namespace Fishing
{
	public class FishingRodHelper : MonoBehaviour
	{
		[SerializeField] private Camera m_mainCamera;
		[SerializeField] private FishingController m_fishingController;
		[SerializeField] private FishingBobberFloatation m_fishingBobberFloatation;

		[Header("Fishing Bob"), SerializeField] private Transform m_fishingBobParent;
		[SerializeField] private Rigidbody m_fishingBobRigidbody;

		[Header("Throw Values"), SerializeField] private float m_maxThrowChargeTime = 2f;
		[SerializeField] private Vector2 m_throwForceRange = new Vector2(2, 5);
		[SerializeField] private Vector2 m_throwForceUpwardBiasRange = new Vector2(1, 3);
		[Header("Crank Control")] [field: SerializeField] public CrankController CrankController { get; private set; } = new();

		private float m_chargeTimer;
		private Vector3 m_initialLocalPos;

		public float ChargeCompletion => math.saturate(m_chargeTimer / m_maxThrowChargeTime);
		public Transform BobberTransform => m_fishingBobberFloatation.transform;
		public FishingBobberFloatation BobberFloatation => m_fishingBobberFloatation;
		public Vector3 InitialLocalPos => m_initialLocalPos;

		private Vector3 DirectionToCamera
		{
			get
			{
				Vector3 dir = m_mainCamera.transform.position - m_fishingBobberFloatation.transform.position;
				dir.y = 0;
				return dir;
			}
		}

		public Action BobberEnteredWater;

		private void Start()
		{
			if (m_mainCamera == null)
			{
				m_mainCamera = Camera.main;
			}

			m_fishingBobberFloatation.EnteredWater += OnWaterEntered;
			m_initialLocalPos = transform.localPosition;
		}

		private void Update()
		{
			CrankController.Update(Time.deltaTime);
		}
		
		private void OnDestroy()
		{
			m_fishingBobberFloatation.EnteredWater -= OnWaterEntered;
		}

		private void OnWaterEntered()
		{
			BobberEnteredWater?.Invoke();	
		}
		
		
		public void UpdateFishingSequence(bool isStunned, bool isReeling, bool isAligned, float reelForce, FishBehaviourValues.Value currentValue)
		{
			Vector3 forceToAdd = isStunned
				? isReeling ? -Vector3.forward * reelForce : Vector3.zero
				: (isAligned ? currentValue.ForceVector * 0.75f : currentValue.ForceVector);
			bool allowApproach = isStunned && isReeling;

			m_fishingBobberFloatation.AddForce(forceToAdd, allowApproach);
		}


		public void ChargeThrow(float dt)
		{
			m_chargeTimer += dt;
		}
		
		public void ThrowBobber()
		{
			float force = math.lerp(m_throwForceRange.x, m_throwForceRange.y, ChargeCompletion);
			float upBias = math.lerp(m_throwForceUpwardBiasRange.x, m_throwForceUpwardBiasRange.y, ChargeCompletion);

			Vector3 throwForceTotal = m_mainCamera.transform.forward * force;
			throwForceTotal += Vector3.up * upBias;

			m_fishingBobRigidbody.transform.SetParent(null);
			SetBobberPhysicsActive(true);
			m_fishingBobRigidbody.AddForce(throwForceTotal, ForceMode.Impulse);
		}

		public void ReturnBobber()
		{
			m_fishingBobRigidbody.transform.SetParent(m_fishingBobParent);
			m_fishingBobRigidbody.transform.localPosition = Vector3.zero;
			m_fishingBobRigidbody.transform.localRotation = Quaternion.identity;
			SetBobberPhysicsActive(false);
			m_fishingBobberFloatation.Reset();
			m_chargeTimer = 0;
		}

		public void SetRodPosition(Vector3 localPosition)
		{
			transform.localPosition = localPosition;
		}

		public void ResetRodPosition()
		{
			transform.localPosition = m_initialLocalPos;
		}
		
		public void SetBobberPhysicsActive(bool isActive) => m_fishingBobRigidbody.isKinematic = !isActive;
	}
}