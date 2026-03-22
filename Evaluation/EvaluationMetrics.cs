using System;
using Rhino.Geometry;

namespace IterativeSunOpt.Evaluation
{
    /// <summary>
    /// 评估指标接口
    /// 所有评估指标都需要实现此接口
    /// </summary>
    public interface IEvaluationMetric
    {
        string Name { get; }
        double Weight { get; set; }
        MetricDirection Direction { get; }
        double Calculate(Brep brep, EvaluationContext context);
    }

    /// <summary>
    /// 指标方向：上升好还是下降好
    /// </summary>
    public enum MetricDirection
    {
        HigherIsBetter,  // 越高越好（如日照率、容积率）
        LowerIsBetter,   // 越低越好（如建筑间距违规）
        TargetIsBest     // 越接近目标值越好（如标准容积率）
    }

    /// <summary>
    /// 评估上下文
    /// 传递评估所需的共享数据
    /// </summary>
    public class EvaluationContext
    {
        public Vector3d SunDirection { get; set; } = new Vector3d(1, 1, -0.5);
        public double SiteArea { get; set; } = 10000.0; // 场地面积（平方米）
        public double MinHeight { get; set; } = 3.0;    // 最小建筑高度（米）
        public double MinSpacing { get; set; } = 15.0;  // 最小建筑间距（米）
        public Point3d ViewPoint { get; set; } = new Point3d(50, 50, 10); // 视野观测点
        public Vector3d WindDirection { get; set; } = new Vector3d(-1, 0, 0); // 主导风向
    }

    #region 核心评估指标

    /// <summary>
    /// 日照评估指标
    /// 计算受光面积占总表面积的比例
    /// </summary>
    public class SunlightMetric : IEvaluationMetric
    {
        public string Name => "日照率";
        public double Weight { get; set; } = 0.3;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            double totalArea = 0.0;
            double sunlitArea = 0.0;
            Vector3d sunDir = context.SunDirection;
            sunDir.Unitize();

            foreach (BrepFace face in brep.Faces)
            {
                try
                {
                    double faceArea = AreaMassProperties.Compute(face)?.Area ?? 0;
                    if (faceArea <= 0) continue;

                    totalArea += faceArea;

                    Vector3d normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
                    double dotProduct = sunDir * normal;

                    if (dotProduct > 0)
                    {
                        sunlitArea += faceArea * dotProduct;
                    }
                }
                catch
                {
                    // 跳过计算失败的面
                    continue;
                }
            }

