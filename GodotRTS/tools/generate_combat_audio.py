"""
Military Combat Audio Foley Generator
Generates studio-grade 44.1kHz 16-bit PCM WAV audio assets for Godot RTS combat feedback.
"""

import os
import struct
import wave
import numpy as np

OUTPUT_DIR = r"e:\code\c++\UnityRTS\GodotRTS\assets\audio\combat"
SAMPLE_RATE = 44100


def save_wav(filename: str, samples: np.ndarray):
    """Normalize, soft-clip, and save 16-bit PCM WAV."""
    filepath = os.path.join(OUTPUT_DIR, filename)
    
    # Soft saturation (analog warmth / punch)
    peak = np.max(np.abs(samples))
    if peak > 0.001:
        samples = samples / peak * 1.35
    samples = np.tanh(samples)
    
    # Normalize to 0.96 peak
    samples = samples / np.max(np.abs(samples)) * 0.96
    
    int_samples = (samples * 32767).astype(np.int16)
    with wave.open(filepath, "w") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(int_samples.tobytes())
    print(f"Saved: {filename} ({len(samples)} samples, {len(samples)/SAMPLE_RATE:.2f}s)")


def env_exp(t, attack, decay):
    att = np.clip(t / max(attack, 0.001), 0.0, 1.0)
    dec = np.exp(-t / max(decay, 0.001))
    return att * dec


def filter_lowpass(sig, cutoff_hz):
    dt = 1.0 / SAMPLE_RATE
    rc = 1.0 / (2.0 * np.pi * cutoff_hz)
    alpha = dt / (rc + dt)
    out = np.empty_like(sig)
    prev = 0.0
    for i in range(len(sig)):
        prev += alpha * (sig[i] - prev)
        out[i] = prev
    return out


def filter_highpass(sig, cutoff_hz):
    lp = filter_lowpass(sig, cutoff_hz)
    return sig - lp


def filter_bandpass(sig, low_hz, high_hz):
    lp = filter_lowpass(sig, high_hz)
    return filter_highpass(lp, low_hz)


def gen_bomb_explosion():
    dur = 2.4
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    # 1. 42Hz Heavy Sub-Bass Core with exponential pitch glide
    pitch = 42.0 * np.exp(-t * 2.2) + 26.0
    phase = 2.0 * np.pi * np.cumsum(pitch) / SAMPLE_RATE
    sub = np.sin(phase) * env_exp(t, 0.008, 0.75) * 1.5
    
    # 2. Explosive transient crack (first 25ms shockwave)
    noise_init = np.random.uniform(-1, 1, len(t))
    transient = filter_bandpass(noise_init, 200, 6000) * env_exp(t, 0.001, 0.035) * 2.2
    
    # 3. Rolling firestorm rumble
    noise_body = np.random.uniform(-1, 1, len(t))
    rumble = filter_lowpass(noise_body, 280) * env_exp(t, 0.03, 0.95) * 1.2
    
    # 4. Secondary ground debris shower
    debris = filter_bandpass(noise_body, 600, 2400) * env_exp(t, 0.12, 0.45) * 0.4
    
    sig = sub + transient + rumble + debris
    save_wav("bomb_explosion.wav", sig)


def gen_heavy_explosion():
    dur = 1.8
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    pitch = 56.0 * np.exp(-t * 3.0) + 32.0
    sub = np.sin(2.0 * np.pi * np.cumsum(pitch) / SAMPLE_RATE) * env_exp(t, 0.005, 0.55) * 1.3
    
    noise = np.random.uniform(-1, 1, len(t))
    transient = filter_bandpass(noise, 150, 4500) * env_exp(t, 0.002, 0.04) * 1.8
    body = filter_lowpass(noise, 380) * env_exp(t, 0.02, 0.65) * 1.1
    
    sig = sub + transient + body
    save_wav("heavy_explosion.wav", sig)


def gen_medium_explosion():
    dur = 1.2
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    pitch = 75.0 * np.exp(-t * 4.5) + 40.0
    sub = np.sin(2.0 * np.pi * np.cumsum(pitch) / SAMPLE_RATE) * env_exp(t, 0.003, 0.35) * 1.1
    
    noise = np.random.uniform(-1, 1, len(t))
    crack = filter_bandpass(noise, 300, 6500) * env_exp(t, 0.001, 0.025) * 2.0
    body = filter_lowpass(noise, 480) * env_exp(t, 0.015, 0.40) * 0.9
    
    sig = sub + crack + body
    save_wav("medium_explosion.wav", sig)


