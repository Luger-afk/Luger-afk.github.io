using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiscordSeManager
{
    public partial class Form1 : Form
    {
        private readonly AppConfig _config;
        private readonly Repository _repo;
        private readonly AudioService _audio = new AudioService();
        private DiscordService _discord;
        private BindingList<SeItem> _binding = new BindingList<SeItem>();

        public Form1()
        {
            InitializeComponent();

            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiscordSeManager");
            Directory.CreateDirectory(baseDir);

            _config = new AppConfig(Path.Combine(baseDir, "config.ini"));
            _repo = new Repository(Path.Combine(baseDir, "db.sqlite3"));

            InitGrid();
            LoadGrid();
        }

        private void InitGrid()
        {
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.Columns.Clear();

            // 再生/停止ボタン
            var colPlay = new DataGridViewButtonColumn { HeaderText = "再生/停止", Text = "▶", UseColumnTextForButtonValue = true, Width = 80 };
            dataGridView1.Columns.Add(colPlay);

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "優先度", DataPropertyName = nameof(SeItem.Priority), Width = 60 });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "対応文字列", DataPropertyName = nameof(SeItem.Trigger), Width = 140 });
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ファイル名", DataPropertyName = nameof(SeItem.FileName), Width = 200, ReadOnly = true });

            var colEng = new DataGridViewCheckBoxColumn { HeaderText = "英単語", DataPropertyName = nameof(SeItem.IsEnglish), Width = 70 };
            dataGridView1.Columns.Add(colEng);

            // 音量倍率スライダー(1-100) + 表示列
            var colVol = new DataGridViewTextBoxColumn { HeaderText = "音量(1-100)", DataPropertyName = nameof(SeItem.VolumePercent), Width = 90 };
            dataGridView1.Columns.Add(colVol);

            var colAdopt = new DataGridViewCheckBoxColumn { HeaderText = "採用", DataPropertyName = nameof(SeItem.IsAdopted), Width = 50 };
            dataGridView1.Columns.Add(colAdopt);

            dataGridView1.DataSource = _binding;
            dataGridView1.CellContentClick += DataGridView1_CellContentClick;
            dataGridView1.CellEndEdit += DataGridView1_CellEndEdit;
        }

        private void LoadGrid()
        {
            _binding = new BindingList<SeItem>(_repo.GetAll());
            dataGridView1.DataSource = _binding;
        }

        private void DataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _binding.Count) return;
            var item = _binding[e.RowIndex];
            // 範囲正規化
            if (item.VolumePercent < 1 || item.VolumePercent > 100) item.VolumePercent = 50;
            if (item.Priority < 1 || item.Priority > 99) item.Priority = ClampEx.Clamp(item.Priority, 1, 99);
            _repo.Update(item);
        }

        private void DataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex == 0) // 再生/停止
            {
                var it = _binding[e.RowIndex];
                try
                {
                    // すでに再生している場合は停止 → 直ちに再生
                    _audio.Stop();
                    _audio.Play(it.FilePath, it.VolumePercent / 100f);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"再生に失敗しました: {ex.Message}");
                }
            }
        }

        private async void btnFetch_Click(object sender, EventArgs e)
        {
            try
            {
                btnFetch.Enabled = false;

                var token = _config.Get("Discord", "BotToken") ?? throw new Exception("設定でBotTokenを指定してください。");
                var chStr = _config.Get("Discord", "ChannelId") ?? throw new Exception("設定でChannelIdを指定してください。");
                if (!ulong.TryParse(chStr, out var channelId)) throw new Exception("ChannelIdが不正です。");

                _discord = new DiscordService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiscordSeManager", "Downloads"));
                await _discord.LoginAsync(token);

                ulong? lastId = null;
                var last = _repo.GetMeta("LastFetchedMessageId");
                if (ulong.TryParse(last, out var lid)) lastId = lid;

                var items = await _discord.FetchNewSeAsync(channelId, lastId);
                foreach (var it in items)
                {
                    _repo.UpsertSe(it); // ファイル名が既にあればスキップ
                }
                if (items.Count > 0)
                {
                    var maxId = items.Max(i => i.MessageId);
                    _repo.SetMeta("LastFetchedMessageId", maxId.ToString());
                }

                LoadGrid();
                MessageBox.Show(items.Count == 0 ? "新しいSEはありません。" : $"{items.Count}件のSEを取り込みました。");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"取得に失敗しました: {ex.Message}");
            }
            finally
            {
                btnFetch.Enabled = true;
            }
        }

        private void btnSettings_Click(object sender, EventArgs e)
        {
            using (var f = new SettingsForm(_config))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    _config.Save();
                }
            } ;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            try
            {
                btnAdd.Enabled = false;

                var outRoot = _config.Get("Output", "RootFolder");
                if (string.IsNullOrWhiteSpace(outRoot)) throw new Exception("出力先フォルダを設定してください。");
                var soundDir = Path.Combine(outRoot, "Sound");
                Directory.CreateDirectory(soundDir);

                // 採用アイテム
                var selected = _binding.Where(x => x.IsAdopted).ToList();
                foreach (var it in selected)
                {
                    // 音量焼き込み出力
                    var baseName = Path.GetFileNameWithoutExtension(it.FileName);
                    var dstNoExt = Path.Combine(soundDir, baseName);
                    var written = _audio.RenderWithVolume(it.FilePath, it.VolumePercent, dstNoExt);
                    // 出力ファイル名は元ファイル名のベース名を使用（拡張子は.wavに統一）
                }

                // ReplaceTag.dic（タブ区切り）を別フォルダ（=出力先ルート）に作成
                var dicPath = Path.Combine(outRoot, "ReplaceTag.dic");
                using (var sw = new StreamWriter(dicPath, true))
                {
                    foreach (var it in selected)
                    {
                        var baseName = Path.GetFileNameWithoutExtension(it.FileName) + ".wav"; // 出力拡張子に合わせる
                        var yn = it.IsEnglish ? "Y" : "N";
                        // [優先度]\t[対応文字列]\t[ファイル名]\t[Y/N]
                        sw.WriteLine($"{it.Priority}\t{yn}\t{it.Trigger}\t(Sound {baseName})");
                    }
                }

                // 画面表示されているSEを全削除（DB＆ファイル）
                _repo.DeleteAllAndFiles();
                LoadGrid();

                MessageBox.Show("SEの出力が完了しました。");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SE追加処理でエラー: {ex.Message}");
            }
            finally
            {
                btnAdd.Enabled = true;
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("一覧のSEを全て削除します。よろしいですか？", "確認", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                try
                {
                    _audio.Stop();
                    _repo.DeleteAllAndFiles();
                    LoadGrid();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"削除に失敗しました: {ex.Message}");
                }
            }
        }
    }
}
