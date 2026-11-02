Shader "Ironhorse/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.25, 0.55, 0.55, 0.65)
        _DeepColor ("Deep", Color) = (0.08, 0.22, 0.32, 0.9)
        _WaveTex ("Wave Noise", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0

        fixed4 _ShallowColor, _DeepColor;
        sampler2D _WaveTex;

        struct Input { float3 worldPos; float3 viewDir; float4 vcol; };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            v.vertex.y += sin(wp.x * 0.35 + _Time.y * 1.3) * 0.04 + cos(wp.z * 0.28 + _Time.y) * 0.04;
            o.vcol = v.color;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv1 = IN.worldPos.xz * 0.05 + _Time.y * float2(0.02, 0.013);
            float2 uv2 = IN.worldPos.xz * 0.08 - _Time.y * float2(0.017, 0.021);
            float n = tex2D(_WaveTex, uv1).r * 0.5 + tex2D(_WaveTex, uv2).r * 0.5;
            float depth = saturate(IN.vcol.r);
            fixed4 c = lerp(_ShallowColor, _DeepColor, depth);
            float fres = pow(1 - saturate(dot(normalize(IN.viewDir), float3(0,1,0))), 3);
            o.Albedo = c.rgb + n * 0.08;
            o.Normal = normalize(float3((n - 0.5) * 0.6, 1, (n - 0.5) * 0.6));
            o.Smoothness = 0.92;
            o.Metallic = 0;
            o.Alpha = saturate(c.a + fres * 0.3);
        }
        ENDCG
    }
}
