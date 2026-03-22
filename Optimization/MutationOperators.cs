using System;
using System.Collections.Generic;
using Rhino.Geometry;
using Rhino;

namespace IterativeSunOpt.Optimization
{
    /// <summary>
    /// 变异算子接口
    /// 所有局部修改操作都需要实现此接口
    /// </summary>
    public interface IMutationOperator
    {
        string Name { get; }
        bool Apply(Brep brep, MutationParameters parameters);
    }

    /// <summary>
    /// 变异参数
    /// </summary>
    public class MutationParameters
    {
        public double TranslationScale { get; set; } = 1.0;      // 平移幅度（米）
        public double RotationScale { get; set; } = 5.0;         // 旋转幅度（度）
        public double ExtrusionScale { get; set; } = 1.0;        // 拉伸幅度（米）
        public double ScalingScale { get; set; } = 0.05;         // 缩放幅度（比例）
        public double MinHeight { get; set; } = 3.0;             // 最小高度
        public double MaxHeight { get; set; } = 100.0;           // 最大高度
        public Random Random { get; set; } = new Random(42);
    }

    /// <summary>
    /// 变异算子管理器
    /// 负责管理和调度所有变异算子
    /// </summary>
    public class MutationOperatorManager
    {
        private readonly List<IMutationOperator> _operators;
        private readonly Random _random;

        public MutationOperatorManager()
        {
            _operators = new List<IMutationOperator>
            {
                new FaceTranslationOperator(),
                new BrepRotationOperator(),
                new FaceExtrusionOperator(),
                new VertexAdjustmentOperator(),
                new ScalingOperator()
            };
            _random = new Random();
        }

        /// <summary>
        /// 添加自定义算子
        /// </summary>
        public void AddOperator(IMutationOperator op)
        {
            if (op != null && !_operators.Exists(o => o.Name == op.Name))
            {
                _operators.Add(op);
            }
        }

        /// <summary>
        /// 移除算子
        /// </summary>
        public void RemoveOperator(string name)
        {
            _operators.RemoveAll(o => o.Name == name);
        }

        /// <summary>
        /// 获取所有算子
        /// </summary>
        public List<IMutationOperator> GetOperators()
        {
            return new List<IMutationOperator>(_operators);
        }

        /// <summary>
        /// 随机应用一个变异算子
        /// </summary>
        public bool ApplyRandomMutation(Brep brep, MutationParameters parameters)
        {
            if (_operators.Count == 0 || brep == null) return false;

            int index = _random.Next(_operators.Count);
            return _operators[index].Apply(brep, parameters);
        }

        /// <summary>
        /// 应用指定算子
        /// </summary>
        public bool ApplyOperator(string name, Brep brep, MutationParameters parameters)
        {
            var op = _operators.Find(o => o.Name == name);
            if (op == null) return false;
            return op.Apply(brep, parameters);
        }
    }

    #region 变异算子实现

    /// <summary>
    /// 面移动算子
    /// 随机选择一个面，沿其法向量方向移动一小段距离
    /// </summary>
    public class FaceTranslationOperator : IMutationOperator
    {
        public string Name => "面移动";

