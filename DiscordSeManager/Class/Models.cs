// ---------------------------------------------
// Models.cs : DBエンティティ
// ---------------------------------------------
using System;

namespace DiscordSeManager
{
    public class SeItem
    {
        public string FileName { get; set; } = string.Empty; // PK
        public ulong MessageId { get; set; }
        public string FilePath { get; set; } = string.Empty; // ダウンロード先（作業用）
        public string Trigger { get; set; } = string.Empty;   // 対応文字列
        public int VolumePercent { get; set; }                // 1-100、投稿時指定。範囲外は50
            = 50;
        public int Priority { get; set; }                    // 1-99
            = 50;
        public bool IsEnglish { get; set; }
        public bool IsAdopted { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}