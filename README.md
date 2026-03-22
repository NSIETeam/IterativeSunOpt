# Iterative SunOpt v2.0.0

一个基于 RhinoCommon 的 Rhino 7/8 C# 插件，通过局部爬山算法和多指标评估体系，实现建筑体块的智能优化设计。

## 核心功能

### 1. 迭代式局部优化
- **局部随机修改**：面移动、整体旋转、面拉伸、顶点微调、比例缩放
- **爬山算法**：只接受改进方案，逐步逼近最优解
- **实时预览**：优化过程中实时显示当前最优方案

### 2. 多指标评估体系

#### 核心指标（6个）
| 指标 | 说明 | 方向 | 默认权重 |
|------|------|------|----------|
| 日照率 | 受光面积占总表面积比例 | 越高越好 | 0.3 |
| 建筑间距 | 到场地边界的最小距离 | 越高越好 | 0.2 |
| 开放空间比例 | 场地中未被建筑占据的空间比例 | 越高越好 | 0.15 |
| 容积率 | 建筑体积与场地面积的比值 | 越高越好 | 0.15 |
| 视野开阔度 | 从观测点看建筑的视野张角 | 越高越好 | 0.1 |
| 通风潜力 | 基于风向的迎风面积比例 | 越高越好 | 0.1 |

#### 扩展指标（13个）
- **环境景观类**：绿地可达性、景观视线保护、自然光渗透率
- **经济效益类**：建造成本、土地利用效率、能源消耗潜力
- **空间品质类**：空间丰富度、尺度协调性
- **安全韧性类**：消防安全距离、抗震性能
- **生态可持续类**：生物多样性支持、低碳排放潜力
- **功能布局类**：停车便利性

### 3. AI Agent 集成
- **可选 AI 辅助**：支持启用/禁用 AI 智能建议
- **建筑类型分析**：自动识别住宅、办公、商业等建筑类型
- **设计约束推荐**：根据建筑类型推荐参数
- **智能参数调优**：AI 基于当前方案提供优化建议

### 4. 底线约束机制
- **最小/最大高度约束**
- **容积率范围约束**
- **建筑间距约束**

## 快速开始

### 安装

**方法一：手动安装**
```
1. 将 IterativeSunOpt.dll 复制到:
   %AppData%\McNeel\Rhinoceros\8.0\Plug-ins\IterativeSunOpt\

2. 启动 Rhino 8，运行命令: IterativeSunOpt
```

**方法二：拖放安装**
```
1. 将 IterativeSunOpt.dll 拖放到 Rhino 视口
2. 在弹出的对话框中确认安装
```

### 使用流程

1. **创建建筑体块**
   - 在 Rhino 中绘制一个简单的建筑体块（Brep 或 Mesh）

2. **运行优化命令**
   ```
   命令: IterativeSunOpt
   ```

3. **设置参数**
   - 选择要优化的对象
   - 设置迭代次数、变形幅度、建筑类型等参数

4. **查看结果**
   - 优化完成后，最优方案会自动添加到文档中
   - 使用 `ShowOptResults` 查看详细结果

## 可用命令

| 命令 | 说明 |
|------|------|
| `IterativeSunOpt` | 运行迭代式建筑优化 |
| `ShowOptResults` | 显示最新优化结果详情 |
| `ConfigMetrics` | 配置评估指标权重 |
| `SetBuildingType` | 设置建筑类型 |
| `SetAIMode` | 设置 AI 模式 |

## 项目结构

```
IterativeSunOpt/
├── PluginMain.cs                    # 插件入口
├── IterativeSunOpt.csproj           # 项目配置
├── Evaluation/
│   ├── EvaluationMetrics.cs         # 核心评估指标
│   └── ExtendedMetrics.cs           # 扩展评估指标
├── Optimization/
│   ├── OptimizationEngine.cs        # 优化引擎核心
│   └── MutationOperators.cs         # 变异算子
├── AI/
│   └── AIAgentService.cs            # AI Agent 服务
└── Commands/
    └── IterativeSunOptCommand.cs    # 用户命令
```

## 编译说明

### 环境要求
- Visual Studio 2019 或更高版本
- .NET Framework 4.8 SDK
- Rhino 7 或 Rhino 8

### 编译步骤

```bash
# 使用 Visual Studio Developer Command Prompt
cd IterativeSunOpt
msbuild IterativeSunOpt.csproj /p:Configuration=Release /p:Platform=x64
```

或运行打包脚本：
```bash
build.bat
```

## 扩展开发

### 添加自定义评估指标

```csharp
public class MyCustomMetric : IEvaluationMetric
{
    public string Name => "自定义指标";
    public double Weight { get; set; } = 0.1;
    public MetricDirection Direction => MetricDirection.HigherIsBetter;

    public double Calculate(Brep brep, EvaluationContext context)
    {
        // 实现评估逻辑
        return 0.0;
    }
}
```

### 添加自定义变异算子

```csharp
public class MyCustomOperator : IMutationOperator
{
    public string Name => "自定义算子";

    public bool Apply(Brep brep, MutationParameters parameters)
    {
        // 实现变异逻辑
        return true;
    }
}
```

## 性能优化建议

1. **降低模型面数**：使用 Mesh Reduce 或 Brep Simplify
2. **关闭实时预览**：在参数设置中选择"关闭预览"
3. **调整迭代次数**：初期测试使用较小的迭代次数
4. **调整指标权重**：关注核心指标，降低次要指标权重

## 已知限制

1. 当前版本主要针对单个建筑体块优化
2. 面拉伸算子的布尔合并可能不稳定
3. AI 模式需要网络连接和有效的 API 配置

## 更新日志

### v2.0.0
- 模块化架构重构
- 添加13个扩展评估指标
- 集成 AI Agent 服务
- 实现底线约束机制
- 添加5个变异算子
- 完善用户命令

### v1.0.0
- 基本的迭代式日照优化功能
- 单一日照评估指标

## 许可证

MIT License

## 联系方式

- 问题反馈：GitHub Issues
- 邮箱：support@example.com

---

**注意**：本插件为教育和研究目的开发，使用前请确保符合当地建筑设计规范和标准。
