using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Steamworks;

namespace WallpaperViewer3
{
    public class WorkshopItem
    {
        public PublishedFileId_t FileId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public uint Subscriptions { get; set; }
        public float Score { get; set; }
        public string? PreviewUrl { get; set; }
        public string? Author { get; set; }
        public DateTime TimeCreated { get; set; }
        public DateTime TimeUpdated { get; set; }
    }

    public class SteamWorkshopQuery
    {
        private static readonly AppId_t WALLPAPER_ENGINE_APP_ID = new AppId_t(431960);
        private readonly string LOG_FILE_PATH = "LOG.JSON";
        private readonly string DEBUG_LOG_PATH = "debug_log.txt";
        private List<WorkshopItem> workshopItems = new List<WorkshopItem>();
        private CallResult<SteamUGCQueryCompleted_t>? onQueryCompleted;

        private void LogDebug(string message)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
            File.AppendAllText(DEBUG_LOG_PATH, logMessage);
            Console.WriteLine(message);
        }

        public async Task Initialize()
        {
            try
            {
                File.WriteAllText(DEBUG_LOG_PATH, ""); // 清空日志文件
                LogDebug("正在初始化Steam API...");
                
                if (!SteamAPI.Init())
                {
                    throw new Exception("Steam API initialization failed!");
                }
                LogDebug("Steam API 初始化成功");

                LogDebug($"当前Steam用户: {SteamFriends.GetPersonaName()}");
                LogDebug("开始查询Workshop内容...");

                await QueryTopWallpapers();
            }
            catch (Exception ex)
            {
                LogDebug($"发生错误: {ex}");
                throw;
            }
        }

        private async Task QueryTopWallpapers()
        {
            LogDebug("创建UGC查询...");
            var queryHandle = SteamUGC.CreateQueryAllUGCRequest(
                EUGCQuery.k_EUGCQuery_RankedByTrend,
                EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items_ReadyToUse,
                WALLPAPER_ENGINE_APP_ID,
                WALLPAPER_ENGINE_APP_ID,
                1  // First page
            );

            LogDebug($"查询句柄: {queryHandle}");

            if (queryHandle == UGCQueryHandle_t.Invalid)
            {
                LogDebug("创建查询句柄失败！");
                return;
            }

            SteamUGC.SetReturnTotalOnly(queryHandle, false);
            SteamUGC.SetReturnLongDescription(queryHandle, true);
            SteamUGC.SetReturnMetadata(queryHandle, true);
            SteamUGC.SetReturnChildren(queryHandle, false);
            SteamUGC.SetReturnAdditionalPreviews(queryHandle, false);
            SteamUGC.SetReturnKeyValueTags(queryHandle, true);
            SteamUGC.SetRankedByTrendDays(queryHandle, 7);
            SteamUGC.SetMatchAnyTag(queryHandle, true);

            LogDebug("发送UGC请求...");
            var apiCall = SteamUGC.SendQueryUGCRequest(queryHandle);
            LogDebug($"API调用句柄: {apiCall}");

            if (apiCall == SteamAPICall_t.Invalid)
            {
                LogDebug("API调用失败！");
                SteamUGC.ReleaseQueryUGCRequest(queryHandle);
                return;
            }

            var tcs = new TaskCompletionSource<bool>();
            onQueryCompleted = CallResult<SteamUGCQueryCompleted_t>.Create();
            
            LogDebug("设置回调...");
            onQueryCompleted.Set(apiCall, (result, failure) =>
            {
                LogDebug($"收到回调 - 失败: {failure}, 结果: {result.m_eResult}");
                
                if (!failure && result.m_eResult == EResult.k_EResultOK)
                {
                    LogDebug($"查询成功，返回结果数: {result.m_unNumResultsReturned}");
                    uint resultsToProcess = Math.Min(result.m_unNumResultsReturned, 50);
                    
                    for (uint i = 0; i < resultsToProcess; i++)
                    {
                        SteamUGCDetails_t details;
                        if (SteamUGC.GetQueryUGCResult(queryHandle, i, out details))
                        {
                            LogDebug($"处理第 {i + 1} 个结果: {details.m_rgchTitle}");
                            var steamId = new CSteamID(details.m_ulSteamIDOwner);
                            
                            var item = new WorkshopItem
                            {
                                FileId = details.m_nPublishedFileId,
                                Title = details.m_rgchTitle,
                                Description = details.m_rgchDescription,
                                Subscriptions = 0, // 暂时设为0，因为我们无法获取订阅数
                                Score = details.m_flScore,
                                PreviewUrl = details.m_rgchURL,
                                Author = SteamFriends.GetFriendPersonaName(steamId),
                                TimeCreated = DateTimeOffset.FromUnixTimeSeconds(details.m_rtimeCreated).DateTime,
                                TimeUpdated = DateTimeOffset.FromUnixTimeSeconds(details.m_rtimeUpdated).DateTime
                            };
                            workshopItems.Add(item);
                        }
                        else
                        {
                            LogDebug($"无法获取第 {i + 1} 个结果的详细信息");
                        }
                    }

                    LogDebug($"成功获取 {workshopItems.Count} 个壁纸信息");
                    
                    // 将结果写入JSON文件
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    string jsonString = JsonSerializer.Serialize(workshopItems, options);
                    File.WriteAllText(LOG_FILE_PATH, jsonString);
                    LogDebug($"数据已写入 {LOG_FILE_PATH}");
                }
                else
                {
                    LogDebug($"查询失败 - 失败状态: {failure}, 结果: {result.m_eResult}");
                }
                
                LogDebug("释放查询句柄");
                SteamUGC.ReleaseQueryUGCRequest(queryHandle);
                tcs.SetResult(true);
            });

            LogDebug("等待查询完成...");
            await tcs.Task;
            LogDebug("查询完成");
        }

        public void Shutdown()
        {
            LogDebug("关闭Steam API");
            SteamAPI.Shutdown();
        }
    }
}
