using System;
using UnityEngine;

namespace Fishing.Fish
{
	[Serializable]
	public struct FishBehaviourValues
	{
		[Serializable]
		public struct Value
		{
			[Tooltip("The direction the fish will pull at this step. Relative to the camera's view.")]
			public Vector2 m_direction;
			public float m_force;
			public float m_neededPullDuration;
			public float m_maxDuration;
			[Tooltip("How long the fish is stunned once this step is completed.")]
			public float m_stunDuration;
			
			public Vector3 ForceVector=> new Vector3(m_direction.x,0,m_direction.y)*m_force;
		}

		[Tooltip("The distance this fish will try to reach before starting the fishing seqeuence.")]
		[field: SerializeField] public float WantedStartDistance { get; private set; }
		[Tooltip("A fish will run through these values sequentially while being fished.")]
		[field: SerializeField] public Value[] Values { get; private set; }

		public int IncrementStep(int currentStep)
		{
			if (currentStep+1 >= Values.Length)
			{
				return currentStep;
			}
			
			return currentStep+1;
		}
	}
}