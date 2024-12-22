Shader "WangQAQ/Table/BottonmTag"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _BlurAmount ("Blur Amount", Range(0, 0.03)) = 0.03
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _BlurAmount;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            float Remap(float value, float s1, float s2, float t1, float t2)
            {
                return t1 + (value - s1) * (t2 - t1) / (s2 - s1);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float radius = tex2D(_MainTex,float2(1,0)).r;
                float2 center = float2(0.5, 0.5); // 圆心在(0,0)，即平面的中心
                float dist = length(i.uv - center);
                
                // 裁剪成圆形
                if (dist > 0.5)
                    discard;

                // 计算内圆半径和外圈颜色
                float edgeStart = Remap( radius , 0 , 1 , 0.1 , 0.5 );
                float edgeEnd = 0.5;

                if (dist <= edgeStart)
                {
                    // 在内圆，显示主贴图
                    return tex2D(_MainTex, i.uv);
                }
                else if (dist <= edgeEnd)
                {
                    // 外圆颜色，长度，等
                    float4 circleColor = tex2D(_MainTex,float2(0,0));

                    // 在外圆边缘，应用中值模糊效果
                    float t = (dist - edgeStart) / (edgeEnd - edgeStart);

                    float2 offsets[9] = {
                        float2(-1.0, -1.0), float2(0.0, -1.0), float2(1.0, -1.0),
                        float2(-1.0,  0.0), float2(0.0,  0.0), float2(1.0,  0.0),
                        float2(-1.0,  1.0), float2(0.0,  1.0), float2(1.0,  1.0)
                    };

                    fixed4 samples[9];
                    samples[0] = tex2D(_MainTex, i.uv + offsets[0] * (_BlurAmount * t) / _MainTex_ST.xy);
                    samples[1] = tex2D(_MainTex, i.uv + offsets[1] * (_BlurAmount * t) / _MainTex_ST.xy);
                    samples[2] = tex2D(_MainTex, i.uv + offsets[2] * (_BlurAmount * t) / _MainTex_ST.xy);
                    samples[3] = tex2D(_MainTex, i.uv + offsets[3] * (_BlurAmount * t) / _MainTex_ST.xy);
                    samples[4] = tex2D(_MainTex, i.uv + offsets[4] * (_BlurAmount * t) / _MainTex_ST.xy);
                    samples[5] = tex2D(_MainTex, i.uv + offsets[5] * (_BlurAmount * t) / _MainTex_ST.xy);
                    samples[6] = tex2D(_MainTex, i.uv + offsets[6] * (_BlurAmount * t) / _MainTex_ST.xy);
                    samples[7] = tex2D(_MainTex, i.uv + offsets[7] * (_BlurAmount * t) / _MainTex_ST.xy);
                    samples[8] = tex2D(_MainTex, i.uv + offsets[8] * (_BlurAmount * t) / _MainTex_ST.xy);

                    for (int m = 0; m < 8; m++)
                    {
                        for (int n = m + 1; n < 9; n++)
                        {
                            if (samples[m].r + samples[m].g + samples[m].b > samples[n].r + samples[n].g + samples[n].b)
                            {
                                fixed4 temp = samples[m];
                                samples[m] = samples[n];
                                samples[n] = temp;
                            }
                        }
                    }

                    fixed4 medianColor = samples[4]; 

                    fixed4 gradientColor = circleColor * t;
                    return lerp(medianColor, gradientColor, t);
                }

                return fixed4(0,0,0,0);
            }

            ENDCG
        }
    }
}