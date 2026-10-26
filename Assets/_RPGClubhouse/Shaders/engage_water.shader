Shader "Engage/Water_Ver_0.4"
{
	Properties
	{
		[Header(Main Properties)] [Space(5)]
		_water_color_1("Water Main Color", Color) = (0.08962262,0.8585987,1,0)
		_water_color_2("Water Specular Tint", Color) = (0.3620951,0.3620951,0.9528248,0)
		_alpha("Alpha", Range(0 , 1)) = 0.5
		_alpha_falloff("Alpha Falloff", Range(-1 , 1)) = 0
		_smoothness("Smoothness", Range(0 , 1)) = 0.5
		_scale("Scale", Range(0.1 , 100)) = 50
		[Space(10)][Header(Textures Set)][Space(5)]
		_Normal_Texture("Wave Normals", 2D) = "bump" {}
		_waveStrength("Normal Strength", Range(0 , 1)) = 1
		[Space(10)]_foam_mask_texture("Foam Mask", 2D) = "white" {}
		_foam_texture("Foam Texture", 2D) = "white" {}

		[Space(10)][Header(Foam Properties)][Space(5)]
		_foam_strength("Foam Strength", Float) = 3.16
		_foam_falloff("Foam Amount", Float) = 1.5

		[Space(10)][Header(Animation Settings)][Space(5)]
		_Normal_1_speed("Normal Speed A", Vector) = (0,0,0,0)
		_Normal_2_speed("Normal Speed B", Vector) = (1,1,0,0)
		_wave_speed("Main Wave Speed", Vector) = (1.5,0,0,0)
		_foam_PanSpeed_1("Foam Speed A", Vector) = (1,0,0,0)
		_foam_PanSpeed_2("Foam Speed B", Vector) = (2,0,0,0)
		[HideInInspector] _texcoord("", 2D) = "white" {}

	}

		SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" }
		Cull Back
		CGPROGRAM
		#include "UnityStandardUtils.cginc"
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma surface surf Standard alpha:fade keepalpha noshadow
		struct Input
		{
			float2 uv_texcoord;
			float3 worldNormal;
			INTERNAL_DATA
			float3 worldPos;
		};

		uniform sampler2D _Normal_Texture;
		uniform float4 _Normal_Texture_ST;
		uniform float2 _Normal_1_speed;
		uniform float _waveStrength;
		uniform float2 _Normal_2_speed;
		uniform sampler2D _foam_mask_texture;
		uniform float2 _wave_speed;
		uniform float4 _foam_mask_texture_ST;
		uniform float _foam_falloff;
		uniform sampler2D _foam_texture;
		uniform float2 _foam_PanSpeed_1;
		uniform float2 _foam_PanSpeed_2;
		uniform float _foam_strength;
		uniform float4 _water_color_1;
		uniform float4 _water_color_2;
		half _alpha;
		half _smoothness;
		half _scale;
		half _alpha_falloff;

		void surf(Input i , inout SurfaceOutputStandard o)
		{
			float2 uv_Normals = i.uv_texcoord * _Normal_Texture_ST.xy + _Normal_Texture_ST.zw;
			float2 uv_TexCoord10 = uv_Normals * float2(_scale,_scale) + float2(0.5,0.5);
			float2 panner11 = (0.01 * _Time.y * _Normal_1_speed + uv_TexCoord10);
			float2 uv_TexCoord39 = uv_Normals * float2((_scale * -1.25),(_scale * -1.25));
			float2 panner12 = (0.01 * _Time.y * _Normal_2_speed + uv_TexCoord39);
			float3 temp_output_4_0 = BlendNormals(UnpackScaleNormal(tex2D(_Normal_Texture, panner11), _waveStrength) , UnpackScaleNormal(tex2D(_Normal_Texture, panner12), _waveStrength));
			o.Normal = temp_output_4_0;
			float2 uv_foam_mask_texture = i.uv_texcoord * _foam_mask_texture_ST.xy * _scale + _foam_mask_texture_ST.zw;
			float2 panner69 = (0.02 * _Time.y * _wave_speed + uv_foam_mask_texture);
			float2 uv_TexCoord124 = i.uv_texcoord * float2((_scale * 2),(_scale * 2));
			float2 panner125 = (0.01 * _Time.y * _foam_PanSpeed_1 + uv_TexCoord124);
			float2 uv_TexCoord129 = i.uv_texcoord * float2((_scale * -1.1), (_scale * -1.1));
			float2 panner130 = (0.01 * _Time.y * _foam_PanSpeed_2 + uv_TexCoord129);
			float3 ase_worldPos = i.worldPos;
			float3 ase_worldViewDir = Unity_SafeNormalize(UnityWorldSpaceViewDir(ase_worldPos));
			float dotResult102 = dot(normalize((WorldNormalVector(i , temp_output_4_0))) , ase_worldViewDir);
			float4 lerpResult94 = lerp(_water_color_1 , _water_color_2 , saturate(pow((1.0 - dotResult102) , 5.0)));
			o.Albedo = (saturate(((pow(tex2D(_foam_mask_texture, panner69).r , _foam_falloff) - min(tex2D(_foam_texture, panner125).r , tex2D(_foam_texture, panner130).r)) * _foam_strength)) + lerpResult94).rgb;
			o.Metallic = 0.0;
			o.Smoothness = _smoothness;
			float2 temp_output_20_0 = (float2(-1,-1) + (i.uv_texcoord - float2(0,0)) * (float2(1,1) - float2(-1,-1)) / (float2(1,1) - float2(0,0)));
			float2 break23 = (temp_output_20_0 * temp_output_20_0);
			o.Alpha = saturate(((1 + _alpha_falloff) - pow(((break23.x + break23.y) * 1) , 0.65))) * _alpha;
		}

		ENDCG
	}
}
