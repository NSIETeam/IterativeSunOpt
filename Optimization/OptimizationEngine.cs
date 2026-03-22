using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rhino.Geometry;
using Rhino;
using IterativeSunOpt.Evaluation;
using IterativeSunOpt.AI;

namespace IterativeSunOpt.Optimization
{
    /// <summary>
    /// 优化引擎核心类
    /// 负责协调整体优化流程：迭代、评估、约束检查
    /// </summary>
    public class OptimizationEngine
    {
        // 评估指标集合
        private readonly List<IEvaluationMetric> _metrics;
        
        // 变异算子管理器
        private readonly MutationOperatorManager _operatorManager;
        
        // AI Agent 服务（可选）
        private readonly IAIAgentService _aiAgentService;
        
        // 是否使用 AI（0=使用AI，1=不使用AI，默认1）
        private int _useAI = 1;
        
        // 当前建筑类型
        private BuildingType _buildingType = BuildingType.Residential;

        // 优化状态
        private Brep _bestBrep;
        private double _bestOverallScore;
        private Dictionary<string, double> _bestMetricScores;
        private int _iterationCount;
        
        // 设计约束（底线机制）
        private DesignConstraints _designConstraints;

        /// <summary>
        /// 是否使用 AI Agent（0=使用，1=不使用）
        /// </summary>
        public int UseAI
        {
            get => _useAI;
            set => _useAI = value;
        }

        /// <summary>
        /// 当前建筑类型
        /// </summary>
        public BuildingType BuildingType
        {
            get => _buildingType;
            set => _buildingType = value;
        }

        /// <summary>
        /// 最优方案
        /// </summary>
        public Brep BestBrep => _bestBrep?.DuplicateBrep();

        /// <summary>
        /// 最优综合得分
        /// </summary>
        public double BestOverallScore => _bestOverallScore;

        /// <summary>
        /// 最优方案的各指标得分
        /// </summary>
        public Dictionary<string, double> BestMetricScores => 
            new Dictionary<string, double>(_bestMetricScores);

        /// <summary>
        /// 当前迭代次数
        /// </summary>
        public int IterationCount => _iterationCount;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public OptimizationEngine()
        {
            // 初始化评估指标（核心指标）
            _metrics = MetricManager.GetCoreMetrics();

            // 初始化变异算子
            _operatorManager = new MutationOperatorManager();

            // 初始化 AI Agent 服务
            _aiAgentService = new AIAgentService();

            // 初始化默认约束
            _designConstraints = _aiAgentService.GetDesignConstraints(_buildingType).Result;
            
            // 初始化得分字典
            _bestMetricScores = new Dictionary<string, double>();
        }

        /// <summary>
        /// 带扩展指标的构造函数
        /// </summary>
        public OptimizationEngine(bool includeExtendedMetrics) : this()
        {
            if (includeExtendedMetrics)
            {
                // 添加扩展指标
                var extendedMetrics = MetricManager.GetExtendedMetrics();
                foreach (var metric in extendedMetrics)
                {
                    _metrics.Add(metric);
                }
            }
        }

        /// <summary>
        /// 添加自定义评估指标
        /// </summary>
        public void AddMetric(IEvaluationMetric metric)
        {
            if (metric != null && !_metrics.Exists(m => m.Name == metric.Name))
            {
                _metrics.Add(metric);
            }
        }

        /// <summary>
        /// 移除评估指标
        /// </summary>
        public void RemoveMetric(string metricName)
        {
            _metrics.RemoveAll(m => m.Name == metricName);
        }

        /// <summary>
        /// 设置指标权重
        /// </summary>
        public void SetMetricWeight(string metricName, double weight)
        {
            var metric = _metrics.Find(m => m.Name == metricName);
            if (metric != null)
            {
                metric.Weight = weight;
            }
        }

        /// <summary>
        /// 获取所有指标
        /// </summary>
        public List<IEvaluationMetric> GetMetrics()
        {
            return new List<IEvaluationMetric>(_metrics);
        }

