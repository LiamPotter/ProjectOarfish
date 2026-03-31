using System;
using Unity.Mathematics;
using UnityEngine;

namespace Fishing
{
	[Serializable]
	public class CrankController
	{
		[SerializeField] private Transform m_crankTransform;
		[SerializeField] private float m_minSpeed;
		[SerializeField] private float m_maxSpeed;


		private float m_wantedSpeed;
		private float m_previousSpeed;
		private float m_currentSpeed;
		private float m_currentLerpTimer;
		private float m_lerpDuration;
		private bool m_isSpinning;
		private bool m_isForward;

		public void Update(float dt)
		{
			if (m_isSpinning == false)
			{
				return;
			}
			
			m_currentLerpTimer += dt;
			float lerpValue = math.saturate(math.unlerp(0, m_lerpDuration, m_currentLerpTimer));
			m_currentSpeed = math.lerp(m_previousSpeed, m_wantedSpeed, lerpValue);
			m_currentSpeed = Mathf.Clamp(m_currentSpeed, m_minSpeed, m_maxSpeed);

			m_crankTransform.Rotate(Vector3.right, m_currentSpeed * dt);
			
			if (Mathf.Approximately(lerpValue, 1) && math.abs(m_currentSpeed) <= 0.01f)
			{
				m_currentSpeed = 0;
				m_isSpinning = false;
			}
		}

		public void SetSpeed(float wantedSpeed, float lerpDuration)
		{
			m_wantedSpeed = wantedSpeed * 100;
			m_previousSpeed = m_currentSpeed;

			m_lerpDuration = lerpDuration;
			m_currentLerpTimer = 0;
			m_isSpinning = true;
		}
	}
}