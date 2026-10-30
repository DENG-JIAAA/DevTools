
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DevTools.Models;

namespace DevTools.Services
{
    /// <summary>
    /// 启动方案配置管理
    /// </summary>
    public class LaunchPlanManager
    {
        private static readonly object LockObject = new();

        private static readonly string ConfigDirectory =
    AppPaths.ConfigDirectory;

        private static readonly string ConfigFilePath =
            Path.Combine(ConfigDirectory, "launch-plans.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// 获取启动方案配置文件路径
        /// </summary>
        public static string GetConfigFilePath()
        {
            return ConfigFilePath;
        }

        /// <summary>
        /// 加载全部启动方案
        /// </summary>
        public static List<LaunchPlan> LoadAll()
        {
            lock (LockObject)
            {
                try
                {
                    if (!Directory.Exists(ConfigDirectory))
                    {
                        Directory.CreateDirectory(ConfigDirectory);
                    }

                    if (!File.Exists(ConfigFilePath))
                    {
                        File.WriteAllText(ConfigFilePath, "[]");
                        return new List<LaunchPlan>();
                    }

                    string json = File.ReadAllText(ConfigFilePath);

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return new List<LaunchPlan>();
                    }

                    return JsonSerializer.Deserialize<List<LaunchPlan>>(
                               json, JsonOptions)
                           ?? new List<LaunchPlan>();
                }
                catch (JsonException ex)
                {
                    throw new Exception(
                        "启动方案配置文件格式错误，请检查 launch-plans.json。",
                        ex);
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"加载启动方案配置失败：{ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 保存全部启动方案
        /// </summary>
        public static void SaveAll(List<LaunchPlan> plans)
        {
            lock (LockObject)
            {
                try
                {
                    Directory.CreateDirectory(ConfigDirectory);

                    plans ??= new List<LaunchPlan>();

                    string json = JsonSerializer.Serialize(
                        plans, JsonOptions);

                    string tempFilePath = ConfigFilePath + ".tmp";

                    File.WriteAllText(tempFilePath, json);

                    File.Move(
                        tempFilePath,
                        ConfigFilePath,
                        true);
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"保存启动方案配置失败：{ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 根据 ID 获取启动方案
        /// </summary>
        public static LaunchPlan? GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return LoadAll().FirstOrDefault(
                x => x.Id == id);
        }

        /// <summary>
        /// 新增或更新启动方案
        /// </summary>
        public static void AddOrUpdate(LaunchPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (string.IsNullOrWhiteSpace(plan.Name))
            {
                throw new ArgumentException("启动方案名称不能为空。");
            }

            lock (LockObject)
            {
                List<LaunchPlan> plans = LoadAll();

                if (string.IsNullOrWhiteSpace(plan.Id))
                {
                    plan.Id = Guid.NewGuid().ToString();
                }

                LaunchPlan? existing = plans.FirstOrDefault(
                    x => x.Id == plan.Id);

                plan.UpdatedAt = DateTime.Now;

                if (existing == null)
                {
                    plan.CreatedAt = DateTime.Now;
                    plans.Add(plan);
                }
                else
                {
                    plan.CreatedAt = existing.CreatedAt;

                    int index = plans.IndexOf(existing);
                    plans[index] = plan;
                }

                SaveAll(plans);
            }
        }

        /// <summary>
        /// 删除启动方案
        /// </summary>
        public static bool Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            lock (LockObject)
            {
                List<LaunchPlan> plans = LoadAll();

                LaunchPlan? plan = plans.FirstOrDefault(
                    x => x.Id == id);

                if (plan == null)
                {
                    return false;
                }

                plans.Remove(plan);

                SaveAll(plans);

                return true;
            }
        }
    }
}
