using System;
using System.Globalization;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing.UI
{
	public class FishingUI : MonoBehaviour
	{
		[SerializeField] private FishingController m_fishingController;
		[SerializeField] private GameObject m_idleUI;
		[SerializeField] private GameObject m_castUI;
		[SerializeField] private GameObject m_fishingSequenceUI;
		[SerializeField] private Image m_castChargeBar;
		[SerializeField] private Image m_rodHealthBar;
		[SerializeField] private Color m_fullHealthColor;
		[SerializeField] private Color m_lowHealthColor;
		[SerializeField] private Image m_fishStaminaBar;
		[SerializeField] private Image m_inputImage;
		[SerializeField] private GameObject m_stunnedVisual;
		[SerializeField] private RectTransform m_inputParent;
		[SerializeField] private RectTransform m_inputTransform;
		[SerializeField] private RectTransform m_fishVelocityTransform;
		[SerializeField] private RectTransform m_fishInputTransform;
		[SerializeField] private TMPro.TMP_Text m_fishInputAlignmentText;
		[SerializeField] private TMPro.TMP_Text m_lastCatchNameText;
		[SerializeField] private TMPro.TMP_Text m_lastCatchResultText;

		private Vector2 m_inputBounds;
		private bool m_wasFishStunned;

		private void Start()
		{
			m_inputBounds = m_inputParent.sizeDelta;
			m_fishingController.StateChange += OnStateChanged;
			OnStateChanged(FishingController.State.Idle);
		}

		private void OnDestroy()
		{
			m_fishingController.StateChange -= OnStateChanged;
		}

		private void OnStateChanged(FishingController.State newState)
		{
			m_idleUI.SetActive(newState == FishingController.State.Idle);
			m_castUI.SetActive(newState == FishingController.State.Cast);
			m_fishingSequenceUI.SetActive(newState == FishingController.State.FishingSequence);

			if (newState is FishingController.State.Idle)
			{
				m_lastCatchNameText.text = m_fishingController.LastCatchType?.name;
				m_lastCatchResultText.text = m_fishingController.LastCatchResult.ToString();
			}
		}

		private void LateUpdate()
		{
			switch (m_fishingController.CurrentState)
			{
				case FishingController.State.Idle:
					m_castChargeBar.fillAmount = m_fishingController.FishingRodHelper.ChargeCompletion;
					break;
				case FishingController.State.Cast:
					break;
				case FishingController.State.FishingSequence:
					m_rodHealthBar.fillAmount = m_fishingController.CurrentRodHealth;
					m_rodHealthBar.color = Color.Lerp(m_lowHealthColor, m_fullHealthColor, m_fishingController.CurrentRodHealth);
					m_fishStaminaBar.fillAmount = m_fishingController.CurrentFishStaminaPercent;
					
					Vector3 fishVel = m_fishingController.FishingRodHelper.BobberFloatation.Velocity.normalized;
					Vector3 fishInput = m_fishingController.FishingRodHelper.BobberFloatation.InputVelocity.normalized;
					m_inputTransform.localPosition = GetRelativeInputSquarePosition(m_fishingController.CurrentInput);
					m_fishVelocityTransform.localPosition = GetRelativeInputSquarePosition(new Vector2(fishVel.x, fishVel.z));
					m_fishInputTransform.localPosition = GetRelativeInputSquarePosition(new Vector2(fishInput.x, fishInput.z));
					
					m_inputImage.color = m_fishingController.IsInputAlignedWithFish ? Color.dodgerBlue : Color.white;

					if (m_wasFishStunned != m_fishingController.IsFishStunned)
					{
						m_stunnedVisual.SetActive(m_fishingController.IsFishStunned);
						m_wasFishStunned = m_fishingController.IsFishStunned;
					}

					m_fishInputAlignmentText.text = Math.Round(m_fishingController.InputToFishAlignment, 2).ToString("P");
					
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private Vector2 GetRelativeInputSquarePosition(Vector2 input)
		{
			float xVelShifted = input.x + 1;
			float zVelShifted = input.y + 1;

			float xLerp = math.saturate(math.unlerp(0, 2, xVelShifted));
			float yLerp = math.saturate(math.unlerp(0, 2, zVelShifted));

			return new Vector2(m_inputBounds.x * xLerp, m_inputBounds.y * yLerp);
		}
	}
}