def gen_water_spout_explosion():
    dur = 2.0
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    # Muffled underwater detonation thud
    pitch = 50.0 * np.exp(-t * 2.5) + 30.0
    sub = np.sin(2.0 * np.pi * np.cumsum(pitch) / SAMPLE_RATE) * env_exp(t, 0.01, 0.45) * 1.4
    
    # Huge cascading water splash & foam rush
    noise = np.random.uniform(-1, 1, len(t))
    water = filter_bandpass(noise, 400, 3200) * env_exp(t, 0.08, 0.95) * 1.2
    hiss = filter_highpass(noise, 2200) * env_exp(t, 0.15, 0.65) * 0.5
    
    sig = sub + water + hiss
    save_wav("water_spout_explosion.wav", sig)


def gen_bomb_whistle():
    dur = 1.25
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    # Stuka/B-2 descending dive siren whistle: 920Hz down to 310Hz with aero vibrato
    base_freq = 920.0 * (1.0 - (t / dur) ** 1.35 * 0.66)
    vibrato = 1.0 + 0.035 * np.sin(2.0 * np.pi * 18.0 * t)
    phase = 2.0 * np.pi * np.cumsum(base_freq * vibrato) / SAMPLE_RATE
    
    # Rich harmonics
    siren = np.sin(phase) * 0.75 + np.sin(phase * 2.0) * 0.35 + np.sin(phase * 3.0) * 0.15
    
    # Wind turbulence
    noise = filter_bandpass(np.random.uniform(-1, 1, len(t)), 400, 1800) * 0.28
    
    # Build up volume towards the ground
    env = (t / dur) ** 1.2 * (1.0 - np.exp(-(dur - t) * 8.0))
    
    sig = (siren + noise) * env
    save_wav("bomb_whistle.wav", sig)


def gen_bomb_release():
    dur = 0.4
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    # Mechanical pneumatic latch release clack + air puff
    click = np.sin(2.0 * np.pi * 580.0 * t) * env_exp(t, 0.002, 0.02) * 1.5
    clack = np.sin(2.0 * np.pi * 220.0 * t) * env_exp(t, 0.005, 0.04) * 1.2
    
    noise = np.random.uniform(-1, 1, len(t))
    puff = filter_bandpass(noise, 250, 1800) * env_exp(t, 0.015, 0.18) * 0.9
    
    sig = click + clack + puff
    save_wav("bomb_release.wav", sig)


def gen_cannon_fire():
    dur = 0.95
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    # Heavy 120mm tank cannon breach snap + low punch
    sub = np.sin(2.0 * np.pi * 68.0 * t) * env_exp(t, 0.004, 0.28) * 1.4
    
    noise = np.random.uniform(-1, 1, len(t))
    snap = filter_bandpass(noise, 400, 7500) * env_exp(t, 0.001, 0.022) * 2.4
    barrel_res = np.sin(2.0 * np.pi * 145.0 * t) * env_exp(t, 0.008, 0.14) * 0.8
    rumble = filter_lowpass(noise, 320) * env_exp(t, 0.02, 0.45) * 1.0
    
    sig = sub + snap + barrel_res + rumble
    save_wav("cannon_fire.wav", sig)


def gen_heavy_artillery_fire():
    dur = 1.6
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    # 155mm howitzer earth-shattering thump
    pitch = 48.0 * np.exp(-t * 2.8) + 28.0
    sub = np.sin(2.0 * np.pi * np.cumsum(pitch) / SAMPLE_RATE) * env_exp(t, 0.006, 0.65) * 1.6
    
    noise = np.random.uniform(-1, 1, len(t))
    concussion = filter_bandpass(noise, 250, 6000) * env_exp(t, 0.001, 0.035) * 2.2
    echo = filter_lowpass(noise, 240) * env_exp(t, 0.04, 0.85) * 1.2
    
    sig = sub + concussion + echo
    save_wav("heavy_artillery_fire.wav", sig)


def gen_naval_cannon_fire():
    dur = 2.1
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    pitch = 38.0 * np.exp(-t * 1.8) + 24.0
    sub = np.sin(2.0 * np.pi * np.cumsum(pitch) / SAMPLE_RATE) * env_exp(t, 0.01, 0.95) * 1.8
    
    noise = np.random.uniform(-1, 1, len(t))
    blast = filter_bandpass(noise, 180, 5000) * env_exp(t, 0.002, 0.05) * 2.0
    ocean_echo = filter_lowpass(noise, 260) * env_exp(t, 0.05, 1.2) * 1.3
    
    sig = sub + blast + ocean_echo
    save_wav("naval_cannon_fire.wav", sig)