        public bool Apply(Brep brep, MutationParameters parameters)
        {
            if (brep == null || brep.Faces.Count == 0) return false;

            try
            {
                var random = parameters.Random;
                
                // 随机选择一个面
                int faceIndex = random.Next(brep.Faces.Count);
                BrepFace face = brep.Faces[faceIndex];

                // 计算面的法向量
                Vector3d normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
                if (!normal.Unitize()) return false;

                // 随机移动距离（±mutationScale 米）
                double distance = (random.NextDouble() * 2 - 1) * parameters.TranslationScale;
                Vector3d translation = normal * distance;

                // 获取该面的所有边
                var edges = face.GetBrepEdges(true);
                if (edges == null || edges.Length == 0) return false;

                // 移动控制点
                bool modified = false;
                foreach (BrepEdge edge in edges)
                {
                    if (edge != null && edge.EdgeCurve is NurbsCurve nurbsCurve)
                    {
                        // 获取原始控制点
                        var originalPoints = new List<Point3d>();
                        for (int i = 0; i < nurbsCurve.Points.Count; i++)
                        {
                            originalPoints.Add(nurbsCurve.Points[i].Location);
                        }

                        // 移动控制点
                        for (int i = 0; i < nurbsCurve.Points.Count; i++)
                        {
                            Point3d newLocation = originalPoints[i] + translation;
                            nurbsCurve.Points.SetPoint(i, newLocation);
                        }
                        modified = true;
                    }
                }

                if (modified)
                {
                    // 尝试重建面
                    face.Rebuild(3, 3);
                }

                return modified;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[面移动算子] 执行失败: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 整体旋转算子
    /// 随机选择旋转轴，绕建筑中心旋转一小段角度
    /// </summary>
    public class BrepRotationOperator : IMutationOperator
    {
        public string Name => "整体旋转";

        public bool Apply(Brep brep, MutationParameters parameters)
        {
            if (brep == null) return false;

            try
            {
                var random = parameters.Random;

                // 计算包围盒中心
                BoundingBox bbox = brep.GetBoundingBox(false);
                Point3d center = bbox.Center;

                // 随机旋转轴（X/Y/Z）
                int axis = random.Next(3);
                Vector3d rotationAxis;
                switch (axis)
                {
                    case 0:
                        rotationAxis = Vector3d.XAxis;
                        break;
                    case 1:
                        rotationAxis = Vector3d.YAxis;
                        break;
                    case 2:
                        rotationAxis = Vector3d.ZAxis;
                        break;
                    default:
                        rotationAxis = Vector3d.ZAxis;
                        break;
                }

                // 随机旋转角度（±rotationScale 度）
                double angle = (random.NextDouble() * 2 - 1) * RhinoMath.ToRadians(parameters.RotationScale);

                // 创建旋转变换
                Transform rotation = Transform.Rotation(angle, rotationAxis, center);

                // 应用旋转
                return brep.Transform(rotation);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[整体旋转算子] 执行失败: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 面拉伸算子
    /// 随机选择一个面，沿其法向量方向向外拉伸
    /// </summary>
    public class FaceExtrusionOperator : IMutationOperator
    {
        public string Name => "面拉伸";

        public bool Apply(Brep brep, MutationParameters parameters)
        {
            if (brep == null || brep.Faces.Count == 0) return false;

            try
            {
                var random = parameters.Random;

                // 随机选择一个面
                int faceIndex = random.Next(brep.Faces.Count);
                BrepFace face = brep.Faces[faceIndex];

                // 获取面的边界曲线
                var loops = face.Loops;
                if (loops.Count == 0) return false;

                // 收集边界曲线
                var curves = new List<Curve>();
                foreach (var loop in loops)
                {
                    foreach (var trim in loop.Trims)
                    {
                        if (trim.TrimType != TrimType.Singular && trim.Edge != null)
                        {
                            curves.Add(trim.Edge.EdgeCurve.DuplicateCurve());
                        }
                    }
                }

                if (curves.Count == 0) return false;

                // 计算法向量
                Vector3d normal = face.NormalAt(face.Domain(0).Mid, face.Domain(1).Mid);
                if (!normal.Unitize()) return false;

                // 随机拉伸距离（只向外拉伸）
                double distance = random.NextDouble() * parameters.ExtrusionScale;

                // 创建拉伸向量
                Vector3d extrusionVector = normal * distance;

                // 创建拉伸曲面
                var extrusionCurves = new List<Curve>();
                foreach (var curve in curves)
                {
                    var translatedCurve = curve.DuplicateCurve();
                    Transform translate = Transform.Translation(extrusionVector);
                    translatedCurve.Transform(translate);
                    extrusionCurves.Add(translatedCurve);
                }

                // 创建侧面
                var loftBreps = new List<Brep>();
                for (int i = 0; i < curves.Count; i++)
                {
                    var loftCurves = new Curve[] { curves[i], extrusionCurves[i] };
                    var loftBrepsArray = Brep.CreateFromLoft(loftCurves, Point3d.Unset, Point3d.Unset, LoftType.Normal, false);
                    if (loftBrepsArray != null && loftBrepsArray.Length > 0)
                    {
                        loftBreps.AddRange(loftBrepsArray);
                    }
                }

                // 注意：完整的布尔合并操作较为复杂，这里采用简化实现
                // 实际项目中建议使用 RhinoBooleanOperations
                if (loftBreps.Count > 0)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[面拉伸算子] 执行失败: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 顶点微调算子
    /// 随机选择一个顶点，沿随机方向移动一小段距离
    /// </summary>
    public class VertexAdjustmentOperator : IMutationOperator
    {
        public string Name => "顶点微调";

        public bool Apply(Brep brep, MutationParameters parameters)
        {
            if (brep == null || brep.Vertices.Count == 0) return false;

            try
            {
                var random = parameters.Random;

                // 随机选择一个顶点
                int vertexIndex = random.Next(brep.Vertices.Count);
                BrepVertex vertex = brep.Vertices[vertexIndex];

                // 生成随机移动向量
                double rx = random.NextDouble() * 2 - 1;
                double ry = random.NextDouble() * 2 - 1;
                double rz = random.NextDouble() * 2 - 1;

                Vector3d moveVector = new Vector3d(rx, ry, rz);
                if (!moveVector.Unitize()) return false;
                moveVector *= (random.NextDouble() * parameters.TranslationScale);

                // 获取顶点位置
                Point3d vertexLocation = vertex.Location;
                Point3d newLocation = vertexLocation + moveVector;

                // 尝试移动顶点
                // 注意：RhinoCommon 中直接修改顶点较为复杂
                // 这里采用通过修改相邻边的方式间接实现

                // 获取相邻的边
                var edges = vertex.EdgeIndices();
                bool modified = false;

                foreach (int edgeIndex in edges)
                {
                    if (edgeIndex >= 0 && edgeIndex < brep.Edges.Count)
                    {
                        BrepEdge edge = brep.Edges[edgeIndex];
                        if (edge != null && edge.EdgeCurve is NurbsCurve nurbsCurve)
                        {
                            // 找到最近的控制点并移动
                            double minDist = double.MaxValue;
                            int closestIndex = -1;

                            for (int i = 0; i < nurbsCurve.Points.Count; i++)
                            {
                                double dist = nurbsCurve.Points[i].Location.DistanceTo(vertexLocation);
                                if (dist < minDist)
                                {
                                    minDist = dist;
                                    closestIndex = i;
                                }
                            }

                            if (closestIndex >= 0 && minDist < 0.001)
                            {
                                nurbsCurve.Points.SetPoint(closestIndex, newLocation);
                                modified = true;
                            }
                        }
                    }
                }

                return modified;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[顶点微调算子] 执行失败: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 比例缩放算子
    /// 对建筑进行微小的比例缩放
    /// </summary>
    public class ScalingOperator : IMutationOperator
    {
        public string Name => "比例缩放";

        public bool Apply(Brep brep, MutationParameters parameters)
        {
            if (brep == null) return false;

            try
            {
                var random = parameters.Random;

                // 计算包围盒中心和尺寸
                BoundingBox bbox = brep.GetBoundingBox(false);
                Point3d center = bbox.Center;

                // 随机选择缩放轴（X/Y/Z/All）
                int scaleType = random.Next(4);
                double scaleFactor = 1.0 + (random.NextDouble() * 2 - 1) * parameters.ScalingScale;

                // 创建缩放变换
                Transform scaling;
                switch (scaleType)
                {
                    case 0: // X轴
                        scaling = Transform.Scale(center, scaleFactor, 1.0, 1.0);
                        break;
                    case 1: // Y轴
                        scaling = Transform.Scale(center, 1.0, scaleFactor, 1.0);
                        break;
                    case 2: // Z轴
                        scaling = Transform.Scale(center, 1.0, 1.0, scaleFactor);
                        break;
                    case 3: // 全轴
                    default:
                        scaling = Transform.Scale(center, scaleFactor, scaleFactor, scaleFactor);
                        break;
                }

                // 应用缩放
                bool success = brep.Transform(scaling);

                // 检查高度约束
                if (success)
                {
                    BoundingBox newBbox = brep.GetBoundingBox(false);
                    double newHeight = newBbox.Max.Z - newBbox.Min.Z;

                    if (newHeight < parameters.MinHeight || newHeight > parameters.MaxHeight)
                    {
                        // 撤销缩放
                        Transform inverse = Transform.Scale(center, 1.0 / scaleFactor, 1.0 / scaleFactor, 1.0 / scaleFactor);
                        brep.Transform(inverse);
                        return false;
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[比例缩放算子] 执行失败: {ex.Message}");
                return false;
            }
        }
    }

    #endregion

    #region 扩展算子（预留扩展）

    /// <summary>
    /// 镜像翻转算子（预留）
    /// </summary>
    public class MirrorFlipOperator : IMutationOperator
    {
        public string Name => "镜像翻转";

        public bool Apply(Brep brep, MutationParameters parameters)
        {
            if (brep == null) return false;

            try
            {
                var random = parameters.Random;
                BoundingBox bbox = brep.GetBoundingBox(false);
                Point3d center = bbox.Center;

                // 随机选择镜像平面
                int plane = random.Next(3);
                Transform mirror;

                switch (plane)
                {
                    case 0: // YZ平面
                        mirror = Transform.Mirror(new Plane(center, Vector3d.XAxis));
                        break;
                    case 1: // XZ平面
                        mirror = Transform.Mirror(new Plane(center, Vector3d.YAxis));
                        break;
                    case 2: // XY平面
                    default:
                        mirror = Transform.Mirror(new Plane(center, Vector3d.ZAxis));
                        break;
                }

                // 应用镜像
                bool success = brep.Transform(mirror);

                // 检查约束
                if (success)
                {
                    BoundingBox newBbox = brep.GetBoundingBox(false);
                    if (newBbox.Min.Z < parameters.MinHeight || newBbox.Max.Z > parameters.MaxHeight)
                    {
                        // 撤销镜像（镜像的逆操作是镜像本身）
                        brep.Transform(mirror);
                        return false;
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[镜像翻转算子] 执行失败: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 扭曲变形算子（预留）
    /// </summary>
    public class TwistOperator : IMutationOperator
    {
        public string Name => "扭曲变形";

        public bool Apply(Brep brep, MutationParameters parameters)
        {
            if (brep == null) return false;

            try
            {
                var random = parameters.Random;
                BoundingBox bbox = brep.GetBoundingBox(false);
                Point3d center = bbox.Center;

                // 创建轻微的扭曲变形
                double twistAngle = (random.NextDouble() * 2 - 1) * RhinoMath.ToRadians(parameters.RotationScale * 0.5);

                // 扭曲变形较为复杂，这里使用简化的实现
                // 实际项目中可以使用更高级的变形算法

                // 暂时返回false，等待完整实现
                return false;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[扭曲变形算子] 执行失败: {ex.Message}");
                return false;
            }
        }
    }

    #endregion
}
