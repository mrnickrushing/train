// Main material for all procedurally built geometry.
//   vertex color rgb : albedo
//   vertex color a   : 1 = normal surface, 0 = emissive at night (windows, lamps)
//   uv2.x / uv2.y    : smoothness / metallic
Shader "Ironhorse/VertexLit"
{
    Properties
    {
        _MainTex ("Detail (grayscale)", 2D) = "white" {}
        _DetailScale ("Detail Scale", Float) = 0
        _DetailStrength ("Detail Strength", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing

        sampler2D _MainTex;
        float _DetailScale;
        float _DetailStrength;
        float _NightGlow;      // global, set by DayNightCycle
        float4 _GlowColor;     // global

        struct Input
        {
            float4 vcol;
            float2 sm;
            float3 worldPos;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vcol = v.color;
            o.sm = v.texcoord1.xy;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 albedo = IN.vcol.rgb;
            if (_DetailScale > 0)
            {
                float d = tex2D(_MainTex, IN.worldPos.xz * _DetailScale).r;
                albedo *= lerp(1, d * 1.6 + 0.2, _DetailStrength);
            }
            float glow = (1 - IN.vcol.a);
            o.Albedo = albedo * (1 - glow * 0.6);
            o.Smoothness = IN.sm.x;
            o.Metallic = IN.sm.y;
            o.Emission = glow * _NightGlow * _GlowColor.rgb * (0.6 + albedo);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
