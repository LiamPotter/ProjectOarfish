using System;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class ObjectShaker : MonoBehaviour
{
	[Flags]
	private enum Axis
	{
		None = 0,
		X = 1,
		Y = 2,
		Z = 4
	}

	[SerializeField] private Axis m_vibrationAxis = Axis.X;
	[SerializeField] private float m_amplitude; // the amount it moves

	[SerializeField, Range(0f, 1f)] private float m_intensity;
	private Vector3 m_initialPosition;
	private Vector3 m_shakeVector = new();

	private bool ShakeX => m_vibrationAxis.HasFlag(Axis.X);
	private bool ShakeY => m_vibrationAxis.HasFlag(Axis.Y);
	private bool ShakeZ => m_vibrationAxis.HasFlag(Axis.Z);

	private void Start()
	{
		m_initialPosition = transform.localPosition;
	}

	private void LateUpdate()
	{
		if (m_intensity <= 0.01f)
		{
			return;
		}
		
		m_shakeVector = Random.insideUnitSphere * (m_amplitude * m_intensity);
		m_shakeVector.x = ShakeX ? m_shakeVector.x : 0f;
		m_shakeVector.y = ShakeY ? m_shakeVector.y : 0f;
		m_shakeVector.z = ShakeZ ? m_shakeVector.z : 0f;

		transform.localPosition = m_initialPosition + m_shakeVector;
		
	}

	public void SetIntensity(float value)
	{
		m_intensity = math.saturate(value);
		if (m_intensity <= 0.01f)
		{
			transform.localPosition = m_initialPosition;
		}
	}
}