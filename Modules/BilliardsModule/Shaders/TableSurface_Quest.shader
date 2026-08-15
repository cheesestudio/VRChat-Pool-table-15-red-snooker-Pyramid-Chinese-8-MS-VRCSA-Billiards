Shader "cheese/TableSurface Quest VRCLV"
{
   Properties
   {
      _EmissionColor ("Emission Colour", Color) = (1,1,1,1)
      _Color ("Tint Colour", Color) = (1,1,1,1)

      _MainTex ("Albedo (RGB)", 2D) = "white" {}
      _EmissionMap ("Emission mask", 2D) = "black" {}

      _TimerPct("Timer Percentage", Range(0, 1)) = 1
   }
   SubShader
   {
      Tags { "RenderType"="Opaque" }
      LOD 200

      CGPROGRAM

      // ---- VRC Light Volumes (VRCLV) support ----
      // LightVolumeSH() automatically falls back to Unity light probes when no
      // Light Volumes are present in the scene, so no toggle is required.
      #include "UnityCG.cginc"
      #include "Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc"
      #include "Lighting.cginc"

      half4 LightingVRC_LV_Lambert (SurfaceOutput s, half3 viewDir, UnityGI gi)
      {
         half NdotL = saturate(dot(s.Normal, gi.light.dir));
         half4 c;
         c.rgb = s.Albedo * gi.light.color * NdotL;
         c.rgb += s.Albedo * gi.indirect.diffuse;
         c.a = s.Alpha;
         return c;
      }

      inline void LightingVRC_LV_Lambert_GI (SurfaceOutput s, UnityGIInput data, inout UnityGI gi)
      {
         LightingLambert_GI(s, data, gi);

         float3 worldNormal = normalize(s.Normal);
         float3 L0, L1r, L1g, L1b;
         LightVolumeSH(data.worldPos, L0, L1r, L1g, L1b, 0, worldNormal);
         gi.indirect.diffuse = LightVolumeEvaluate(worldNormal, L0, L1r, L1g, L1b);
      }

      #pragma surface surf VRC_LV_Lambert noforwardadd vertex:vert
      #pragma target 3.5

      sampler2D _MainTex;
      sampler2D _EmissionMap;
      sampler2D _TimerMap;

      fixed4 _EmissionColor;
      fixed3 _Color;

      float _TimerPct;

      struct Input
      {
         float2 uv_MainTex;
         float3 modelPos;
         float3 worldPos;
      };

      void vert(inout appdata_full v, out Input o)
      {
         UNITY_INITIALIZE_OUTPUT(Input, o);
         o.modelPos = v.vertex.xyz;
      }

      static const float M_PI = 3.14159265358979323846264338327950288;

      void surf (Input IN, inout SurfaceOutput o)
      {
         fixed4 sample_diffuse = tex2D (_MainTex, IN.uv_MainTex);
         fixed4 sample_emission = tex2D( _EmissionMap, IN.uv_MainTex );

         o.Albedo = lerp( sample_diffuse.rgb, _Color * sample_diffuse.rgb * 2.0, pow(sample_diffuse.a,0.1) );

         float timer_pct = clamp(_TimerPct, 0, 1);
         // add a small fudge factor so that the light connects
         float surf_angle_pct = (M_PI + atan2(IN.modelPos.x, IN.modelPos.z)) / (2 * M_PI) / 1.04 + (1 - 1 / 1.04);
         float angle_cl = clamp((surf_angle_pct - timer_pct) * 40.0, 0, 1.5);
         o.Emission = sample_emission.r * _EmissionColor * angle_cl;
      }

      ENDCG
   }
   FallBack "Diffuse"
}