        /// <summary>
        /// 开始优化
        /// </summary>
        public OptimizationResult Optimize(
            Brep initialBrep,
            OptimizationParameters parameters,
            EvaluationContext evaluationContext,
            Action<IterationInfo> progressCallback = null)
        {
            if (initialBrep == null)
            {
                return new OptimizationResult
                {
                    Success = false,
                    Message = "初始几何体不能为空"
                };
            }

            // 初始化状态
            _bestBrep = initialBrep.DuplicateBrep();
            _bestOverallScore = CalculateOverallScore(_bestBrep, evaluationContext, out _bestMetricScores);
            _iterationCount = 0;

            var mutationParams = new MutationParameters
            {
                TranslationScale = parameters.MutationScale,
                RotationScale = parameters.RotationScale,
                ExtrusionScale = parameters.ExtrusionScale,
                MinHeight = _designConstraints.MinHeight,
                MaxHeight = _designConstraints.MaxHeight,
                Random = new Random(parameters.Seed)
            };

            // 如果使用 AI，获取初始建议
            if (_useAI == 0)
            {
                try
                {
                    var aiRequest = new AIAgentRequest
                    {
                        BuildingType = _buildingType,
                        CurrentHeight = _bestBrep.GetBoundingBox(false).Max.Z,
                        CurrentFAR = CalculateFAR(_bestBrep, evaluationContext),
                        CurrentSunlightScore = _bestMetricScores.GetValueOrDefault("日照率", 0.0),
                        CurrentMetrics = _bestMetricScores,
                        OptimizationGoals = parameters.OptimizationGoals
                    };

                    var aiResponse = _aiAgentService.GetOptimizationSuggestion(aiRequest).Result;
                    if (aiResponse.Success)
                    {
                        // 应用 AI 建议的参数调整
                        ApplyAISuggestions(aiResponse.Suggestions, ref mutationParams);
                    }
                }
                catch (Exception ex)
                {
                    // AI 调用失败，降级到纯算法模式
                    RhinoApp.WriteLine($"AI 调用失败: {ex.Message}，使用纯算法模式");
                }
            }

            // 迭代优化
            for (int i = 0; i < parameters.MaxIterations; i++)
            {
                _iterationCount = i + 1;

                // 检查是否应该停止
                if (parameters.StopCondition?.Invoke(i, _bestOverallScore) ?? false)
                {
                    break;
                }

                // 保存当前最优状态
                Brep candidate = _bestBrep.DuplicateBrep();

                // 执行局部随机修改
                if (!_operatorManager.ApplyRandomMutation(candidate, mutationParams))
                {
                    continue; // 修改失败，跳过本次迭代
                }

                // 底线约束检查
                if (!CheckDesignConstraints(candidate))
                {
                    continue; // 违反底线约束，拒绝修改
                }

                // 计算新得分
                double newScore = CalculateOverallScore(candidate, evaluationContext, out var newMetricScores);

                // 判断是否改进
                bool improved = IsImprovement(newScore, _bestOverallScore);

                if (improved)
                {
                    _bestBrep = candidate;
                    _bestOverallScore = newScore;
                    _bestMetricScores = newMetricScores;

                    // 触发进度回调
                    progressCallback?.Invoke(new IterationInfo
                    {
                        Iteration = _iterationCount,
                        OverallScore = newScore,
                        MetricScores = newMetricScores,
                        Improved = true
                    });
                }
                else
                {
                    progressCallback?.Invoke(new IterationInfo
                    {
                        Iteration = _iterationCount,
                        OverallScore = newScore,
                        MetricScores = newMetricScores,
                        Improved = false
                    });
                }
            }

            return new OptimizationResult
            {
                Success = true,
                BestBrep = _bestBrep.DuplicateBrep(),
                BestScore = _bestOverallScore,
                BestMetricScores = new Dictionary<string, double>(_bestMetricScores),
                TotalIterations = _iterationCount,
                Message = $"优化完成，共迭代 {_iterationCount} 次"
            };
        }

        /// <summary>
        /// 计算综合得分（所有指标的加权总和）
        /// </summary>
        public double CalculateOverallScore(
            Brep brep,
            EvaluationContext context,
            out Dictionary<string, double> metricScores)
        {
            metricScores = new Dictionary<string, double>();
            double totalScore = 0.0;
            double totalWeight = 0.0;

            foreach (var metric in _metrics)
            {
                try
                {
                    double score = metric.Calculate(brep, context);
                    metricScores[metric.Name] = score;

                    // 根据指标方向调整得分
                    double normalizedScore = NormalizeScore(score, metric.Direction);

                    totalScore += normalizedScore * metric.Weight;
                    totalWeight += metric.Weight;
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"[警告] 指标 {metric.Name} 计算失败: {ex.Message}");
                    metricScores[metric.Name] = 0.0;
                }
            }

            return totalWeight > 0 ? totalScore / totalWeight : 0.0;
        }

        /// <summary>
        /// 根据指标方向标准化得分
        /// </summary>
        private double NormalizeScore(double score, MetricDirection direction)
        {
            switch (direction)
            {
                case MetricDirection.HigherIsBetter:
                    return score; // 越高越好
                case MetricDirection.LowerIsBetter:
                    return 1.0 - Math.Min(1.0, Math.Max(0.0, score)); // 越低越好
                case MetricDirection.TargetIsBest:
                    // 假设目标值为 0.5，偏离越多得分越低
                    double deviation = Math.Abs(score - 0.5);
                    return Math.Max(0, 1.0 - deviation * 2.0);
                default:
                    return score;
            }
        }

