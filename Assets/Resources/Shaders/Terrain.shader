// Valley ground: up to six CC0 photo-texture layers (Resources/Textures/Terrain*), height-blended.
//   uv0.xy (TEXCOORD0) : weights of grass, dry grass
//   uv2.xy (TEXCOORD2) : weights of dirt, sand
//   uv3.xy (TEXCOORD3) : weights of rock, gravel
//   1 - sum of weights : untextured remainder (snow caps, river banks) uses the vertex color
//   vertex color       : the old flat color; used for the remainder and the built-in fallback
Shader "Ironhorse/Terrain"
{
    Properties
    {
        _TerrainAlbedo ("Albedo array (A = height)", 2DArray) = "" {}
        _TerrainNormal ("Normal array (R AO, G ny, B rough, A nx)", 2DArray) = "" {}
        _MacroTex ("Macro variation (grayscale)", 2D) = "gray" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D_ARRAY(_TerrainAlbedo); SAMPLER(sampler_TerrainAlbedo);
        TEXTURE2D_ARRAY(_TerrainNormal); SAMPLER(sampler_TerrainNormal);
        TEXTURE2D(_MacroTex); SAMPLER(sampler_MacroTex);
        CBUFFER_START(UnityPerMaterial)
            float4 _MacroTex_ST;
        CBUFFER_END
        // Meters per texture tile for grass, dry grass, dirt, sand, rock, gravel.
        static const float kTile[6] = { 4.5, 6.0, 5.0, 5.0, 9.0, 3.0 };
        // Per layer color correction so the photo textures sit in the game's warm palette.
        static const float3 kTint[6] = { float3(0.86, 1.04, 0.66), float3(1.08, 1.04, 0.66), float3(1.0, 0.95, 0.9), float3(1.05, 1.0, 0.95), float3(1.05, 0.95, 0.9), float3(1.0, 1.0, 1.0) };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 w01 : TEXCOORD0;
                float2 w23 : TEXCOORD2;
                float2 w45 : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 w0123 : TEXCOORD2;
                float2 w45 : TEXCOORD3;
                float fogCoord : TEXCOORD4;
                float4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.w0123 = float4(v.w01, v.w23);
                o.w45 = v.w45;
                o.color = v.color;
                o.fogCoord = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Flat layers sample the XZ plane; rock is triplanar so cliffs don't stretch.
            void SampleLayer(int i, float3 pos, float3 blend, float3 dx, float3 dy, out float4 alb, out float4 nrm)
            {
                float t = 1.0 / kTile[i];
                UNITY_BRANCH if (i == 4)
                {
                    float4 ax = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainAlbedo, sampler_TerrainAlbedo, pos.zy * t, i, dx.zy * t, dy.zy * t);
                    float4 az = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainAlbedo, sampler_TerrainAlbedo, pos.xy * t, i, dx.xy * t, dy.xy * t);
                    float4 ay = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainAlbedo, sampler_TerrainAlbedo, pos.xz * t, i, dx.xz * t, dy.xz * t);
                    alb = ax * blend.x + ay * blend.y + az * blend.z;
                    nrm = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainNormal, sampler_TerrainNormal, pos.xz * t, i, dx.xz * t, dy.xz * t);
                    nrm.ga = lerp(float2(0.5, 0.5), nrm.ga, blend.y); // normal detail only on the flatter part
                }
                else
                {
                    alb = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainAlbedo, sampler_TerrainAlbedo, pos.xz * t, i, dx.xz * t, dy.xz * t);
                    nrm = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainNormal, sampler_TerrainNormal, pos.xz * t, i, dx.xz * t, dy.xz * t);
                }
                alb.rgb *= kTint[i];
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 pos = i.positionWS;
                float3 nGeo = normalize(i.normalWS);
                float3 dx = ddx(pos), dy = ddy(pos);
                float3 blend = pow(abs(nGeo), 4); blend /= dot(blend, 1);

                float w[6] = { i.w0123.x, i.w0123.y, i.w0123.z, i.w0123.w, i.w45.x, i.w45.y };
                float wsum = 0;
                [unroll] for (int k = 0; k < 6; k++) wsum += w[k];

                // Height blend: layers with taller texture detail (pebbles, tufts) win at transitions.
                float4 alb[6]; float4 nrm[6];
                float hmax = -1;
                [unroll] for (int k2 = 0; k2 < 6; k2++)
                {
                    alb[k2] = 0; nrm[k2] = float4(1, 0.5, 0.8, 0.5);
                    UNITY_BRANCH if (w[k2] > 0.003)
                    {
                        SampleLayer(k2, pos, blend, dx, dy, alb[k2], nrm[k2]);
                        hmax = max(hmax, alb[k2].a + w[k2]);
                    }
                }
                float3 albedo = 0; float4 n = 0; float total = 0;
                [unroll] for (int k3 = 0; k3 < 6; k3++)
                {
                    float hw = w[k3] > 0.003 ? max(alb[k3].a + w[k3] - (hmax - 0.25), 0) : 0;
                    albedo += alb[k3].rgb * hw; n += nrm[k3] * hw; total += hw;
                }
                albedo /= max(total, 1e-4); n /= max(total, 1e-4);

                // Large-scale brightness/hue variation hides tiling from the high camera.
                float m1 = SAMPLE_TEXTURE2D(_MacroTex, sampler_MacroTex, pos.xz * 0.006).r;
                float m2 = SAMPLE_TEXTURE2D(_MacroTex, sampler_MacroTex, pos.xz * 0.021 + 0.37).r;
                float macro = (m1 * 0.65 + m2 * 0.35) - 0.5;
                albedo *= 1 + macro * 0.45;
                albedo = lerp(albedo, albedo * float3(1.06, 1.0, 0.86), saturate(macro * 2));

                float3 vcol = i.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    vcol = SRGBToLinear(vcol);
                #endif
                float remainder = saturate(1 - wsum);
                albedo = lerp(albedo, vcol, remainder);

                float2 nxy = float2(n.a, n.g) * 2 - 1;
                float3 normalWS = normalize(nGeo + float3(nxy.x, 0, nxy.y) * (1 - remainder) * 1.3);

                InputData inputData = (InputData)0;
                inputData.positionWS = pos;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(pos);
                inputData.shadowCoord = TransformWorldToShadowCoord(pos);
                inputData.fogCoord = i.fogCoord;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.metallic = 0;
                s.smoothness = lerp(saturate(1 - n.b) * 0.5, 0.1, remainder);
                s.occlusion = lerp(lerp(1, n.r, 0.8), 1, remainder);
                s.alpha = 1;

                half4 c = UniversalFragmentPBR(inputData, s);
                c.rgb = MixFog(c.rgb, inputData.fogCoord);
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 vertShadow(A a) : SV_POSITION
            {
                float3 ws = TransformObjectToWorld(a.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(a.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - ws);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, n, ld));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return cs;
            }
            half4 fragShadow() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            struct A { float4 positionOS : POSITION; };
            float4 vertDepth(A a) : SV_POSITION { return TransformObjectToHClip(a.positionOS.xyz); }
            half fragDepth() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vertDN
            #pragma fragment fragDN
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V vertDN(A a)
            {
                V o;
                o.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                return o;
            }
            half4 fragDN(V i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }

    // Built-in pipeline and unsupported hardware: the flat vertex-colored look.
    FallBack "Ironhorse/VertexLit"
}
