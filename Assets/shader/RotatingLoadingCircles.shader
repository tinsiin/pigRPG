Shader "Custom/RotatingLoadingCircles_Eased"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {} // ダミーテクスチャ（使用しません）
        _CircleColor ("Circle Color", Color) = (1,1,1,1) // 小円の色
        _BackgroundColor ("Background Color", Color) = (0,0,0,0) // 透明背景
        _MainRadius ("Main Circle Radius", Float) = 0.5 // メイン円の半径
        _SmallRadius ("Small Circle Radius", Float) = 0.05 // 小円の半径
        _CircleCount ("Circle Count", Int) = 12 // 小円の数
        _RotationSpeed ("Rotation Speed", Float) = 1.0 // 回転速度（サイクルあたりの回転数）
        _CycleTime ("Cycle Time", Float) = 2.0 // 1サイクルの時間（秒）
        _Opacity ("Opacity", Float) = 1.0 // 全体の不透明度
        _ColorGradientStart ("Color Gradient Start", Color) = (1,1,1,1) // フェードイン開始色
        _ColorGradientEnd ("Color Gradient End", Color) = (0,1,1,1) // フェードアウト終了色
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha // 透明度のブレンディング設定
        ZWrite Off // Zバッファ書き込み無効
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex; // ダミーテクスチャ
            fixed4 _CircleColor; // 小円の色
            fixed4 _BackgroundColor; // 背景色（透明）
            float _MainRadius; // メイン円の半径
            float _SmallRadius; // 小円の半径
            int _CircleCount; // 小円の数
            float _RotationSpeed; // 回転速度
            float _CycleTime; // サイクル時間
            float _Opacity; // 全体の不透明度
            fixed4 _ColorGradientStart; // フェードイン開始色
            fixed4 _ColorGradientEnd; // フェードアウト終了色

            // シンプルな乱数生成関数
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898,78.233))) * 43758.5453);
            }

            // イージング関数：easeInOutQuad
            float easeInOutQuad(float t)
            {
                return t < 0.5 ? 2.0 * t * t : -1.0 + (4.0 - 2.0 * t) * t;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2.0 - 1.0; // UV座標を[-1,1]に変換
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float dist = length(uv);
                float angle = atan2(uv.y, uv.x);
                if (angle < 0.0)
                    angle += 6.28318530718; // 角度を[0, 2π]に変換

                // 現在の時間
                float time = _Time.y;

                // 回転角度の計算
                float rotation = (time / _CycleTime) * _RotationSpeed * 6.28318530718; // ラジアン単位

                // セクター角度
                float sectorAngle = 6.28318530718 / float(_CircleCount);

                fixed4 finalColor = _BackgroundColor;

                // 最大32個までループ（パフォーマンスのため）
                #define MAX_CIRCLES 32

                for(int i = 0; i < MAX_CIRCLES; i++)
                {
                    if(i >= _CircleCount)
                        break;

                    // 小円の角度
                    float circleAngle = sectorAngle * float(i) + rotation;
                    circleAngle = fmod(circleAngle, 6.28318530718); // [0, 2π]に制限

                    // 小円の位置
                    float2 circlePos = float2(cos(circleAngle), sin(circleAngle)) * _MainRadius;

                    // フラグメントから小円までの距離
                    float d = distance(uv, circlePos);

                    // 各小円のフェーズ（ランダムタイミング）
                    float phase = float(i) / float(_CircleCount);

                    // 正規化された時間 [0,1]
                    float normalizedTime = fmod(time, _CycleTime) / _CycleTime;

                    // フェーズを考慮した小円の進行度
                    float circleProgress = normalizedTime - phase;
                    circleProgress = fmod(circleProgress + 1.0, 1.0); // [0,1]に制限

                    // フェードイン・フェードアウトの進行度（イージング適用）
                    float opacityFactor;
                    if(circleProgress < 0.5)
                    {
                        // フェードアウト（加速的に消える）
                        opacityFactor = 1.0 - easeInOutQuad(circleProgress * 2.0); // [0,1] -> [1,0]
                    }
                    else
                    {
                        // フェードイン（加速的に出現）
                        opacityFactor = easeInOutQuad((circleProgress - 0.5) * 2.0); // [0,1] -> [0,1]
                    }

                    // 小円のエッジの滑らかさ（アンチエイリアス）
                    float edge = smoothstep(_SmallRadius + 0.005, _SmallRadius - 0.005, d);

                    // 最終的なアルファ値
                    float alpha = edge * opacityFactor;

                    // 累積カラーに加算
                    finalColor += _CircleColor * alpha * _Opacity;
                }

                // 色をクランプ
                finalColor = clamp(finalColor, 0.0, 1.0);

                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Transparent"
}
