using System.Text;
using System.Text.Json;
using NotifyHubAPI.Models;

namespace NotifyHubAPI.Tests
{
    /// <summary>
    /// API测试客户端 - 用于验证部署后的API功能
    /// </summary>
    public class ApiTestClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public ApiTestClient(string baseUrl, string apiKey)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }

        /// <summary>
        /// 测试健康检查端点
        /// </summary>
        public async Task<bool> TestHealthCheckAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/health");
                Console.WriteLine($"健康检查: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"响应: {content}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"健康检查失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 测试API信息端点
        /// </summary>
        public async Task<bool> TestInfoEndpointAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/info");
                Console.WriteLine($"信息端点: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"服务信息: {content}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"信息端点测试失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 测试发送邮件功能
        /// </summary>
        public async Task<string?> TestSendEmailAsync()
        {
            try
            {
                var emailRequest = new EmailRequest
                {
                    To = new List<string> { "test@example.com" },
                    Subject = "API测试邮件",
                    Body = "这是一封来自NotifyHubAPI的测试邮件，发送时间: " + DateTime.Now,
                    Category = "API_TEST",
                    IsHtml = false
                };

                var json = JsonSerializer.Serialize(emailRequest, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/email/send", content);

                Console.WriteLine($"发送邮件: {response.StatusCode}");

                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"响应: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<EmailSendResponse>>(responseContent,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                    return apiResponse?.Data?.EmailId;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送邮件测试失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 测试查询邮件状态
        /// </summary>
        public async Task<bool> TestEmailStatusAsync(string emailId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/email/status/{emailId}");
                Console.WriteLine($"查询邮件状态: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"邮件状态: {content}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"查询邮件状态失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 测试邮件历史查询
        /// </summary>
        public async Task<bool> TestEmailHistoryAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/email/history?pageSize=5");
                Console.WriteLine($"查询邮件历史: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"邮件历史: {content}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"查询邮件历史失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 测试无效API Key
        /// </summary>
        public async Task<bool> TestInvalidApiKeyAsync()
        {
            try
            {
                using var testClient = new HttpClient();
                testClient.DefaultRequestHeaders.Add("X-API-Key", "invalid-key");

                var response = await testClient.GetAsync($"{_baseUrl}/api/email/history");
                Console.WriteLine($"无效API Key测试: {response.StatusCode}");

                // 应该返回401 Unauthorized
                return response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"无效API Key测试失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 运行完整测试套件
        /// </summary>
        public async Task RunFullTestSuiteAsync()
        {
            Console.WriteLine("=== NotifyHubAPI 功能测试 ===");
            Console.WriteLine($"测试目标: {_baseUrl}");
            Console.WriteLine($"API Key: {_apiKey[..8]}...");
            Console.WriteLine();

            var results = new Dictionary<string, bool>();

            // 1. 健康检查
            Console.WriteLine("1. 测试健康检查...");
            results["健康检查"] = await TestHealthCheckAsync();
            Console.WriteLine();

            // 2. 信息端点
            Console.WriteLine("2. 测试信息端点...");
            results["信息端点"] = await TestInfoEndpointAsync();
            Console.WriteLine();

            // 3. 无效API Key
            Console.WriteLine("3. 测试API Key验证...");
            results["API Key验证"] = await TestInvalidApiKeyAsync();
            Console.WriteLine();

            // 4. 发送邮件
            Console.WriteLine("4. 测试发送邮件...");
            var emailId = await TestSendEmailAsync();
            results["发送邮件"] = !string.IsNullOrEmpty(emailId);
            Console.WriteLine();

            // 5. 查询邮件状态
            if (!string.IsNullOrEmpty(emailId))
            {
                Console.WriteLine("5. 测试查询邮件状态...");
                results["查询邮件状态"] = await TestEmailStatusAsync(emailId);
                Console.WriteLine();
            }

            // 6. 邮件历史
            Console.WriteLine("6. 测试邮件历史查询...");
            results["邮件历史查询"] = await TestEmailHistoryAsync();
            Console.WriteLine();

            // 输出测试结果
            Console.WriteLine("=== 测试结果汇总 ===");
            foreach (var result in results)
            {
                var status = result.Value ? "✓ 通过" : "✗ 失败";
                Console.WriteLine($"{result.Key}: {status}");
            }

            var passedCount = results.Values.Count(x => x);
            var totalCount = results.Count;
            Console.WriteLine($"\n总计: {passedCount}/{totalCount} 项测试通过");
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// 测试程序入口
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // 配置测试参数
            var baseUrl = args.Length > 0 ? args[0] : "https://localhost:7000";
            var apiKey = args.Length > 1 ? args[1] : "default-api-key-2024";

            var testClient = new ApiTestClient(baseUrl, apiKey);

            try
            {
                await testClient.RunFullTestSuiteAsync();
            }
            finally
            {
                testClient.Dispose();
            }

            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }
    }
}