using System;
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

        private void Start()
        {
            m_fishingController.StateChange += OnStateChanged;
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
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
           
        }
    }
}
