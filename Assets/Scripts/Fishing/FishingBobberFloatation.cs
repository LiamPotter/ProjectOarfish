using System;
using Crest;
using Unity.Mathematics;
using UnityEngine;

namespace Fishing
{
	public class FishingBobberFloatation : FloatingObjectBase
	{
		[SerializeField] private float m_bobberFloatationWidth = 2f;
		[SerializeField] private float m_bobberFloatOffset = 0.1f;

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
		public Vector3 InputVelocity => m_inputVelocity;
		
		private Vector3 m_inputVelocity = Vector3.zero;
		private Vector3 m_displacement;
		private float m_height;
		private Camera m_camera;

		public Action EnteredWater;

		private void Start()
		{
			m_camera = Camera.main;
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
				EnteredWater?.Invoke();
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

		public void AddForce(Vector3 force, bool allowApproach = true)
		{
			m_inputVelocity = force;

			Vector3 relativeForce = GetVectorRelativeToCamera(force, out Vector3 dirFromCam);
			m_rigidbody.AddForce(relativeForce, ForceMode.Acceleration);

			if (allowApproach == false)
			{
				var forcePosition = m_rigidbody.position + m_bobberFloatOffset * Vector3.up;
				m_rigidbody.AddForceAtPosition(10 * Vector3.Dot(-dirFromCam, m_rigidbody.linearVelocity) *dirFromCam, forcePosition,
					ForceMode.Acceleration);
			}
		}

		public void AddTorqueForce(Vector3 force,  bool relativeToCamera = false)
		{
			var forcePosition = m_rigidbody.position + m_bobberFloatOffset * Vector3.up;
			Vector3 relativeForce = GetVectorRelativeToCamera(force, out Vector3 _);
			
			m_rigidbody.AddForceAtPosition(relativeToCamera?relativeForce:force, forcePosition,ForceMode.Acceleration);
		}

		private Vector3 GetVectorRelativeToCamera(Vector3 vector, out Vector3 directionFromCamera)
		{
			Vector3 flatPos = transform.position;
			flatPos.y = 0;
			Vector3 flatCamPos = m_camera.transform.position;
			flatCamPos.y = 0;
			directionFromCamera = math.normalize(flatPos-flatCamPos);
			Vector3 rightFromCam = math.cross(directionFromCamera, Vector3.up);
			Vector3 relativeInputX = rightFromCam * vector.x;
			Vector3 relativeInputZ = directionFromCamera * vector.z;
			Vector3 relativeInput = relativeInputX + relativeInputZ;
			relativeInput.y = 0;

			return relativeInput;
		}

		private void OnDrawGizmos()
		{
			if (m_debugDraw == false || m_camera == null)
			{
				return;
			}
			Vector3 flatPos = transform.position;
			flatPos.y = 0;
			Vector3 flatCamPos = m_camera.transform.position;
			flatCamPos.y = 0;
			Vector3 dirFromCam = math.normalize(flatPos-flatCamPos);
			Vector3 rightFromCam = math.cross(dirFromCam, Vector3.up);
			Gizmos.color = Color.darkGreen;
			Gizmos.DrawRay(transform.position, dirFromCam * 5);
			Gizmos.color = Color.blueViolet;
			Gizmos.DrawRay(transform.position, rightFromCam * 5);


		}
	}
}