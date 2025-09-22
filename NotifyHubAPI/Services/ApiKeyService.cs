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

            // 从配置文件加载API密钥
            var apiKeysSection = configuration.GetSection("ApiKeys");
            foreach (var kvp in apiKeysSection.GetChildren())
            {
                var projectName = kvp.Key;
                var apiKey = kvp.Value;

                if (!string.IsNullOrEmpty(apiKey))
                {
                    _apiKeys[apiKey] = projectName;
                    _logger.LogInformation("已加载API密钥配置，项目: {ProjectName}", projectName);
                }
            }

            if (_apiKeys.Count == 0)
            {
                _logger.LogWarning("未找到任何API密钥配置");
            }
        }

        public bool IsValidApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return false;

            var isValid = _apiKeys.ContainsKey(apiKey);

            if (!isValid)
            {
                _logger.LogWarning("无效的API密钥访问尝试: {ApiKey}", apiKey[..Math.Min(8, apiKey.Length)] + "...");
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
            // 返回项目名到API密钥的映射，用于管理界面
            return _apiKeys.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
        }
    }
}