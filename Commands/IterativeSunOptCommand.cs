using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using IterativeSunOpt.Optimization;
using IterativeSunOpt.Evaluation;
using IterativeSunOpt.AI;

namespace IterativeSunOpt.Commands
{
    /// <summary>
    /// 迭代式局部日照优化命令
    /// 命令名: IterativeSunOpt
    /// </summary>
    public class IterativeSunOptCommand : Command
    {
        public override string EnglishName => "IterativeSunOpt";

        // 优化引擎实例
        private static OptimizationEngine _engine;
        
        // 当前文档和预览对象
        private static RhinoDoc _currentDoc;
        private static Guid _previewObjectId = Guid.Empty;

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            _currentDoc = doc;
            
            // 初始化优化引擎（包含扩展指标）
            if (_engine == null)
            {
                _engine = new OptimizationEngine(true);
            }

            try
            {
                // 1. 选择对象
                var objRef = GetSelectedObject(doc);
                if (objRef == null) return Result.Cancel;

                Brep initialBrep = ExtractBrepFromSelection(objRef);
                if (initialBrep == null) return Result.Failure;

                // 2. 获取优化参数
                var (parameters, context, useAI, buildingType) = GetOptimizationParameters();
                if (parameters == null) return Result.Cancel;

                // 3. 配置优化引擎
                _engine.UseAI = useAI;
                _engine.BuildingType = buildingType;

                // 4. 计算初始得分
                var extContext = new ExtendedEvaluationContext
                {
                    SunDirection = context.SunDirection,
                    SiteArea = context.SiteArea,
                    MinHeight = context.MinHeight,
                    MinSpacing = context.MinSpacing,
                    ViewPoint = context.ViewPoint,
                    WindDirection = context.WindDirection
                };

                var initialScore = _engine.CalculateOverallScore(initialBrep, extContext, out var initialMetricScores);
                
                RhinoApp.WriteLine("=== 初始方案评估 ===");
                RhinoApp.WriteLine($"综合得分: {initialScore:F4}");
                foreach (var kvp in initialMetricScores)
                {
                    RhinoApp.WriteLine($"  {kvp.Key}: {kvp.Value:F4}");
                }
                RhinoApp.WriteLine("");

                // 5. 开始迭代优化
                RhinoApp.WriteLine("=== 开始迭代优化 ===");
                RhinoApp.WriteLine($"使用 AI: {(useAI == 0 ? "是" : "否")}");
                RhinoApp.WriteLine($"建筑类型: {buildingType}");
                RhinoApp.WriteLine($"最大迭代次数: {parameters.MaxIterations}");
                RhinoApp.WriteLine("按 ESC 停止优化...");
                RhinoApp.WriteLine("");

                var result = _engine.Optimize(
                    initialBrep,
                    parameters,
                    extContext,
                    progressCallback: (info) =>
                    {
                        // 实时更新预览
                        if (parameters.UsePreview)
                        {
                            UpdatePreview(info.OverallScore, info.MetricScores);
                        }

                        // 显示改进信息
                        if (info.Improved)
                        {
                            RhinoApp.Write($"\r迭代 {info.Iteration}/{parameters.MaxIterations} | 综合得分: {info.OverallScore:F4} (改进! ✓)       ");
                        }
                    }
                );

                // 6. 完成优化
                RhinoApp.WriteLine("\n");

                if (result.Success)
                {
                    // 清除预览
                    ClearPreview();

                    // 将最优结果添加到文档
                    var finalObjectId = doc.Objects.AddBrep(result.BestBrep);
                    doc.Objects.Select(finalObjectId);
                    doc.Views.Redraw();

                    // 显示最终结果
                    RhinoApp.WriteLine("=== 优化完成 ===");
                    RhinoApp.WriteLine($"总迭代次数: {result.TotalIterations}");
                    RhinoApp.WriteLine($"初始综合得分: {initialScore:F4}");
                    RhinoApp.WriteLine($"最优综合得分: {result.BestScore:F4}");
                    RhinoApp.WriteLine($"改进幅度: {((result.BestScore - initialScore) / Math.Max(0.001, initialScore) * 100):F2}%");
                    RhinoApp.WriteLine("");
                    RhinoApp.WriteLine("=== 最优方案指标 ===");
                    foreach (var kvp in result.BestMetricScores)
                    {
                        RhinoApp.WriteLine($"  {kvp.Key}: {kvp.Value:F4}");
                    }

                    return Result.Success;
                }
                else
                {
                    RhinoApp.WriteLine($"优化失败: {result.Message}");
                    return Result.Failure;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"发生错误: {ex.Message}");
                return Result.Failure;
            }
        }

