// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Animpic/Foliage"
{
	Properties
	{
		[SingleLineTexture][Header(Maps)][Space(7)]_Texture00("Texture", 2D) = "white" {}
		[SingleLineTexture]_SmoothnessTexture3("Smoothness", 2D) = "white" {}
		_Tiling("Tiling", Float) = 1
		[Header(Settings)][Space(5)]_Color1("Main Color", Color) = (1,1,1,0)
		_AlphaCutoff("Alpha Cutoff", Range( 0 , 1)) = 0.35
		_Smoothness("Smoothness", Range( 0 , 1)) = 0
		[Header(Second Color Settings)][Space(5)][Toggle(_COLOR2ENABLE_ON)] _Color2Enable("Enable", Float) = 0
		_Color2("Second Color", Color) = (0,0,0,0)
		[KeywordEnum(Vertex_Position_Based,UV_Based)] _Color2OverlayType("Overlay Method", Float) = 0
		_Color2Level("Level", Float) = 0.59
		_Color2Fade("Fade", Range( -1 , 1)) = -1
		[Header(Show Settings)][Space(5)][Toggle(_SNOW_ON)] _Snow("Enable", Float) = 0
		[SingleLineTexture]_SnowTexture("Snow Texture", 2D) = "white" {}
		_SnowColor("Snow Color", Color) = (0.8980392,0.8980392,0.8980392,0)
		[KeywordEnum(Vertex_Group,UV_Group)] _SnowOverlay("Snow Overlay", Float) = 0
		_SnowTiling1("Snow Tiling", Float) = 1
		_SnowLevel("SnowLevel", Float) = 0.59
		_SnowFade("SnowFade", Range( -2 , 2)) = -1
		[Header(Wind Settings)][Space(5)][Toggle(_WIND_ON)] _WIND("Enable", Float) = 1
		_WindForce("Force", Range( 0 , 1)) = 0.3
		_WindWavesScale("Waves Scale", Range( 0 , 1)) = 0.25
		_WindSpeed("Speed", Range( 0 , 1)) = 0.5
		[Toggle(_FIXTHEBASEOFFOLIAGE_ON)] _Fixthebaseoffoliage("Anchor the foliage base", Float) = 0
		[Header(Lighting Settings)][Space(5)]_DirectLightOffset("Direct Light Offset", Range( 0 , 1)) = 0
		_DirectLightInt("Direct Light Int", Range( 1 , 10)) = 1
		_IndirectLightInt("Indirect Light Int", Range( 1 , 10)) = 1
		_TranslucencyInt("Translucency Int", Range( 0 , 100)) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Grass"  "Queue" = "Geometry+0" }
		Cull Off
		CGINCLUDE
		#include "UnityPBSLighting.cginc"
		#include "UnityShaderVariables.cginc"
		#include "UnityCG.cginc"
		#include "Lighting.cginc"
		#pragma target 3.0
		#pragma shader_feature_local _WIND_ON
		#pragma shader_feature_local _FIXTHEBASEOFFOLIAGE_ON
		#pragma shader_feature_local _SNOW_ON
		#pragma shader_feature_local _COLOR2ENABLE_ON
		#pragma shader_feature_local _COLOR2OVERLAYTYPE_VERTEX_POSITION_BASED _COLOR2OVERLAYTYPE_UV_BASED
		#pragma shader_feature_local _SNOWOVERLAY_VERTEX_GROUP _SNOWOVERLAY_UV_GROUP
		#ifdef UNITY_PASS_SHADOWCASTER
			#undef INTERNAL_DATA
			#undef WorldReflectionVector
			#undef WorldNormalVector
			#define INTERNAL_DATA half3 internalSurfaceTtoW0; half3 internalSurfaceTtoW1; half3 internalSurfaceTtoW2;
			#define WorldReflectionVector(data,normal) reflect (data.worldRefl, half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal)))
			#define WorldNormalVector(data,normal) half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal))
		#endif
		struct Input
		{
			float3 worldPos;
			float2 uv_texcoord;
			float3 worldNormal;
			INTERNAL_DATA
		};

		struct SurfaceOutputCustomLightingCustom
		{
			half3 Albedo;
			half3 Normal;
			half3 Emission;
			half Metallic;
			half Smoothness;
			half Occlusion;
			half Alpha;
			Input SurfInput;
			UnityGIInput GIData;
		};

		uniform float _WindSpeed;
		uniform float _WindWavesScale;
		uniform float _WindForce;
		uniform sampler2D _Texture00;
		uniform float _Tiling;
		uniform float _DirectLightOffset;
		uniform float4 _Color1;
		uniform float4 _Color2;
		uniform float _Color2Level;
		uniform float _Color2Fade;
		uniform float4 _SnowColor;
		uniform float _SnowLevel;
		uniform sampler2D _SnowTexture;
		uniform float _SnowTiling1;
		uniform float _SnowFade;
		uniform float _DirectLightInt;
		uniform float _IndirectLightInt;
		uniform sampler2D _SmoothnessTexture3;
		uniform float _Smoothness;
		uniform float _TranslucencyInt;
		uniform float _AlphaCutoff;


		float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }

		float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }

		float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }

		float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }

		float snoise( float3 v )
		{
			const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
			float3 i = floor( v + dot( v, C.yyy ) );
			float3 x0 = v - i + dot( i, C.xxx );
			float3 g = step( x0.yzx, x0.xyz );
			float3 l = 1.0 - g;
			float3 i1 = min( g.xyz, l.zxy );
			float3 i2 = max( g.xyz, l.zxy );
			float3 x1 = x0 - i1 + C.xxx;
			float3 x2 = x0 - i2 + C.yyy;
			float3 x3 = x0 - 0.5;
			i = mod3D289( i);
			float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
			float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
			float4 x_ = floor( j / 7.0 );
			float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
			float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
			float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
			float4 h = 1.0 - abs( x ) - abs( y );
			float4 b0 = float4( x.xy, y.xy );
			float4 b1 = float4( x.zw, y.zw );
			float4 s0 = floor( b0 ) * 2.0 + 1.0;
			float4 s1 = floor( b1 ) * 2.0 + 1.0;
			float4 sh = -step( h, 0.0 );
			float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
			float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
			float3 g0 = float3( a0.xy, h.x );
			float3 g1 = float3( a0.zw, h.y );
			float3 g2 = float3( a1.xy, h.z );
			float3 g3 = float3( a1.zw, h.w );
			float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
			g0 *= norm.x;
			g1 *= norm.y;
			g2 *= norm.z;
			g3 *= norm.w;
			float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
			m = m* m;
			m = m* m;
			float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
			return 42.0 * dot( m, px);
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float3 ase_worldPos = mul( unity_ObjectToWorld, v.vertex );
			float mulTime34 = _Time.y * ( _WindSpeed * 5 );
			float simplePerlin3D35 = snoise( ( ase_worldPos + mulTime34 )*_WindWavesScale );
			float temp_output_231_0 = ( simplePerlin3D35 * 0.01 );
			#ifdef _FIXTHEBASEOFFOLIAGE_ON
				float staticSwitch376 = ( temp_output_231_0 * pow( v.texcoord.xy.y , 2.0 ) );
			#else
				float staticSwitch376 = temp_output_231_0;
			#endif
			#ifdef _WIND_ON
				float staticSwitch341 = ( staticSwitch376 * ( _WindForce * 30 ) );
			#else
				float staticSwitch341 = 0.0;
			#endif
			float Wind191 = staticSwitch341;
			float3 temp_cast_0 = (Wind191).xxx;
			v.vertex.xyz += temp_cast_0;
			v.vertex.w = 1;
		}

		inline half4 LightingStandardCustomLighting( inout SurfaceOutputCustomLightingCustom s, half3 viewDir, UnityGI gi )
		{
			UnityGIInput data = s.GIData;
			Input i = s.SurfInput;
			half4 c = 0;
			#ifdef UNITY_PASS_FORWARDBASE
			float ase_lightAtten = data.atten;
			if( _LightColor0.a == 0)
			ase_lightAtten = 0;
			#else
			float3 ase_lightAttenRGB = gi.light.color / ( ( _LightColor0.rgb ) + 0.000001 );
			float ase_lightAtten = max( max( ase_lightAttenRGB.r, ase_lightAttenRGB.g ), ase_lightAttenRGB.b );
			#endif
			#if defined(HANDLE_SHADOWS_BLENDING_IN_GI)
			half bakedAtten = UnitySampleBakedOcclusion(data.lightmapUV.xy, data.worldPos);
			float zDist = dot(_WorldSpaceCameraPos - data.worldPos, UNITY_MATRIX_V[2].xyz);
			float fadeDist = UnityComputeShadowFadeDistance(data.worldPos, zDist);
			ase_lightAtten = UnityMixRealtimeAndBakedShadows(data.atten, bakedAtten, UnityComputeShadowFade(fadeDist));
			#endif
			float2 temp_cast_0 = (_Tiling).xx;
			float2 uv_TexCoord445 = i.uv_texcoord * temp_cast_0;
			float2 Tiling446 = uv_TexCoord445;
			float4 tex2DNode1 = tex2D( _Texture00, Tiling446 );
			float OpacityMask263 = tex2DNode1.a;
			float3 ase_worldPos = i.worldPos;
			#if defined(LIGHTMAP_ON) && UNITY_VERSION < 560 //aseld
			float3 ase_worldlightDir = 0;
			#else //aseld
			float3 ase_worldlightDir = Unity_SafeNormalize( UnityWorldSpaceLightDir( ase_worldPos ) );
			#endif //aseld
			float3 ase_worldNormal = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float3 ase_normWorldNormal = normalize( ase_worldNormal );
			float dotResult413 = dot( ase_worldlightDir , ase_normWorldNormal );
			#if defined(LIGHTMAP_ON) && ( UNITY_VERSION < 560 || ( defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK) && defined(SHADOWS_SCREEN) ) )//aselc
			float4 ase_lightColor = 0;
			#else //aselc
			float4 ase_lightColor = _LightColor0;
			#endif //aselc
			float4 temp_output_10_0 = ( _Color1 * tex2DNode1 );
			float3 ase_vertex3Pos = mul( unity_WorldToObject, float4( i.worldPos , 1 ) );
			#if defined(_COLOR2OVERLAYTYPE_VERTEX_POSITION_BASED)
				float staticSwitch470 = ase_vertex3Pos.y;
			#elif defined(_COLOR2OVERLAYTYPE_UV_BASED)
				float staticSwitch470 = i.uv_texcoord.y;
			#else
				float staticSwitch470 = ase_vertex3Pos.y;
			#endif
			float SecondColorMask476 = saturate( ( ( staticSwitch470 + _Color2Level ) * ( _Color2Fade * 2 ) ) );
			float4 lerpResult332 = lerp( temp_output_10_0 , ( _Color2 * tex2D( _Texture00, Tiling446 ) ) , SecondColorMask476);
			#ifdef _COLOR2ENABLE_ON
				float4 staticSwitch340 = lerpResult332;
			#else
				float4 staticSwitch340 = temp_output_10_0;
			#endif
			#if defined(_SNOWOVERLAY_VERTEX_GROUP)
				float staticSwitch514 = ase_vertex3Pos.y;
			#elif defined(_SNOWOVERLAY_UV_GROUP)
				float staticSwitch514 = i.uv_texcoord.y;
			#else
				float staticSwitch514 = ase_vertex3Pos.y;
			#endif
			float2 temp_cast_1 = (_SnowTiling1).xx;
			float2 uv_TexCoord527 = i.uv_texcoord * temp_cast_1;
			float4 temp_output_518_0 = saturate( ( ( staticSwitch514 + _SnowLevel + tex2D( _SnowTexture, uv_TexCoord527 ) ) * ( _SnowFade * 2 ) ) );
			float4 SnowMask314 = temp_output_518_0;
			float4 lerpResult504 = lerp( staticSwitch340 , _SnowColor , SnowMask314);
			#ifdef _SNOW_ON
				float4 staticSwitch505 = lerpResult504;
			#else
				float4 staticSwitch505 = staticSwitch340;
			#endif
			float4 Albedo259 = staticSwitch505;
			float4 DirectLight440 = ( ( ( 1.0 - saturate( (dotResult413*1.0 + _DirectLightOffset) ) ) * ase_lightAtten ) * ase_lightColor * Albedo259 * _DirectLightInt );
			UnityGI gi462 = gi;
			float3 diffNorm462 = ase_worldNormal;
			gi462 = UnityGI_Base( data, 1, diffNorm462 );
			float3 indirectDiffuse462 = gi462.indirect.diffuse + diffNorm462 * 0.0001;
			float4 IndirectLight439 = ( float4( indirectDiffuse462 , 0.0 ) * Albedo259 * _IndirectLightInt );
			SurfaceOutputStandard s443 = (SurfaceOutputStandard ) 0;
			s443.Albedo = float3( 0,0,0 );
			s443.Normal = ase_worldNormal;
			s443.Emission = float3( 0,0,0 );
			s443.Metallic = 0.0;
			s443.Smoothness = ( tex2D( _SmoothnessTexture3, Tiling446 ) * _Smoothness ).r;
			s443.Occlusion = 1.0;

			data.light = gi.light;

			UnityGI gi443 = gi;
			#ifdef UNITY_PASS_FORWARDBASE
			Unity_GlossyEnvironmentData g443 = UnityGlossyEnvironmentSetup( s443.Smoothness, data.worldViewDir, s443.Normal, float3(0,0,0));
			gi443 = UnityGlobalIllumination( data, s443.Occlusion, s443.Normal, g443 );
			#endif

			float3 surfResult443 = LightingStandard ( s443, viewDir, gi443 ).rgb;
			surfResult443 += s443.Emission;

			#ifdef UNITY_PASS_FORWARDADD//443
			surfResult443 -= s443.Emission;
			#endif//443
			float3 Smoothness441 = saturate( surfResult443 );
			float3 ase_worldViewDir = Unity_SafeNormalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			float dotResult401 = dot( ase_worldlightDir , ase_worldViewDir );
			float TranslucencyMask417 = (-dotResult401*1.0 + -0.2);
			float dotResult399 = dot( ase_worldlightDir , ase_normWorldNormal );
			float4 Translucency442 = saturate( ( ( TranslucencyMask417 * ( ( ( (dotResult399*1.0 + 1.0) * ase_lightAtten ) * ase_lightColor * Albedo259 ) * 0.25 ) ) * _TranslucencyInt ) );
			c.rgb = ( DirectLight440 + IndirectLight439 + float4( Smoothness441 , 0.0 ) + Translucency442 ).rgb;
			c.a = 1;
			clip( OpacityMask263 - _AlphaCutoff );
			return c;
		}

		inline void LightingStandardCustomLighting_GI( inout SurfaceOutputCustomLightingCustom s, UnityGIInput data, inout UnityGI gi )
		{
			s.GIData = data;
		}

		void surf( Input i , inout SurfaceOutputCustomLightingCustom o )
		{
			o.SurfInput = i;
			o.Normal = float3(0,0,1);
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf StandardCustomLighting keepalpha fullforwardshadows nolightmap  nodynlightmap nodirlightmap nometa noforwardadd vertex:vertexDataFunc 

		ENDCG
		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			ZWrite On
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_shadowcaster
			#pragma multi_compile UNITY_PASS_SHADOWCASTER
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#include "HLSLSupport.cginc"
			#if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
				#define CAN_SKIP_VPOS
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			struct v2f
			{
				V2F_SHADOW_CASTER;
				float2 customPack1 : TEXCOORD1;
				float4 tSpace0 : TEXCOORD2;
				float4 tSpace1 : TEXCOORD3;
				float4 tSpace2 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			v2f vert( appdata_full v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_INITIALIZE_OUTPUT( v2f, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				Input customInputData;
				vertexDataFunc( v, customInputData );
				float3 worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
				half3 worldNormal = UnityObjectToWorldNormal( v.normal );
				half3 worldTangent = UnityObjectToWorldDir( v.tangent.xyz );
				half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				half3 worldBinormal = cross( worldNormal, worldTangent ) * tangentSign;
				o.tSpace0 = float4( worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x );
				o.tSpace1 = float4( worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y );
				o.tSpace2 = float4( worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z );
				o.customPack1.xy = customInputData.uv_texcoord;
				o.customPack1.xy = v.texcoord;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
				return o;
			}
			half4 frag( v2f IN
			#if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
			) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				Input surfIN;
				UNITY_INITIALIZE_OUTPUT( Input, surfIN );
				surfIN.uv_texcoord = IN.customPack1.xy;
				float3 worldPos = float3( IN.tSpace0.w, IN.tSpace1.w, IN.tSpace2.w );
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldPos = worldPos;
				surfIN.worldNormal = float3( IN.tSpace0.z, IN.tSpace1.z, IN.tSpace2.z );
				surfIN.internalSurfaceTtoW0 = IN.tSpace0.xyz;
				surfIN.internalSurfaceTtoW1 = IN.tSpace1.xyz;
				surfIN.internalSurfaceTtoW2 = IN.tSpace2.xyz;
				SurfaceOutputCustomLightingCustom o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutputCustomLightingCustom, o )
				surf( surfIN, o );
				UnityGI gi;
				UNITY_INITIALIZE_OUTPUT( UnityGI, gi );
				o.Alpha = LightingStandardCustomLighting( o, worldViewDir, gi ).a;
				#if defined( CAN_SKIP_VPOS )
				float2 vpos = IN.pos;
				#endif
				SHADOW_CASTER_FRAGMENT( IN )
			}
			ENDCG
		}
	}
	Fallback "Diffuse"
}
/*ASEBEGIN
Version=18935
711.8491;225.5094;1049.66;622.9246;4094.733;-1783.898;1.448882;True;False
Node;AmplifyShaderEditor.CommentaryNode;506;-3559.228,-454.4722;Inherit;False;1512.547;530.5914;Comment;10;472;473;474;475;469;468;467;471;476;470;Second Color;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;447;-2787.07,436.0726;Inherit;False;750;278.1;;3;446;444;445;Tiling;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;313;-5466.776,-482.5264;Inherit;False;1858.103;826.4135;;14;314;526;512;527;513;515;525;514;520;516;519;517;518;521;Snow Mask;1,1,1,1;0;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;467;-3509.228,-245.7289;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PosVertexDataNode;468;-3473.947,-404.4722;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;444;-2721.233,552.7335;Inherit;False;Property;_Tiling;Tiling;2;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;445;-2533.233,533.7333;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;469;-3056.076,-117.3869;Inherit;False;Property;_Color2Level;Level;9;0;Create;False;0;0;0;False;0;False;0.59;-0.24;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;470;-3249.846,-301.1938;Inherit;False;Property;_Color2OverlayType;Overlay Method;8;0;Create;False;0;0;0;False;0;False;0;0;0;True;;KeywordEnum;2;Vertex_Position_Based;UV_Based;Create;True;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;526;-5436.462,156.2937;Inherit;False;Property;_SnowTiling1;Snow Tiling;15;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;471;-3164.601,-38.99401;Inherit;False;Property;_Color2Fade;Fade;10;0;Create;False;0;0;0;False;0;False;-1;0.8;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;446;-2258.231,529.7333;Inherit;False;Tiling;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;262;-5465.631,-1647.714;Inherit;False;2646.528;1067.095;;22;448;450;449;259;263;340;332;367;10;337;3;247;1;366;368;156;499;500;501;503;504;505;Albedo;1,1,1,1;0;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;513;-5314.288,-214.873;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;527;-5248.462,137.2935;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PosVertexDataNode;512;-5279.007,-373.6163;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;473;-2880.562,-290.6764;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleNode;472;-2863.757,-35.52328;Inherit;False;2;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;514;-5054.906,-270.3379;Inherit;False;Property;_SnowOverlay;Snow Overlay;14;0;Create;False;0;0;0;False;0;False;0;0;0;True;;KeywordEnum;2;Vertex_Group;UV_Group;Create;True;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;520;-4861.136,-86.531;Inherit;False;Property;_SnowLevel;SnowLevel;16;0;Create;False;0;0;0;False;0;False;0.59;-0.24;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;515;-4968.476,-5.95341;Inherit;False;Property;_SnowFade;SnowFade;17;0;Create;True;0;0;0;False;0;False;-1;0.8;-2;2;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;448;-5412.208,-1091.293;Inherit;False;446;Tiling;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;474;-2663.131,-129.1188;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;525;-4972.251,110.6411;Inherit;True;Property;_SnowTexture;Snow Texture;12;1;[SingleLineTexture];Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;519;-4685.622,-259.8205;Inherit;True;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ScaleNode;516;-4668.817,-4.667384;Inherit;False;2;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;449;-5202.812,-1174.035;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SaturateNode;475;-2479.435,-129.5724;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;368;-5179.614,-1144.078;Inherit;True;Property;_Texture00;Texture;0;1;[SingleLineTexture];Create;False;0;0;0;False;2;Header(Maps);Space(7);False;None;None;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.WireNode;450;-5200.624,-958.4913;Inherit;False;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;517;-4468.191,-98.2629;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;476;-2290.002,-134.9381;Inherit;True;SecondColorMask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;1;-4862.312,-1351.035;Inherit;True;Property;_LeavesTexture;Leaves Texture;0;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;366;-4861.479,-960.2595;Inherit;True;Property;_TextureSample0;Texture Sample 0;18;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;3;-4780.577,-1540.59;Inherit;False;Property;_Color1;Main Color;3;0;Create;False;0;0;0;False;2;Header(Settings);Space(5);False;1,1,1,0;0.1386746,0.461,0.2773493,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;247;-4777.857,-1146.771;Inherit;False;Property;_Color2;Second Color;7;0;Create;False;0;0;0;False;0;False;0,0,0,0;0.1597293,0.4433961,0.04433948,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SaturateNode;518;-4284.495,-98.7165;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;10;-4457.864,-1451.376;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;337;-4495.567,-1227.672;Inherit;False;476;SecondColorMask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;367;-4461.048,-1057.932;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.CommentaryNode;392;-5462.447,1272.131;Inherit;False;2898.686;1981.707;;5;425;412;404;394;393;Lighting;0.7,0.686289,0.49,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;314;-3875.003,-96.92156;Inherit;True;SnowMask;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;332;-4191.402,-1271.017;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.CommentaryNode;393;-5361.706,2675.193;Inherit;False;2228.953;513.9716;;17;442;435;434;427;424;421;418;415;411;408;406;402;400;399;398;397;463;Translucency;0.8,0.7843137,0.5607843,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;66;-5466.88,434.7253;Inherit;False;2621.259;742.2787;;18;191;341;188;56;345;190;36;376;359;356;231;35;358;357;182;228;34;344;Wind;1,1,1,1;0;0
Node;AmplifyShaderEditor.StaticSwitch;340;-3859.422,-1455.011;Inherit;False;Property;_Color2Enable;Enable;6;0;Create;False;0;0;0;False;2;Header(Second Color Settings);Space(5);False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ColorNode;501;-4267.281,-1044.148;Inherit;False;Property;_SnowColor;Snow Color;13;0;Create;False;0;0;0;False;0;False;0.8980392,0.8980392,0.8980392,0;0.8980392,0.8980392,0.8980392,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;503;-3946.695,-905.8631;Inherit;False;314;SnowMask;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.CommentaryNode;394;-5364.779,1393.619;Inherit;False;1214.467;434.7671;;7;417;407;405;403;401;396;395;Translucency Mask;0.8,0.7843137,0.5607843,1;0;0
Node;AmplifyShaderEditor.WorldSpaceLightDirHlpNode;396;-5314.778,1443.62;Inherit;False;True;1;0;FLOAT;0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;395;-5252.485,1606.638;Inherit;False;World;True;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldSpaceLightDirHlpNode;398;-5316.385,2740.921;Inherit;False;True;1;0;FLOAT;0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;36;-5432.24,691.3121;Inherit;False;Property;_WindSpeed;Speed;21;0;Create;False;0;0;0;False;0;False;0.5;0.2;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;504;-3615.125,-1155.627;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.WorldNormalVector;397;-5286.607,2888.92;Inherit;False;True;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.StaticSwitch;505;-3357.783,-1452.906;Inherit;False;Property;_Snow;Enable;11;0;Create;False;0;0;0;False;2;Header(Show Settings);Space(5);False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ScaleNode;344;-5135.242,696.7037;Inherit;False;5;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;400;-5017.406,2950.468;Inherit;False;Constant;_Float1;Float 1;18;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;404;-5366.333,1896.834;Inherit;False;1679.855;716.6;;14;440;437;433;432;430;428;423;420;416;413;410;409;461;507;Direct Light;0.8,0.7843137,0.5607843,1;0;0
Node;AmplifyShaderEditor.DotProductOpNode;401;-5000.993,1524.467;Inherit;True;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DotProductOpNode;399;-5013.608,2808.92;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;403;-4886.512,1748.443;Inherit;False;Constant;_TranslucencyOffset;Translucency Offset;19;0;Create;True;0;0;0;False;0;False;-0.2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.NegateNode;405;-4787.39,1525.151;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;259;-3046.768,-1447.552;Inherit;False;Albedo;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LightAttenuation;463;-4764.616,2965.657;Inherit;False;0;1;FLOAT;0
Node;AmplifyShaderEditor.WorldNormalVector;409;-5293.338,2125.355;Inherit;False;True;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldPosInputsNode;228;-4965.367,528.4799;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldSpaceLightDirHlpNode;410;-5325.434,1969.801;Inherit;False;True;1;0;FLOAT;0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleTimeNode;34;-4965.56,696.4692;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;402;-4765.339,2810.053;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LightColorNode;411;-4509.65,2972.542;Inherit;False;0;3;COLOR;0;FLOAT3;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleAddOpNode;182;-4719.293,608.7031;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;407;-4624.75,1613.723;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;412;-4103.667,1396.346;Inherit;False;1479.169;401.6033;;7;441;436;426;422;419;414;443;Smoothness;0.8,0.7843137,0.5607843,1;0;0
Node;AmplifyShaderEditor.DotProductOpNode;413;-5026.275,2040.562;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;190;-4870.377,811.9411;Inherit;False;Property;_WindWavesScale;Waves Scale;20;0;Create;False;0;0;0;False;0;False;0.25;0.4;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;408;-4490.469,2862.259;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;416;-5247.354,2300.44;Inherit;False;Property;_DirectLightOffset;Direct Light Offset;23;0;Create;True;0;0;0;False;2;Header(Lighting Settings);Space(5);False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;406;-4527.381,3097.51;Inherit;False;259;Albedo;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;415;-4285.006,2949.23;Inherit;False;3;3;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;358;-4405.196,1070.673;Inherit;False;Constant;_Float0;Float 0;14;0;Create;True;0;0;0;False;0;False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;420;-4885.467,2041.694;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;414;-4064.032,1482.131;Inherit;False;446;Tiling;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;357;-4483.792,940.533;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.NoiseGeneratorNode;35;-4506.449,699.5806;Inherit;True;Simplex3D;False;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;0.4;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;417;-4396.828,1608.337;Inherit;False;TranslucencyMask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleNode;231;-4188.372,703.8373;Inherit;False;0.01;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;421;-4211.854,2836.586;Inherit;False;417;TranslucencyMask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;356;-4198.264,960.5025;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;419;-3862.988,1457.242;Inherit;True;Property;_SmoothnessTexture3;Smoothness;1;1;[SingleLineTexture];Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;422;-3838.436,1687.676;Inherit;False;Property;_Smoothness;Smoothness;5;0;Create;True;0;0;0;False;0;False;0;0.07;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;423;-4693.165,2046.97;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleNode;418;-4113.641,2948.857;Inherit;False;0.25;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;359;-3989.254,821.5805;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;427;-4047.613,3062.894;Inherit;False;Property;_TranslucencyInt;Translucency Int;26;0;Create;True;0;0;0;False;0;False;0;8;0;100;0;1;FLOAT;0
Node;AmplifyShaderEditor.LightAttenuation;461;-4582.841,2241.318;Inherit;False;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;424;-3916.838,2879.035;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;426;-3491.987,1583.242;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.OneMinusNode;507;-4549.112,2044.694;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;425;-3630.025,1900.546;Inherit;False;933.2305;378.5339;;5;439;438;431;429;462;Indirect Light;0.8,0.7843137,0.5607843,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;56;-3952.269,977.8912;Inherit;False;Property;_WindForce;Force;19;0;Create;False;0;0;0;False;0;False;0.3;0.48;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;433;-4326.078,2163.435;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;428;-4357.409,2432.611;Inherit;False;259;Albedo;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;431;-3489.251,2044.545;Inherit;False;259;Albedo;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.IndirectDiffuseLighting;462;-3535.912,1950.822;Inherit;False;Tangent;1;0;FLOAT3;0,0,1;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LightColorNode;432;-4339.132,2313.63;Inherit;False;0;3;COLOR;0;FLOAT3;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;429;-3590.024,2147.642;Inherit;False;Property;_IndirectLightInt;Indirect Light Int;25;0;Create;True;0;0;0;False;0;False;1;1;1;10;0;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleNode;345;-3665.352,982.3984;Inherit;False;30;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;434;-3718.531,2945.038;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.StaticSwitch;376;-3795.695,700.9271;Inherit;False;Property;_Fixthebaseoffoliage;Anchor the foliage base;22;0;Create;False;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CustomStandardSurface;443;-3307.774,1488.447;Inherit;False;Metallic;Tangent;6;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,1;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;1;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;430;-4460.419,2523.262;Inherit;False;Property;_DirectLightInt;Direct Light Int;24;0;Create;True;0;0;0;False;0;False;1;1;1;10;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;438;-3221.504,2026.079;Inherit;True;3;3;0;FLOAT3;0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;437;-4096.474,2334.455;Inherit;False;4;4;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;435;-3544.774,2945.197;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;436;-3029.443,1489.01;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;188;-3463.908,823.2846;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;-1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;442;-3365.886,2940.199;Inherit;False;Translucency;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;439;-2939.794,2021.035;Inherit;False;IndirectLight;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;441;-2832.614,1484.81;Inherit;False;Smoothness;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StaticSwitch;341;-3275.733,794.1859;Inherit;False;Property;_WIND;Enable;18;0;Create;False;0;0;0;False;2;Header(Wind Settings);Space(5);False;0;1;1;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;440;-3913.9,2329.491;Inherit;False;DirectLight;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;454;-2650.286,-990.9691;Inherit;False;442;Translucency;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;452;-2648.286,-1175.969;Inherit;False;439;IndirectLight;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;453;-2648.286,-1083.969;Inherit;False;441;Smoothness;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;191;-3060.489,793.7185;Inherit;False;Wind;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;451;-2640.286,-1262.969;Inherit;False;440;DirectLight;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;263;-4489.452,-1309.93;Inherit;False;OpacityMask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;500;-4864.87,-753.0747;Inherit;False;1;0;SAMPLER2D;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;236;-2414.381,-918.5184;Inherit;False;191;Wind;1;0;OBJECT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;499;-4368.934,-838.6364;Inherit;True;Property;_TextureSample1;Texture Sample 1;18;0;Create;True;0;0;0;False;0;False;-1;None;None;True;1;False;white;Auto;False;Instance;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;156;-3949.634,-716.5307;Inherit;False;Property;_AlphaCutoff;Alpha Cutoff;4;0;Create;True;0;0;0;False;0;False;0.35;0.35;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;521;-4101.418,-85.94488;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;455;-2382.285,-1165.969;Inherit;False;4;4;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT3;0,0,0;False;3;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;459;-2647.525,-1349.218;Inherit;False;263;OpacityMask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;151;-2186.056,-1406.464;Float;False;True;-1;2;;0;0;CustomLighting;Animpic/Foliage;False;False;False;False;False;False;True;True;True;False;True;True;False;False;False;False;False;False;False;False;False;Off;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Custom;0.45;True;True;0;True;Grass;;Geometry;All;18;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;5;False;-1;10;False;-1;0;5;False;-1;10;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;True;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;True;156;0;0;0;False;0.1;False;-1;0;False;-1;False;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;445;0;444;0
WireConnection;470;1;468;2
WireConnection;470;0;467;2
WireConnection;446;0;445;0
WireConnection;527;0;526;0
WireConnection;473;0;470;0
WireConnection;473;1;469;0
WireConnection;472;0;471;0
WireConnection;514;1;512;2
WireConnection;514;0;513;2
WireConnection;474;0;473;0
WireConnection;474;1;472;0
WireConnection;525;1;527;0
WireConnection;519;0;514;0
WireConnection;519;1;520;0
WireConnection;519;2;525;0
WireConnection;516;0;515;0
WireConnection;449;0;448;0
WireConnection;475;0;474;0
WireConnection;450;0;448;0
WireConnection;517;0;519;0
WireConnection;517;1;516;0
WireConnection;476;0;475;0
WireConnection;1;0;368;0
WireConnection;1;1;449;0
WireConnection;366;0;368;0
WireConnection;366;1;450;0
WireConnection;518;0;517;0
WireConnection;10;0;3;0
WireConnection;10;1;1;0
WireConnection;367;0;247;0
WireConnection;367;1;366;0
WireConnection;314;0;518;0
WireConnection;332;0;10;0
WireConnection;332;1;367;0
WireConnection;332;2;337;0
WireConnection;340;1;10;0
WireConnection;340;0;332;0
WireConnection;504;0;340;0
WireConnection;504;1;501;0
WireConnection;504;2;503;0
WireConnection;505;1;340;0
WireConnection;505;0;504;0
WireConnection;344;0;36;0
WireConnection;401;0;396;0
WireConnection;401;1;395;0
WireConnection;399;0;398;0
WireConnection;399;1;397;0
WireConnection;405;0;401;0
WireConnection;259;0;505;0
WireConnection;34;0;344;0
WireConnection;402;0;399;0
WireConnection;402;2;400;0
WireConnection;182;0;228;0
WireConnection;182;1;34;0
WireConnection;407;0;405;0
WireConnection;407;2;403;0
WireConnection;413;0;410;0
WireConnection;413;1;409;0
WireConnection;408;0;402;0
WireConnection;408;1;463;0
WireConnection;415;0;408;0
WireConnection;415;1;411;0
WireConnection;415;2;406;0
WireConnection;420;0;413;0
WireConnection;420;2;416;0
WireConnection;35;0;182;0
WireConnection;35;1;190;0
WireConnection;417;0;407;0
WireConnection;231;0;35;0
WireConnection;356;0;357;2
WireConnection;356;1;358;0
WireConnection;419;1;414;0
WireConnection;423;0;420;0
WireConnection;418;0;415;0
WireConnection;359;0;231;0
WireConnection;359;1;356;0
WireConnection;424;0;421;0
WireConnection;424;1;418;0
WireConnection;426;0;419;0
WireConnection;426;1;422;0
WireConnection;507;0;423;0
WireConnection;433;0;507;0
WireConnection;433;1;461;0
WireConnection;345;0;56;0
WireConnection;434;0;424;0
WireConnection;434;1;427;0
WireConnection;376;1;231;0
WireConnection;376;0;359;0
WireConnection;443;4;426;0
WireConnection;438;0;462;0
WireConnection;438;1;431;0
WireConnection;438;2;429;0
WireConnection;437;0;433;0
WireConnection;437;1;432;0
WireConnection;437;2;428;0
WireConnection;437;3;430;0
WireConnection;435;0;434;0
WireConnection;436;0;443;0
WireConnection;188;0;376;0
WireConnection;188;1;345;0
WireConnection;442;0;435;0
WireConnection;439;0;438;0
WireConnection;441;0;436;0
WireConnection;341;0;188;0
WireConnection;440;0;437;0
WireConnection;191;0;341;0
WireConnection;263;0;1;4
WireConnection;500;0;368;0
WireConnection;499;0;500;0
WireConnection;521;0;518;0
WireConnection;455;0;451;0
WireConnection;455;1;452;0
WireConnection;455;2;453;0
WireConnection;455;3;454;0
WireConnection;151;10;459;0
WireConnection;151;13;455;0
WireConnection;151;11;236;0
ASEEND*/
//CHKSM=8A8455DC6DA5634E049DC383AE0FF80C84CE67A7