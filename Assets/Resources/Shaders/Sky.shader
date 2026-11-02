// Gradient sky with sun disc and glow, driven by globals from DayNightCycle.
Shader "Ironhorse/Sky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _SkyTop, _SkyHorizon, _SkyGround, _SunColor;
            float4 _SunDir;
            float _StarAmount;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert(float4 v : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v);
                o.dir = v.xyz;
                return o;
            }
            float hash(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float3 col = h > 0 ? lerp(_SkyHorizon.rgb, _SkyTop.rgb, pow(saturate(h), 0.45)) : lerp(_SkyHorizon.rgb, _SkyGround.rgb, saturate(-h * 4));
                float3 sd = normalize(_SunDir.xyz);
                float s = saturate(dot(d, sd));
                col += _SunColor.rgb * (pow(s, 900) * 6 + pow(s, 12) * 0.35 + pow(s, 3) * 0.12 * saturate(1 - h * 2));
                if (_StarAmount > 0 && h > 0)
                {
                    float3 cell = floor(d * 220);
                    float st = step(0.9975, hash(cell));
                    col += st * _StarAmount * saturate(h * 3);
                }
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
