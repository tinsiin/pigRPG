Shader "Custom/InfiniteConvergingRectangles"
{
    Properties
    {
        _LineColor ("建物の色", Color) = (1,1,1,1)
        _BackgroundColor ("背景色", Color) = (0,0,0,0) // アルファを0に設定して透明に
        _LineWidth ("ライン幅", Float) = 0.02
        _BuildingWidth ("建物の幅 (度)", Float) = 5.0
        _BuildingHeight ("建物の高さ", Float) = 0.5
        _BuildingCount ("建物の数", Int) = 20
        _ConvergenceSpeed ("収束速度", Float) = 1.0
        _CircleRadius ("初期半径", Float) = 1.0
        _Rotation ("回転角度 (度)", Float) = 0.0
        _ColorGradientStart ("色グラデーション開始", Color) = (1,1,1,1)
        _ColorGradientEnd ("色グラデーション終了", Color) = (0,1,1,1)
        _CycleTime ("収束サイクル時間", Float) = 5.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha // 透明度のブレンディング設定
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

            fixed4 _LineColor;
            fixed4 _BackgroundColor;
            float _LineWidth;        // ラインの幅
            float _BuildingWidth;    // 建物の幅（角度）
            float _BuildingHeight;   // 建物の高さ（半径方向）
            int _BuildingCount;      // 建物の数
            float _ConvergenceSpeed; // 収束速度
            float _CircleRadius;     // 初期半径
            float _Rotation;         // 全体の回転角度
            fixed4 _ColorGradientStart; // 色グラデーション開始色
            fixed4 _ColorGradientEnd;   // 色グラデーション終了色
            float _CycleTime;            // 収束サイクル時間

            // 簡易的な乱数生成関数
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898,78.233))) * 43758.5453);
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

                // 回転を適用
                float rotationRad = radians(_Rotation);
                angle -= rotationRad;
                if (angle < 0.0)
                    angle += 6.28318530718;

                // 各建物のセクター角度
                float sectorAngle = 6.28318530718 / _BuildingCount;

                // 現在の建物インデックス
                int buildingIndex = floor(angle / sectorAngle);

                // 建物の中心角
                float buildingCenterAngle = (buildingIndex + 0.5) * sectorAngle;

                // 建物の幅をラジアンに変換
                float buildingWidthRad = radians(_BuildingWidth);

                // 建物の角度範囲内か判定
                float angleDifference = abs(angle - buildingCenterAngle);
                angleDifference = min(angleDifference, 6.28318530718 - angleDifference); // 最短距離を取得

                bool isWithinAngle = angleDifference < (buildingWidthRad / 2.0);

                if (!isWithinAngle)
                {
                    // 建物の角度範囲外なら背景色（透明）
                    return _BackgroundColor;
                }

                // 各建物にランダムなフェーズを割り当て
                float phase = rand(float2(buildingIndex, 0.0)); // フェーズは0.0から1.0の範囲

                // 時間に基づく収束値（無限ループ）
                float time = _Time.y * _ConvergenceSpeed;
                float totalCycleTime = _CycleTime;
                float adjustedTime = time - (phase * totalCycleTime);
                float loopTime = fmod(adjustedTime, totalCycleTime);
                if (loopTime < 0.0)
                    loopTime += totalCycleTime;

                // 建物の現在の位置（収束）
                float currentRadius = _CircleRadius - (loopTime / totalCycleTime) * (_CircleRadius + _BuildingHeight);

                // 建物の矩形範囲内か判定
                bool isBuilding = (dist >= currentRadius && dist <= (currentRadius + _BuildingHeight));

                if (isBuilding)
                {
                    // 色のグラデーション
                    float gradientFactor = (dist - currentRadius) / _BuildingHeight;
                    fixed4 buildingColor = lerp(_ColorGradientStart, _ColorGradientEnd, gradientFactor);

                    // 建物の色のアルファを1に設定
                    buildingColor.a = 1.0;

                    return buildingColor;
                }

                // 円周の線を描画
                float baseLine = smoothstep(_LineWidth, 0.0, abs(dist - _CircleRadius));
                if (baseLine > 0.0)
                {
                    // ラインの色のアルファを計算
                    fixed4 lineColor = lerp(_BackgroundColor, _LineColor, baseLine);
                    lineColor.a = baseLine; // ラインの強度に応じてアルファを設定
                    return lineColor;
                }

                // 背景色（透明）
                return _BackgroundColor;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Transparent"
}
