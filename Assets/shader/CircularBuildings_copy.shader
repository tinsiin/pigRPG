Shader "Custom/CircularBuildings"
{
    Properties
    {
        _LineColor ("Line Color", Color) = (1,1,1,1)
        _BackgroundColor ("Background Color", Color) = (0,0,0,1)
        _LineWidth ("Line Width", Float) = 2.0
        _CircleRadius ("Circle Radius", Float) = 0.4
        _BuildingCount ("Building Count", Int) = 50
        _BuildingHeightMin ("Building Height Min", Float) = 0.1
        _BuildingHeightMax ("Building Height Max", Float) = 0.3
        _RandomSeed ("Random Seed", Float) = 1234.0
        _Rotation ("Rotation Angle (Degrees)", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

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
            float _LineWidth;
            float _CircleRadius;
            int _BuildingCount;
            float _BuildingHeightMin;
            float _BuildingHeightMax;
            float _RandomSeed;
            float _Rotation; // 回転角度（度数法）

            // ランダム値生成のためのハッシュ関数
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2.0 - 1.0; // UV座標を[-1,1]の範囲に変換
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 center = float2(0.0, 0.0);
                float distFromCenter = length(uv - center);

                // 背景色の設定
                fixed4 col = _BackgroundColor;

                // パラメータの取得
                float circleRadius = _CircleRadius;
                int buildingCount = _BuildingCount;
                float lineWidth = _LineWidth / _ScreenParams.x * 2.0; // 画面幅に応じて線の太さを調整
                float minHeight = _BuildingHeightMin;
                float maxHeight = _BuildingHeightMax;
                float randomSeed = _RandomSeed;
                float rotationAngle = radians(_Rotation); // 度をラジアンに変換

                // UV座標を回転
                float cosAngle = cos(-rotationAngle); // 時計回りに回転させるため符号を反転
                float sinAngle = sin(-rotationAngle);
                float2 rotatedUV;
                rotatedUV.x = uv.x * cosAngle - uv.y * sinAngle;
                rotatedUV.y = uv.x * sinAngle + uv.y * cosAngle;

                // 現在のピクセルの角度を計算
                float angle = atan2(rotatedUV.y, rotatedUV.x);
                if (angle < 0.0)
                    angle += 6.28318530718; // 負の角度を正に変換

                // ビルのインデックスを計算
                float buildingAngle = angle / (6.28318530718 / buildingCount);
                int buildingIndex = floor(buildingAngle);

                // ランダムなビルの高さを決定
                float randomValue = rand(float2(buildingIndex, randomSeed));
                float buildingHeight = lerp(minHeight, maxHeight, randomValue);

                // ビルの内側と外側の半径を計算
                float innerRadius = circleRadius;
                float outerRadius = circleRadius + buildingHeight;

                // ビルの側面のエッジを計算
                float edge = abs(frac(buildingAngle) - 0.5) * buildingCount;

                // ビルの側面の線を描画
                float sideLine = smoothstep(lineWidth, 0.0, edge);

                // ビルの上部の線を描画
                float topLine = step(innerRadius + buildingHeight - lineWidth * 0.5, distFromCenter) * step(distFromCenter, innerRadius + buildingHeight + lineWidth * 0.5);

                // 円周の基礎部分の線を描画
                float baseLine = step(circleRadius - lineWidth * 0.5, distFromCenter) * step(distFromCenter, circleRadius + lineWidth * 0.5);

                // すべての線を結合
                fixed lineValue = max(sideLine * step(innerRadius, distFromCenter) * step(distFromCenter, outerRadius), max(topLine, baseLine));

                // 線の色を適用
                col = lerp(col, _LineColor, lineValue);

                return col;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Transparent"
}
