// ---------------------------------------------
// DiscordService.cs : Discordからの取得
// ---------------------------------------------
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DiscordSeManager
{
    public class DiscordService : IDisposable
    {
        private readonly DiscordSocketClient _client;
        private readonly HttpClient _http = new HttpClient();
        private readonly string _downloadDir;

        public DiscordService(string downloadDir)
        {
            _downloadDir = downloadDir;
            Directory.CreateDirectory(downloadDir);

            var conf = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent,
                MessageCacheSize = 0,
                AlwaysDownloadUsers = false,
                LogLevel = LogSeverity.Info
            };
            _client = new DiscordSocketClient(conf);
        }

        public async Task LoginAsync(string token)
        {
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
            // 接続待機（軽い待機）
            await Task.Delay(2000);
        }

        public async Task<List<SeItem>> FetchNewSeAsync(ulong channelId, ulong? lastMessageIdOrNull)
        {
            var channel = _client.GetChannel(channelId) as IMessageChannel;
            if (channel == null) throw new Exception("チャンネルが見つかりませんでした。");

            var collected = new List<IMessage>();
            var fromId = lastMessageIdOrNull;

            // Discord.Netはレートリミットを内部処理するが、追加で小休止
            for (int page = 0; page < 50; page++)
            {
                IEnumerable<IMessage> pageMsgs;
                if (fromId.HasValue)
                    pageMsgs = await channel.GetMessagesAsync(fromId.Value, Direction.After, 100).FlattenAsync();
                else
                    pageMsgs = await channel.GetMessagesAsync(limit: 100).FlattenAsync();

                var list = pageMsgs.OrderBy(m => m.Id).ToList();
                if (list.Count == 0) break;

                collected.AddRange(list);
                fromId = list.Last().Id; // 次はさらに後ろ

                await Task.Delay(350); // レート緩和
            }

            var results = new List<SeItem>();
            foreach (var msg in collected)
            {
                if (msg.Attachments == null || msg.Attachments.Count == 0) continue;

                // 投稿本文から トリガー文字列 と 音量% を拾う簡易パーサ（例: trigger:XXX volume:80 priority:10 english:Y）
                // 指定がない／範囲外はデフォルト適用
                var trigger = ParseTag(msg.Content, "trigger") ?? string.Empty;
                var vol = ParseInt(msg.Content, "volume");
                int volume = (vol >= 1 && vol <= 100) ? vol.Value : 50;
                var pri = ParseInt(msg.Content, "priority");
                int priority = (pri >= 1 && pri <= 99) ? pri.Value : 50;
                var eng = ParseTag(msg.Content, "english");
                bool isEng = (eng?.Equals("y", StringComparison.OrdinalIgnoreCase) ?? false);

                foreach (var att in msg.Attachments)
                {
                    var ext = Path.GetExtension(att.Filename).ToLowerInvariant();
                    if (ext != ".mp3" && ext != ".wav") continue;

                    // ダウンロードサイズ制限：Discord CDNのサイズが取得できる場合はチェック（ContentLength）
                    if (att.Size > 50 * 1024 * 1024) // 50MB超はスキップ（要件: 制限に引っかかる場合はダウンロードしない）
                        continue;

                    var safeName = $"{att.Title}{ext}"; // ファイル名主キー。重複はDB側でスキップ
                    var localPath = Path.Combine(_downloadDir, safeName);

                    trigger = string.IsNullOrEmpty(trigger) ? att.Title : trigger;

                    // 既に存在 → 以降の処理（DB UpsertはDO NOTHING）
                    try
                    {
                        using (var resp = await _http.GetAsync(att.Url))
                        {
                            resp.EnsureSuccessStatusCode();
                            using (var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                            {
                                await resp.Content.CopyToAsync(fs);
                            } ;
                        } ;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"ダウンロード失敗: {att.Url} : {ex.Message}");
                    }

                    results.Add(new SeItem
                    {
                        FileName = safeName,
                        MessageId = msg.Id,
                        FilePath = localPath,
                        Trigger = trigger,
                        VolumePercent = volume,
                        Priority = priority,
                        IsEnglish = isEng,
                        IsAdopted = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            return results;
        }

        private static string ParseTag(string content, string key)
        {
            // 例: key:value / key= value 形式をざっくり抽出
            var idx = content.IndexOf(key + ":", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) idx = content.IndexOf(key + "=", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var rest = content.Substring(idx + key.Length + 1).Trim();
            var end = rest.IndexOfAny(new char[] { '\\', '\r', ' ', '\t' });
            if (end >= 0) rest = rest.Substring(0,end);
            return rest.Trim();
        }
        private static int? ParseInt(string content, string key)
        {
            var t = ParseTag(content, key);
            if (int.TryParse(t, out var v)) return v;
            return null;
        }

        public void Dispose()
        {
            _http.Dispose();
            _client?.Dispose();
        }
    }
}