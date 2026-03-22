# 维护指南

本文档为开发人员提供插件的维护、扩展和故障排除指南。

---

## 项目结构

```
IterativeSunOpt/
├── PluginMain.cs              # 插件入口
├── IterativeSunOpt.csproj     # 项目配置
├── build.bat                  # 打包脚本
├── README.md                  # 用户文档
├── LICENSE                    # 许可证
├── MAINTENANCE.md             # 本文件
├── Evaluation/                # 评估指标模块
│   ├── EvaluationMetrics.cs   # 核心指标（6个）
│   └── ExtendedMetrics.cs     # 扩展指标（13个）
├── Optimization/              # 优化算法模块
│   ├── OptimizationEngine.cs  # 优化引擎核心
│   └── MutationOperators.cs   # 变异算子（5种）
├── AI/                        # AI Agent 模块
│   └── AIAgentService.cs      # AI 服务实现
└── Commands/                  # 命令模块
    └── IterativeSunOptCommand.cs
```

---

## 核心接口

### IEvaluationMetric - 评估指标接口

```csharp
public interface IEvaluationMetric
{
    string Name { get; }
    double Weight { get; set; }
    MetricDirection Direction { get; }
    double Calculate(Brep brep, EvaluationContext context);
}
```

### IMutationOperator - 变异算子接口

```csharp
public interface IMutationOperator
{
    string Name { get; }
    bool Apply(Brep brep, MutationParameters parameters);
}
```

### IAIAgentService - AI 服务接口

```csharp
public interface IAIAgentService
{
    Task<AIAgentResponse> GetOptimizationSuggestion(AIAgentRequest request);
    Task<BuildingTypeAnalysis> AnalyzeBuildingType(string buildingType);
    Task<DesignConstraints> GetDesignConstraints(BuildingType buildingType);
}
```

---

## 添加新评估指标

1. 创建新类实现 `IEvaluationMetric` 接口
2. 定义 `Name`、`Weight`、`Direction` 属性
3. 实现 `Calculate()` 方法
4. 在 `OptimizationEngine` 或 `MetricManager` 中注册

示例：
```csharp
public class NoiseMetric : IEvaluationMetric
{
    public string Name => "噪声影响";
    public double Weight { get; set; } = 0.1;
    public MetricDirection Direction => MetricDirection.LowerIsBetter;

    public double Calculate(Brep brep, EvaluationContext context)
    {
        // 实现评估逻辑
        return 0.0;
    }
}
```

---

## 添加新变异算子

1. 创建新类实现 `IMutationOperator` 接口
2. 定义 `Name` 属性
3. 实现 `Apply()` 方法
4. 在 `MutationOperatorManager` 中注册

示例：
```csharp
public class MirrorFlipOperator : IMutationOperator
{
    public string Name => "镜像翻转";

    public bool Apply(Brep brep, MutationParameters parameters)
    {
        // 实现变异逻辑
        return true;
    }
}
```

---

## AI 服务配置

在 `AIAgentService.cs` 中修改以下配置：

```csharp
private const string AIAgentBaseUrl = "https://api.your-ai-service.com/v1";
private const string AIAgentApiKey = "YOUR_API_KEY_HERE";
private const int RequestTimeout = 30000;
```

---

## 故障排除

### 编译问题

**找不到 RhinoCommon.dll**
- 确认 Rhino 8 已安装
- 检查 `.csproj` 中的引用路径

**Newtonsoft.Json 未找到**
- 本实现已移除 Newtonsoft.Json 依赖
- 使用内置的简化序列化

### 运行时问题

**插件加载失败**
- 检查 .NET Framework 版本（需要 4.8）
- 检查平台配置（必须是 x64）

**优化过程中崩溃**
- 降低迭代次数
- 简化模型面数
- 关闭实时预览

---

## 性能优化

1. **降低面数**：简化几何体
2. **关闭预览**：禁用实时更新
3. **减少指标**：只计算必要的指标
4. **调整参数**：使用较小的变异幅度

---

## 测试建议

1. 使用简单几何体（立方体、球体）
2. 初始测试迭代次数设为 20-50
3. 逐步增加复杂度
4. 记录性能指标

---

## 版本发布

1. 更新版本号（`PluginMain.cs`、`.csproj`）
2. 更新 `README.md`
3. 运行 `build.bat` 编译打包
4. 测试验证
5. 发布 ZIP 包

---

**最后更新**：2024
**维护者**：IterativeSunOpt 开发团队
