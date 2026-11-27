using System;
using System.Threading;
using UnityEngine;

namespace ModTemplates
{
    /// <summary>
    /// 零第三方依赖的 RIFF PCM16 WAV 解码器（支持后台线程解码 + 主线程构造 AudioClip）。
    /// 来源蒸馏：PortraitStudio.AuthorKit (AuthorVoice / VoiceWave)。
    /// 规范：RIFF PCM16、1~2 声道、8~96 kHz、单条 <= 120 秒、解码上限 32 MiB。
    /// </summary>
    internal static class WavPcmDecoder
    {
        internal sealed class PcmData
        {
            public float[] Samples;
            public int Channels;
            public int SampleRate;
        }

        private static int U16(byte[] b, int p) => b[p] | (b[p + 1] << 8);
        private static uint U32(byte[] b, int p) => (uint)(b[p] | (b[p + 1] << 8) | (b[p + 2] << 16) | (b[p + 3] << 24));
        private static bool Tag(byte[] b, int p, string s) => b[p] == s[0] && b[p + 1] == s[1] && b[p + 2] == s[2] && b[p + 3] == s[3];

        public static PcmData Decode(byte[] bytes, CancellationToken cancel = default)
        {
            if (bytes == null || bytes.Length < 44 || bytes.Length > 32 * 1024 * 1024
                || !Tag(bytes, 0, "RIFF") || !Tag(bytes, 8, "WAVE"))
                throw new InvalidOperationException("Invalid or oversized WAV");

            int channels = 0, rate = 0, dataOffset = -1, dataSize = 0;
            for (int p = 12; p + 8 <= bytes.Length;)
            {
                long size = U32(bytes, p + 4);
                long end = p + 8L + size;
                if (end > bytes.Length)
                    throw new InvalidOperationException("Truncated WAV chunk");

                if (Tag(bytes, p, "fmt "))
                {
                    if (channels != 0 || size < 16 || U16(bytes, p + 8) != 1 || U16(bytes, p + 22) != 16)
                        throw new InvalidOperationException("Expected PCM16 WAV");
                    channels = U16(bytes, p + 10);
                    rate = (int)U32(bytes, p + 12);
                    if ((channels != 1 && channels != 2) || rate < 8000 || rate > 96000)
                        throw new InvalidOperationException("Unsupported WAV channels or sample rate");
                }
                else if (Tag(bytes, p, "data"))
                {
                    if (dataOffset >= 0) throw new InvalidOperationException("Duplicate WAV data chunk");
                    dataOffset = p + 8;
                    dataSize = (int)size;
                }

                p = checked((int)(end + (size & 1)));
            }

            if (channels <= 0 || dataOffset < 0 || dataSize <= 0 || dataSize % (channels * 2) != 0)
                throw new InvalidOperationException("Missing or malformed WAV data chunk");
            if (dataSize / (channels * 2L) > rate * 120L || dataSize > 16 * 1024 * 1024)
                throw new InvalidOperationException("WAV duration or decoded size exceeds safety limit");

            var samples = new float[dataSize / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                if ((i & 4095) == 0) cancel.ThrowIfCancellationRequested();
                samples[i] = (short)U16(bytes, dataOffset + i * 2) / 32768f;
            }
            return new PcmData { Samples = samples, Channels = channels, SampleRate = rate };
        }

        /// <summary>在 Unity 主线程调用，把解码好的 PCM 转为 AudioClip。</summary>
        public static AudioClip CreateClip(string clipName, PcmData pcm)
        {
            var clip = AudioClip.Create(clipName, pcm.Samples.Length / pcm.Channels, pcm.Channels, pcm.SampleRate, false);
            clip.SetData(pcm.Samples, 0);
            return clip;
        }
    }
}
