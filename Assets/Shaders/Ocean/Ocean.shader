Shader "Ocean/OceanSurface"
{
    Properties
    {
        // Wave parameters
        _WaveHeight     ("Wave Height",         Range(0, 10))   = 1.0
        _WaveSpeed      ("Wave Speed",          Range(0, 5))    = 1.0
        _WaveLength     ("Wave Length",         Range(1, 100))  = 20.0
        _WaveSteepness  ("Wave Steepness",      Range(0, 1))    = 0.5
        _WaveDirection1 ("Wave Direction 1",    Vector)         = (1, 0, 0.5, 0)
        _WaveDirection2 ("Wave Direction 2",    Vector)         = (0.8, 0, 0.6, 0)
        _WaveDirection3 ("Wave Direction 3",    Vector)         = (0.3, 0, -0.9, 0)

        // Visual
        _ShallowColor   ("Shallow Color",       Color)          = (0.0, 0.5, 0.6, 0.8)
        _DeepColor      ("Deep Color",          Color)          = (0.0, 0.1, 0.3, 0.9)
        _DepthDistance  ("Depth Fade Distance", Range(0, 50))   = 10.0
        _Smoothness     ("Smoothness",          Range(0, 1))    = 0.92
        _Metallic       ("Metallic",            Range(0, 1))    = 0.0
        _FresnelPower   ("Fresnel Power",       Range(0.1, 10)) = 3.0

        // Foam
        _FoamColor      ("Foam Color",          Color)          = (1, 1, 1, 1)
        _FoamTex        ("Foam Texture",        2D)             = "white" {}
        _FoamThreshold  ("Foam Threshold",      Range(0, 1))    = 0.6
        _FoamScale      ("Foam Scale",          Range(0.1, 10)) = 2.0

        // Normal / detail
        _NormalTex      ("Normal Map",          2D)             = "bump" {}
        _NormalStrength ("Normal Strength",     Range(0, 2))    = 0.6
        _NormalScale    ("Normal Tiling",       Range(0.1, 10)) = 1.5
        _NormalSpeed    ("Normal Scroll Speed", Range(0, 2))    = 0.3

        // Underwater visibility / murk
        [Header(Underwater Fading)]
        _AbsorptionDepth ("Absorption Depth",    Range(0.1, 50)) = 8.0
        _MurkDensity     ("Murk Density",        Range(0, 5))    = 1.2
        _MurkColor       ("Murk / Extinction Color", Color)      = (0.0, 0.12, 0.18, 1.0)
        _MinAlpha        ("Min Surface Alpha",   Range(0, 1))    = 0.55
        _MaxAlpha        ("Max Surface Alpha",   Range(0, 1))    = 0.97
        _AbsorptionR     ("Absorption – Red",    Range(0, 5))    = 2.8
        _AbsorptionG     ("Absorption – Green",  Range(0, 5))    = 0.9
        _AbsorptionB     ("Absorption – Blue",   Range(0, 5))    = 0.3

        // Wake trail
        [Header(Wake)]
        _WakeRT          ("Wake Render Texture",    2D)            = "black" {}
        _WakeRTWorldPos  ("Wake RT World Pos (XZ)", Vector)        = (0, 0, 0, 0)
        _WakeRTWorldSize ("Wake RT World Size",     Float)         = 120.0
        _WakeFoamStrength("Wake Foam Strength",     Range(0, 3))   = 1.4
        _WakeTurbStrength("Wake Turbulence Strength",Range(0, 2))  = 0.8
        _WakeFoamColor   ("Wake Foam Color",        Color)         = (1, 1, 1, 1)

        [HideInInspector] _Mode("__mode", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            // Reject any pixel where the HullMask shader wrote stencil = 1.
            // This prevents the ocean surface from rendering inside the boat hull.
            Stencil
            {
                Ref  1
                Comp NotEqual
                Pass Keep
                Fail Keep
            }

            HLSLPROGRAM
            #pragma vertex   OceanVert
            #pragma fragment OceanFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // ---------------------------------------------------------------
            // Properties
            // ---------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float  _WaveHeight;
                float  _WaveSpeed;
                float  _WaveLength;
                float  _WaveSteepness;
                float4 _WaveDirection1;
                float4 _WaveDirection2;
                float4 _WaveDirection3;

                float4 _ShallowColor;
                float4 _DeepColor;
                float  _DepthDistance;
                float  _Smoothness;
                float  _Metallic;
                float  _FresnelPower;

                float4 _FoamColor;
                float4 _FoamTex_ST;
                float  _FoamThreshold;
                float  _FoamScale;

                float4 _NormalTex_ST;
                float  _NormalStrength;
                float  _NormalScale;
                float  _NormalSpeed;

                // Underwater fading
                float  _AbsorptionDepth;
                float  _MurkDensity;
                float4 _MurkColor;
                float  _MinAlpha;
                float  _MaxAlpha;
                float  _AbsorptionR;
                float  _AbsorptionG;
                float  _AbsorptionB;
                // Wake RT
                float4 _WakeRTWorldPos;
                float  _WakeRTWorldSize;
                float  _WakeFoamStrength;
                float  _WakeTurbStrength;
                float4 _WakeFoamColor;
            CBUFFER_END

            TEXTURE2D(_WakeRT);   SAMPLER(sampler_WakeRT);
            TEXTURE2D(_FoamTex);   SAMPLER(sampler_FoamTex);
            TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);

            // ---------------------------------------------------------------
            // Gerstner Wave
            // ---------------------------------------------------------------
            struct GerstnerWave
            {
                float2 direction;
                float  steepness;
                float  wavelength;
            };

            float3 GerstnerWaveOffset(GerstnerWave wave, float3 pos, float time)
            {
                float k     = 2.0 * PI / wave.wavelength;
                float c     = sqrt(9.8 / k);
                float2 d    = normalize(wave.direction);
                float f     = k * (dot(d, pos.xz) - c * time);
                float a     = wave.steepness / k;

                return float3(
                    d.x * (a * cos(f)),
                    a   *  sin(f),
                    d.y * (a * cos(f))
                );
            }

            float3 GerstnerWaveNormal(GerstnerWave wave, float3 pos, float time)
            {
                float k  = 2.0 * PI / wave.wavelength;
                float c  = sqrt(9.8 / k);
                float2 d = normalize(wave.direction);
                float f  = k * (dot(d, pos.xz) - c * time);
                float a  = wave.steepness / k;

                return float3(
                    -(d.x * k * a * cos(f)),
                    1.0 - (wave.steepness * sin(f)),
                    -(d.y * k * a * cos(f))
                );
            }

            // Compute combined Gerstner displacement for 3 wave trains
            float3 ComputeGerstnerDisplacement(float3 worldPos, float time,
                                               out float3 outNormal)
            {
                float scaledTime = time * _WaveSpeed;

                GerstnerWave w1, w2, w3;
                w1.direction  = normalize(_WaveDirection1.xz);
                w1.steepness  = _WaveSteepness;
                w1.wavelength = _WaveLength;

                w2.direction  = normalize(_WaveDirection2.xz);
                w2.steepness  = _WaveSteepness * 0.7;
                w2.wavelength = _WaveLength   * 0.6;

                w3.direction  = normalize(_WaveDirection3.xz);
                w3.steepness  = _WaveSteepness * 0.5;
                w3.wavelength = _WaveLength   * 0.4;

                float3 disp  = GerstnerWaveOffset(w1, worldPos, scaledTime)
                             + GerstnerWaveOffset(w2, worldPos, scaledTime)
                             + GerstnerWaveOffset(w3, worldPos, scaledTime);

                // Scale vertical by _WaveHeight
                disp.y *= _WaveHeight;

                float3 n = GerstnerWaveNormal(w1, worldPos, scaledTime)
                         + GerstnerWaveNormal(w2, worldPos, scaledTime)
                         + GerstnerWaveNormal(w3, worldPos, scaledTime);
                outNormal = normalize(n);

                return disp;
            }

            // ---------------------------------------------------------------
            // Vertex / Fragment structs
            // ---------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                float4 screenPos    : TEXCOORD3;
                float  fogFactor    : TEXCOORD4;
            };

            // ---------------------------------------------------------------
            // Vertex shader
            // ---------------------------------------------------------------
            Varyings OceanVert(Attributes IN)
            {
                Varyings OUT;

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float  t        = _Time.y;

                float3 gNormal;
                float3 disp = ComputeGerstnerDisplacement(worldPos, t, gNormal);
                worldPos   += disp;

                OUT.positionWS = worldPos;
                OUT.normalWS   = TransformObjectToWorldNormal(gNormal);
                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.uv         = IN.uv;
                OUT.screenPos  = ComputeScreenPos(OUT.positionCS);
                OUT.fogFactor  = ComputeFogFactor(OUT.positionCS.z);

                return OUT;
            }

            // ---------------------------------------------------------------
            // Fragment shader
            // ---------------------------------------------------------------
            half4 OceanFrag(Varyings IN) : SV_Target
            {
                float  t   = _Time.y;
                float3 pos = IN.positionWS;

                // ---- Scrolling normal map (two layers, opposite directions) ----
                float2 uvN1 = pos.xz * _NormalScale * 0.05 + float2(t, t * 0.7) * _NormalSpeed;
                float2 uvN2 = pos.xz * _NormalScale * 0.04 + float2(-t * 0.6, t * 0.9) * _NormalSpeed;
                float3 n1   = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uvN1));
                float3 n2   = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uvN2));
                float3 detailNormal = normalize(float3(
                    (n1.xy + n2.xy) * _NormalStrength,
                    1.0
                ));

                // Blend Gerstner normal with detail normal
                float3 N = normalize(IN.normalWS + float3(detailNormal.x, 0, detailNormal.y));

                // ---- Depth sampling ----
                float2 screenUV    = IN.screenPos.xy / IN.screenPos.w;
                float  sceneDepth  = LinearEyeDepth(
                    SampleSceneDepth(screenUV),
                    _ZBufferParams
                );
                float  surfaceDepth = IN.screenPos.w;
                // How far below the surface does the scene geometry sit?
                float  waterDepth   = max(0.0, sceneDepth - surfaceDepth);

                // ---- Beer-Lambert light absorption (per channel) ----
                // Each colour channel is extinguished at its own rate with depth.
                // Red is absorbed first, blue last — this is what gives deep water
                // its characteristic blue-green hue even with neutral geometry below.
                float3 absorption = float3(
                    exp(-_AbsorptionR * waterDepth / _AbsorptionDepth),
                    exp(-_AbsorptionG * waterDepth / _AbsorptionDepth),
                    exp(-_AbsorptionB * waterDepth / _AbsorptionDepth)
                );

                // ---- Depth-based colour blend (shallow → deep) ----
                float  depthFrac  = saturate(waterDepth / _DepthDistance);
                float4 waterColor = lerp(_ShallowColor, _DeepColor, depthFrac);

                // ---- Underwater murk fog ----
                // Exponential fog fills the water column; geometry far below
                // the surface is progressively hidden by the murk colour.
                // murkFactor → 0 at the surface, → 1 at depth.
                float murkFactor = 1.0 - exp(-_MurkDensity * waterDepth / _AbsorptionDepth);
                // Tint the absorbed colour toward the murk colour
                waterColor.rgb = lerp(waterColor.rgb * absorption, _MurkColor.rgb, murkFactor);

                // ---- Opacity driven by depth ----
                // Shallow water lets geometry show through; deep/murky water is opaque.
                // _MinAlpha is the base transparency even in still, shallow water.
                float depthAlpha = saturate(waterDepth / _AbsorptionDepth);
                float murkAlpha  = lerp(_MinAlpha, _MaxAlpha, max(depthFrac, murkFactor));
                waterColor.a     = murkAlpha;

                // ---- Fresnel ----
                float3 viewDir = normalize(GetCameraPositionWS() - pos);
                float  fresnel = pow(1.0 - saturate(dot(N, viewDir)), _FresnelPower);

                // ---- PBR lighting ----
                InputData lightInput    = (InputData)0;
                lightInput.positionWS   = pos;
                lightInput.normalWS     = N;
                lightInput.viewDirectionWS = viewDir;
                lightInput.shadowCoord  = TransformWorldToShadowCoord(pos);
                lightInput.fogCoord     = IN.fogFactor;
                lightInput.bakedGI      = half3(0,0,0);

                SurfaceData surf = (SurfaceData)0;
                surf.albedo      = waterColor.rgb;
                surf.alpha       = waterColor.a;   // depth + murk driven opacity
                surf.smoothness  = _Smoothness;
                surf.metallic    = _Metallic;
                surf.normalTS    = detailNormal;
                surf.occlusion   = 1.0;

                half4 litColor = UniversalFragmentPBR(lightInput, surf);
                litColor.rgb   = lerp(litColor.rgb, half3(1,1,1), fresnel * 0.3);

                // ---- Wake RT sampling ----
                // Convert world XZ to wake RT UV using the world-space anchor
                // pushed by WakeCamera.cs each frame.
                float2 wakeUV = (pos.xz - _WakeRTWorldPos.xy) / _WakeRTWorldSize + 0.5;
                half4  wakeSample   = SAMPLE_TEXTURE2D(_WakeRT, sampler_WakeRT, wakeUV);
                half   wakeFoam     = saturate(wakeSample.r * _WakeFoamStrength);
                half   wakeTurb     = saturate(wakeSample.g * _WakeTurbStrength);

                // Wake turbulence: roughen the surface normal where the wake is active
                // This breaks up the specular highlight, giving the churned-water look.
                N = normalize(N + float3(
                    sin(pos.x * 3.0 + t * 2.0) * wakeTurb * 0.35,
                    0.0,
                    cos(pos.z * 3.0 + t * 1.7) * wakeTurb * 0.35
                ));

                // ---- Foam ----
                float2 foamUV   = pos.xz * 0.05 * _FoamScale + float2(t * 0.05, t * 0.03);
                float  foamTex  = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV).r;
                float  foamMask = saturate((N.y - _FoamThreshold) * 5.0) * foamTex;
                litColor.rgb    = lerp(litColor.rgb, _FoamColor.rgb, foamMask * _FoamColor.a);

                // ---- Wake foam composite ----
                // Wake foam sits on top of the regular foam, using its own colour
                // so designers can tint it slightly off-white for dirty/disturbed water.
                litColor.rgb    = lerp(litColor.rgb, _WakeFoamColor.rgb, wakeFoam * _WakeFoamColor.a);
                // Slightly increase alpha over the wake so it reads against deep water
                litColor.a      = saturate(litColor.a + wakeFoam * 0.15);

                // ---- Fog ----
                litColor.rgb = MixFog(litColor.rgb, IN.fogFactor);

                return litColor;
            }
            ENDHLSL
        }

        // Shadow caster pass (transparent ocean casts no shadow)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };
            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
            half4 ShadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
