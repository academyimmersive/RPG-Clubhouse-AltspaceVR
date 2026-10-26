//Revision 2 - Added Metallic/Smooth values unique for Decal

Shader "Custom/DecalMat_Surface (bake compat)"
{
    Properties
    {
		[Header(Base Properties)]
        _Color ("Base Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
		[NoScaleOffset]
		_MetallicGlossMap("Metallic (RGB), Gloss (A)", 2D) = "black" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0
		[Header(Decal Properties)]
		[NoScaleOffset]
		_Decal("Decal (RGB)", 2D) = "white" {}
		_DecalColor("Decal Color", Color) = (1,1,1,1)
		_DecalTiling("DecalTiling", Vector) = (1,1,0,0)
        _DecalGlossiness ("Smoothness (Decal)", Range(0,1)) = 0.5
        _DecalMetallic ("Metallic (Decal)", Range(0,1)) = 0
		[NoScaleOffset]
		_BumpMap("Normal", 2D) = "bump" {}
        _Scale ("Scale", Range(0,1)) = 1

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;
		sampler2D _Decal;
		sampler2D _MetallicGlossMap;
		sampler2D _BumpMap;
		float4 _DecalTiling;
		fixed4 _Color;
		fixed4 _DecalColor;

        struct Input
        {
            float2 uv_MainTex;
			float2 uv4_Decal;
        };

        half _Glossiness;
        half _Metallic;
        float _Scale;

		half _DecalGlossiness;
		half _DecalMetallic;

        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_INSTANCING_BUFFER_END(Props)



        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			fixed4 decal = tex2D(_Decal, (IN.uv4_Decal + _DecalTiling.zw)*_DecalTiling.xy) * _DecalColor;
			float interpolant = saturate(floor(IN.uv4_Decal.x) + (1 - decal.a));
			o.Albedo = lerp(decal, c, interpolant);

			fixed4 mg = tex2D(_MetallicGlossMap, IN.uv_MainTex);
            o.Metallic = lerp(_DecalMetallic, saturate(mg.rgb + _Metallic), interpolant);
            o.Smoothness = lerp(_DecalGlossiness, saturate(mg.a + _Glossiness), interpolant);
			o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _Scale);

            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