        /// <summary>
        /// 判断是否为改进（爬山算法：只接受更好的解）
        /// </summary>
        private bool IsImprovement(double newScore, double currentScore)
        {
            return newScore > currentScore + 0.0001; // 添加微小阈值避免浮点误差
        }

        /// <summary>
        /// 检查设计约束（底线机制）
        /// </summary>
        private bool CheckDesignConstraints(Brep brep)
        {
            if (brep == null) return false;

            BoundingBox bbox = brep.GetBoundingBox(false);
            double height = bbox.Max.Z - bbox.Min.Z;

            // 检查高度约束
            if (height < _designConstraints.MinHeight || height > _designConstraints.MaxHeight)
            {
                return false;
            }

            // 计算容积率
            double far = CalculateFAR(brep, new EvaluationContext());

            // 检查容积率约束
            if (far < _designConstraints.MinFAR || far > _designConstraints.MaxFAR)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 计算容积率
        /// </summary>
        private double CalculateFAR(Brep brep, EvaluationContext context)
        {
            if (brep == null) return 0.0;

            VolumeMassProperties volumeProps = VolumeMassProperties.Compute(brep);
            double buildingVolume = volumeProps?.Volume ?? 0;
            return buildingVolume / context.SiteArea;
        }

        /// <summary>
        /// 应用 AI 建议调整参数
        /// </summary>
        private void ApplyAISuggestions(List<OptimizationSuggestion> suggestions, ref MutationParameters parameters)
        {
            if (suggestions == null) return;

            foreach (var suggestion in suggestions)
            {
                switch (suggestion.Parameter.ToLower())
                {
                    case "height":
                        // 调整最大高度约束
                        if (suggestion.Value > 0)
                        {
                            _designConstraints.MaxHeight = Math.Max(
                                _designConstraints.MaxHeight,
                                suggestion.Value
                            );
                        }
                        break;
                    case "rotation":
                        // 调整旋转幅度
                        if (suggestion.Value > 0)
                        {
                            parameters.RotationScale = suggestion.Value;
                        }
                        break;
                    case "translation":
                        // 调整移动幅度
                        if (suggestion.Value > 0)
                        {
                            parameters.TranslationScale = suggestion.Value;
                        }
                        break;
                    case "extrusion":
                        // 调整拉伸幅度
                        if (suggestion.Value > 0)
                        {
                            parameters.ExtrusionScale = suggestion.Value;
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 更新设计约束
        /// </summary>
        public void UpdateDesignConstraints(DesignConstraints constraints)
        {
            if (constraints != null)
            {
                _designConstraints = constraints;
            }
        }

        /// <summary>
        /// 获取当前设计约束
        /// </summary>
        public DesignConstraints GetDesignConstraints()
        {
            return _designConstraints;
        }

        /// <summary>
        /// 获取建筑类型分析
        /// </summary>
        public async Task<BuildingTypeAnalysis> AnalyzeBuildingType()
        {
            return await _aiAgentService.AnalyzeBuildingType(_buildingType.ToString());
        }

        /// <summary>
        /// 应用推荐权重配置
        /// </summary>
        public void ApplyRecommendedWeights(string projectType)
        {
            var weights = MetricManager.GetRecommendedWeights(projectType);
            foreach (var kvp in weights)
            {
                SetMetricWeight(kvp.Key, kvp.Value);
            }
        }
    }

    #region 数据模型

    /// <summary>
    /// 优化参数
    /// </summary>
    public class OptimizationParameters
    {
        public int MaxIterations { get; set; } = 100;
        public double MutationScale { get; set; } = 1.0;
        public double RotationScale { get; set; } = 5.0;
        public double ExtrusionScale { get; set; } = 1.0;
        public int Seed { get; set; } = 42;
        public bool UsePreview { get; set; } = true;
        public List<string> OptimizationGoals { get; set; } = new List<string>();
        
        /// <summary>
        /// 停止条件回调（返回 true 时停止迭代）
        /// </summary>
        public Func<int, double, bool> StopCondition { get; set; }
    }

    /// <summary>
    /// 优化结果
    /// </summary>
    public class OptimizationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Brep BestBrep { get; set; }
        public double BestScore { get; set; }
        public Dictionary<string, double> BestMetricScores { get; set; }
        public int TotalIterations { get; set; }
    }

    /// <summary>
    /// 迭代信息
    /// </summary>
    public class IterationInfo
    {
        public int Iteration { get; set; }
        public double OverallScore { get; set; }
        public Dictionary<string, double> MetricScores { get; set; }
        public bool Improved { get; set; }
    }

    #endregion
}
