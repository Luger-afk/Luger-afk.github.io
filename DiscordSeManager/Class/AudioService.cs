// ---------------------------------------------
// AudioService.cs : 再生＆音量変換（保存）
// ---------------------------------------------
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;

namespace DiscordSeManager
{
    public class AudioService : IDisposable
    {
        private IWavePlayer _player;
        private AudioFileReader _reader;

        public void Play(string path, float volume01)
        {
            Stop();
            _reader = new AudioFileReader(path);
            _reader.Volume = ClampEx.Clamp(volume01, 0f, 1f);
            _player = new WaveOutEvent();
            _player.Init(_reader);
            _player.Play();
        }

        public void Stop()
        {
            try { _player?.Stop(); } catch { }
            _reader?.Dispose();
            _player?.Dispose();
            _reader = null;
            _player = null;
        }

        // SE追加時: 音量倍率を波形に焼き込んで保存
        // 仕様: 元形式維持が理想だが、エンコード依存回避のため 16bit PCM WAV に統一出力。
        public string RenderWithVolume(string srcPath, int volumePercent, string dstPathWithoutExt)
        {
            var gain = ClampEx.Clamp(volumePercent, 1, 100) / 50f; // 50で等倍、100で2倍、1で0.02倍
            var outPath = dstPathWithoutExt + ".wav"; // 拡張子は.wav

            using (var reader = new AudioFileReader(srcPath))
            {
                var volProvider = new VolumeSampleProvider(reader) { Volume = gain };
                WaveFileWriter.CreateWaveFile16(outPath, volProvider);
                return outPath;
            } ;
        }

        public void Dispose() => Stop();
    }
}