// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Animpic/SyleWater"
{
	Properties
	{
		[NoScaleOffset][Normal][SingleLineTexture][Header(Maps)][Space(7)]_WavesNormal("Waves Normal", 2D) = "white" {}
		[Header(Settings)][Space(5)]_WaterColor("Water Color", Color) = (0,0,0,0)
		_DepthColor("Depth Color", Color) = (0,0,0,0)
		_Opacity("Opacity", Range( 0 , 1)) = 0
		_Smoothness("Smoothness", Range( 0 , 1)) = 0
		[Header(Wave)][Space(5)]_WavesTile("Waves Tile", Float) = 1
		_WavesSpeed("Waves Speed", Range( 0 , 1)) = 0
		_WavesNormalIntensity("Waves Normal Intensity", Range( 0 , 2)) = 1
		[Header(Foam Settings)][Sapce(5)]_FoamColor("Foam Color", Color) = (1,1,1,0)
		[SingleLineTexture]_FoamTexture("Foam Texture", 2D) = "white" {}
		_FoamTile("Foam Tile", Float) = 1
		_FoamContrast("Foam Contrast", Range( -5 , 5)) = 0
		_FoamDistance("Foam Distance", Range( 0 , 5)) = 0
		_FoamDensity("Foam Density", Range( 0.1 , 1)) = 0.5105882
		_DepthDistance("Depth Distance", Float) = 0
		_RefractionScale("Refraction Scale", Range( 0 , 1)) = 0.2
		_ShoreOpacity("Shore Opacity", Range( 0 , 1)) = 0
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True" }
		Cull Back
		GrabPass{ }
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#include "UnityStandardUtils.cginc"
		#include "UnityCG.cginc"
		#pragma target 3.0
		#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex);
		#else
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex)
		#endif
		#pragma surface surf Standard alpha:fade keepalpha nodirlightmap nometa noforwardadd vertex:vertexDataFunc 
		struct Input
		{
			float3 worldPos;
			float4 screenPos;
			float eyeDepth;
		};

		uniform sampler2D _WavesNormal;
		uniform float _WavesSpeed;
		uniform float _WavesTile;
		uniform float _WavesNormalIntensity;
		uniform float4 _DepthColor;
		UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
		uniform float4 _CameraDepthTexture_TexelSize;
		uniform float _DepthDistance;
		uniform float4 _WaterColor;
		ASE_DECLARE_SCREENSPACE_TEXTURE( _GrabTexture )
		uniform float _RefractionScale;
		uniform sampler2D _FoamTexture;
		uniform float _FoamDensity;
		uniform float _FoamTile;
		uniform float4 _FoamColor;
		uniform float _FoamContrast;
		uniform float _FoamDistance;
		uniform float _Smoothness;
		uniform float _Opacity;
		uniform float _ShoreOpacity;


		inline float4 ASE_ComputeGrabScreenPos( float4 pos )
		{
			#if UNITY_UV_STARTS_AT_TOP
			float scale = -1.0;
			#else
			float scale = 1.0;
			#endif
			float4 o = pos;
			o.y = pos.w * 0.5f;
			o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
			return o;
		}


		float2 voronoihash61( float2 p )
		{
			
			p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
			return frac( sin( p ) *43758.5453);
		}


		float voronoi61( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
		{
			float2 n = floor( v );
			float2 f = frac( v );
			float F1 = 8.0;
			float F2 = 8.0; float2 mg = 0;
			for ( int j = -1; j <= 1; j++ )
			{
				for ( int i = -1; i <= 1; i++ )
			 	{
			 		float2 g = float2( i, j );
			 		float2 o = voronoihash61( n + g );
					o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
					float d = 0.5 * dot( r, r );
			 //		if( d<F1 ) {
			 //			F2 = F1;
			 			float h = smoothstep(0.0, 1.0, 0.5 + 0.5 * (F1 - d) / smoothness); F1 = lerp(F1, d, h) - smoothness * h * (1.0 - h);mg = g; mr = r; id = o;
			 //		} else if( d<F2 ) {
			 //			F2 = d;
			
			 //		}
			 	}
			}
			return F1;
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			o.eyeDepth = -UnityObjectToViewPos( v.vertex.xyz ).z;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float mulTime43 = _Time.y * ( _WavesSpeed * 0.1 );
			float2 _Vector0 = float2(1,1);
			float3 ase_worldPos = i.worldPos;
			float2 appendResult106 = (float2(ase_worldPos.x , ase_worldPos.z));
			float2 WaveTile68 = ( appendResult106 * _WavesTile );
			float2 panner114 = ( ( 1.0 - mulTime43 ) * _Vector0 + WaveTile68);
			float2 panner112 = ( mulTime43 * _Vector0 + WaveTile68);
			float3 Normal89 = ( UnpackScaleNormal( tex2D( _WavesNormal, panner114 ), _WavesNormalIntensity ) + UnpackScaleNormal( tex2D( _WavesNormal, panner112 ), _WavesNormalIntensity ) );
			o.Normal = Normal89;
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float clampResult140 = clamp( _DepthDistance , 0.1 , 100.0 );
			float screenDepth83 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth83 = abs( ( screenDepth83 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( clampResult140 ) );
			float temp_output_364_0 = saturate( distanceDepth83 );
			float4 Depth158 = saturate( ( saturate( ( _DepthColor * temp_output_364_0 ) ) + saturate( ( ( 1.0 - temp_output_364_0 ) * _WaterColor ) ) ) );
			float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ase_screenPos );
			float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
			float4 screenColor341 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_GrabTexture,ase_grabScreenPosNorm.xy);
			float4 temp_output_277_0 = ( ase_grabScreenPosNorm + float4( ( Normal89 * ( _RefractionScale * 0.1 ) ) , 0.0 ) );
			float4 screenColor274 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_GrabTexture,temp_output_277_0.xy);
			float eyeDepth337 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, temp_output_277_0.xy ));
			float ifLocalVar336 = 0;
			if( eyeDepth337 > i.eyeDepth )
				ifLocalVar336 = 1.0;
			else if( eyeDepth337 < i.eyeDepth )
				ifLocalVar336 = 0.0;
			float4 lerpResult342 = lerp( screenColor341 , screenColor274 , ifLocalVar336);
			float4 Refractions282 = saturate( lerpResult342 );
			float4 lerpResult323 = lerp( Depth158 , Refractions282 , Depth158);
			float mulTime3 = _Time.y * ( ( _WavesSpeed * 0.1 ) * 50 );
			float time61 = mulTime3;
			float2 voronoiSmoothId61 = 0;
			float voronoiSmooth61 = ( 1.0 - _FoamDensity );
			float2 appendResult387 = (float2(ase_worldPos.x , ase_worldPos.z));
			float2 coords61 = ( appendResult387 * _FoamTile ) * 1.0;
			float2 id61 = 0;
			float2 uv61 = 0;
			float voroi61 = voronoi61( coords61, time61, id61, uv61, voronoiSmooth61, voronoiSmoothId61 );
			float4 temp_output_377_0 = ( tex2D( _FoamTexture, uv61 ) * _FoamColor * voroi61 );
			float clampResult138 = clamp( _FoamContrast , -1.0 , 1.0 );
			float4 temp_cast_4 = (( 1.0 - clampResult138 )).xxxx;
			float screenDepth17 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth17 = abs( ( screenDepth17 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( _FoamDistance ) );
			float temp_output_19_0 = ( 1.0 - distanceDepth17 );
			float4 Foam183 = saturate( ( ( pow( ( temp_output_377_0 + float4( 0,0,0,0 ) ) , temp_cast_4 ) + temp_output_19_0 ) + temp_output_19_0 ) );
			o.Albedo = ( lerpResult323 + Foam183 ).rgb;
			o.Smoothness = _Smoothness;
			float screenDepth345 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth345 = abs( ( screenDepth345 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( _ShoreOpacity ) );
			o.Alpha = ( _Opacity * saturate( distanceDepth345 ) );
		}

		ENDCG
	}
	Fallback "Diffuse"
}
/*ASEBEGIN
Version=18935
795.1698;286.1887;1049.66;622.9246;5118.261;-1431.373;1;True;False
Node;AmplifyShaderEditor.CommentaryNode;95;-3824.365,-1375.077;Inherit;False;750.6597;273.6137;;5;68;107;100;106;66;Water Tile;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;58;-5325.692,716.2116;Inherit;False;Property;_WavesSpeed;Waves Speed;6;0;Create;True;0;0;0;False;0;False;0;0.1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.WorldPosInputsNode;66;-3798.399,-1323.941;Inherit;True;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.CommentaryNode;94;-4667.802,-235.4393;Inherit;False;1884.301;658.2482;;13;89;51;38;46;88;127;101;113;43;53;114;112;368;Water Normal;1,1,1,1;0;0
Node;AmplifyShaderEditor.DynamicAppendNode;106;-3562.786,-1289.869;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;100;-3569.609,-1195.695;Inherit;False;Property;_WavesTile;Waves Tile;5;0;Create;True;0;0;0;False;2;Header(Wave);Space(5);False;1;0.15;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;369;-5004.584,240.2518;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleNode;368;-4638.944,-0.3209209;Inherit;False;0.1;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;107;-3407.743,-1265.166;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;68;-3272.042,-1267.734;Inherit;False;WaveTile;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleTimeNode;43;-4487.345,0.7766876;Inherit;False;1;0;FLOAT;0.01;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;53;-4301.596,-123.3793;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;101;-4167.346,31.80211;Inherit;False;68;WaveTile;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;113;-4127.348,-95.25723;Inherit;False;Constant;_Vector0;Vector 0;15;0;Create;True;0;0;0;False;0;False;1,1;1,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TexturePropertyNode;88;-3891.171,188.9039;Inherit;True;Property;_WavesNormal;Waves Normal;0;3;[NoScaleOffset];[Normal];[SingleLineTexture];Create;True;0;0;0;False;2;Header(Maps);Space(7);False;None;None;True;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.PannerNode;112;-3905.534,-37.77647;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;127;-3939.174,90.67975;Inherit;False;Property;_WavesNormalIntensity;Waves Normal Intensity;7;0;Create;True;0;0;0;False;0;False;1;0.15;0;2;0;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;370;-5008.393,1331.049;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;114;-3903.747,-158.6633;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;179;-4683.186,1361.523;Inherit;False;1987.534;1167.486;;28;377;375;365;61;380;63;131;5;3;135;7;18;138;64;136;17;19;6;186;188;183;132;382;383;384;387;388;389;Water Foam;1,1,1,1;0;0
Node;AmplifyShaderEditor.WorldPosInputsNode;388;-4732.61,1650.283;Inherit;True;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ScaleNode;132;-4599.445,1871.712;Inherit;False;0.1;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;38;-3555.847,21.62537;Inherit;True;Property;_TextureSample0;Texture Sample 0;8;1;[Normal];Create;True;0;0;0;False;0;False;-1;None;None;True;0;True;bump;Auto;True;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;46;-3554.048,-186.5422;Inherit;True;Property;_TextureSample1;Texture Sample 1;9;1;[Normal];Create;True;0;0;0;False;0;False;-1;None;None;True;0;True;bump;Auto;True;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;157;-4659.584,-1026.889;Inherit;False;1873.944;744.1856;;14;230;158;211;361;360;356;355;84;227;85;229;364;83;140;Water Depth;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;84;-4643.805,-677.4294;Inherit;False;Property;_DepthDistance;Depth Distance;14;0;Create;True;0;0;0;False;0;False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;343;-4679.51,468.8801;Inherit;False;1890.645;833.2174;;16;282;302;342;336;340;339;337;338;278;344;281;276;277;274;341;273;Water Refraction;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;63;-4669.472,2140.098;Inherit;False;Property;_FoamDensity;Foam Density;13;0;Create;True;0;0;0;False;0;False;0.5105882;0.392;0.1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;384;-4501.974,1781.926;Inherit;False;Property;_FoamTile;Foam Tile;10;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;51;-3199.566,-86.7878;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode;387;-4496.997,1684.355;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ScaleNode;131;-4597.672,1947.424;Inherit;False;50;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;5;-4407.835,2034.939;Inherit;False;Constant;_VoronoiScale;Voronoi Scale;7;0;Create;True;0;0;0;False;0;False;1;50;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;276;-4638.446,906.6764;Inherit;False;Property;_RefractionScale;Refraction Scale;15;0;Create;True;0;0;0;False;0;False;0.2;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;389;-4341.954,1709.058;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleTimeNode;3;-4408.707,1947.331;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;140;-4419.02,-667.8232;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0.1;False;2;FLOAT;100;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;135;-4388.493,2144.8;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;89;-3076.233,-91.45;Inherit;False;Normal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.VoronoiNode;61;-4183.733,1876.588;Inherit;True;0;0;1;0;1;False;1;False;True;False;4;0;FLOAT2;0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;3;FLOAT;0;FLOAT;1;FLOAT2;2
Node;AmplifyShaderEditor.ScaleNode;344;-4367.545,913.1115;Inherit;False;0.1;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;83;-4206.59,-692.8423;Inherit;False;True;False;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;281;-4399.849,783.9325;Inherit;False;89;Normal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TexturePropertyNode;380;-4182.038,1591.325;Inherit;True;Property;_FoamTexture;Foam Texture;9;1;[SingleLineTexture];Create;True;0;0;0;False;0;False;ab397318286c3d3408185b3651c4a5eb;ab397318286c3d3408185b3651c4a5eb;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.RangedFloatNode;7;-4027.416,2182.883;Inherit;False;Property;_FoamContrast;Foam Contrast;11;0;Create;True;0;0;0;False;0;False;0;0.7;-5;5;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;364;-3945.969,-683.0903;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;365;-3858.618,1888.586;Inherit;False;Property;_FoamColor;Foam Color;8;0;Create;True;0;0;0;False;2;Header(Foam Settings);Sapce(5);False;1,1,1,0;0.443152,1,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;375;-3939.901,1590.427;Inherit;True;Property;_TextureSample3;Texture Sample 3;15;0;Create;True;0;0;0;False;0;False;-1;ab397318286c3d3408185b3651c4a5eb;ab397318286c3d3408185b3651c4a5eb;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;278;-4212.066,830.9935;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GrabScreenPosition;273;-4353.181,555.8664;Inherit;False;0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;377;-3619.893,1741.837;Inherit;False;3;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.ClampOpNode;138;-3748.494,2188.015;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;-1;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;227;-3941.246,-503.9413;Inherit;False;Property;_WaterColor;Water Color;1;0;Create;True;0;0;0;False;2;Header(Settings);Space(5);False;0,0,0,0;0,0.310371,0.6886792,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;277;-4048.125,723.0851;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;18;-4076.458,2352.524;Inherit;False;Property;_FoamDistance;Foam Distance;12;0;Create;True;0;0;0;False;0;False;0;0.7;0;5;0;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;229;-3888.343,-597.7963;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;85;-3944.845,-937.4282;Inherit;False;Property;_DepthColor;Depth Color;2;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0.7075471,0.7008876,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ScreenDepthNode;337;-3865.318,896.3377;Inherit;False;0;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;339;-3838.009,1090.226;Inherit;False;Constant;_Float1;Float 1;13;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;340;-3837.681,1185.043;Inherit;False;Constant;_Float2;Float 2;13;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;356;-3712.938,-563.5092;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;355;-3712.49,-828.2192;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.OneMinusNode;136;-3571.863,2188.496;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;17;-3765.193,2335.527;Inherit;False;True;False;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;382;-3415.345,1740.632;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SurfaceDepthNode;338;-3949.401,990.4517;Inherit;False;0;1;0;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScreenColorNode;274;-3739.576,719.9767;Inherit;False;Global;_GrabScreen0;Grab Screen 0;12;0;Create;True;0;0;0;False;0;False;Object;-1;False;False;False;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PowerNode;6;-3277.63,1740.547;Inherit;False;False;2;0;COLOR;0,0,0,0;False;1;FLOAT;1;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;360;-3562.759,-763.9302;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;361;-3563.759,-610.9302;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ScreenColorNode;341;-3739.279,555.3654;Inherit;False;Global;_GrabScreen1;Grab Screen 1;12;0;Create;True;0;0;0;False;0;False;Instance;274;False;False;False;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ConditionalIfNode;336;-3624.686,1003.005;Inherit;False;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;19;-3471.737,2354.854;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;383;-3105.913,1740.383;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;230;-3407.008,-698.364;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;342;-3436.551,724.7099;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;211;-3244.841,-698.358;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;186;-2977.549,1739.022;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;302;-3235.646,725.1398;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;188;-2851.165,1740.025;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;346;-2497.432,2.734185;Inherit;False;Property;_ShoreOpacity;Shore Opacity;16;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;282;-3047.97,719.4788;Inherit;False;Refractions;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;158;-3060.034,-703.2271;Inherit;False;Depth;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;183;-2876.31,1850.328;Inherit;False;Foam;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;292;-2446.545,-389.9474;Inherit;False;282;Refractions;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.DepthFade;345;-2196.995,-19.85512;Inherit;False;True;False;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;209;-2446.957,-466.2645;Inherit;False;158;Depth;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;351;-1933.858,-15.9605;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;194;-2080.647,-128.4403;Inherit;False;Property;_Opacity;Opacity;3;0;Create;True;0;0;0;False;0;False;0;0.8;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;191;-2009.184,-360.6124;Inherit;False;183;Foam;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;323;-2169.146,-451.6337;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;64;-3469.378,1970.205;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;103;-1788.448,-324.6163;Inherit;False;89;Normal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;25;-1734.732,-446.8278;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;16;-1879.195,-227.412;Inherit;False;Property;_Smoothness;Smoothness;4;0;Create;True;0;0;0;False;0;False;0;0.9;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;350;-1750.915,-75.26579;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;-1514.26,-391.2095;Float;False;True;-1;2;;0;0;Standard;Animpic/SyleWater;False;False;False;False;False;False;False;False;True;False;True;True;False;False;True;True;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Transparent;0.5;True;False;0;False;Transparent;;Transparent;All;18;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;2;5;False;-1;10;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;106;0;66;1
WireConnection;106;1;66;3
WireConnection;369;0;58;0
WireConnection;368;0;369;0
WireConnection;107;0;106;0
WireConnection;107;1;100;0
WireConnection;68;0;107;0
WireConnection;43;0;368;0
WireConnection;53;0;43;0
WireConnection;112;0;101;0
WireConnection;112;2;113;0
WireConnection;112;1;43;0
WireConnection;370;0;58;0
WireConnection;114;0;101;0
WireConnection;114;2;113;0
WireConnection;114;1;53;0
WireConnection;132;0;370;0
WireConnection;38;0;88;0
WireConnection;38;1;112;0
WireConnection;38;5;127;0
WireConnection;38;7;88;1
WireConnection;46;0;88;0
WireConnection;46;1;114;0
WireConnection;46;5;127;0
WireConnection;46;7;88;1
WireConnection;51;0;46;0
WireConnection;51;1;38;0
WireConnection;387;0;388;1
WireConnection;387;1;388;3
WireConnection;131;0;132;0
WireConnection;389;0;387;0
WireConnection;389;1;384;0
WireConnection;3;0;131;0
WireConnection;140;0;84;0
WireConnection;135;0;63;0
WireConnection;89;0;51;0
WireConnection;61;0;389;0
WireConnection;61;1;3;0
WireConnection;61;2;5;0
WireConnection;61;3;135;0
WireConnection;344;0;276;0
WireConnection;83;0;140;0
WireConnection;364;0;83;0
WireConnection;375;0;380;0
WireConnection;375;1;61;2
WireConnection;278;0;281;0
WireConnection;278;1;344;0
WireConnection;377;0;375;0
WireConnection;377;1;365;0
WireConnection;377;2;61;0
WireConnection;138;0;7;0
WireConnection;277;0;273;0
WireConnection;277;1;278;0
WireConnection;229;0;364;0
WireConnection;337;0;277;0
WireConnection;356;0;229;0
WireConnection;356;1;227;0
WireConnection;355;0;85;0
WireConnection;355;1;364;0
WireConnection;136;0;138;0
WireConnection;17;0;18;0
WireConnection;382;0;377;0
WireConnection;274;0;277;0
WireConnection;6;0;382;0
WireConnection;6;1;136;0
WireConnection;360;0;355;0
WireConnection;361;0;356;0
WireConnection;341;0;273;0
WireConnection;336;0;337;0
WireConnection;336;1;338;0
WireConnection;336;2;339;0
WireConnection;336;4;340;0
WireConnection;19;0;17;0
WireConnection;383;0;6;0
WireConnection;383;1;19;0
WireConnection;230;0;360;0
WireConnection;230;1;361;0
WireConnection;342;0;341;0
WireConnection;342;1;274;0
WireConnection;342;2;336;0
WireConnection;211;0;230;0
WireConnection;186;0;383;0
WireConnection;186;1;19;0
WireConnection;302;0;342;0
WireConnection;188;0;186;0
WireConnection;282;0;302;0
WireConnection;158;0;211;0
WireConnection;183;0;188;0
WireConnection;345;0;346;0
WireConnection;351;0;345;0
WireConnection;323;0;209;0
WireConnection;323;1;292;0
WireConnection;323;2;209;0
WireConnection;64;0;377;0
WireConnection;25;0;323;0
WireConnection;25;1;191;0
WireConnection;350;0;194;0
WireConnection;350;1;351;0
WireConnection;0;0;25;0
WireConnection;0;1;103;0
WireConnection;0;4;16;0
WireConnection;0;9;350;0
ASEEND*/
//CHKSM=59F025239AD0AB554C0854591FE1E68FA35685ED