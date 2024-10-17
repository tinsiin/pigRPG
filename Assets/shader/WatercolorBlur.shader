Shader "Custom/WatercolorBlurWithPaperTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PaperTex ("Paper Texture", 2D) = "white" {} // ���̃e�N�X�`����ǉ�
        _TimeScale ("Time Scale", Float) = 1.0
        _BlurAmount ("Blur Amount", Float) = 0.02
        _AlphaRange ("Alpha Range", Vector) = (0.2, 0.5, 0, 0)
        _PaperTextureIntensity ("Paper Texture Intensity", Range(0,1)) = 1.0 // ���̃e�N�X�`���̋��x
        _PaperAlphaIntensity ("Paper Alpha Intensity", Range(0,1)) = 1.0 // ���̃e�N�X�`���ɂ��A���t�@�l�̋��x
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
            sampler2D _PaperTex; // ���̃e�N�X�`��
            float4 _PaperTex_ST;
            float _TimeScale;
            float _BlurAmount;
            float4 _AlphaRange; // Vector�^��float4
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

            // 2D�m�C�Y�֐�
            float noise(float2 p)
            {
                return frac(sin(dot(p ,float2(12.9898,78.233))) * 43758.5453);
            }

            // �X���[�Y�ȃm�C�Y�i�t���N�^���E�m�C�Y�j
            float smoothNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                // 4�̊p�̃m�C�Y�l���擾
                float a = noise(i);
                float b = noise(i + float2(1.0, 0.0));
                float c = noise(i + float2(0.0, 1.0));
                float d = noise(i + float2(1.0, 1.0));

                // ���
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // �t���N�^���E�u���E���^���m�C�Y
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
                o.uv_paper = TRANSFORM_TEX(v.uv, _PaperTex); // ���̃e�N�X�`���p��UV���W
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 uv_paper = i.uv_paper;
                float time = _Time.y * _TimeScale;

                // UV���W�𒲐�
                float n = fbm(uv * 10.0 + time * 0.1);

                // �I�t�Z�b�g�̌v�Z
                float2 offset = float2(n * _BlurAmount, n * _BlurAmount);

                // �ڂ������ʂ̂��߂̃T���v�����O
                fixed4 color = tex2D(_MainTex, uv + offset);
                color += tex2D(_MainTex, uv - offset);
                color += tex2D(_MainTex, uv + float2(offset.x, -offset.y));
                color += tex2D(_MainTex, uv + float2(-offset.x, offset.y));
                color /= 4.0;

                // �m�C�Y�Ɋ�Â��ăA���t�@�l��ύX
                float alpha = smoothstep(_AlphaRange.x, _AlphaRange.y, n);

                // ���̃e�N�X�`�����T���v�����O
                fixed4 paperColor = tex2D(_PaperTex, uv_paper);

                // ���̃e�N�X�`�����O���[�X�P�[���ɕϊ�
                float paperGray = dot(paperColor.rgb, float3(0.299, 0.587, 0.114));

                // ���̎�����F�ɓK�p
                color.rgb *= lerp(1.0, paperGray, _PaperTextureIntensity);

                // ���̃e�N�X�`�����A���t�@�l�ɓK�p
                float paperAlpha = lerp(1.0, paperGray, _PaperAlphaIntensity);

                // �ŏI�I�ȃA���t�@�l���v�Z
                color.a *= alpha * paperAlpha;

                return color;
            }
            ENDCG
        }
    }
}
