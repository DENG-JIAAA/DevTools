
using System;
using System.Collections.Generic;

namespace DevTools.Models
{
    /// <summary>
    /// 启动方案中的一个启动阶段
    /// </summary>
    public class LaunchStage
    {
        /// <summary>
        /// 阶段唯一标识
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 阶段名称，例如：第一批、基础服务
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 阶段执行顺序
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// 阶段内服务启动方式：Parallel / Sequential
        /// 默认并行
        /// </summary>
        public string ExecutionMode { get; set; } = "Parallel";

        /// <summary>
        /// 当前阶段包含的服务 ID
        /// </summary>
        public List<string> ServiceIds { get; set; } = new();
    }
}