        private ObjRef GetSelectedObject(RhinoDoc doc)
        {
            var go = new GetObject();
            go.SetCommandPrompt("选择要优化的 Brep 或 Mesh 对象");
            go.GeometryFilter = ObjectType.Brep | ObjectType.Mesh;
            go.Get();
            
            if (go.CommandResult() != Result.Success)
                return null;
            
            return go.Object(0);
        }

        private Brep ExtractBrepFromSelection(ObjRef objRef)
        {
            if (objRef.Object() is ObjBrep objBrep && objBrep.BrepGeometry != null)
            {
                return objBrep.BrepGeometry.DuplicateBrep();
            }
            else if (objRef.Object() is ObjMesh objMesh && objMesh.MeshGeometry != null)
            {
                var breps = Brep.CreateFromMesh(objMesh.MeshGeometry, false);
                if (breps != null && breps.Length > 0)
                {
                    return breps[0];
                }
            }

            RhinoApp.WriteLine("请选择有效的 Brep 或 Mesh 对象");
            return null;
        }

        private (
            OptimizationParameters parameters,
            EvaluationContext context,
            int useAI,
            BuildingType buildingType
        ) GetOptimizationParameters()
        {
            var parameters = new OptimizationParameters();
            var context = new EvaluationContext();

            // 获取迭代次数
            var iterOption = new OptionInteger(100, 10, 10000);
            var getIter = new GetOption();
            getIter.SetCommandPrompt("设置最大迭代次数");
            getIter.AddOption("Iterations", ref iterOption);
            getIter.AcceptNothing(true);
            
            if (getIter.Get() == GetResult.Option)
            {
                parameters.MaxIterations = iterOption.CurrentValue;
            }

            // 获取变形幅度
            var scaleOption = new OptionDouble(1.0, 0.1, 10.0);
            var getScale = new GetOption();
            getScale.SetCommandPrompt("设置变形幅度（米）");
            getScale.AddOption("Scale", ref scaleOption);
            getScale.AcceptNothing(true);
            
            if (getScale.Get() == GetResult.Option)
            {
                parameters.MutationScale = scaleOption.CurrentValue;
            }

            // 获取旋转幅度
            var rotationOption = new OptionDouble(5.0, 1.0, 45.0);
            var getRotation = new GetOption();
            getRotation.SetCommandPrompt("设置旋转幅度（度）");
            getRotation.AddOption("Rotation", ref rotationOption);
            getRotation.AcceptNothing(true);
            
            if (getRotation.Get() == GetResult.Option)
            {
                parameters.RotationScale = rotationOption.CurrentValue;
            }

            // 获取建筑类型
            var buildingTypeNames = Enum.GetNames(typeof(BuildingType));
            var typeOption = new OptionList(buildingTypeNames, 0);
            var getType = new GetOption();
            getType.SetCommandPrompt("选择建筑类型");
            getType.AddOptionList("BuildingType", ref typeOption, "建筑类型");
            getType.AcceptNothing(true);
            
            BuildingType selectedBuildingType = BuildingType.Residential;
            if (getType.Get() == GetResult.Option)
            {
                string selectedType = buildingTypeNames[typeOption.CurrentListIndex];
                Enum.TryParse(selectedType, out selectedBuildingType);
            }

            // 获取是否使用 AI
            var useAIOptionNames = new[] { "使用 AI", "不使用 AI" };
            var useAIOption = new OptionList(useAIOptionNames, 1);
            var getUseAI = new GetOption();
            getUseAI.SetCommandPrompt("选择是否使用 AI 辅助");
            getUseAI.AddOptionList("UseAI", ref useAIOption, "AI 模式");
            getUseAI.AcceptNothing(true);
            
            int useAI = 1;
            if (getUseAI.Get() == GetResult.Option)
            {
                useAI = useAIOption.CurrentListIndex;
            }

            // 获取场地面积
            var areaOption = new OptionDouble(10000.0, 100.0, 1000000.0);
            var getArea = new GetOption();
            getArea.SetCommandPrompt("设置场地面积（平方米）");
            getArea.AddOption("SiteArea", ref areaOption);
            getArea.AcceptNothing(true);
            
            if (getArea.Get() == GetResult.Option)
            {
                context.SiteArea = areaOption.CurrentValue;
            }

            // 是否实时预览
            var previewOption = new OptionToggle(true, "开启", "关闭");
            var getPreview = new GetOption();
            getPreview.SetCommandPrompt("设置实时预览");
            getPreview.AddOption("Preview", ref previewOption);
            getPreview.AcceptNothing(true);
            
            if (getPreview.Get() == GetResult.Option)
            {
                parameters.UsePreview = previewOption.CurrentValue;
            }

            return (parameters, context, useAI, selectedBuildingType);
        }

