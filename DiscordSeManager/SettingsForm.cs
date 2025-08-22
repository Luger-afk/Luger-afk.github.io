using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiscordSeManager
{
    public partial class SettingsForm : Form
    {
        private readonly AppConfig _config;
        public SettingsForm(AppConfig config)
        {
            _config = config;
            InitializeComponent();
            txtToken.Text = _config.Get("Discord", "BotToken", "");
            txtChannel.Text = _config.Get("Discord", "ChannelId", "");
            txtMessage.Text = _config.Get("Discord", "LastMessageID", "");
            txtOutput.Text = _config.Get("Output", "RootFolder", "");
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            _config.Set("Discord", "BotToken", txtToken.Text.Trim());
            _config.Set("Discord", "ChannelId", txtChannel.Text.Trim());
            _config.Set("Discord", "LastMessageId", txtMessage.Text.Trim());
            _config.Set("Output", "RootFolder", txtOutput.Text.Trim());
            DialogResult = DialogResult.OK;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var f = new FolderBrowserDialog())
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    txtOutput.Text = f.SelectedPath;
                }
            } ;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