def gen_missile_launch():
    dur = 1.1
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    # Ignition pop
    pop = np.sin(2.0 * np.pi * 180.0 * t) * env_exp(t, 0.003, 0.035) * 1.2
    
    # Roaring rocket motor exhaust
    noise = np.random.uniform(-1, 1, len(t))
    roar = filter_bandpass(noise, 220, 2800) * env_exp(t, 0.04, 0.55) * 1.4
    flame_hiss = filter_highpass(noise, 3000) * env_exp(t, 0.06, 0.45) * 0.6
    
    sig = pop + roar + flame_hiss
    save_wav("missile_launch.wav", sig)


def gen_rifle_fire():
    dur = 0.38
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    noise = np.random.uniform(-1, 1, len(t))
    crack = filter_bandpass(noise, 800, 9500) * env_exp(t, 0.0008, 0.018) * 2.4
    body = filter_bandpass(noise, 200, 1600) * env_exp(t, 0.003, 0.08) * 1.2
    metal = np.sin(2.0 * np.pi * 1400.0 * t) * env_exp(t, 0.005, 0.03) * 0.4
    
    sig = crack + body + metal
    save_wav("rifle_fire.wav", sig)


def gen_machinegun_fire():
    dur = 0.30
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    noise = np.random.uniform(-1, 1, len(t))
    bark = filter_bandpass(noise, 600, 8000) * env_exp(t, 0.001, 0.016) * 2.2
    low = filter_bandpass(noise, 160, 900) * env_exp(t, 0.004, 0.06) * 1.1
    
    sig = bark + low
    save_wav("machinegun_fire.wav", sig)


def gen_aa_cannon_fire():
    dur = 0.45
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    noise = np.random.uniform(-1, 1, len(t))
    flak_pop = filter_bandpass(noise, 500, 8500) * env_exp(t, 0.001, 0.020) * 2.5
    airburst = filter_bandpass(noise, 180, 1400) * env_exp(t, 0.005, 0.12) * 1.3
    
    sig = flak_pop + airburst
    save_wav("aa_cannon_fire.wav", sig)


def gen_armor_pierce():
    dur = 0.65
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    # Sharp metallic shattering crack + dull armor plate ring
    noise = np.random.uniform(-1, 1, len(t))
    shatter = filter_bandpass(noise, 1200, 11000) * env_exp(t, 0.0005, 0.025) * 2.5
    plate_thud = np.sin(2.0 * np.pi * 210.0 * t) * env_exp(t, 0.004, 0.12) * 1.5
    metal_ring = np.sin(2.0 * np.pi * 2650.0 * t) * env_exp(t, 0.008, 0.28) * 0.7
    
    sig = shatter + plate_thud + metal_ring
    save_wav("armor_pierce_impact.wav", sig)


def gen_ricochet():
    dur = 0.75
    t = np.linspace(0, dur, int(SAMPLE_RATE * dur), endpoint=False)
    
    # Glancing impact crack
    noise = np.random.uniform(-1, 1, len(t))
    impact = filter_bandpass(noise, 1500, 9000) * env_exp(t, 0.0006, 0.015) * 2.2
    
    # High-pitched spinning projectile whine ascending then fading
    freq = 1800.0 + 2200.0 * (t / dur) ** 0.6
    phase = 2.0 * np.pi * np.cumsum(freq) / SAMPLE_RATE
    whine = np.sin(phase) * env_exp(t, 0.005, 0.24) * 0.95
    
    sig = impact + whine
    save_wav("ricochet.wav", sig)


def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    print("Generating Red-Alert grade military combat audio foley samples...")
    gen_bomb_explosion()
    gen_heavy_explosion()
    gen_medium_explosion()
    gen_water_spout_explosion()
    gen_bomb_whistle()
    gen_bomb_release()
    gen_cannon_fire()
    gen_heavy_artillery_fire()
    gen_naval_cannon_fire()
    gen_missile_launch()
    gen_rifle_fire()
    gen_machinegun_fire()
    gen_aa_cannon_fire()
    gen_armor_pierce()
    gen_ricochet()
    print("All 15 military audio samples generated successfully in assets/audio/combat/!")


if __name__ == "__main__":
    main()
