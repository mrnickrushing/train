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
        // Town ground mask (global, from TownStreets): R dirt road, G gravel street, B worn lot, A wheel ruts.
        TEXTURE2D(_TownMask); SAMPLER(sampler_TownMask);
        float4 _TownMaskRect; // xMin, zMin, 1/width, 1/height (0 = no mask)
        TEXTURE2D(_RoadMask); SAMPLER(sampler_RoadMask);
        float4 _RoadMaskRect; // haul roads across the whole valley: r = dirt, g = wheel ruts
        TEXTURE2D(_GroundTint); SAMPLER(sampler_GroundTint);
        float4 _GroundTintRect; // world colour map (GroundTint.cs): rgb * 2 multiplies the grass
        CBUFFER_START(UnityPerMaterial)
            float4 _MacroTex_ST;
        CBUFFER_END
        // Meters per texture tile for grass, dry grass, dirt, sand, rock, gravel.
        static const float kTile[6] = { 7.0, 8.0, 4.0, 5.0, 9.0, 2.6 };
        // Per layer color correction so the photo textures sit in the game's warm palette.
        static const float3 kTint[6] = { float3(0.74, 0.98, 0.5), float3(1.08, 1.04, 0.66), float3(1.0, 0.95, 0.9), float3(1.05, 1.0, 0.95), float3(1.05, 0.95, 0.9), float3(1.0, 1.0, 1.0) };
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
            void SampleLayer(int i, float3 pos, float3 blend, float3 dx, float3 dy, float anti, out float4 alb, out float4 nrm)
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
                    // Anti-tiling: a second copy, turned 37 degrees and 2.3x larger, faded in and out by world noise
                    // (anti), so the photo tile never lines up into a visible grid.
                    const float2x2 R = float2x2(0.7986, -0.6018, 0.6018, 0.7986);
                    float t2 = t * 0.435;
                    float2 uv2 = mul(R, pos.xz) * t2 + float2(0.37, 0.71);
                    float2 dx2 = mul(R, dx.xz) * t2, dy2 = mul(R, dy.xz) * t2;
                    float4 a1 = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainAlbedo, sampler_TerrainAlbedo, pos.xz * t, i, dx.xz * t, dy.xz * t);
                    float4 n1 = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainNormal, sampler_TerrainNormal, pos.xz * t, i, dx.xz * t, dy.xz * t);
                    float4 a2 = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainAlbedo, sampler_TerrainAlbedo, uv2, i, dx2, dy2);
                    float4 n2 = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainNormal, sampler_TerrainNormal, uv2, i, dx2, dy2);
                    // Height-aware crossfade: the taller detail of either copy wins in the blend zone, so there's no smear.
                    float k = saturate((anti - 0.5) * 3 + (a2.a - a1.a) * 1.5 + 0.5);
                    alb = lerp(a1, a2, k);
                    // Grass: soften the photo's hard bright speckles toward a blurred copy (a low mip of the same tile),
                    // so blades read as a soft carpet with clumps rather than glitter.
                    UNITY_BRANCH if (i <= 1)
                    {
                        float3 soft = SAMPLE_TEXTURE2D_ARRAY_GRAD(_TerrainAlbedo, sampler_TerrainAlbedo, pos.xz * t, i, dx.xz * t * 14, dy.xz * t * 14).rgb;
                        alb.rgb = lerp(soft, alb.rgb, 0.5);
                    }
                    nrm = lerp(n1, n2, k);
                    // Rotate the second copy's tangent-plane normal back into world XZ.
                    float2 nx2 = n2.ag * 2 - 1; nx2 = mul(transpose(R), nx2);
                    nrm.ag = lerp(n1.ag, nx2 * 0.5 + 0.5, k);
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
                // Streets and lots painted into the ground: soft-edged dirt tracks, gravel, worn earth.
                float2 muv = (pos.xz - _TownMaskRect.xy) * _TownMaskRect.zw;
                float4 town = _TownMaskRect.z > 0 && all(muv > 0) && all(muv < 1) ? SAMPLE_TEXTURE2D(_TownMask, sampler_TownMask, muv) : 0;
                float2 ruv = (pos.xz - _RoadMaskRect.xy) * _RoadMaskRect.zw;
                float2 haul = _RoadMaskRect.z > 0 && all(ruv > 0) && all(ruv < 1) ? SAMPLE_TEXTURE2D(_RoadMask, sampler_RoadMask, ruv).rg : 0;
                town.r = max(town.r, haul.r * (1 - town.g)); // country roads fade into the town's gravel where they meet
                town.a = max(town.a, haul.g);
                float cover = saturate(town.r + town.g + town.b);
                [unroll] for (int k0 = 0; k0 < 6; k0++) w[k0] *= 1 - cover;
                w[2] += town.r + town.b; // dirt (roads and worn lots)
                w[5] += town.g;          // gravel
                float wsum = 0;
                [unroll] for (int k = 0; k < 6; k++) wsum += w[k];

                // Height blend: layers with taller texture detail (pebbles, tufts) win at transitions.
                float4 alb[6]; float4 nrm[6];
                float hmax = -1;
                float anti = SAMPLE_TEXTURE2D(_MacroTex, sampler_MacroTex, pos.xz * 0.043 + float2(0.29, 0.53)).r;
                [unroll] for (int k2 = 0; k2 < 6; k2++)
                {
                    alb[k2] = 0; nrm[k2] = float4(1, 0.5, 0.8, 0.5);
                    UNITY_BRANCH if (w[k2] > 0.003)
                    {
                        SampleLayer(k2, pos, blend, dx, dy, anti, alb[k2], nrm[k2]);
                        hmax = max(hmax, alb[k2].a + w[k2]);
                    }
                }
                float3 albedo = 0; float4 n = 0; float total = 0;
                [unroll] for (int k3 = 0; k3 < 6; k3++)
                {
                    float hw = w[k3] > 0.003 ? max(alb[k3].a + w[k3] - (hmax - (k3 <= 1 ? 0.6 : 0.25)), 0) : 0; // the two grasses mix softly (no speckle)
                    albedo += alb[k3].rgb * hw; n += nrm[k3] * hw; total += hw;
                }
                albedo /= max(total, 1e-4); n /= max(total, 1e-4);
                albedo *= 1 - town.a * 0.35; // packed wheel ruts
                // Trodden town earth is dustier and greyer than the raw dirt layer.
                albedo = lerp(albedo, dot(albedo, float3(0.3, 0.59, 0.11)) * float3(1.06, 1.0, 0.9), 0.4 * saturate(town.r + town.b));

                // Rock: red sandstone in the southern desert, weathered grey granite in the green country to the north.
                float rockW = saturate(w[4] / max(wsum, 1e-3));
                float northRock = saturate((pos.z + 40) / 160);
                float lumR = dot(albedo, float3(0.3, 0.59, 0.11));
                albedo = lerp(albedo, lumR * float3(0.98, 0.98, 0.96) * 1.15, rockW * northRock * 0.85);
                // Large-scale brightness/hue variation hides tiling from the high camera.
                float m1 = SAMPLE_TEXTURE2D(_MacroTex, sampler_MacroTex, pos.xz * 0.006).r;
                float m2 = SAMPLE_TEXTURE2D(_MacroTex, sampler_MacroTex, pos.xz * 0.021 + 0.37).r;
                float macro = (m1 * 0.65 + m2 * 0.35) - 0.5;
                albedo *= 1 + macro * 0.45;
                albedo = lerp(albedo, albedo * float3(1.06, 1.0, 0.86), saturate(macro * 2));
                // Grass only: an independent noise swings meadows between sun-cured gold and deeper green, and a fine
                // clump pattern breaks up the flat lawn look. Dirt, roads and rock (not green) are left alone.
                float green = saturate((albedo.g - max(albedo.r, albedo.b)) * 10);
                float m3 = SAMPLE_TEXTURE2D(_MacroTex, sampler_MacroTex, pos.xz * 0.011 + float2(0.61, 0.13)).r - 0.5;
                float m4 = SAMPLE_TEXTURE2D(_MacroTex, sampler_MacroTex, pos.xz * 0.085 + float2(0.21, 0.77)).r - 0.5;
                float3 cured = albedo * float3(1.25, 1.08, 0.6);
                float3 lush = albedo * float3(0.62, 0.86, 0.6);
                albedo = lerp(albedo, m3 > 0 ? cured : lush, saturate(abs(m3) * 2.8) * green * 0.9);
                albedo *= 1 + m4 * 0.22 * green;
                // Tufts and clumps: metre-scale light and dark mottling on the grass, readable from mid zoom, and a
                // slightly darker, cooler shade in the hollows between them.
                float c1 = SAMPLE_TEXTURE2D(_MacroTex, sampler_MacroTex, pos.xz * 0.045 + float2(0.11, 0.47)).r - 0.5;
                float c2 = SAMPLE_TEXTURE2D(_MacroTex, sampler_MacroTex, pos.xz * 0.13 + float2(0.83, 0.19)).r - 0.5;
                float clump = c1 * 0.7 + c2 * 0.3;
                albedo *= lerp(float3(1, 1, 1), clump > 0 ? float3(1.2, 1.14, 0.86) : float3(0.7, 0.82, 0.76), smoothstep(0.02, 0.3, abs(clump)) * green);
                // Big patches you can read from the top of the zoom: whole hillsides of yellow-green, deep green and olive.
                float m5 = SAMPLE_TEXTURE2D(_MacroTex, sampler_MacroTex, pos.xz * 0.0032 + float2(0.43, 0.29)).r - 0.5;
                float m6 = SAMPLE_TEXTURE2D(_MacroTex, sampler_MacroTex, pos.xz * 0.0071 + float2(0.17, 0.83)).r - 0.5;
                float3 sunlit = albedo * float3(1.22, 1.12, 0.62);
                float3 deep = albedo * float3(0.55, 0.78, 0.55);
                albedo = lerp(albedo, m5 > 0 ? sunlit : deep, saturate(abs(m5) * 7) * green * 0.9);
                albedo = lerp(albedo, albedo * float3(0.92, 0.86, 0.62), saturate(m6 * 6) * green * 0.65); // olive scrub
                float2 tuv = (pos.xz - _GroundTintRect.xy) * _GroundTintRect.zw;
                float grassW = saturate((w[0] + w[1]) / max(wsum, 1e-3));
                if (_GroundTintRect.z > 0) albedo *= lerp(float3(1, 1, 1), SAMPLE_TEXTURE2D(_GroundTint, sampler_GroundTint, tuv).rgb * 2, grassW);
                // Slopes and hollows read darker (grass thins to earth on the steep bits), crests catch the light.
                float slope = 1 - saturate(nGeo.y);
                albedo *= lerp(1.04, 0.8, saturate(slope * 3.2));

                float3 vcol = i.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    vcol = SRGBToLinear(vcol);
                #endif
                float remainder = saturate(1 - wsum);
                albedo = lerp(albedo, vcol, remainder);

                float2 nxy = float2(n.a, n.g) * 2 - 1;
                float3 normalWS = normalize(nGeo + float3(nxy.x, 0, nxy.y) * (1 - remainder) * 1.15);

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
