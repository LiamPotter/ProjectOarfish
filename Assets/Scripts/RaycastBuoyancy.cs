using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class RaycastBuoyancy : MonoBehaviour
{
	[Serializable]
	public struct BuoyantPosition
	{
		public Transform transform;
		public Color color;
		public float strength;
	}

	[SerializeField] private RawImage rawImage;
	[SerializeField] private Renderer oceanRenderer;
	[SerializeField] private CustomRenderTexture oceanRenderTexture;
	[SerializeField] private Rigidbody thisRigidbody;
	[SerializeField] private BuoyantPosition[] buoyantPositions;
	[SerializeField] private LayerMask waterLayer;

	private Vector2 xExtents;
	private Vector2 zExtents;
	private Vector2 texturePercent;
	private Vector2 texturePos;
	private float textureYVal;
	private Transform oceanTransform;
	private Material oceanMaterial;
	private Vector3[] hits;
	private int textureSize;
	private Texture2D sampleTexture;
	private RenderTexture prevRenderTexture;
	private RenderTexture tempRenderTexture;
	private Texture tempTexture;
	private Color textureSampledColor;

	private void Start()
	{
		hits = new Vector3[buoyantPositions.Length];
		oceanMaterial = oceanRenderer.material;
		oceanTransform = oceanRenderer.transform;
		textureSize = oceanRenderTexture.width;
		sampleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBAFloat, false);
		oceanRenderTexture.Initialize();
		tempRenderTexture = RenderTexture.GetTemporary(textureSize, textureSize, oceanRenderTexture.depth);
	}

	private void OnDisable()
	{
		oceanRenderTexture.Release();
	}

	private void Update()
	{
		oceanRenderTexture.Update();
		CollectHits();
		ApplyForces();
	}

	private void CollectHits()
	{
		float xScale = 125f;
		float zScale = 125f;
		float xMax = oceanTransform.position.x + (1 * xScale);
		float zMax = oceanTransform.position.z + (1 * zScale);
		xExtents = new Vector2(xMax, -xMax);
		zExtents = new Vector2(zMax, -zMax);

		float oceanYScale = oceanTransform.localScale.y;

		prevRenderTexture = RenderTexture.active;
		Graphics.Blit(oceanRenderer.material.mainTexture, tempRenderTexture, new Vector2(-1, -1), new Vector2(1, 1));
		RenderTexture.active = tempRenderTexture;
		sampleTexture.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);

		sampleTexture.Apply();
		rawImage.texture = sampleTexture;
		for (int i = 0; i < hits.Length; i++)
		{
			Vector3 localOceanPos = buoyantPositions[i].transform.position;
			Vector2 localOceanPos2D = new Vector2(localOceanPos.x, localOceanPos.z);
			float xPercent = math.saturate(math.unlerp(xExtents.x, xExtents.y, localOceanPos2D.x));
			float yPercent = math.saturate(math.unlerp(zExtents.x, zExtents.y, localOceanPos2D.y));

			int xPixel = Mathf.RoundToInt(xPercent * textureSize);
			int yPixel = Mathf.RoundToInt(yPercent * textureSize);

			texturePercent = new Vector2(xPercent, yPercent);
			texturePos = new Vector2(xPixel, yPixel);

			textureSampledColor = sampleTexture.GetPixel(xPixel, yPixel);
			textureYVal = math.max(math.max(textureSampledColor.r, textureSampledColor.g), textureSampledColor.b);

			float scaledYVal = oceanTransform.position.y + (oceanYScale * textureYVal);
			hits[i] = new Vector3(buoyantPositions[i].transform.position.x, scaledYVal, buoyantPositions[i].transform.position.z);
		}

		tempRenderTexture.Release();
		RenderTexture.active = prevRenderTexture;
	}

	private void ApplyForces()
	{
		// foreach (RaycastHit hit in hits)
		// {
		//     
		// }
	}

	private void OnDrawGizmos()
	{
		if (hits == null || hits.Length == 0)
		{
			return;
		}

		for (int i = 0; i < buoyantPositions.Length; i++)
		{
			BuoyantPosition bPos = buoyantPositions[i];
			Vector3 hit = hits[i];
			Gizmos.color = bPos.color;
			Gizmos.DrawSphere(hit, 0.1f);
			Gizmos.DrawLine(bPos.transform.position, hit);
		}
	}
}