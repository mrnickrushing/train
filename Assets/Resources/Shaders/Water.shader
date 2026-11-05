Shader "Ironhorse/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.2, 0.46, 0.5, 0.7)
        _DeepColor ("Deep", Color) = (0.05, 0.19, 0.4, 0.95)
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
                float2 uv3 = i.positionWS.xz * 0.21 + _Time.y * float2(-0.031, 0.024); // small chop for the sun to glint on
                const float e = 0.012;
                float a1 = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv1).r, a2 = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv2).r, a3 = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv3).r;
                float n = a1 * 0.5 + a2 * 0.5;
                // Wave slopes from finite differences of the two swells and the chop: a real normal, not a tilt.
                float dx = (SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv1 + float2(e, 0)).r - a1) + (SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv2 + float2(e, 0)).r - a2) + (SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv3 + float2(e, 0)).r - a3) * 0.6;
                float dz = (SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv1 + float2(0, e)).r - a1) + (SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv2 + float2(0, e)).r - a2) + (SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, uv3 + float2(0, e)).r - a3) * 0.6;
                float depth = saturate(i.color.r);
                float4 c = lerp(_ShallowColor, _DeepColor, smoothstep(0.0, 0.75, depth));
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float fres = pow(1 - saturate(view.y), 3);
                // Foam where the water laps the shore, broken up and drifting; flecks of white on the crests out deep.
                float foamNoise = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, i.positionWS.xz * 0.3 + _Time.y * float2(0.05, -0.04)).r;
                float foam = saturate((0.16 - depth) / 0.16) * smoothstep(0.35, 0.7, foamNoise + (1 - depth) * 0.25);
                // From far away the chop would alias into glitter: calm the normal and the flecks with distance.
                float far = saturate((distance(i.positionWS, _WorldSpaceCameraPos) - 90) / 220);
                float flecks = smoothstep(0.8, 0.92, a3 * 0.6 + n * 0.4) * smoothstep(0.25, 0.6, depth) * 0.25 * (1 - far);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                float bump = lerp(7, 1.6, far);
                inputData.normalWS = normalize(float3(-dx * bump, 1, -dz * bump));
                inputData.viewDirectionWS = view;
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogCoord;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = lerp(c.rgb + n * 0.06, float3(0.92, 0.95, 0.95), saturate(foam * 0.85 + flecks));
                s.smoothness = lerp(0.93, 0.4, saturate(foam));
                s.occlusion = 1;
                s.alpha = saturate(c.a + fres * 0.3 + foam * 0.5);
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
