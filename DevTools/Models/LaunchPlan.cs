
using System;
using System.Collections.Generic;

namespace DevTools.Models
{
    /// <summary>
    /// 本地 Java 服务启动方案
    /// </summary>
    public class LaunchPlan
    {
        /// <summary>
        /// 方案唯一标识
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 方案名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 方案描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 启动阶段
        /// </summary>
        public List<LaunchStage> Stages { get; set; } = new();

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
