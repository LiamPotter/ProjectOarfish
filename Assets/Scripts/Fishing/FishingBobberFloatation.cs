using System;
using Crest;
using UnityEngine;

namespace Fishing
{
	public class FishingBobberFloatation : FloatingObjectBase
	{
		[SerializeField] private float m_bobberFloatationWidth = 2f;
		[SerializeField] private float m_bobberFloatOffset = 0.1f;
		[SerializeField] private float m_fishForce = 0.1f;

		[Tooltip("Strength of buoyancy force per meter of submersion in water.")] [SerializeField]
		private float m_buoyancyCoeff = 3f;

		[SerializeField] private float m_dragInWaterUp = 3f;
		[SerializeField] private float m_dragInWaterRight = 2f;
		[SerializeField] private float m_dragInWaterForward = 1f;
		[SerializeField] private Rigidbody m_rigidbody = null;
		[SerializeField] private bool m_debugDraw = false;

		private bool m_inWater;
		SampleHeightHelper m_sampleHeightHelper = new SampleHeightHelper();
		public override float ObjectWidth => m_bobberFloatationWidth;
		public override bool InWater => m_inWater;
		public override Vector3 Velocity => m_rigidbody.linearVelocity;

		private Vector3 m_displacement;
		private float m_height;
		private Vector2 m_fishForceVector;
		private Camera m_camera;

		public Action EnteredWater;

		private void Start()
		{
			m_camera = Camera.main;
		}

		private void Update()
		{
			float xInput = Input.GetKey(KeyCode.J) ? -1 : Input.GetKey(KeyCode.L) ? 1 : 0;
			float yInput = Input.GetKey(KeyCode.K) ? -1 : Input.GetKey(KeyCode.I) ? 1 : 0;
			Vector2 rawInput = new Vector2(xInput, yInput);
			Vector2 relativeInputX = m_camera.transform.right * xInput;
			Vector2 relativeInputY = m_camera.transform.forward * yInput;
			Vector2 relativeInput = relativeInputX + relativeInputY;

			m_fishForceVector = relativeInput * m_fishForce;
		}

		// Update is called once per frame
		private void FixedUpdate()
		{
			UnityEngine.Profiling.Profiler.BeginSample("FishingBobberFloatation.FixedUpdate");

			m_sampleHeightHelper.Init(transform.position, ObjectWidth, true);
			m_sampleHeightHelper.Sample(out Vector3 disp, out var normal, out var waterSurfaceVel);

			m_displacement = disp;
			float height = disp.y + OceanRenderer.Instance.SeaLevel;

			float bottomDepth = height - transform.position.y + m_bobberFloatOffset;

			m_height = height;
			bool inWaterCheck = bottomDepth > 0f;

			if (m_inWater == false && inWaterCheck)
			{
				m_rigidbody.linearVelocity = Vector3.zero;
				m_rigidbody.angularVelocity = Vector3.zero;
				m_inWater = true;
			}


			if (!m_inWater)
			{
				UnityEngine.Profiling.Profiler.EndSample();
				return;
			}

			var buoyancy = m_buoyancyCoeff * bottomDepth * bottomDepth * bottomDepth * -Physics.gravity.normalized;

			var velocityRelativeToWater = Velocity - waterSurfaceVel;

			m_rigidbody.AddForce(buoyancy, ForceMode.Acceleration);

			// Apply drag relative to water
			var forcePosition = m_rigidbody.position + m_bobberFloatOffset * Vector3.up;
			m_rigidbody.AddForceAtPosition(m_dragInWaterUp * Vector3.Dot(Vector3.up, -velocityRelativeToWater) * Vector3.up, forcePosition,
				ForceMode.Acceleration);
			m_rigidbody.AddForceAtPosition(m_dragInWaterRight * Vector3.Dot(transform.right, -velocityRelativeToWater) * transform.right, forcePosition,
				ForceMode.Acceleration);
			m_rigidbody.AddForceAtPosition(m_dragInWaterForward * Vector3.Dot(transform.forward, -velocityRelativeToWater) * transform.forward, forcePosition,
				ForceMode.Acceleration);
			

			UnityEngine.Profiling.Profiler.EndSample();
		}

		public void Reset()
		{
			m_inWater = false;
		}

		public void AddForce(Vector3 force)
		{
			m_rigidbody.AddForce(force, ForceMode.Acceleration);
			Vector3 relativeInputX = m_camera.transform.right * force.x;
			Vector3 relativeInputZ = m_camera.transform.forward * force.z;
			Vector3 relativeInput = relativeInputX + relativeInputZ;
			relativeInput.y = 0;
			m_rigidbody.AddForce(relativeInput, ForceMode.Acceleration);
		}

		private void OnDrawGizmos()
		{
			if (m_debugDraw == false)
			{
				return;
			}

			Gizmos.color = Color.greenYellow;
			Gizmos.DrawSphere(transform.position + m_displacement, 0.1f);
			Gizmos.color = Color.darkRed;
			Gizmos.DrawSphere(transform.position - m_displacement, 0.1f);
			UnityEditor.Handles.Label(transform.position + Vector3.up / 2, $"height: {m_height}");
		}
	}
}