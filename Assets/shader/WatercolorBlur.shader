Shader "Custom/WatercolorBlurWithPaperTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PaperTex ("Paper Texture", 2D) = "white" {} // 紙のテクスチャを追加
        _TimeScale ("Time Scale", Float) = 1.0
        _BlurAmount ("Blur Amount", Float) = 0.02
        _AlphaRange ("Alpha Range", Vector) = (0.2, 0.5, 0, 0)
        _PaperTextureIntensity ("Paper Texture Intensity", Range(0,1)) = 1.0 // 紙のテクスチャの強度
        _PaperAlphaIntensity ("Paper Alpha Intensity", Range(0,1)) = 1.0 // 紙のテクスチャによるアルファ値の強度
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _PaperTex; // 紙のテクスチャ
            float4 _PaperTex_ST;
            float _TimeScale;
            float _BlurAmount;
            float4 _AlphaRange; // Vector型はfloat4
            float _PaperTextureIntensity;
            float _PaperAlphaIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv_paper : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            // 2Dノイズ関数
            float noise(float2 p)
            {
                return frac(sin(dot(p ,float2(12.9898,78.233))) * 43758.5453);
            }

            // スムーズなノイズ（フラクタル・ノイズ）
            float smoothNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                // 4つの角のノイズ値を取得
                float a = noise(i);
                float b = noise(i + float2(1.0, 0.0));
                float c = noise(i + float2(0.0, 1.0));
                float d = noise(i + float2(1.0, 1.0));

                // 補間
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // フラクタル・ブラウン運動ノイズ
            float fbm(float2 p)
            {
                float total = 0.0;
                float amplitude = 1.0;
                float frequency = 1.0;
                for(int i = 0; i < 4; i++)
                {
                    total += smoothNoise(p * frequency) * amplitude;
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                return total;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv_paper = TRANSFORM_TEX(v.uv, _PaperTex); // 紙のテクスチャ用のUV座標
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 uv_paper = i.uv_paper;
                float time = _Time.y * _TimeScale;

                // UV座標を調整
                float n = fbm(uv * 10.0 + time * 0.1);

                // オフセットの計算
                float2 offset = float2(n * _BlurAmount, n * _BlurAmount);

                // ぼかし効果のためのサンプリング
                fixed4 color = tex2D(_MainTex, uv + offset);
                color += tex2D(_MainTex, uv - offset);
                color += tex2D(_MainTex, uv + float2(offset.x, -offset.y));
                color += tex2D(_MainTex, uv + float2(-offset.x, offset.y));
                color /= 4.0;

                // ノイズに基づいてアルファ値を変更
                float alpha = smoothstep(_AlphaRange.x, _AlphaRange.y, n);

                // 紙のテクスチャをサンプリング
                fixed4 paperColor = tex2D(_PaperTex, uv_paper);

                // 紙のテクスチャをグレースケールに変換
                float paperGray = dot(paperColor.rgb, float3(0.299, 0.587, 0.114));

                // 紙の質感を色に適用
                color.rgb *= lerp(1.0, paperGray, _PaperTextureIntensity);

                // 紙のテクスチャをアルファ値に適用
                float paperAlpha = lerp(1.0, paperGray, _PaperAlphaIntensity);

                // 最終的なアルファ値を計算
                color.a *= alpha * paperAlpha;

                return color;
            }
            ENDCG
        }
    }
}
