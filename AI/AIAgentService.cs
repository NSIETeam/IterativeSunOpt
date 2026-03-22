using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace IterativeSunOpt.AI
{
    /// <summary>
    /// AI Agent 服务接口
    /// 提供智能建筑布局建议和参数优化
    /// </summary>
    public interface IAIAgentService
    {
        Task<AIAgentResponse> GetOptimizationSuggestion(AIAgentRequest request);
        Task<BuildingTypeAnalysis> AnalyzeBuildingType(string buildingType);
        Task<DesignConstraints> GetDesignConstraints(BuildingType buildingType);
    }

    /// <summary>
    /// AI Agent 服务实现
    /// 默认使用 REST API 调用，可替换为其他实现方式
    /// </summary>
    public class AIAgentService : IAIAgentService
    {
        // ========================================
        // API 配置区域 - 请在此处修改 AI 服务地址
        // ========================================
        
        /// <summary>
        /// AI Agent 服务基础 URL
        /// 示例: "https://api.your-ai-service.com/v1"
        /// 本地服务示例: "http://localhost:8080/api"
        /// </summary>
        private const string AIAgentBaseUrl = "https://api.archai-ai.com/v1";
        
        /// <summary>
        /// API 密钥（如果需要）
        /// </summary>
        private const string AIAgentApiKey = "YOUR_API_KEY_HERE";
        
        /// <summary>
        /// 请求超时时间（毫秒）
        /// </summary>
        private const int RequestTimeout = 30000;

        private readonly HttpClient _httpClient;

        public AIAgentService()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(AIAgentBaseUrl);
            _httpClient.Timeout = TimeSpan.FromMilliseconds(RequestTimeout);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {AIAgentApiKey}");
            _httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
        }

        /// <summary>
        /// 获取优化建议
        /// </summary>
        public async Task<AIAgentResponse> GetOptimizationSuggestion(AIAgentRequest request)
        {
            try
            {
                string json = SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("/optimization/suggest", content);
                response.EnsureSuccessStatusCode();
                
                string responseJson = await response.Content.ReadAsStringAsync();
                return DeserializeObject<AIAgentResponse>(responseJson);
            }
            catch (Exception ex)
            {
                // 如果 API 调用失败，返回默认建议
                return GetDefaultSuggestion(request);
            }
        }

        /// <summary>
        /// 分析建筑类型
        /// </summary>
        public async Task<BuildingTypeAnalysis> AnalyzeBuildingType(string buildingType)
        {
            try
            {
                var request = new { buildingType = buildingType };
                string json = SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("/analysis/building-type", content);
                response.EnsureSuccessStatusCode();
                
                string responseJson = await response.Content.ReadAsStringAsync();
                return DeserializeObject<BuildingTypeAnalysis>(responseJson);
            }
            catch (Exception ex)
            {
                // 返回默认分析结果
                return GetDefaultBuildingTypeAnalysis(buildingType);
            }
        }

        /// <summary>
        /// 获取设计约束
        /// </summary>
        public async Task<DesignConstraints> GetDesignConstraints(BuildingType buildingType)
        {
            try
            {
                var request = new { buildingType = buildingType.ToString() };
                string json = SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("/constraints/get", content);
                response.EnsureSuccessStatusCode();
                
                string responseJson = await response.Content.ReadAsStringAsync();
                return DeserializeObject<DesignConstraints>(responseJson);
            }
            catch (Exception ex)
            {
                // 返回默认约束
                return GetDefaultConstraints(buildingType);
            }
        }

        // ========================================
        // 默认/后备逻辑区域 - 当 AI 服务不可用时使用
        // ========================================

        private AIAgentResponse GetDefaultSuggestion(AIAgentRequest request)
        {
            return new AIAgentResponse
            {
                Success = false,
                Message = "AI 服务不可用，使用默认建议",
                Suggestions = new List<OptimizationSuggestion>
                {
                    new OptimizationSuggestion
                    {
                        Type = "height_adjustment",
                        Description = "建议增加建筑高度以提高容积率",
                        Parameter = "height",
                        Value = request.CurrentHeight * 1.2,
                        Confidence = 0.6
                    },
                    new OptimizationSuggestion
                    {
                        Type = "spacing_optimization",
                        Description = "建议调整建筑朝向以改善日照",
                        Parameter = "rotation",
                        Value = 15.0,
                        Confidence = 0.7
                    }
                }
            };
        }

        private BuildingTypeAnalysis GetDefaultBuildingTypeAnalysis(string buildingType)
        {
            var analysis = new BuildingTypeAnalysis
            {
                BuildingType = buildingType,
                PrimaryFunction = DeterminePrimaryFunction(buildingType),
                RecommendedHeightRange = GetRecommendedHeightRange(buildingType),
                KeyMetrics = GetKeyMetrics(buildingType)
            };

            return analysis;
        }

        private DesignConstraints GetDefaultConstraints(BuildingType buildingType)
        {
            return new DesignConstraints
            {
                BuildingType = buildingType,
                MinHeight = GetMinHeight(buildingType),
                MaxHeight = GetMaxHeight(buildingType),
                MinFAR = GetMinFAR(buildingType),
                MaxFAR = GetMaxFAR(buildingType),
                MinSpacing = GetMinSpacing(buildingType),
                RequiredOpenSpaceRatio = GetRequiredOpenSpaceRatio(buildingType)
            };
        }

        // ========================================
        // 辅助方法
        // ========================================

        private string DeterminePrimaryFunction(string buildingType)
        {
            switch (buildingType.ToLower())
            {
                case "residential":
                case "住宅":
                    return "居住";
                case "office":
                case "办公":
                    return "办公";
                case "commercial":
                case "商业":
                    return "商业";
                case "mixed":
                case "综合体":
                    return "混合功能";
                default:
                    return "通用";
            }
        }

        private Tuple<double, double> GetRecommendedHeightRange(string buildingType)
        {
            switch (buildingType.ToLower())
            {
                case "residential":
                case "住宅":
                    return Tuple.Create(18.0, 100.0);
                case "office":
                case "办公":
                    return Tuple.Create(30.0, 150.0);
                case "commercial":
                case "商业":
                    return Tuple.Create(12.0, 60.0);
                default:
                    return Tuple.Create(15.0, 100.0);
            }
        }

        private List<string> GetKeyMetrics(string buildingType)
        {
            var metrics = new List<string> { "日照率", "容积率" };

            switch (buildingType.ToLower())
            {
                case "residential":
                case "住宅":
                    metrics.AddRange(new[] { "建筑间距", "通风", "视野" });
                    break;
                case "office":
                case "办公":
                    metrics.AddRange(new[] { "开放空间", "视野", "通风" });
                    break;
                case "commercial":
                case "商业":
                    metrics.AddRange(new[] { "开放空间", "视野", "建筑间距" });
                    break;
            }

            return metrics;
        }

        private double GetMinHeight(BuildingType buildingType)
        {
            switch (buildingType)
            {
                case BuildingType.Residential:
                    return 12.0;
                case BuildingType.Office:
                    return 18.0;
                case BuildingType.Commercial:
                    return 9.0;
                default:
                    return 9.0;
            }
        }

        private double GetMaxHeight(BuildingType buildingType)
        {
            switch (buildingType)
            {
                case BuildingType.Residential:
                    return 100.0;
                case BuildingType.Office:
                    return 200.0;
                case BuildingType.Commercial:
                    return 80.0;
                default:
                    return 100.0;
            }
        }

        private double GetMinFAR(BuildingType buildingType)
        {
            switch (buildingType)
            {
                case BuildingType.Residential:
                    return 1.5;
                case BuildingType.Office:
                    return 2.0;
                case BuildingType.Commercial:
                    return 2.5;
                default:
                    return 1.5;
            }
        }

        private double GetMaxFAR(BuildingType buildingType)
        {
            switch (buildingType)
            {
                case BuildingType.Residential:
                    return 3.5;
                case BuildingType.Office:
                    return 5.0;
                case BuildingType.Commercial:
                    return 6.0;
                default:
                    return 4.0;
            }
        }

        private double GetMinSpacing(BuildingType buildingType)
        {
            switch (buildingType)
            {
                case BuildingType.Residential:
                    return 15.0;
                case BuildingType.Office:
                    return 20.0;
                case BuildingType.Commercial:
                    return 18.0;
                default:
                    return 15.0;
            }
        }

        private double GetRequiredOpenSpaceRatio(BuildingType buildingType)
        {
            switch (buildingType)
            {
                case BuildingType.Residential:
                    return 0.3;
                case BuildingType.Office:
                    return 0.25;
                case BuildingType.Commercial:
                    return 0.35;
                default:
                    return 0.3;
            }
        }

        // 简单的 JSON 序列化/反序列化（避免依赖 Newtonsoft.Json）
        private string SerializeObject<T>(T obj)
        {
            // 简化实现，实际项目中建议使用 Newtonsoft.Json
            var sb = new StringBuilder();
            sb.Append("{");
            
            var type = typeof(T);
            var properties = type.GetProperties();
            bool first = true;
            
            foreach (var prop in properties)
            {
                if (!first) sb.Append(",");
                first = false;
                
                var value = prop.GetValue(obj);
                sb.Append($"\"{prop.Name}\":");
                
                if (value is string strValue)
                {
                    sb.Append($"\"{strValue}\"");
                }
                else if (value is double dblValue)
                {
                    sb.Append(dblValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                else if (value is int intValue)
                {
                    sb.Append(intValue);
                }
                else if (value is bool boolValue)
                {
                    sb.Append(boolValue.ToString().ToLower());
                }
                else
                {
                    sb.Append($"\"{value}\"");
                }
            }
            
            sb.Append("}");
            return sb.ToString();
        }

        private T DeserializeObject<T>(string json) where T : new()
        {
            // 简化实现，返回默认实例
            // 实际项目中建议使用 Newtonsoft.Json
            return new T();
        }
    }

    // ========================================
    // 数据模型定义
    // ========================================

    public enum BuildingType
    {
        Residential,  // 住宅
        Office,       // 办公
        Commercial,   // 商业
        Mixed,        // 综合体
        Industrial,   // 工业
        Public        // 公共建筑
    }

    public class AIAgentRequest
    {
        public BuildingType BuildingType { get; set; }
        public double CurrentHeight { get; set; }
        public double CurrentFAR { get; set; }
        public double CurrentSunlightScore { get; set; }
        public Dictionary<string, double> CurrentMetrics { get; set; }
        public List<string> OptimizationGoals { get; set; }
    }

    public class AIAgentResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<OptimizationSuggestion> Suggestions { get; set; }
    }

    public class OptimizationSuggestion
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public string Parameter { get; set; }
        public double Value { get; set; }
        public double Confidence { get; set; }
    }

    public class BuildingTypeAnalysis
    {
        public string BuildingType { get; set; }
        public string PrimaryFunction { get; set; }
        public Tuple<double, double> RecommendedHeightRange { get; set; }
        public List<string> KeyMetrics { get; set; }
    }

    public class DesignConstraints
    {
        public BuildingType BuildingType { get; set; }
        public double MinHeight { get; set; }
        public double MaxHeight { get; set; }
        public double MinFAR { get; set; }
        public double MaxFAR { get; set; }
        public double MinSpacing { get; set; }
        public double RequiredOpenSpaceRatio { get; set; }
    }
}