        private void UpdatePreview(double score, Dictionary<string, double> metricScores)
        {
            if (_currentDoc == null) return;

            try
            {
                var bestBrep = _engine.BestBrep;
                if (bestBrep == null) return;

                if (_previewObjectId != Guid.Empty)
                {
                    _currentDoc.Objects.Delete(_previewObjectId, false);
                }

                _previewObjectId = _currentDoc.Objects.AddBrep(bestBrep);
                
                var objRef = new ObjRef(_previewObjectId);
                if (objRef.Object() is RhinoObject rhObj)
                {
                    rhObj.Attributes.ObjectColor = System.Drawing.Color.LimeGreen;
                    rhObj.Attributes.ColorSource = ObjectColorSource.ColorFromObject;
                    rhObj.CommitChanges();
                }

                _currentDoc.Views.Redraw();
            }
            catch
            {
                // 预览更新失败不影响主流程
            }
        }

        private void ClearPreview()
        {
            if (_currentDoc == null) return;

            try
            {
                if (_previewObjectId != Guid.Empty)
                {
                    _currentDoc.Objects.Delete(_previewObjectId, false);
                    _previewObjectId = Guid.Empty;
                }
                _currentDoc.Views.Redraw();
            }
            catch
            {
                // 清除失败不影响主流程
            }
        }
    }

    /// <summary>
    /// 显示优化结果命令
    /// </summary>
    public class ShowOptResultsCommand : Command
    {
        public override string EnglishName => "ShowOptResults";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (IterativeSunOptCommand._engine == null)
            {
                RhinoApp.WriteLine("请先运行 IterativeSunOpt 命令进行优化");
                return Result.Nothing;
            }

            var bestBrep = IterativeSunOptCommand._engine.BestBrep;
            if (bestBrep == null)
            {
                RhinoApp.WriteLine("没有可用的优化结果");
                return Result.Nothing;
            }

            RhinoApp.WriteLine("=== 最新优化结果 ===");
            RhinoApp.WriteLine($"综合得分: {IterativeSunOptCommand._engine.BestOverallScore:F4}");
            RhinoApp.WriteLine($"迭代次数: {IterativeSunOptCommand._engine.IterationCount}");
            RhinoApp.WriteLine("");
            RhinoApp.WriteLine("各指标得分:");
            
            foreach (var kvp in IterativeSunOptCommand._engine.BestMetricScores)
            {
                RhinoApp.WriteLine($"  {kvp.Key}: {kvp.Value:F4}");
            }

            return Result.Success;
        }
    }

    /// <summary>
    /// 配置评估指标命令
    /// </summary>
    public class ConfigMetricsCommand : Command
    {
        public override string EnglishName => "ConfigMetrics";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (IterativeSunOptCommand._engine == null)
            {
                RhinoApp.WriteLine("请先运行 IterativeSunOpt 命令初始化优化引擎");
                return Result.Nothing;
            }

            var metrics = IterativeSunOptCommand._engine.GetMetrics();
            
            RhinoApp.WriteLine("=== 当前评估指标 ===");
            for (int i = 0; i < metrics.Count; i++)
            {
                var metric = metrics[i];
                RhinoApp.WriteLine($"{i + 1}. {metric.Name} (权重: {metric.Weight:F2})");
            }
            RhinoApp.WriteLine("");

            var go = new GetInteger();
            go.SetCommandPrompt("选择要修改权重的指标序号");
            go.SetLowerLimit(1);
            go.SetUpperLimit(metrics.Count);
            if (go.Get() != GetResult.Number)
                return Result.Cancel;

            int selectedIndex = go.Number() - 1;
            var selectedMetric = metrics[selectedIndex];

            var gw = new GetInteger();
            gw.SetCommandPrompt($"输入 {selectedMetric.Name} 的权重 (1-100)");
            gw.SetLowerLimit(1);
            gw.SetUpperLimit(100);
            gw.SetDefaultInteger((int)(selectedMetric.Weight * 100));
            if (gw.Get() != GetResult.Number)
                return Result.Cancel;

            double newWeight = gw.Number() / 100.0;
            IterativeSunOptCommand._engine.SetMetricWeight(selectedMetric.Name, newWeight);

            RhinoApp.WriteLine($"已将 {selectedMetric.Name} 的权重设置为 {newWeight:F2}");

            return Result.Success;
        }
    }

    /// <summary>
    /// 设置建筑类型命令
    /// </summary>
    public class SetBuildingTypeCommand : Command
    {
        public override string EnglishName => "SetBuildingType";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (IterativeSunOptCommand._engine == null)
            {
                RhinoApp.WriteLine("请先运行 IterativeSunOpt 命令初始化优化引擎");
                return Result.Nothing;
            }

            var buildingTypeNames = Enum.GetNames(typeof(BuildingType));
            var typeOption = new OptionList(buildingTypeNames, 0);
            var getType = new GetOption();
            getType.SetCommandPrompt("选择建筑类型");
            getType.AddOptionList("BuildingType", ref typeOption, "建筑类型");
            getType.AcceptNothing(true);
            
            if (getType.Get() == GetResult.Option)
            {
                string selectedType = buildingTypeNames[typeOption.CurrentListIndex];
                Enum.TryParse(selectedType, out BuildingType buildingType);
                IterativeSunOptCommand._engine.BuildingType = buildingType;
                RhinoApp.WriteLine($"建筑类型已设置为: {buildingType}");
                
                // 应用推荐权重
                IterativeSunOptCommand._engine.ApplyRecommendedWeights(selectedType);
                RhinoApp.WriteLine("已应用推荐权重配置");
            }

            return Result.Success;
        }
    }

    /// <summary>
    /// 设置 AI 模式命令
    /// </summary>
    public class SetAIModeCommand : Command
    {
        public override string EnglishName => "SetAIMode";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (IterativeSunOptCommand._engine == null)
            {
                RhinoApp.WriteLine("请先运行 IterativeSunOpt 命令初始化优化引擎");
                return Result.Nothing;
            }

            var useAIOptionNames = new[] { "使用 AI", "不使用 AI" };
            var useAIOption = new OptionList(useAIOptionNames, 1);
            var getUseAI = new GetOption();
            getUseAI.SetCommandPrompt("选择 AI 模式");
            getUseAI.AddOptionList("UseAI", ref useAIOption, "AI 模式");
            getUseAI.AcceptNothing(true);
            
            if (getUseAI.Get() == GetResult.Option)
            {
                int useAI = useAIOption.CurrentListIndex;
                IterativeSunOptCommand._engine.UseAI = useAI;
                RhinoApp.WriteLine($"AI 模式已设置为: {(useAI == 0 ? "使用 AI" : "不使用 AI")}");
                
                if (useAI == 0)
                {
                    RhinoApp.WriteLine("提示: AI 模式需要配置有效的 API 地址和密钥");
                }
            }

            return Result.Success;
        }
    }
}
