using Microsoft.Extensions.Options;

namespace NotifyHubAPI.Services
{
    public interface IApiKeyService
    {
        /// <summary>
        /// 验证API密钥是否有效
        /// </summary>
        /// <param name="apiKey">API密钥</param>
        /// <returns>是否有效</returns>
        bool IsValidApiKey(string apiKey);

        /// <summary>
        /// 根据API密钥获取项目名称
        /// </summary>
        /// <param name="apiKey">API密钥</param>
        /// <returns>项目名称，如果不存在返回null</returns>
        string? GetProjectByApiKey(string apiKey);

        /// <summary>
        /// 获取所有有效的API密钥
        /// </summary>
        /// <returns>API密钥字典</returns>
        Dictionary<string, string> GetAllApiKeys();
    }

    public class ApiKeyService : IApiKeyService
    {
        private readonly Dictionary<string, string> _apiKeys;
        private readonly ILogger<ApiKeyService> _logger;

        public ApiKeyService(IConfiguration configuration, ILogger<ApiKeyService> logger)
        {
            _logger = logger;
            _apiKeys = new Dictionary<string, string>();

            // 优先级：环境变量 > 用户机密 > 配置文件
            LoadFromEnvironmentVariables();
            LoadFromConfiguration(configuration);

            if (_apiKeys.Count == 0)
            {
                var errorMessage = "严重错误：未找到任何API密钥配置。请检查以下位置：" +
                                 "1. 环境变量（NOTIFYHUB_APIKEY_*）" +
                                 "2. 用户机密（ApiKeys section）" +
                                 "3. 配置文件（ApiKeys section）";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            _logger.LogInformation("已加载 {Count} 个API密钥配置", _apiKeys.Count);
        }

        private void LoadFromEnvironmentVariables()
        {
            var envVars = Environment.GetEnvironmentVariables()
                .Cast<System.Collections.DictionaryEntry>()
                .Where(kv => kv.Key.ToString()?.StartsWith("NOTIFYHUB_APIKEY_") == true)
                .ToList();

            foreach (var envVar in envVars)
            {
                var key = envVar.Key.ToString();
                var value = envVar.Value?.ToString();

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                    continue;

                // 提取项目名称: NOTIFYHUB_APIKEY_DEFAULT -> DEFAULT
                var projectName = key.Substring("NOTIFYHUB_APIKEY_".Length);

                _apiKeys[value] = projectName;
                _logger.LogInformation("已从环境变量加载API密钥，项目: {ProjectName}", projectName);
            }
        }

        private void LoadFromConfiguration(IConfiguration configuration)
        {
            var apiKeysSection = configuration.GetSection("ApiKeys");
            if (!apiKeysSection.Exists())
            {
                _logger.LogDebug("配置中未找到ApiKeys节");
                return;
            }

            foreach (var kvp in apiKeysSection.GetChildren())
            {
                var projectKey = kvp.Key;
                var apiKeyValue = kvp.Value;

                if (string.IsNullOrEmpty(apiKeyValue))
                    continue;

                // 如果环境变量中已存在相同的API密钥，跳过（环境变量优先级更高）
                if (_apiKeys.ContainsKey(apiKeyValue))
                {
                    _logger.LogDebug("API密钥已存在（来自环境变量），跳过配置项: {ProjectKey}", projectKey);
                    continue;
                }

                // 提取项目名称: NOTIFYHUB_APIKEY_DEFAULT -> DEFAULT
                var projectName = projectKey.StartsWith("NOTIFYHUB_APIKEY_")
                    ? projectKey.Substring("NOTIFYHUB_APIKEY_".Length)
                    : projectKey;

                _apiKeys[apiKeyValue] = projectName;
                _logger.LogInformation("已从配置加载API密钥，项目: {ProjectName}", projectName);
            }
        }

        public bool IsValidApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return false;

            var isValid = _apiKeys.ContainsKey(apiKey);

            if (!isValid)
            {
                _logger.LogWarning("无效的API密钥访问尝试: {ApiKey}",
                    apiKey.Length > 8 ? apiKey[..8] + "..." : apiKey);
            }

            return isValid;
        }

        public string? GetProjectByApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return null;

            return _apiKeys.TryGetValue(apiKey, out var projectName) ? projectName : null;
        }

        public Dictionary<string, string> GetAllApiKeys()
        {
            return _apiKeys.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
        }
    }
}