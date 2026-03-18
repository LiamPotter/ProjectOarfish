Shader "Ocean/HullMask"
{
    // -------------------------------------------------------------------------
    // Hull Mask Shader
    // -------------------------------------------------------------------------
    // Renders the boat hull into the stencil buffer ONLY.
    // Nothing is written to colour or depth — the hull remains visually
    // unchanged by this shader.  The ocean shader reads the stencil and
    // discards any fragment where this shader wrote ref = 1, preventing
    // water from appearing inside the hull.
    //
    // Setup:
    //   1. Create a material using this shader.
    //   2. Add a MeshRenderer to the boat hull GameObject (or a child that
    //      matches the underwater hull volume exactly) and assign this material
    //      to it — either as the sole material or as an additional material slot.
    //   3. Set the Renderer's "Sorting Layer / Order" so it executes BEFORE
    //      the ocean surface (Queue 2499 renders before Transparent 3000).
    //   4. The ocean shader will automatically mask the interior.
    //
    // Render queue 2499 = just before Transparent (3000), so the stencil is
    // written before the ocean pass reads it.
    // -------------------------------------------------------------------------

    Properties
    {
        // No user-facing properties needed.
        // The stencil reference value matches the ocean shader (Ref 1).
        [HideInInspector] _StencilRef ("Stencil Ref", Int) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry+1"   // 2001 — renders before ocean (Transparent = 3000)
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── Pass 1: Write stencil from BACK faces ─────────────────────────────
        // Back faces of the hull define the "inside" volume when seen from above.
        // We render back faces first so that even when the camera is level with
        // the waterline, the hull interior is always masked correctly.
        Pass
        {
            Name "HullMask_BackFace"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            // Write nothing to colour or depth
            ColorMask 0
            ZWrite    Off
            // Back faces only
            Cull      Front

            Stencil
            {
                Ref       1
                Comp      Always   // always write
                Pass      Replace  // write Ref into stencil buffer
                Fail      Keep
                ZFail     Replace  // also write on depth fail (camera inside hull)
            }

            HLSLPROGRAM
            #pragma vertex   HullVert
            #pragma fragment HullFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings HullVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            // Fragment writes nothing — stencil op does all the work
            half4 HullFrag(Varyings IN) : SV_Target
            {
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }

        // ── Pass 2: Write stencil from FRONT faces ────────────────────────────
        // Front faces define the outer hull boundary.  Writing the stencil here
        // as well ensures that grazing-angle views (camera near the waterline)
        // are covered and avoids artefacts at the hull/ocean intersection edge.
        Pass
        {
            Name "HullMask_FrontFace"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ColorMask 0
            ZWrite    Off
            Cull      Back   // front faces only

            Stencil
            {
                Ref       1
                Comp      Always
                Pass      Replace
                Fail      Keep
                ZFail     Keep   // front-face depth fail = occluded, don't mask
            }

            HLSLPROGRAM
            #pragma vertex   HullVert
            #pragma fragment HullFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings HullVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 HullFrag(Varyings IN) : SV_Target
            {
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
