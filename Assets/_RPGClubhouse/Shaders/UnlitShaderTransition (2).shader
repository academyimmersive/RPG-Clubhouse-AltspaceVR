Shader "Unlit/UnlitShaderTransition"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MainTex2 ("Texture", 2D) = "white" {}
        _Mix("Mix", Range(0, 1.1)) = 0

        _VRadius("Vignette Radius", Range(0.0, 1.0)) = 1
        _VSoft("Vignette Softness", Range(0.0, 1.0)) = 0.5
    }
    SubShader
    {
         ZWrite Off
         Blend SrcAlpha OneMinusSrcAlpha
         Tags { "RenderType"="Transparent" "Queue"="Transparent" }
         LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;            
            sampler2D _MainTex2;
            float4 _MainTex2_ST;
            float _Mix;
            half _VRadius;
            half _VSoft;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 centered_uv = i.uv - 0.5;
                float distFromCenter = distance(i.uv.xy, float2(0.5, 0.5));
                float vignette = sqrt(dot(centered_uv.xy, centered_uv.xy));
                float vignette2 = smoothstep(_VRadius, _VRadius - _VSoft, distFromCenter);
                fixed4 black = fixed4(0,0,0,0);
                // sample the texture
                fixed4 col = lerp(tex2D(_MainTex, i.uv) , tex2D(_MainTex2, i.uv),_Mix);
                col.a = lerp(0, 1, vignette2);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
