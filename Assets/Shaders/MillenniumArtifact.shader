Shader "DuelGenesis/MillenniumArtifact"
{
    Properties
    {
        _ArtifactColor ("Artifact Color", Color) = (1,1,1,1)
        _ArtifactMetallic ("Metallic", Range(0,1)) = 0.7
        _ArtifactSmoothness ("Smoothness", Range(0,1)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ArtifactColor;
                float _ArtifactMetallic;
                float _ArtifactSmoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs norm = GetVertexNormalInputs(input.normalOS);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = normalize(norm.normalWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 l = normalize(mainLight.direction);
                float3 h = normalize(l + v);

                float ndl = saturate(dot(n, l));
                float ndh = saturate(dot(n, h));
                float specPower = lerp(8.0, 128.0, _ArtifactSmoothness);
                float spec = pow(ndh, specPower) * lerp(0.08, 1.0, _ArtifactMetallic);

                float3 ambient = SampleSH(n) * 0.55;
                float3 diffuse = _ArtifactColor.rgb * (ambient + mainLight.color * ndl * mainLight.shadowAttenuation);
                float3 specular = mainLight.color * spec * mainLight.shadowAttenuation;
                return half4(diffuse + specular, _ArtifactColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
