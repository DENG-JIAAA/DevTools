
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DevTools.Models;

namespace DevTools.Services
{
    public static class ServiceManager
    {
        private static readonly string ConfigDirectory =
    AppPaths.ConfigDirectory;

        private static readonly string ConfigFilePath =
            Path.Combine(ConfigDirectory, "services.json");

        private static readonly object LockObject = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// 加载全部服务配置
        /// </summary>
        public static List<ServiceInfo> LoadAll()
        {
            lock (LockObject)
            {
                try
                {
                    if (!File.Exists(ConfigFilePath))
                    {
                        return new List<ServiceInfo>();
                    }

                    string json = File.ReadAllText(ConfigFilePath);

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return new List<ServiceInfo>();
                    }

                    return JsonSerializer.Deserialize<List<ServiceInfo>>(
                               json,
                               JsonOptions)
                           ?? new List<ServiceInfo>();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "加载 Java 服务配置失败：" + ex.Message, ex);
                }
            }
        }

        /// <summary>
        /// 保存全部服务配置
        /// </summary>
        public static void SaveAll(List<ServiceInfo> services)
        {
            lock (LockObject)
            {
                try
                {
                    Directory.CreateDirectory(ConfigDirectory);

                    string json = JsonSerializer.Serialize(
                        services,
                        JsonOptions);

                    string tempFilePath = ConfigFilePath + ".tmp";

                    File.WriteAllText(tempFilePath, json);

                    File.Move(tempFilePath, ConfigFilePath, true);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "保存 Java 服务配置失败：" + ex.Message, ex);
                }
            }
        }

        /// <summary>
        /// 添加服务
        /// </summary>
        public static void Add(ServiceInfo service)
        {
            List<ServiceInfo> services = LoadAll();

            if (services.Any(x => x.Id == service.Id))
            {
                throw new InvalidOperationException("该服务已经存在。");
            }

            services.Add(service);
            SaveAll(services);
        }

        /// <summary>
        /// 更新服务
        /// </summary>
        public static void Update(ServiceInfo service)
        {
            List<ServiceInfo> services = LoadAll();

            int index = services.FindIndex(x => x.Id == service.Id);

            if (index < 0)
            {
                throw new InvalidOperationException("未找到需要更新的服务。");
            }

            services[index] = service;
            SaveAll(services);
        }

        /// <summary>
        /// 删除服务
        /// </summary>
        public static void Delete(string serviceId)
        {
            List<ServiceInfo> services = LoadAll();

            int removedCount = services.RemoveAll(x => x.Id == serviceId);

            if (removedCount == 0)
            {
                throw new InvalidOperationException("未找到需要删除的服务。");
            }

            SaveAll(services);
        }
    }
}
