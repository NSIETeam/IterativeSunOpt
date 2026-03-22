using System;
using Rhino;
using Rhino.Plugins;

namespace IterativeSunOpt
{
    /// <summary>
    /// 插件主类
    /// 负责插件的生命周期管理和命令注册
    /// </summary>
    public class IterativeSunOptPlugin : PlugIn
    {
        public IterativeSunOptPlugin()
        {
            Instance = this;
        }

        /// <summary>
        /// 插件实例（单例）
        /// </summary>
        public static IterativeSunOptPlugin Instance { get; private set; }

        /// <summary>
        /// 插件名称
        /// </summary>
        public override string PlugInName => "Iterative SunOpt";

        /// <summary>
        /// 插件 ID
        /// </summary>
        public override string PlugInId => "IterativeSunOpt";

        /// <summary>
        /// 插件版本
        /// </summary>
        public override string Version => "2.0.0";

        /// <summary>
        /// 插件加载时调用
        /// </summary>
        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            try
            {
                RhinoApp.WriteLine($"=== {PlugInName} v{Version} 已加载 ===");
                RhinoApp.WriteLine("");
                RhinoApp.WriteLine("可用命令:");
                RhinoApp.WriteLine("  IterativeSunOpt  - 迭代式建筑优化");
                RhinoApp.WriteLine("  ShowOptResults   - 显示优化结果");
                RhinoApp.WriteLine("  ConfigMetrics    - 配置评估指标权重");
                RhinoApp.WriteLine("  SetBuildingType  - 设置建筑类型");
                RhinoApp.WriteLine("  SetAIMode        - 设置 AI 模式");
                RhinoApp.WriteLine("");
                RhinoApp.WriteLine("提示: 输入命令名即可使用");
                RhinoApp.WriteLine("");
                
                return LoadReturnCode.Success;
            }
            catch (Exception ex)
            {
                errorMessage = $"插件加载失败: {ex.Message}";
                return LoadReturnCode.Failure;
            }
        }

        /// <summary>
        /// 插件卸载时调用
        /// </summary>
        protected override void OnUnload()
        {
            RhinoApp.WriteLine($"=== {PlugInName} 插件已卸载 ===");
            base.OnUnload();
        }
    }
}
