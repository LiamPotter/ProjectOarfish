using System;
using UnityEngine;

namespace Fishing.Fish
{
	public class FishBrain : MonoBehaviour
	{
		[SerializeField] private FishSwimController m_swimController;

		public FishSwimController SwimController => m_swimController;
		public Action<FishBrain> ReachedTarget;

		private void Start()
		{
			m_swimController.ReachedTarget += OnTargetReached;
		}

		private void OnDestroy()
		{
			m_swimController.ReachedTarget -= OnTargetReached;
		}

		
		private void OnTargetReached()
		{
			ReachedTarget?.Invoke(this);
		}
	}
}