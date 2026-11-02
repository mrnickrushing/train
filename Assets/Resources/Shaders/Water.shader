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
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_WaveTex); SAMPLER(sampler_WaveTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _WaveTex_ST;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float4 color : COLOR; float fogCoord : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                ws.y += sin(ws.x * 0.35 + _Time.y * 1.3) * 0.04 + cos(ws.z * 0.28 + _Time.y) * 0.04;
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.color = v.color;
                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv1 = i.positionWS.xz * 0.05 + _Time.y * float2(0.02, 0.013);
                float2 uv2 = i.positionWS.xz * 0.08 - _Time.y * float2(0.017, 0.021);
                float n = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv1).r * 0.5 + SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv2).r * 0.5;
                float4 c = lerp(_ShallowColor, _DeepColor, saturate(i.color.r));
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float fres = pow(1 - saturate(view.y), 3);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = normalize(float3((n - 0.5) * 0.6, 1, (n - 0.5) * 0.6));
                inputData.viewDirectionWS = view;
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogCoord;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = c.rgb + n * 0.08;
                s.smoothness = 0.92;
                s.occlusion = 1;
                s.alpha = saturate(c.a + fres * 0.3);
                half4 col = UniversalFragmentPBR(inputData, s);
                col.rgb = MixFog(col.rgb, inputData.fogCoord);
                col.a = s.alpha;
                return col;
            }
            ENDHLSL
        }
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
