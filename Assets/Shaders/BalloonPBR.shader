Shader "BalloonSimulation/BalloonPBR"
{
    Properties
    {
        // Basic PBR properties
        _MainTex ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Metallic ("Metallic", Range(0,1)) = 0.1
        _Roughness ("Roughness", Range(0,1)) = 0.5
        _Transparency ("Transparency", Range(0,1)) = 0.8
        
        // Balloon specific properties
        _DeformationStrength ("Deformation Strength", Range(0,1)) = 1.0
        _SurfaceTension ("Surface Tension", Range(0,1)) = 0.5
        _InternalPressure ("Internal Pressure", Range(0.8,1.5)) = 1.0
        
        // Reflection and refraction
        _ReflectionStrength ("Reflection Strength", Range(0,1)) = 0.8
        _RefractionIndex ("Refraction Index", Range(1.0,1.5)) = 1.1
        _FresnelPower ("Fresnel Power", Range(1,5)) = 2.0
        
        // Environment mapping
        _EnvMap ("Environment Map", Cube) = "" {}
        _EnvMapIntensity ("Environment Intensity", Range(0,2)) = 1.0
        
        // Iridescence for soap bubble effect
        _IridescenceStrength ("Iridescence Strength", Range(0,1)) = 0.3
        _IridescenceThickness ("Thickness", Range(100,1000)) = 300
        
        // Animation properties
        _TimeScale ("Time Scale", Float) = 1.0
        _WindInfluence ("Wind Influence", Range(0,1)) = 0.1
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        LOD 300
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            
            // GPU Instancing support
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            // Properties
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURECUBE(_EnvMap);
            SAMPLER(sampler_EnvMap);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MainTex_ST;
                float _Metallic;
                float _Roughness;
                float _Transparency;
                float _DeformationStrength;
                float _SurfaceTension;
                float _InternalPressure;
                float _ReflectionStrength;
                float _RefractionIndex;
                float _FresnelPower;
                float _EnvMapIntensity;
                float _IridescenceStrength;
                float _IridescenceThickness;
                float _TimeScale;
                float _WindInfluence;
            CBUFFER_END
            
            // Instancing buffers for indirect rendering
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<float4x4> _TransformBuffer;
                StructuredBuffer<float4> _ColorBuffer;
                StructuredBuffer<float4> _MaterialPropertyBuffer; // metallic, roughness, transparency, deformation
                StructuredBuffer<float4x4> _DeformationBuffer;
            #endif
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
                float3 viewDirWS : TEXCOORD5;
                float4 fogFactorAndVertexLight : TEXCOORD6;
                float4 shadowCoord : TEXCOORD7;
                float4 instanceData : TEXCOORD8; // metallic, roughness, transparency, deformation
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            void setup()
            {
                // Setup for GPU instancing
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                float4x4 objectToWorld = UNITY_MATRIX_M;
                float4 instanceColor = _BaseColor;
                float4 materialProps = float4(_Metallic, _Roughness, _Transparency, 1.0);
                float4x4 deformationMatrix = unity_ObjectToWorld;
                
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    objectToWorld = _TransformBuffer[unity_InstanceID];
                    instanceColor = _ColorBuffer[unity_InstanceID];
                    materialProps = _MaterialPropertyBuffer[unity_InstanceID];
                    deformationMatrix = _DeformationBuffer[unity_InstanceID];
                #endif
                
                // Apply deformation to vertex position
                float3 deformedPos = mul(deformationMatrix, float4(input.positionOS.xyz, 1.0)).xyz;
                deformedPos = lerp(input.positionOS.xyz, deformedPos, _DeformationStrength);
                
                // Apply surface tension and pressure effects
                float3 normal = input.normalOS;
                float pressureEffect = (_InternalPressure - 1.0) * 0.1;
                deformedPos += normal * pressureEffect;
                
                // Apply wind deformation
                float windPhase = _Time.y * _TimeScale + deformedPos.x * 0.1;
                float windDeform = sin(windPhase) * _WindInfluence * 0.05;
                deformedPos.x += windDeform;
                deformedPos.z += windDeform * 0.5;
                
                // Transform to world space
                float3 positionWS = mul(objectToWorld, float4(deformedPos, 1.0)).xyz;
                output.positionWS = positionWS;
                output.positionCS = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
                
                // Calculate normals in world space
                float3 normalWS = mul((float3x3)objectToWorld, normal);
                float3 tangentWS = mul((float3x3)objectToWorld, input.tangentOS.xyz);
                output.normalWS = normalize(normalWS);
                output.tangentWS = normalize(tangentWS);
                output.bitangentWS = normalize(cross(output.normalWS, output.tangentWS) * input.tangentOS.w);
                
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.viewDirWS = normalize(GetCameraPositionWS() - positionWS);
                
                // Fog and lighting
                half fogFactor = ComputeFogFactor(output.positionCS.z);
                output.fogFactorAndVertexLight = half4(fogFactor, 0, 0, 0);
                
                // Shadow coordinates
                output.shadowCoord = TransformWorldToShadowCoord(positionWS);
                
                // Store instance material properties
                output.instanceData = materialProps;
                
                return output;
            }
            
            // Iridescence calculation for soap bubble effect
            float3 CalculateIridescence(float3 normal, float3 viewDir, float thickness)
            {
                float cosTheta = dot(normal, viewDir);
                float phase = thickness * cosTheta * 7.0; // Scale for visible wavelengths
                
                // RGB wavelengths (approximate)
                float r = sin(phase + 0.0) * 0.5 + 0.5;
                float g = sin(phase + 2.094) * 0.5 + 0.5; // 2π/3
                float b = sin(phase + 4.188) * 0.5 + 0.5; // 4π/3
                
                return float3(r, g, b) * _IridescenceStrength;
            }
            
            // Fresnel calculation
            float CalculateFresnel(float3 normal, float3 viewDir, float refractionIndex)
            {
                float cosTheta = dot(normal, viewDir);
                float r0 = pow((1.0 - refractionIndex) / (1.0 + refractionIndex), 2.0);
                return r0 + (1.0 - r0) * pow(1.0 - cosTheta, _FresnelPower);
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                // Sample base texture
                float4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                albedo *= _BaseColor;
                
                // Use instance data if available
                float metallic = input.instanceData.x;
                float roughness = input.instanceData.y;
                float transparency = input.instanceData.z;
                
                // Prepare surface data
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                
                // Calculate fresnel for transparency
                float fresnel = CalculateFresnel(normalWS, viewDirWS, _RefractionIndex);
                
                // Environment reflection
                float3 reflectionDir = reflect(-viewDirWS, normalWS);
                float4 envReflection = SAMPLE_TEXTURECUBE(_EnvMap, sampler_EnvMap, reflectionDir);
                envReflection.rgb *= _EnvMapIntensity;
                
                // Calculate iridescence
                float3 iridescence = CalculateIridescence(normalWS, viewDirWS, _IridescenceThickness);
                
                // Main lighting calculation
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 lightDir = normalize(mainLight.direction);
                float3 halfDir = normalize(lightDir + viewDirWS);
                
                // Lambertian diffuse
                float NdotL = saturate(dot(normalWS, lightDir));
                float3 diffuse = albedo.rgb * NdotL * mainLight.color * mainLight.shadowAttenuation;
                
                // Specular (simplified PBR)
                float NdotH = saturate(dot(normalWS, halfDir));
                float specularPower = lerp(1.0, 128.0, 1.0 - roughness);
                float specular = pow(NdotH, specularPower) * (1.0 - roughness);
                float3 specularColor = lerp(float3(0.04, 0.04, 0.04), albedo.rgb, metallic);
                float3 specularResult = specular * specularColor * mainLight.color * mainLight.shadowAttenuation;
                
                // Combine lighting
                float3 color = diffuse * (1.0 - metallic) + specularResult;
                
                // Add environment reflection
                color = lerp(color, envReflection.rgb, fresnel * _ReflectionStrength * (1.0 - roughness));
                
                // Add iridescence
                color += iridescence * fresnel;
                
                // Apply fog
                color = MixFog(color, input.fogFactorAndVertexLight.x);
                
                // Final alpha calculation
                float alpha = albedo.a * (1.0 - transparency) + fresnel * transparency;
                alpha = saturate(alpha);
                
                return float4(color, alpha);
            }
            ENDHLSL
        }
        
        // Shadow caster pass for proper shadowing
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            float3 _LightDirection;
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            void setup() {}
            
            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                // Apply shadow bias
                positionWS = ApplyShadowBias(positionWS, normalWS, _LightDirection);
                output.positionCS = TransformWorldToHClip(positionWS);
                
                return output;
            }
            
            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }
    
    // Fallback for older hardware
    FallBack "Universal Render Pipeline/Lit"
}