# IterativeSunOpt v2.0.0 发布说明

## 发布日期
2024

## 版本概述
IterativeSunOpt v2.0.0 是一个重大更新版本，实现了完整的模块化架构设计和多指标评估体系。

## 主要功能

### 1. 多指标评估体系
- **核心指标（6个）**：日照率、建筑间距、开放空间比例、容积率、视野开阔度、通风潜力
- **扩展指标（13个）**：绿地可达性、景观视线保护、自然光渗透率、建造成本、土地利用效率、能源消耗潜力、空间丰富度、尺度协调性、消防安全距离、抗震性能、生物多样性支持、低碳排放潜力、停车便利性

### 2. 优化算法
- 局部爬山算法
- 5种变异算子：面移动、整体旋转、面拉伸、顶点微调、比例缩放
- 底线约束机制

### 3. AI Agent 集成
- 可选 AI 辅助优化
- 建筑类型自动识别
- 设计约束推荐

### 4. 用户命令
- IterativeSunOpt：运行优化
- ShowOptResults：显示结果
- ConfigMetrics：配置指标
- SetBuildingType：设置类型
- SetAIMode：设置 AI 模式

## 文件清单

```
IterativeSunOpt/
├── IterativeSunOpt.dll          # 插件主文件
├── README.md                     # 用户文档
├── LICENSE                       # MIT 许可证
├── MAINTENANCE.md               # 维护文档
├── INSTALL.txt                   # 安装说明
```

## 系统要求
- Rhino 7 或 Rhino 8
- Windows 10/11
- .NET Framework 4.8

## 安装方法

### 方法一：手动安装
1. 将 IterativeSunOpt.dll 复制到：
   `%AppData%\McNeel\Rhinoceros\8.0\Plug-ins\IterativeSunOpt\`
2. 启动 Rhino 8
3. 运行命令：IterativeSunOpt

### 方法二：拖放安装
1. 将 IterativeSunOpt.dll 拖放到 Rhino 视口
2. 在弹出的对话框中确认安装

## 快速开始
1. 在 Rhino 中创建一个建筑体块
2. 运行 IterativeSunOpt 命令
3. 选择体块并设置参数
4. 等待优化完成

## 已知问题
- 面拉伸算子的布尔合并可能不稳定
- 当前版本仅支持单建筑优化

## 技术支持
- GitHub Issues
- Email: support@example.com

## 致谢
感谢 RhinoCommon SDK 和所有贡献者。

---
**IterativeSunOpt 开发团队**
