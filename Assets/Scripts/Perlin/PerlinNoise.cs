using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerlinNoise : MonoBehaviour
{
    [SerializeField] private Renderer thisRenderer;
    [SerializeField] private Material thisMaterial;
    [SerializeField] private float scale = 0.1f;
    
    private Texture2D perlinTexture;
    private int width = 256;
    private int height = 256;
    private float moveTimer;

    private void Start()
    {
        perlinTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        thisRenderer.material = new Material(thisMaterial);
        GenerateNoise();
    }

    // Update is called once per frame
    void Update()
    {
        moveTimer += Time.deltaTime * 0.01f;
        GenerateNoise();
    }

    private void GenerateNoise()
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Calculate coordinates for sampling the noise
                // Multiply by scale to control the "zoom" or frequency of the noise
                float xCoord = x * scale;
                float yCoord = y * scale;

                // Get the noise sample
                float sample = Mathf.PerlinNoise(xCoord, yCoord);
                
                // Create a color from the sample and set the pixel
                Color color = new Color(sample, sample, sample);
                perlinTexture.SetPixel(x, y, color);
            }
        }
        
        perlinTexture.Apply();
        thisRenderer.material.mainTexture = perlinTexture;
    }
}
