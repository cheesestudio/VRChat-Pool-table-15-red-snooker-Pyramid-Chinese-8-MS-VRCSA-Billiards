Shader "cheese/VRC LV Standard"
{
   // Drop-in replacement for the built-in "Standard" shader with optional
   // VRC Light Volumes (VRCLV) support.
   //
   // When VRCLV_ENABLED is NOT defined it behaves like Standard (baked light
   // probes). Uncomment the define (and install the VRC Light Volumes package)
   // to make it sample runtime light volumes instead.
   Properties
   {
      _Color ("Color", Color) = (1,1,1,1)
      _MainTex ("Albedo (RGB)", 2D) = "white" {}

      _Glossiness ("Smoothness", Range(0,1)) = 0.5
      _Metallic ("Metallic", Range(0,1)) = 0.0
      _MetallicGlossMap ("Metallic/Smoothness (R:A)", 2D) = "white" {}

      _BumpScale ("Normal Strength", Range(0,2)) = 1.0
      _BumpMap ("Normal Map", 2D) = "bump" {}

      _OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1.0
      _OcclusionMap ("Occlusion", 2D) = "white" {}

      _EmissionColor ("Emission Color", Color) = (0,0,0,1)
      _EmissionMap ("Emission", 2D) = "black" {}
   }
   SubShader
   {
      Tags { "RenderType"="Opaque" }
      LOD 300

      CGPROGRAM
      #pragma surface surf VRC_LV fullforwardshadows
      #pragma target 3.5

      // ---- VRC Light Volumes (VRCLV) support ----
      // LightVolumeSH() automatically falls back to Unity light probes when no
      // Light Volumes are present in the scene, so no toggle is required.
      #include "UnityCG.cginc"
      #include "Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc"
      #include "UnityPBSLighting.cginc"

      half4 LightingVRC_LV (SurfaceOutputStandard s, half3 viewDir, UnityGI gi)
      {
         half3 lightVolumeSpecular = gi.indirect.specular;
         gi.indirect.specular = 0;
         half4 color = LightingStandard(s, viewDir, gi);
         color.rgb += lightVolumeSpecular;
         return color;
      }

      inline void LightingVRC_LV_GI (SurfaceOutputStandard s, UnityGIInput data, inout UnityGI gi)
      {
         LightingStandard_GI(s, data, gi);

         float3 worldNormal = normalize(s.Normal);
         float3 worldViewDir = normalize(data.worldViewDir);
         float3 L0, L1r, L1g, L1b;
         LightVolumeSH(data.worldPos, L0, L1r, L1g, L1b, 0, worldNormal);
         gi.indirect.diffuse = LightVolumeEvaluate(worldNormal, L0, L1r, L1g, L1b);
         gi.indirect.specular = LightVolumeSpecularDominant(s.Albedo, s.Smoothness, s.Metallic, worldNormal, worldViewDir, L0, L1r, L1g, L1b);
      }

      sampler2D _MainTex;
      sampler2D _MetallicGlossMap;
      sampler2D _BumpMap;
      sampler2D _OcclusionMap;
      sampler2D _EmissionMap;

      half _Glossiness;
      half _Metallic;
      half _BumpScale;
      half _OcclusionStrength;
      fixed4 _Color;
      fixed4 _EmissionColor;

      struct Input
      {
         float2 uv_MainTex;
         float3 worldPos;
      };

      void surf (Input IN, inout SurfaceOutputStandard o)
      {
         fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
         o.Albedo = c.rgb;

         fixed4 mg = tex2D (_MetallicGlossMap, IN.uv_MainTex);
         o.Metallic = _Metallic * mg.r;
         o.Smoothness = _Glossiness * mg.a;

         o.Normal = UnpackScaleNormal (tex2D (_BumpMap, IN.uv_MainTex), _BumpScale);

         o.Occlusion = lerp (1.0, tex2D (_OcclusionMap, IN.uv_MainTex).r, _OcclusionStrength);

         o.Emission = _EmissionColor.rgb * tex2D (_EmissionMap, IN.uv_MainTex).rgb;

         o.Alpha = c.a;
      }
      ENDCG
   }
   FallBack "Diffuse"
}