            return totalArea > 0 ? sunlitArea / totalArea : 0.0;
        }
    }

    /// <summary>
    /// 建筑间距评估指标
    /// 检查建筑体块间的最小距离是否满足要求
    /// </summary>
    public class SpacingMetric : IEvaluationMetric
    {
        public string Name => "建筑间距";
        public double Weight { get; set; } = 0.2;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            // 单个建筑体块的简化处理：计算到场地边界的最小距离
            BoundingBox bbox = brep.GetBoundingBox(false);
            Point3d center = bbox.Center;

            // 计算到场地四个角点的距离（假设场地为 100x100 米）
            double minDistance = double.MaxValue;
            Point3d[] corners = new Point3d[]
            {
                new Point3d(0, 0, 0),
                new Point3d(100, 0, 0),
                new Point3d(100, 100, 0),
                new Point3d(0, 100, 0)
            };

            foreach (var corner in corners)
            {
                double distance = center.DistanceTo(corner);
                if (distance < minDistance)
                    minDistance = distance;
            }

            // 将最小距离转换为得分（0-1）
            // 距离大于最小间距要求得满分，否则按比例扣分
            double score = Math.Max(0, (minDistance - context.MinSpacing) / 50.0);
            return Math.Min(1.0, score);
        }
    }

    /// <summary>
    /// 开放空间比例评估指标
    /// 计算场地中未被建筑占据的空间比例
    /// </summary>
    public class OpenSpaceMetric : IEvaluationMetric
    {
        public string Name => "开放空间比例";
        public double Weight { get; set; } = 0.15;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            // 计算建筑占地面积（投影面积）
            double buildingFootprint = 0.0;
            foreach (BrepFace face in brep.Faces)
            {
                try
                {
                    Vector3d normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
                    // 只计算水平面（法向量接近 Z 轴）
                    if (Math.Abs(normal.Z) > 0.9)
                    {
                        buildingFootprint += AreaMassProperties.Compute(face)?.Area ?? 0;
                    }
                }
                catch
                {
                    continue;
                }
            }

            // 开放空间比例 = (场地面积 - 建筑占地面积) / 场地面积
            double openSpaceRatio = (context.SiteArea - buildingFootprint) / context.SiteArea;
            return Math.Max(0, Math.Min(1.0, openSpaceRatio));
        }
    }

    /// <summary>
    /// 容积率评估指标
    /// 计算建筑总体积与场地面积的比值
    /// </summary>
    public class FARMetric : IEvaluationMetric
    {
        public string Name => "容积率";
        public double Weight { get; set; } = 0.15;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        private double _targetFAR = 2.5; // 目标容积率

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            // 计算建筑体积
            VolumeMassProperties volumeProps = VolumeMassProperties.Compute(brep);
            double buildingVolume = volumeProps?.Volume ?? 0;

            if (buildingVolume <= 0) return 0.0;

            // 容积率 = 建筑体积 / 场地面积
            double far = buildingVolume / context.SiteArea;

            // 将容积率转换为得分（越接近目标容积率得分越高）
            double deviation = Math.Abs(far - _targetFAR);
            double score = Math.Max(0, 1.0 - deviation / 3.0); // 允许偏离 3.0

            return score;
        }
    }

    /// <summary>
    /// 视野评估指标
    /// 计算从观测点看建筑的视野开阔度
    /// </summary>
    public class ViewMetric : IEvaluationMetric
    {
        public string Name => "视野开阔度";
        public double Weight { get; set; } = 0.1;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            Point3d viewPoint = context.ViewPoint;
            BoundingBox bbox = brep.GetBoundingBox(false);
            Point3d buildingCenter = bbox.Center;

            // 计算观测点到建筑中心的距离
            double distance = viewPoint.DistanceTo(buildingCenter);
            if (distance <= 0) return 0.0;

            // 计算建筑在观测点的张角（简化：使用对边距离）
            double buildingSize = bbox.Diagonal.Length;
            double angle = Math.Atan2(buildingSize, distance); // 弧度

            // 将张角转换为得分（角度适中得高分，太近或太远得分降低）
            double targetAngle = Math.PI / 6; // 目标角度 30 度
            double deviation = Math.Abs(angle - targetAngle);
            double score = Math.Max(0, 1.0 - deviation / targetAngle);

            return score;
        }
    }

    /// <summary>
    /// 通风评估指标
    /// 基于风向投影计算建筑的通风潜力
    /// </summary>
    public class VentilationMetric : IEvaluationMetric
    {
        public string Name => "通风潜力";
        public double Weight { get; set; } = 0.1;
        public MetricDirection Direction => MetricDirection.HigherIsBetter;

        public double Calculate(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            Vector3d windDir = context.WindDirection;
            windDir.Unitize();

            double totalFrontalArea = 0.0;
            double totalArea = 0.0;

            foreach (BrepFace face in brep.Faces)
            {
                try
                {
                    double faceArea = AreaMassProperties.Compute(face)?.Area ?? 0;
                    if (faceArea <= 0) continue;

                    totalArea += faceArea;

                    Vector3d normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
                    double dotProduct = Math.Abs(windDir * normal); // 绝对值，双向都算

                    totalFrontalArea += faceArea * dotProduct;
                }
                catch
                {
                    continue;
                }
            }

            // 通风潜力 = 迎风面积 / 总表面积
            return totalArea > 0 ? totalFrontalArea / totalArea : 0.0;
        }
    }

    #endregion
}
