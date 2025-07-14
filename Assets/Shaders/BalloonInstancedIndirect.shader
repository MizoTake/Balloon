Shader "BalloonSimulation/InstancedIndirect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.8
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8.0)) = 3.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma multi_compile_instancing
        #pragma instancing_options procedural:setup
        #pragma target 4.5

        sampler2D _MainTex;
        half _Glossiness;
        half _Metallic;
        float4 _RimColor;
        float _RimPower;

        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            StructuredBuffer<float4x4> _Matrices;
            StructuredBuffer<float4> _Colors;
        #endif

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float3 worldNormal;
        };

        void setup()
        {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                float4x4 data = _Matrices[unity_InstanceID];
                unity_ObjectToWorld = data;
                
                // Calculate inverse matrix for normal transformation
                unity_WorldToObject = float4x4(
                    data[0][0], data[1][0], data[2][0], 0,
                    data[0][1], data[1][1], data[2][1], 0,
                    data[0][2], data[1][2], data[2][2], 0,
                    -dot(data[0].xyz, data[3].xyz),
                    -dot(data[1].xyz, data[3].xyz),
                    -dot(data[2].xyz, data[3].xyz),
                    1
                );
            #endif
        }

        void vert(inout appdata_full v)
        {
            // Vertex shader runs after setup()
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Base color from texture and instance color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex);
            
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                c *= _Colors[unity_InstanceID];
            #endif
            
            // Calculate rim lighting
            float rim = 1.0 - saturate(dot(normalize(IN.viewDir), IN.worldNormal));
            float rimIntensity = pow(rim, _RimPower);
            
            // Apply rim color
            c.rgb = lerp(c.rgb, _RimColor.rgb, rimIntensity * _RimColor.a);
            
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    
    FallBack "Diffuse"
}