// 塔被選取時的外框（反向外殼 inverted hull）。
// 只畫背面（Cull Front），頂點沿平滑法線在螢幕空間往外推固定像素，
// 因此物件本身的正面會擋住外殼，只留下輪廓外圍一圈純色。
// 平滑法線由 TowerOutlineHighlighter 寫入 UV3（TEXCOORD3）；UV3 為零時退回使用網格法線。
// Stencil：只畫在 Stencil 不等於 _StencilRef 的像素。TowerOutlineMask 先把整座塔看得到的像素標記起來，
// 所以外殼壓在塔的任何部位上都會被擋掉，部位交界處不會出現內部線條。
Shader "CyberWizard/TowerOutline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 0.84, 0.37, 1)
        _OutlineWidth("Outline Width (pixels)", Range(0, 16)) = 4
        _StencilRef("Stencil Reference", Range(1, 255)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            Stencil
            {
                Ref [_StencilRef]
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _StencilRef;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 smoothNormalOS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 normalOS = dot(input.smoothNormalOS, input.smoothNormalOS) > 1e-6
                    ? input.smoothNormalOS
                    : input.normalOS;
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);

                // 法線投影到螢幕像素空間取方向，再換回裁剪空間；乘 w 抵銷透視除法，讓寬度與距離無關。
                float2 normalPixels = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy * _ScreenParams.xy;
                float lengthPixels = length(normalPixels);
                if (lengthPixels > 1e-5)
                {
                    float2 offsetPixels = normalPixels / lengthPixels * _OutlineWidth;
                    positionCS.xy += offsetPixels * 2.0 / _ScreenParams.xy * positionCS.w;
                }

                output.positionCS = positionCS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
