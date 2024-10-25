Shader "Custom/RotatingLoadingCircles_Eased"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {} // �_�~�[�e�N�X�`���i�g�p���܂���j
        _CircleColor ("Circle Color", Color) = (1,1,1,1) // ���~�̐F
        _BackgroundColor ("Background Color", Color) = (0,0,0,0) // �����w�i
        _MainRadius ("Main Circle Radius", Float) = 0.5 // ���C���~�̔��a
        _SmallRadius ("Small Circle Radius", Float) = 0.05 // ���~�̔��a
        _CircleCount ("Circle Count", Int) = 12 // ���~�̐�
        _RotationSpeed ("Rotation Speed", Float) = 1.0 // ��]���x�i�T�C�N��������̉�]���j
        _CycleTime ("Cycle Time", Float) = 2.0 // 1�T�C�N���̎��ԁi�b�j
        _Opacity ("Opacity", Float) = 1.0 // �S�̂̕s�����x
        _ColorGradientStart ("Color Gradient Start", Color) = (1,1,1,1) // �t�F�[�h�C���J�n�F
        _ColorGradientEnd ("Color Gradient End", Color) = (0,1,1,1) // �t�F�[�h�A�E�g�I���F
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha // �����x�̃u�����f�B���O�ݒ�
        ZWrite Off // Z�o�b�t�@�������ݖ���
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

            sampler2D _MainTex; // �_�~�[�e�N�X�`��
            fixed4 _CircleColor; // ���~�̐F
            fixed4 _BackgroundColor; // �w�i�F�i�����j
            float _MainRadius; // ���C���~�̔��a
            float _SmallRadius; // ���~�̔��a
            int _CircleCount; // ���~�̐�
            float _RotationSpeed; // ��]���x
            float _CycleTime; // �T�C�N������
            float _Opacity; // �S�̂̕s�����x
            fixed4 _ColorGradientStart; // �t�F�[�h�C���J�n�F
            fixed4 _ColorGradientEnd; // �t�F�[�h�A�E�g�I���F

            // �V���v���ȗ��������֐�
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898,78.233))) * 43758.5453);
            }

            // �C�[�W���O�֐��FeaseInOutQuad
            float easeInOutQuad(float t)
            {
                return t < 0.5 ? 2.0 * t * t : -1.0 + (4.0 - 2.0 * t) * t;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2.0 - 1.0; // UV���W��[-1,1]�ɕϊ�
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float dist = length(uv);
                float angle = atan2(uv.y, uv.x);
                if (angle < 0.0)
                    angle += 6.28318530718; // �p�x��[0, 2��]�ɕϊ�

                // ���݂̎���
                float time = _Time.y;

                // ��]�p�x�̌v�Z
                float rotation = (time / _CycleTime) * _RotationSpeed * 6.28318530718; // ���W�A���P��

                // �Z�N�^�[�p�x
                float sectorAngle = 6.28318530718 / float(_CircleCount);

                fixed4 finalColor = _BackgroundColor;

                // �ő�32�܂Ń��[�v�i�p�t�H�[�}���X�̂��߁j
                #define MAX_CIRCLES 32

                for(int i = 0; i < MAX_CIRCLES; i++)
                {
                    if(i >= _CircleCount)
                        break;

                    // ���~�̊p�x
                    float circleAngle = sectorAngle * float(i) + rotation;
                    circleAngle = fmod(circleAngle, 6.28318530718); // [0, 2��]�ɐ���

                    // ���~�̈ʒu
                    float2 circlePos = float2(cos(circleAngle), sin(circleAngle)) * _MainRadius;

                    // �t���O�����g���珬�~�܂ł̋���
                    float d = distance(uv, circlePos);

                    // �e���~�̃t�F�[�Y�i�����_���^�C�~���O�j
                    float phase = float(i) / float(_CircleCount);

                    // ���K�����ꂽ���� [0,1]
                    float normalizedTime = fmod(time, _CycleTime) / _CycleTime;

                    // �t�F�[�Y���l���������~�̐i�s�x
                    float circleProgress = normalizedTime - phase;
                    circleProgress = fmod(circleProgress + 1.0, 1.0); // [0,1]�ɐ���

                    // �t�F�[�h�C���E�t�F�[�h�A�E�g�̐i�s�x�i�C�[�W���O�K�p�j
                    float opacityFactor;
                    if(circleProgress < 0.5)
                    {
                        // �t�F�[�h�A�E�g�i�����I�ɏ�����j
                        opacityFactor = 1.0 - easeInOutQuad(circleProgress * 2.0); // [0,1] -> [1,0]
                    }
                    else
                    {
                        // �t�F�[�h�C���i�����I�ɏo���j
                        opacityFactor = easeInOutQuad((circleProgress - 0.5) * 2.0); // [0,1] -> [0,1]
                    }

                    // ���~�̃G�b�W�̊��炩���i�A���`�G�C���A�X�j
                    float edge = smoothstep(_SmallRadius + 0.005, _SmallRadius - 0.005, d);

                    // �ŏI�I�ȃA���t�@�l
                    float alpha = edge * opacityFactor;

                    // �ݐσJ���[�ɉ��Z
                    finalColor += _CircleColor * alpha * _Opacity;
                }

                // �F���N�����v
                finalColor = clamp(finalColor, 0.0, 1.0);

                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Transparent"
}
