Shader "Ocean/WakeDecay"
{
    // -------------------------------------------------------------------------
    // Wake Decay Shader
    // -------------------------------------------------------------------------
    // Fullscreen blit applied to the WakeRenderTexture every frame.
    // Multiplies every pixel by _DecayRate, causing old stamps to fade away.
    // This simulates the natural dissipation of a boat wake over time.
    // -------------------------------------------------------------------------
    Properties
    {
        _MainTex    ("Source RT",   2D)          = "black" {}
        _DecayRate  ("Decay Rate",  Range(0, 1)) = 0.97    // per-frame multiplier
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "WakeDecay"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZWrite Off
            ZTest  Always
            Cull   Off
            Blend  Off

            HLSLPROGRAM
            #pragma vertex   DecayVert
            #pragma fragment DecayFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _DecayRate;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings DecayVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 DecayFrag(Varyings IN) : SV_Target
            {
                half4 current = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                // Fade uniformly — R (foam), G (turb), A (mask) all decay together
                return current * _DecayRate;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
