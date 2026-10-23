# -*- coding: utf-8 -*-
"""
生成 44.1kHz 16-Bit 立体声 (Stereo) 90BPM 萧杀冷峻电影级 RTS 战场背景音乐 (Bleak Martial Battle Theme)
具有强烈的肃杀战地压迫感、重型战鼓、大提琴冷峻断奏 (D 弗里吉亚调式)、低沉长号/圆号怒吼、冷铁战砧与寒风战场氛围。
"""

import math
import struct
import wave
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

def generate_bleak_battle_bgm(output_path: str, duration_bars: int = 16):
    sample_rate = 44100
    bpm = 90.0
    beat_duration = 60.0 / bpm          # 每拍约 0.6667 秒
    bar_duration = beat_duration * 4.0  # 每小节 2.6667 秒
    total_duration = duration_bars * bar_duration
    num_frames = int(total_duration * sample_rate)

    print(f"[BGM] Synthesizing bleak battlefield BGM: {total_duration:.2f}s, sample_rate={sample_rate}Hz...")

    # 音符基频定义 (Hz)
    D1 = 36.71
    Eb1 = 38.89
    F1 = 43.65
    G1 = 49.00
    A1 = 55.00
    Bb1 = 58.27
    C2 = 65.41
    D2 = 73.42
    Eb2 = 77.78
    F2 = 87.31
    G2 = 98.00
    A2 = 110.00
    Bb2 = 116.54
    C3 = 130.81
    D3 = 146.83
    Eb3 = 155.56
    F3 = 174.61
    G3 = 196.00
    A3 = 220.00

    # 双声道采样缓冲区
    left_samples = [0.0] * num_frames
    right_samples = [0.0] * num_frames

    # 一阶低通状态
    lpf_l = 0.0
    lpf_r = 0.0
    # 截止频率 2200Hz
    cutoff = 2200.0
    alpha = (2.0 * math.pi * cutoff / sample_rate) / (1.0 + 2.0 * math.pi * cutoff / sample_rate)

    # 寒风噪声状态
    noise_seed = 1234567

    def next_noise():
        nonlocal noise_seed
        noise_seed = (noise_seed * 1103515245 + 12345) & 0x7FFFFFFF
        return (noise_seed / 0x7FFFFFFF) * 2.0 - 1.0

    wind_filter_l = 0.0
    wind_filter_r = 0.0

    for i in range(num_frames):
        t = i / sample_rate
        bar = int(t / bar_duration) % duration_bars
        t_in_bar = t % bar_duration
        current_beat = t_in_bar / beat_duration
        t_in_beat = t_in_bar % beat_duration

        # --- 1. 深海次重低音基石 (Sub-Bass & Contrabass Foundation) ---
        # 4 小节一段的和声进行: D 小调 -> Bb 大调 -> G 小调 -> A 弗里吉亚属和弦
        if bar < 4:
            root_freq = D1
        elif bar < 8:
            root_freq = Bb1 * 0.5  # 29.14 Hz
        elif bar < 12:
            root_freq = G1
        else:
            root_freq = A1 * 0.5   # 27.50 Hz

        # 慢速呼吸震颤
        tremolo = 1.0 + 0.08 * math.sin(2.0 * math.pi * 3.2 * t)
        sub_tone = (0.75 * math.sin(2.0 * math.pi * root_freq * t) +
                    0.25 * math.sin(4.0 * math.pi * root_freq * t)) * tremolo * 0.42

        # --- 2. 萧杀大提琴断奏 (Staccato Cello Ostinato) ---
        # 冰冷严峻的八分音符律动: D2 - D2 - Eb2 - D2 | C2 - D2 - Eb2 - D2
        eighth_note_idx = int(t_in_bar / (beat_duration * 0.5)) % 8
        t_in_eighth = t_in_bar % (beat_duration * 0.5)

        if bar < 4:
            ostinato_notes = [D2, D2, Eb2, D2, C2, D2, Eb2, D2]
        elif bar < 8:
            ostinato_notes = [Bb1, Bb1, C2, Bb1, A1, Bb1, C2, Bb1]
        elif bar < 12:
            ostinato_notes = [G1, G1, A1, G1, F1, G1, A1, G1]
        else:
            ostinato_notes = [A1, A1, Bb1, A1, G1, A1, Bb1, A1]

        cello_freq = ostinato_notes[eighth_note_idx]
        # 大提琴锯齿波近似 + 快速指数衰减 (弓弦撞击感)
        cello_env = math.exp(-t_in_eighth * 10.5)
        phase_c = (t * cello_freq) % 1.0
        saw_c = (2.0 * phase_c - 1.0)
        # 加入纯五度和微弱八度泛音
        cello_val = (saw_c * 0.55 + 0.35 * math.sin(2.0 * math.pi * cello_freq * 1.5 * t)) * cello_env * 0.36

        # --- 3. 铁血战鼓与冷砧打击 (War Drums & Cold Anvil) ---
        drum_l = 0.0
        drum_r = 0.0

        # 重型太鼓扫频 (第 1 拍和第 3 拍重击)
        is_kick_beat = (current_beat < 1.0) or (current_beat >= 2.0 and current_beat < 3.0)
        t_kick = t_in_beat if (current_beat < 1.0 or (current_beat >= 2.0 and current_beat < 3.0)) else 999.0
        if t_kick < 0.8:
            # 68Hz 极低频下潜至 26Hz
            kick_freq = 26.0 + 46.0 * math.exp(-t_kick * 28.0)
            kick_env = math.exp(-t_kick * 8.5)
            kick_tone = math.sin(2.0 * math.pi * kick_freq * t_kick) * kick_env * 0.58
            drum_l += kick_tone
            drum_r += kick_tone

        # 滚奏军用暗鼓 (十六分音符幽灵音符 Ghost Notes，第 2 拍和第 4 拍加重)
        sixteenth_idx = int(t_in_bar / (beat_duration * 0.25)) % 16
        t_sixteenth = t_in_bar % (beat_duration * 0.25)
        # 在第 4, 12 十六分音符（即第 2 拍和第 4 拍）力度增强
        snare_accent = 0.38 if (sixteenth_idx in (4, 12)) else 0.14
        snare_env = math.exp(-t_sixteenth * 32.0)
        snare_noise = next_noise() * snare_accent * snare_env
        drum_l += snare_noise * 0.95
        drum_r += snare_noise * 1.05

        # 冷铁战砧重击 (Metallic Anvil Accent) - 每 4 小节首拍第 1 拍响彻全场
        if bar % 4 == 0 and current_beat < 0.5:
            t_anvil = t_in_bar
            anvil_env = math.exp(-t_anvil * 7.0)
            # 金属不谐和高频震荡 (840Hz + 2150Hz + 3380Hz)
            anvil_tone = (0.5 * math.sin(2.0 * math.pi * 840.0 * t_anvil) +
                          0.3 * math.sin(2.0 * math.pi * 2150.0 * t_anvil) +
                          0.2 * math.sin(2.0 * math.pi * 3380.0 * t_anvil)) * anvil_env * 0.28
            drum_l += anvil_tone * 0.8
            drum_r += anvil_tone * 1.2

        # --- 4. 肃杀低沉铜管怒吼 (Grim Brass Swells) ---
        brass_l = 0.0
        brass_r = 0.0
        # 在第 2, 3, 6, 7, 10, 11, 14, 15 小节的后半段蓄势涌起
        if bar in (2, 3, 6, 7, 10, 11, 14, 15):
            t_swell = t_in_bar / bar_duration  # 0.0 -> 1.0
            brass_env = math.sin(math.pi * t_swell) * 0.32
            # 和弦音
            if bar < 4:
                b1, b2, b3 = D2, A2, D3
            elif bar < 8:
                b1, b2, b3 = Bb1, F2, Bb2
            elif bar < 12:
                b1, b2, b3 = G1, D2, G2
            else:
                b1, b2, b3 = A1, Eb2, A2  # 弗里吉亚增四/减五特征音，杀伐刺骨

            # 铜管丰富谐波
            b_val = (0.4 * math.sin(2.0 * math.pi * b1 * t) +
                     0.35 * math.sin(2.0 * math.pi * b2 * t) +
                     0.25 * math.sin(2.0 * math.pi * b3 * t)) * brass_env
            brass_l += b_val * 1.15
            brass_r += b_val * 0.85

        # --- 5. 战场寒风风鸣氛围 (Desolate Wind Ambience) ---
        # 慢速低频带通白噪声
        raw_noise = next_noise() * 0.09
        wind_filter_l += 0.04 * (raw_noise - wind_filter_l)
        wind_filter_r += 0.038 * (raw_noise - wind_filter_r)
        wind_l = wind_filter_l * (1.0 + 0.3 * math.sin(2.0 * math.pi * 0.15 * t))
        wind_r = wind_filter_r * (1.0 + 0.3 * math.cos(2.0 * math.pi * 0.12 * t))

        # --- 6. 双声道立体声空间声场混音 ---
        # 大提琴偏左，铜管偏右，鼓居中，寒风环绕
        raw_l = sub_tone + (cello_val * 1.15) + drum_l + brass_l + wind_l
        raw_r = sub_tone + (cello_val * 0.85) + drum_r + brass_r + wind_r

        # 低通滤波消除高频数字毛刺
        lpf_l += alpha * (raw_l - lpf_l)
        lpf_r += alpha * (raw_r - lpf_r)

        # 循环首尾平滑交叉淡入淡出 (0.3s)
        fade_time = 0.3
        if t < fade_time:
            fade = t / fade_time
            lpf_l *= fade
            lpf_r *= fade
        elif (total_duration - t) < fade_time:
            fade = (total_duration - t) / fade_time
            lpf_l *= fade
            lpf_r *= fade

        left_samples[i] = max(-1.0, min(1.0, lpf_l))
        right_samples[i] = max(-1.0, min(1.0, lpf_r))

    # 输出 16-Bit PCM WAV 文件
    with wave.open(output_path, "wb") as wf:
        wf.setnchannels(2)      # 双声道立体声
        wf.setsampwidth(2)      # 16 位 (2 字节)
        wf.setframerate(sample_rate)

        raw_bytes = bytearray()
        for i in range(num_frames):
            s_l = int(left_samples[i] * 32767.0)
            s_r = int(right_samples[i] * 32767.0)
            raw_bytes.extend(struct.pack("<hh", s_l, s_r))
        wf.writeframes(raw_bytes)

    size_kb = os.path.getsize(output_path) / 1024.0
    print(f"[BGM] Successfully generated bleak battlefield BGM: {output_path} ({size_kb:.1f} KB)")


def num_bars_desc(bars: int) -> str:
    return f"{bars}小节 90BPM 铁血行军速"


if __name__ == "__main__":
    out_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "assets", "audio")
    os.makedirs(out_dir, exist_ok=True)
    target_wav = os.path.join(out_dir, "bgm.wav")
    generate_bleak_battle_bgm(target_wav, duration_bars=16)
