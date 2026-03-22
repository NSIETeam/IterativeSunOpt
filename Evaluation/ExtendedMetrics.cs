using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace IterativeSunOpt.Evaluation
{
    /// <summary>
    /// 扩展的评估上下文
    /// 包含扩展评估指标所需的额外数据
    /// </summary>
    public class ExtendedEvaluationContext : EvaluationContext
    {
        // 环境景观类
        public Point3d[] GreenSpaceCenters { get; set; }           // 绿地中心点
        public Point3d[] ImportantViewPoints { get; set; }          // 重要景观观测点
        public double[] BuildingHeightsAround { get; set; }         // 周边建筑高度
        
        // 功能布局类
        public Point3d[] ParkingLocations { get; set; }             // 停车场位置
        public Point3d[] CulturalFacilities { get; set; }           // 文化设施位置
        
        // 经济参数
        public double UnitCostPerVolume { get; set; } = 2000.0;     // 单位造价（元/立方米）
        public double MaxBudget { get; set; } = 10000000.0;         // 最大预算
        
        // 安全约束
        public double MinFireSpacing { get; set; } = 6.0;           // 最小消防间距
        public double[] SurroundingBuildingDistances { get; set; }  // 周边建筑距离
        
        // 生态参数
        public double VegetationDensity { get; set; } = 0.3;        // 植被密度系数
    }

    #region 环境景观类指标

    /// <summary>
    /// 绿地可达性评估指标
    /// 计算建筑到绿地/公园的平均距离，反映居住者对绿化环境的可达性
    /// </summary>
    public class GreenAccessibilityMetric : IEvaluationMetric
    {
        public string Name => "绿地可达性";
        public double Weight { get; set; } = 0.08;
        public MetricDirection Direction => MetricDirection.LowerIsBetter;

        private const double IdealDistance = 100.0;      // 理想距离（米）
        private const double MaxDistance = 500.0;        // 最大可接受距离（米）

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            // 获取绿地中心点
            var extContext = context as ExtendedEvaluationContext;
            Point3d[] greenSpaces = extContext?.GreenSpaceCenters;

            if (greenSpaces == null || greenSpaces.Length == 0)
            {
                // 如果没有定义绿地，返回默认值
                return 1.0;
            }

            // 计算建筑中心点
            BoundingBox bbox = brep.GetBoundingBox(false);
            Point3d buildingCenter = bbox.Center;

            // 计算到最近绿地的距离
            double minDistance = double.MaxValue;
            foreach (var greenSpace in greenSpaces)
            {
                double distance = buildingCenter.DistanceTo(greenSpace);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            // 归一化：距离越近得分越高
            double score = Math.Max(0, (MaxDistance - minDistance) / (MaxDistance - IdealDistance));

            return Math.Min(1.0, score);
        }
    }

    /// <summary>
    /// 景观视线保护评估指标
    /// 评估建筑是否遮挡了重要景观视线
    /// </summary>
    public class ViewProtectionMetric : IEvaluationMetric
    {
        public string Name => "景观视线保护";
        public double Weight { get; set; } = 0.08;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            // 获取重要景观观测点
            var extContext = context as ExtendedEvaluationContext;
            Point3d[] viewPoints = extContext?.ImportantViewPoints;

            if (viewPoints == null || viewPoints.Length == 0)
            {
                return 1.0;  // 如果没有定义观测点，默认不遮挡
            }

            // 计算建筑投影面积
            BoundingBox bbox = brep.GetBoundingBox(false);
            double buildingProjectedArea = (bbox.Max.X - bbox.Min.X) * (bbox.Max.Y - bbox.Min.Y);

            // 计算视线遮挡率
            double totalOcclusion = 0.0;
            foreach (var viewPoint in viewPoints)
            {
                double distance = viewPoint.DistanceTo(bbox.Center);
                double angularSize = Math.Atan2(Math.Sqrt(buildingProjectedArea), distance);

                // 角度越大，遮挡越严重
                totalOcclusion += angularSize;
            }

            // 归一化：遮挡率越低，得分越高
            double maxOcclusion = Math.PI / 2;  // 最大遮挡率（90度）
            double score = Math.Max(0, 1.0 - totalOcclusion / (viewPoints.Length * maxOcclusion));

            return score;
        }
    }

    /// <summary>
    /// 自然光渗透率评估指标
    /// 计算建筑内部的自然光渗透深度
    /// </summary>
    public class DaylightPenetrationMetric : IEvaluationMetric
    {
        public string Name => "自然光渗透率";
        public double Weight { get; set; } = 0.06;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            // 计算建筑的体形系数（表面积/体积）
            VolumeMassProperties volumeProps = VolumeMassProperties.Compute(brep);
            if (volumeProps == null) return 0.0;

            double volume = volumeProps.Volume;

            double surfaceArea = 0.0;
            foreach (BrepFace face in brep.Faces)
            {
                surfaceArea += AreaMassProperties.Compute(face).Area;
            }

            if (volume <= 0) return 0.0;

            double shapeFactor = surfaceArea / volume;

            // 体形系数越小，自然光渗透越好
            // 归一化：假设理想体形系数为 0.3，最大为 1.0
            double idealShapeFactor = 0.3;
            double maxShapeFactor = 1.0;

            double score = Math.Max(0, 1.0 - (shapeFactor - idealShapeFactor) / (maxShapeFactor - idealShapeFactor));

            return Math.Min(1.0, Math.Max(0, score));
        }
    }

    #endregion

    #region 经济效益类指标

    /// <summary>
    /// 建造成本估算评估指标
    /// 基于建筑体积和形态估算建造成本
    /// </summary>
    public class ConstructionCostMetric : IEvaluationMetric
    {
        public string Name => "建造成本";
        public double Weight { get; set; } = 0.05;
        public MetricDirection Direction => MetricDirection.LowerIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            var extContext = context as ExtendedEvaluationContext;
            double unitCost = extContext?.UnitCostPerVolume ?? 2000.0;
            double maxBudget = extContext?.MaxBudget ?? 10000000.0;

            // 计算建筑体积
            VolumeMassProperties volumeProps = VolumeMassProperties.Compute(brep);
            if (volumeProps == null) return 0.0;

            double buildingVolume = volumeProps.Volume;

            // 计算建筑表面积
            double buildingSurfaceArea = 0.0;
            foreach (BrepFace face in brep.Faces)
            {
                buildingSurfaceArea += AreaMassProperties.Compute(face).Area;
            }

            // 计算体形系数（表面积/体积）
            double shapeFactor = buildingVolume > 0 ? buildingSurfaceArea / buildingVolume : 0.0;

            // 计算估算成本
            // 基础成本：体积 × 单位造价
            // 修正系数：体形系数越大，成本越高
            double baseCost = buildingVolume * unitCost;
            double cost = baseCost * (1.0 + shapeFactor * 0.1);

            // 归一化：成本越低，得分越高
            double score = Math.Max(0, 1.0 - cost / maxBudget);

            return score;
        }
    }

    /// <summary>
    /// 土地利用效率评估指标
    /// 评估建筑对土地资源的利用效率
    /// </summary>
    public class LandUseEfficiencyMetric : IEvaluationMetric
    {
        public string Name => "土地利用效率";
        public double Weight { get; set; } = 0.06;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        private double _targetFAR = 2.5;  // 目标容积率

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            // 计算建筑体积
            VolumeMassProperties volumeProps = VolumeMassProperties.Compute(brep);
            if (volumeProps == null) return 0.0;

            double buildingVolume = volumeProps.Volume;

            // 计算建筑占地面积
            double footprintArea = 0.0;
            BoundingBox bbox = brep.GetBoundingBox(false);

            // 简化：使用包围盒的底面积
            footprintArea = (bbox.Max.X - bbox.Min.X) * (bbox.Max.Y - bbox.Min.Y);

            // 容积率
            double far = context.SiteArea > 0 ? buildingVolume / context.SiteArea : 0;

            // 土地利用效率 = 建筑面积 / 占地面积
            // 假设层高为3米
            double floorArea = buildingVolume / 3.0;
            double efficiency = footprintArea > 0 ? floorArea / footprintArea : 0;

            // 归一化：效率越高越好，但容积率也要合理
            double efficiencyScore = Math.Min(1.0, efficiency / 10.0);
            double farScore = 1.0 - Math.Abs(far - _targetFAR) / 3.0;
            farScore = Math.Max(0, Math.Min(1.0, farScore));

            // 综合得分
            return 0.6 * efficiencyScore + 0.4 * farScore;
        }
    }

    /// <summary>
    /// 能源消耗潜力评估指标
    /// 评估建筑的能源消耗潜力
    /// </summary>
    public class EnergyConsumptionMetric : IEvaluationMetric
    {
        public string Name => "能源消耗潜力";
        public double Weight { get; set; } = 0.05;
        public MetricDirection Direction => MetricDirection.LowerIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            // 计算建筑体积和表面积
            VolumeMassProperties volumeProps = VolumeMassProperties.Compute(brep);
            if (volumeProps == null) return 0.0;

            double volume = volumeProps.Volume;

            double surfaceArea = 0.0;
            foreach (BrepFace face in brep.Faces)
            {
                surfaceArea += AreaMassProperties.Compute(face).Area;
            }

            if (volume <= 0) return 0.0;

            // 体形系数（表面积/体积）
            double shapeFactor = surfaceArea / volume;

            // 体形系数越大，能耗越高
            // 归一化：理想体形系数为 0.3，最大为 0.8
            double idealFactor = 0.3;
            double maxFactor = 0.8;

            double score = Math.Max(0, 1.0 - (shapeFactor - idealFactor) / (maxFactor - idealFactor));

            return Math.Min(1.0, score);
        }
    }

    #endregion

    #region 空间品质类指标

    /// <summary>
    /// 空间丰富度评估指标
    /// 评估建筑空间形态的丰富程度
    /// </summary>
    public class SpatialRichnessMetric : IEvaluationMetric
    {
        public string Name => "空间丰富度";
        public double Weight { get; set; } = 0.05;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            // 方法1：计算面的数量（面越多，形态越丰富）
            int faceCount = brep.Faces.Count;

            // 方法2：计算体积变化率（体块的复杂度）
            BoundingBox bbox = brep.GetBoundingBox(false);
            double boundingBoxVolume = (bbox.Max.X - bbox.Min.X) *
                                       (bbox.Max.Y - bbox.Min.Y) *
                                       (bbox.Max.Z - bbox.Min.Z);

            VolumeMassProperties volumeProps = VolumeMassProperties.Compute(brep);
            double actualVolume = volumeProps?.Volume ?? 0.0;

            double volumeRatio = boundingBoxVolume > 0 ? actualVolume / boundingBoxVolume : 0.0;

            // 综合计算空间丰富度
            // 面的贡献（归一化）
            double faceScore = Math.Min(1.0, faceCount / 20.0);

            // 体积比的贡献（1 - volumeRatio 越大，体块越复杂）
            double volumeScore = 1.0 - volumeRatio;

            // 综合得分
            double richnessScore = 0.6 * faceScore + 0.4 * volumeScore;

            return richnessScore;
        }
    }

    /// <summary>
    /// 尺度协调性评估指标
    /// 评估建筑与周边建筑尺度的协调性
    /// </summary>
    public class ScaleHarmonyMetric : IEvaluationMetric
    {
        public string Name => "尺度协调性";
        public double Weight { get; set; } = 0.05;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            var extContext = context as ExtendedEvaluationContext;
            double[] surroundingHeights = extContext?.BuildingHeightsAround;

            BoundingBox bbox = brep.GetBoundingBox(false);
            double buildingHeight = bbox.Max.Z - bbox.Min.Z;

            if (surroundingHeights == null || surroundingHeights.Length == 0)
            {
                // 如果没有周边建筑数据，返回默认值
                return 0.7;
            }

            // 计算周边建筑平均高度
            double avgSurroundingHeight = 0.0;
            foreach (double h in surroundingHeights)
            {
                avgSurroundingHeight += h;
            }
            avgSurroundingHeight /= surroundingHeights.Length;

            // 计算高度比
            double heightRatio = avgSurroundingHeight > 0 ? buildingHeight / avgSurroundingHeight : 1.0;

            // 理想高度比：0.8 ~ 1.2
            // 归一化：高度比越接近1，得分越高
            double idealRatio = 1.0;
            double maxDeviation = 0.5;  // 允许最大偏离50%

            double deviation = Math.Abs(heightRatio - idealRatio);
            double score = Math.Max(0, 1.0 - deviation / maxDeviation);

            return score;
        }
    }

    #endregion

    #region 安全韧性类指标

    /// <summary>
    /// 消防安全距离评估指标
    /// 评估建筑与周边建筑的消防安全距离
    /// </summary>
    public class FireSafetyDistanceMetric : IEvaluationMetric
    {
        public string Name => "消防安全距离";
        public double Weight { get; set; } = 0.06;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            var extContext = context as ExtendedEvaluationContext;
            double minFireSpacing = extContext?.MinFireSpacing ?? 6.0;
            double[] surroundingDistances = extContext?.SurroundingBuildingDistances;

            if (surroundingDistances == null || surroundingDistances.Length == 0)
            {
                return 0.8;  // 没有周边建筑数据时返回默认值
            }

            // 找到最小间距
            double minDistance = double.MaxValue;
            foreach (double dist in surroundingDistances)
            {
                if (dist < minDistance)
                    minDistance = dist;
            }

            // 归一化：距离大于最小消防间距得高分
            double score = Math.Min(1.0, minDistance / (minFireSpacing * 2));

            return score;
        }
    }

    /// <summary>
    /// 抗震性能评估指标
    /// 评估建筑的抗震性能潜力
    /// </summary>
    public class SeismicPerformanceMetric : IEvaluationMetric
    {
        public string Name => "抗震性能";
        public double Weight { get; set; } = 0.05;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            BoundingBox bbox = brep.GetBoundingBox(false);
            double height = bbox.Max.Z - bbox.Min.Z;
            double width = bbox.Max.X - bbox.Min.X;
            double depth = bbox.Max.Y - bbox.Min.Y;

            // 计算高宽比
            double minDimension = Math.Min(width, depth);
            double aspectRatio = minDimension > 0 ? height / minDimension : 0;

            // 理想高宽比：1 ~ 4
            // 归一化：高宽比越接近理想范围，得分越高
            double idealMinRatio = 1.0;
            double idealMaxRatio = 4.0;

            double score = 0.0;

            if (aspectRatio >= idealMinRatio && aspectRatio <= idealMaxRatio)
            {
                score = 1.0;
            }
            else if (aspectRatio < idealMinRatio)
            {
                score = aspectRatio / idealMinRatio;
            }
            else // aspectRatio > idealMaxRatio
            {
                double maxRatio = 8.0;
                score = Math.Max(0, 1.0 - (aspectRatio - idealMaxRatio) / (maxRatio - idealMaxRatio));
            }

            return score;
        }
    }

    #endregion

    #region 生态可持续类指标

    /// <summary>
    /// 生物多样性支持评估指标
    /// 评估建筑对周边生物多样性的支持程度
    /// </summary>
    public class BiodiversitySupportMetric : IEvaluationMetric
    {
        public string Name => "生物多样性支持";
        public double Weight { get; set; } = 0.04;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            // 计算建筑表面积
            double surfaceArea = 0.0;
            double roofArea = 0.0;

            foreach (BrepFace face in brep.Faces)
            {
                double faceArea = AreaMassProperties.Compute(face).Area;
                surfaceArea += faceArea;

                // 检测是否为屋顶面（法向量接近Z轴）
                Vector3d normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
                if (normal.Z > 0.9)
                {
                    roofArea += faceArea;
                }
            }

            // 计算可用于绿化的面积（屋顶）
            var extContext = context as ExtendedEvaluationContext;
            double vegetationDensity = extContext?.VegetationDensity ?? 0.3;

            // 绿化潜力
            double greeningPotential = roofArea / (surfaceArea + 0.001) * vegetationDensity;

            // 归一化
            double score = Math.Min(1.0, greeningPotential * 3);

            return score;
        }
    }

    /// <summary>
    /// 低碳排放潜力评估指标
    /// 评估建筑的低碳排放潜力
    /// </summary>
    public class LowCarbonPotentialMetric : IEvaluationMetric
    {
        public string Name => "低碳排放潜力";
        public double Weight { get; set; } = 0.04;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            // 计算建筑体积
            VolumeMassProperties volumeProps = VolumeMassProperties.Compute(brep);
            if (volumeProps == null) return 0.0;

            double volume = volumeProps.Volume;

            // 计算建筑表面积
            double surfaceArea = 0.0;
            foreach (BrepFace face in brep.Faces)
            {
                surfaceArea += AreaMassProperties.Compute(face).Area;
            }

            if (volume <= 0) return 0.0;

            // 体形系数（越小越节能）
            double shapeFactor = surfaceArea / volume;

            // 日照率（越大越节能）
            SunlightMetric sunlightMetric = new SunlightMetric();
            double sunlightScore = sunlightMetric.Calculate(brep, context);

            // 综合低碳潜力
            // 体形系数贡献（理想值 0.3）
            double shapeScore = Math.Max(0, 1.0 - (shapeFactor - 0.3) / 0.5);

            // 综合得分
            double score = 0.6 * shapeScore + 0.4 * sunlightScore;

            return Math.Min(1.0, Math.Max(0, score));
        }
    }

    #endregion

    #region 停车便利性指标

    /// <summary>
    /// 停车便利性评估指标
    /// 评估建筑到停车场的距离
    /// </summary>
    public class ParkingAccessibilityMetric : IEvaluationMetric
    {
        public string Name => "停车便利性";
        public double Weight { get; set; } = 0.04;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        private const double IdealDistance = 50.0;       // 理想距离（米）
        private const double MaxDistance = 200.0;        // 最大可接受距离（米）

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            var extContext = context as ExtendedEvaluationContext;
            Point3d[] parkingLocations = extContext?.ParkingLocations;

            if (parkingLocations == null || parkingLocations.Length == 0)
            {
                return 0.7;  // 没有停车场数据时返回默认值
            }

            // 计算建筑中心点
            BoundingBox bbox = brep.GetBoundingBox(false);
            Point3d buildingCenter = bbox.Center;

            // 计算到最近停车场的距离
            double minDistance = double.MaxValue;
            foreach (var parking in parkingLocations)
            {
                double distance = buildingCenter.DistanceTo(parking);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            // 归一化：距离越近得分越高
            double score = Math.Max(0, (MaxDistance - minDistance) / (MaxDistance - IdealDistance));

            return Math.Min(1.0, score);
        }
    }

    #endregion

    #region 指标管理器

    /// <summary>
    /// 评估指标管理器
    /// 用于管理和注册所有评估指标
    /// </summary>
    public static class MetricManager
    {
        /// <summary>
        /// 获取所有核心指标
        /// </summary>
        public static List<IEvaluationMetric> GetCoreMetrics()
        {
            return new List<IEvaluationMetric>
            {
                new SunlightMetric(),
                new SpacingMetric(),
                new OpenSpaceMetric(),
                new FARMetric(),
                new ViewMetric(),
                new VentilationMetric()
            };
        }

        /// <summary>
        /// 获取所有扩展指标
        /// </summary>
        public static List<IEvaluationMetric> GetExtendedMetrics()
        {
            return new List<IEvaluationMetric>
            {
                // 环境景观类
                new GreenAccessibilityMetric(),
                new ViewProtectionMetric(),
                new DaylightPenetrationMetric(),
                
                // 经济效益类
                new ConstructionCostMetric(),
                new LandUseEfficiencyMetric(),
                new EnergyConsumptionMetric(),
                
                // 空间品质类
                new SpatialRichnessMetric(),
                new ScaleHarmonyMetric(),
                
                // 安全韧性类
                new FireSafetyDistanceMetric(),
                new SeismicPerformanceMetric(),
                
                // 生态可持续类
                new BiodiversitySupportMetric(),
                new LowCarbonPotentialMetric(),
                
                // 功能布局类
                new ParkingAccessibilityMetric()
            };
        }

        /// <summary>
        /// 获取所有指标（核心 + 扩展）
        /// </summary>
        public static List<IEvaluationMetric> GetAllMetrics()
        {
            var metrics = GetCoreMetrics();
            metrics.AddRange(GetExtendedMetrics());
            return metrics;
        }

        /// <summary>
        /// 根据项目类型获取推荐的指标配置
        /// </summary>
        public static Dictionary<string, double> GetRecommendedWeights(string projectType)
        {
            switch (projectType.ToLower())
            {
                case "residential":
                case "住宅":
                    return new Dictionary<string, double>
                    {
                        { "日照率", 0.25 },
                        { "建筑间距", 0.20 },
                        { "开放空间比例", 0.15 },
                        { "容积率", 0.15 },
                        { "视野开阔度", 0.10 },
                        { "通风潜力", 0.05 },
                        { "绿地可达性", 0.05 },
                        { "建造成本", 0.05 }
                    };

                case "office":
                case "办公":
                    return new Dictionary<string, double>
                    {
                        { "日照率", 0.15 },
                        { "建筑间距", 0.15 },
                        { "开放空间比例", 0.15 },
                        { "容积率", 0.20 },
                        { "视野开阔度", 0.15 },
                        { "通风潜力", 0.10 },
                        { "土地利用效率", 0.05 },
                        { "能源消耗潜力", 0.05 }
                    };

                case "commercial":
                case "商业":
                    return new Dictionary<string, double>
                    {
                        { "日照率", 0.10 },
                        { "建筑间距", 0.15 },
                        { "开放空间比例", 0.20 },
                        { "容积率", 0.15 },
                        { "视野开阔度", 0.15 },
                        { "通风潜力", 0.05 },
                        { "停车便利性", 0.10 },
                        { "空间丰富度", 0.10 }
                    };

                case "lowcarbon":
                case "低碳":
                    return new Dictionary<string, double>
                    {
                        { "日照率", 0.15 },
                        { "建筑间距", 0.10 },
                        { "开放空间比例", 0.15 },
                        { "容积率", 0.10 },
                        { "视野开阔度", 0.10 },
                        { "通风潜力", 0.10 },
                        { "生物多样性支持", 0.10 },
                        { "低碳排放潜力", 0.10 },
                        { "能源消耗潜力", 0.10 }
                    };

                default:
                    return new Dictionary<string, double>
                    {
                        { "日照率", 0.20 },
                        { "建筑间距", 0.15 },
                        { "开放空间比例", 0.15 },
                        { "容积率", 0.15 },
                        { "视野开阔度", 0.10 },
                        { "通风潜力", 0.10 },
                        { "建造成本", 0.05 },
                        { "空间丰富度", 0.10 }
                    };
            }
        }
    }

    #endregion
}
