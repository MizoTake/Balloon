using UnityEngine;
using Unity.Mathematics;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Preset configurations for realistic balloon behavior
    /// </summary>
    [System.Serializable]
    public static class BalloonPresets
    {
        /// <summary>
        /// Realistic helium balloon parameters
        /// </summary>
        public static SimulationParameters RealisticHeliumBalloon()
        {
            return new SimulationParameters
            {
                gravity = 9.81f,                    // 標準重力加速度
                airDensity = 1.225f,               // 海面での空気密度 (kg/m³)
                windStrength = 1.5f,               // 穏やかな風
                windDirection = math.normalize(new float3(1f, 0.1f, 0.5f)),
                damping = 0.985f,                  // 空気抵抗（高めで滑らかな動き）
                collisionElasticity = 0.6f,        // 中程度の弾性
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100f)
            };
        }
        
        /// <summary>
        /// Indoor party balloon parameters (no wind)
        /// </summary>
        public static SimulationParameters IndoorPartyBalloon()
        {
            return new SimulationParameters
            {
                gravity = 9.81f,
                airDensity = 1.2f,                 // 室内の空気密度
                windStrength = 0.0f,               // 風なし
                windDirection = float3.zero,
                damping = 0.99f,                   // 室内なので空気抵抗少なめ
                collisionElasticity = 0.7f,        // やや高めの弾性
                worldBounds = new Bounds(Vector3.zero, new Vector3(50f, 30f, 50f))
            };
        }
        
        /// <summary>
        /// Outdoor festival balloon parameters
        /// </summary>
        public static SimulationParameters OutdoorFestivalBalloon()
        {
            return new SimulationParameters
            {
                gravity = 9.81f,
                airDensity = 1.225f,
                windStrength = 3.0f,               // 強めの風
                windDirection = math.normalize(new float3(1f, 0.2f, 0.3f)),
                damping = 0.97f,                   // 屋外なので抵抗大きめ
                collisionElasticity = 0.5f,        // 低めの弾性
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 150f)
            };
        }
        
        /// <summary>
        /// Low gravity fun parameters (moon-like)
        /// </summary>
        public static SimulationParameters LowGravityBalloon()
        {
            return new SimulationParameters
            {
                gravity = 1.62f,                   // 月の重力
                airDensity = 0.5f,                 // 薄い大気
                windStrength = 0.5f,
                windDirection = new float3(0f, 1f, 0f),
                damping = 0.995f,                  // 抵抗極小
                collisionElasticity = 0.9f,        // 高い弾性
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 200f)
            };
        }
        
        /// <summary>
        /// Recommended balloon data settings
        /// </summary>
        public static class BalloonDataPresets
        {
            // 標準的なパーティー風船
            public const float StandardRadius = 0.4f;          // 40cm diameter
            public const float StandardMass = 0.004f;          // 4グラム
            public const float StandardBuoyancy = 0.016f;      // ヘリウムの浮力係数
            
            // 小さい風船
            public const float SmallRadius = 0.25f;           // 25cm diameter
            public const float SmallMass = 0.002f;             // 2グラム
            public const float SmallBuoyancy = 0.018f;         // 小さいほど浮力が強い
            
            // 大きい風船
            public const float LargeRadius = 0.6f;            // 60cm diameter
            public const float LargeMass = 0.008f;             // 8グラム
            public const float LargeBuoyancy = 0.014f;         // 大きいほど浮力が弱い
            
            // 水風船（浮かない）
            public const float WaterBalloonRadius = 0.3f;
            public const float WaterBalloonMass = 0.15f;       // 150グラム（水入り）
            public const float WaterBalloonBuoyancy = 0.001f;  // ほぼ浮力なし
        }
    }
    
    /// <summary>
    /// Helper component to apply presets in the editor
    /// </summary>
    public class BalloonPresetApplier : MonoBehaviour
    {
        public enum PresetType
        {
            RealisticHelium,
            IndoorParty,
            OutdoorFestival,
            LowGravity,
            Custom
        }
        
        [Header("Preset Selection")]
        public PresetType selectedPreset = PresetType.RealisticHelium;
        
        [Header("Balloon Type Distribution")]
        [Range(0, 100)]
        public float standardBalloonPercentage = 70f;
        [Range(0, 100)]
        public float smallBalloonPercentage = 20f;
        [Range(0, 100)]
        public float largeBalloonPercentage = 10f;
        
        [Header("Fine Tuning")]
        [Range(0.5f, 2f)]
        public float buoyancyMultiplier = 1f;
        [Range(0.5f, 2f)]
        public float massMultiplier = 1f;
        
        [ContextMenu("Apply Preset")]
        public void ApplyPreset()
        {
            BalloonManager manager = GetComponent<BalloonManager>();
            if (manager == null)
            {
                Debug.LogError("BalloonManager component not found!");
                return;
            }
            
            // Apply simulation parameters based on preset
            SimulationParameters parameters = selectedPreset switch
            {
                PresetType.RealisticHelium => BalloonPresets.RealisticHeliumBalloon(),
                PresetType.IndoorParty => BalloonPresets.IndoorPartyBalloon(),
                PresetType.OutdoorFestival => BalloonPresets.OutdoorFestivalBalloon(),
                PresetType.LowGravity => BalloonPresets.LowGravityBalloon(),
                _ => manager.simulationParameters
            };
            
            // Apply to manager
            manager.simulationParameters = parameters;
            
            Debug.Log($"Applied {selectedPreset} preset to BalloonManager");
        }
        
        /// <summary>
        /// Generates balloon parameters based on distribution settings
        /// </summary>
        public void GetRandomBalloonParameters(out float radius, out float mass, out float buoyancy)
        {
            float random = UnityEngine.Random.Range(0f, 100f);
            
            if (random < smallBalloonPercentage)
            {
                radius = BalloonPresets.BalloonDataPresets.SmallRadius;
                mass = BalloonPresets.BalloonDataPresets.SmallMass * massMultiplier;
                buoyancy = BalloonPresets.BalloonDataPresets.SmallBuoyancy * buoyancyMultiplier;
            }
            else if (random < smallBalloonPercentage + largeBalloonPercentage)
            {
                radius = BalloonPresets.BalloonDataPresets.LargeRadius;
                mass = BalloonPresets.BalloonDataPresets.LargeMass * massMultiplier;
                buoyancy = BalloonPresets.BalloonDataPresets.LargeBuoyancy * buoyancyMultiplier;
            }
            else
            {
                radius = BalloonPresets.BalloonDataPresets.StandardRadius;
                mass = BalloonPresets.BalloonDataPresets.StandardMass * massMultiplier;
                buoyancy = BalloonPresets.BalloonDataPresets.StandardBuoyancy * buoyancyMultiplier;
            }
        }
    }
}