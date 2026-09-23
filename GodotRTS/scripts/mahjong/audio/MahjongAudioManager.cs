using Godot;
using System;
using System.Collections.Generic;

namespace GodotRTS.Mahjong.Audio
{
    public partial class MahjongAudioManager : Node
    {
        public static MahjongAudioManager? Instance { get; private set; }

        private AudioStreamPlayer sfxPlayer = null!;
        private Dictionary<string, AudioStreamWav> sfxCache = new Dictionary<string, AudioStreamWav>();

        public override void _Ready()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            sfxPlayer = new AudioStreamPlayer();
            AddChild(sfxPlayer);

            GenerateProceduralAudio();
        }

        private void GenerateProceduralAudio()
        {
            sfxCache["click"] = CreateToneWav(800, 0.05f, decay: 40.0f);
            sfxCache["deal"] = CreateNoiseWav(0.04f, 800, 1500);
            sfxCache["discard"] = CreateTileImpactWav();
            sfxCache["action"] = CreateActionPopWav();
            sfxCache["hu"] = CreateFanfareWav(true);
            sfxCache["win"] = CreateFanfareWav(true);
            sfxCache["lose"] = CreateFanfareWav(false);
        }

        public void PlaySfx(string name)
        {
            if (sfxCache.TryGetValue(name, out var wav))
            {
                AudioStreamPlayer tempPlayer = new AudioStreamPlayer();
                tempPlayer.Stream = wav;
                AddChild(tempPlayer);
                tempPlayer.Play();
                tempPlayer.Finished += () => tempPlayer.QueueFree();
            }
        }

        private AudioStreamWav CreateToneWav(float frequency, float duration, float decay = 10.0f)
        {
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * duration);
            byte[] data = new byte[totalSamples * 2];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * decay);
                float sample = Mathf.Sin(2.0f * Mathf.Pi * frequency * t) * env * 0.5f;
                short val = (short)(Mathf.Clamp(sample, -1.0f, 1.0f) * 32767);

                data[i * 2] = (byte)(val & 0xFF);
                data[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }

            AudioStreamWav wav = new AudioStreamWav();
            wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
            wav.MixRate = sampleRate;
            wav.Data = data;
            return wav;
        }

        private AudioStreamWav CreateNoiseWav(float duration, float minFreq, float maxFreq)
        {
            int sampleRate = 44100;
            int totalSamples = (int)(sampleRate * duration);
            byte[] data = new byte[totalSamples * 2];
            Random r = new Random();

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1.0f - (t / duration);
                float sample = ((float)r.NextDouble() * 2.0f - 1.0f) * env * 0.3f;
                short val = (short)(Mathf.Clamp(sample, -1.0f, 1.0f) * 32767);

                data[i * 2] = (byte)(val & 0xFF);
                data[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }

            AudioStreamWav wav = new AudioStreamWav();
            wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
            wav.MixRate = sampleRate;
            wav.Data = data;
            return wav;
        }

        private AudioStreamWav CreateTileImpactWav()
        {
            int sampleRate = 44100;
            float duration = 0.12f;
            int totalSamples = (int)(sampleRate * duration);
            byte[] data = new byte[totalSamples * 2];
            Random r = new Random();

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 35.0f);
                float tone = Mathf.Sin(2.0f * Mathf.Pi * 480.0f * t) * env;
                float click = ((float)r.NextDouble() * 2.0f - 1.0f) * Mathf.Exp(-t * 120.0f);

                float mix = (tone * 0.6f + click * 0.4f) * 0.7f;
                short val = (short)(Mathf.Clamp(mix, -1.0f, 1.0f) * 32767);

                data[i * 2] = (byte)(val & 0xFF);
                data[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }

            AudioStreamWav wav = new AudioStreamWav();
            wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
            wav.MixRate = sampleRate;
            wav.Data = data;
            return wav;
        }

        private AudioStreamWav CreateActionPopWav()
        {
            int sampleRate = 44100;
            float duration = 0.15f;
            int totalSamples = (int)(sampleRate * duration);
            byte[] data = new byte[totalSamples * 2];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = 600.0f + 400.0f * (t / duration);
                float env = Mathf.Sin(Mathf.Pi * (t / duration));
                float sample = Mathf.Sin(2.0f * Mathf.Pi * freq * t) * env * 0.5f;

                short val = (short)(Mathf.Clamp(sample, -1.0f, 1.0f) * 32767);

                data[i * 2] = (byte)(val & 0xFF);
                data[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }

            AudioStreamWav wav = new AudioStreamWav();
            wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
            wav.MixRate = sampleRate;
            wav.Data = data;
            return wav;
        }

        private AudioStreamWav CreateFanfareWav(bool isWin)
        {
            int sampleRate = 44100;
            float duration = 0.5f;
            int totalSamples = (int)(sampleRate * duration);
            byte[] data = new byte[totalSamples * 2];

            float[] freqs = isWin ? new float[] { 523.25f, 659.25f, 783.99f, 1046.50f } : new float[] { 400.0f, 350.0f, 300.0f, 250.0f };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                int noteIndex = Mathf.Clamp((int)(t / (duration / freqs.Length)), 0, freqs.Length - 1);
                float freq = freqs[noteIndex];

                float noteT = t % (duration / freqs.Length);
                float env = Mathf.Exp(-noteT * 12.0f);
                float sample = Mathf.Sin(2.0f * Mathf.Pi * freq * t) * env * 0.4f;

                short val = (short)(Mathf.Clamp(sample, -1.0f, 1.0f) * 32767);

                data[i * 2] = (byte)(val & 0xFF);
                data[i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }

            AudioStreamWav wav = new AudioStreamWav();
            wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
            wav.MixRate = sampleRate;
            wav.Data = data;
            return wav;
        }
    }
}
