Shader "Ocean/WakeRenderer"
{
    // -------------------------------------------------------------------------
    // Wake Renderer Shader
    // -------------------------------------------------------------------------
    // Used ONLY to draw wake stamp quads into the WakeRenderTexture.
    // Each stamp quad is a world-aligned quad placed behind the boat.
    // The RT stores:
    //   R = wake foam intensity  (bright white crest foam)
    //   G = wake turbulence      (used to roughen normals in ocean shader)
    //   B = (reserved / future)
    //   A = overall wake mask    (drives edge fade)
    //
    // Blending is Additive so multiple overlapping stamps accumulate naturally.
    // The WakeCamera CS blits a decay pass each frame to fade old stamps out.
    // -------------------------------------------------------------------------
    Properties
    {
        _WakeTex        ("Wake Stamp Texture",  2D)             = "white" {}
        _WakeColor      ("Wake Foam Color",     Color)          = (1, 1, 1, 1)
        _Intensity      ("Stamp Intensity",     Range(0, 2))    = 1.0
        _SpeedFade      ("Speed Fade",          Range(0, 1))    = 1.0  // set by C# per-stamp
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "WakeStamp"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            // Additive blend: stamps accumulate in the RT
            Blend One One
            ZWrite Off
            ZTest  Always   // always write — RT camera is orthographic top-down
            Cull   Off

            HLSLPROGRAM
            #pragma vertex   WakeVert
            #pragma fragment WakeFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _WakeTex_ST;
                float4 _WakeColor;
                float  _Intensity;
                float  _SpeedFade;
            CBUFFER_END

            TEXTURE2D(_WakeTex); SAMPLER(sampler_WakeTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;       // per-vertex age/alpha packed by C#
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : TEXCOORD1;
            };

            Varyings WakeVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _WakeTex);
                OUT.color      = IN.color;
                return OUT;
            }

            half4 WakeFrag(Varyings IN) : SV_Target
            {
                half4 stamp = SAMPLE_TEXTURE2D(_WakeTex, sampler_WakeTex, IN.uv);

                // age fade is packed into vertex alpha by WakeTrail.cs
                float ageFade = IN.color.a;

                // R = foam,  G = turbulence (offset UVs slightly for variation)
                float2 turbUV = IN.uv + float2(0.05, 0.05);
                half   foam   = stamp.r * ageFade * _Intensity * _SpeedFade;
                half   turb   = stamp.g * ageFade * _Intensity * _SpeedFade * 0.6;

                // Pack into RGBA output:
                //   R = foam  G = turbulence  B = 0  A = mask
                return half4(foam, turb, 0.0, foam) * _WakeColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
