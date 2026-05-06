import io
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly


TARGET_SR = 16000
FRAME_MS = 30

ABS_ENERGY_THRESHOLD = 0.0055
NOISE_MULTIPLIER = 3.0
MIN_PEAK = 0.022
MIN_SPEECH_SEC = 0.45
MIN_SPEECH_RATIO = 0.08
MIN_FINAL_RMS = 0.004

MAX_SPEECH_RATIO_FOR_LOW_RMS = 0.90

MAX_GAP_SEC = 0.35
PADDING_SEC = 0.18


def decode_audio(audio_bytes: bytes):
    if not audio_bytes:
        raise ValueError("audio_bytes is empty")

    audio_buffer = io.BytesIO(audio_bytes)

    audio, sr = sf.read(
        audio_buffer,
        dtype="float32",
        always_2d=False
    )

    if audio.ndim > 1:
        audio = np.mean(audio, axis=1)

    audio = audio.astype(np.float32)

    if audio.size == 0:
        return audio, int(sr)

    peak = float(np.max(np.abs(audio)))

    if peak > 1.0:
        audio = audio / peak

    return audio, int(sr)


def resample_to_16k(audio: np.ndarray, sr: int):
    if audio.size == 0:
        return audio.astype(np.float32)

    if sr == TARGET_SR:
        return audio.astype(np.float32)

    gcd = np.gcd(sr, TARGET_SR)
    up = TARGET_SR // gcd
    down = sr // gcd

    audio_16k = resample_poly(audio, up, down)

    return audio_16k.astype(np.float32)


def frame_generator(audio: np.ndarray, sr: int, frame_ms: int):
    frame_size = int(sr * frame_ms / 1000)

    total = len(audio)
    offset = 0

    while offset + frame_size <= total:
        frame = audio[offset:offset + frame_size]
        yield frame, offset, offset + frame_size
        offset += frame_size


def rms_energy(frame: np.ndarray) -> float:
    if frame is None or frame.size == 0:
        return 0.0

    return float(np.sqrt(np.mean(frame * frame)))


def zero_crossing_rate(frame: np.ndarray) -> float:
    if frame is None or frame.size < 2:
        return 0.0

    signs = np.sign(frame)
    signs[signs == 0] = 1

    crossings = np.sum(signs[:-1] != signs[1:])

    return float(crossings / len(frame))


def calc_noise_floor(energies):
    if not energies:
        return 0.0

    arr = np.array(energies, dtype=np.float32)

    return float(np.percentile(arr, 20))


def reject_result(reason: str, debug=None):
    if debug is None:
        debug = {}

    return {
        "has_speech": False,
        "speech_audio": np.array([], dtype=np.float32),
        "sr": TARGET_SR,
        "speech_ratio": 0.0,
        "reason": reason,
        "debug": debug
    }


def analyze_audio_bytes(audio_bytes: bytes):
    audio, sr = decode_audio(audio_bytes)
    audio_16k = resample_to_16k(audio, sr)

    if audio_16k.size == 0:
        return reject_result("empty_audio")

    frames = list(frame_generator(audio_16k, TARGET_SR, FRAME_MS))

    if not frames:
        return reject_result("no_frames")

    energies = []
    zcrs = []

    for frame, start, end in frames:
        energies.append(rms_energy(frame))
        zcrs.append(zero_crossing_rate(frame))

    noise_floor = calc_noise_floor(energies)

    dynamic_threshold = max(
        ABS_ENERGY_THRESHOLD,
        noise_floor * NOISE_MULTIPLIER
    )

    speech_segments = []

    for idx, (frame, start, end) in enumerate(frames):
        energy = energies[idx]
        zcr = zcrs[idx]

        zcr_ok = 0.01 <= zcr <= 0.40

        if energy >= dynamic_threshold and zcr_ok:
            speech_segments.append((start, end))

    debug_base = {
        "noise_floor": noise_floor,
        "dynamic_threshold": dynamic_threshold,
        "abs_threshold": ABS_ENERGY_THRESHOLD,
        "frame_count": len(frames),
        "speech_frame_count": len(speech_segments)
    }

    if not speech_segments:
        return reject_result("no_speech_frames", debug_base)

    merged = []

    current_start, current_end = speech_segments[0]
    max_gap_samples = int(MAX_GAP_SEC * TARGET_SR)

    for start, end in speech_segments[1:]:
        if start - current_end <= max_gap_samples:
            current_end = end
        else:
            merged.append((current_start, current_end))
            current_start, current_end = start, end

    merged.append((current_start, current_end))

    chunks = []
    padding = int(PADDING_SEC * TARGET_SR)

    for start, end in merged:
        s = max(0, start - padding)
        e = min(len(audio_16k), end + padding)

        if e > s:
            chunks.append(audio_16k[s:e])

    if not chunks:
        return reject_result("empty_chunks", debug_base)

    speech_audio = np.concatenate(chunks).astype(np.float32)

    speech_sec = len(speech_audio) / float(TARGET_SR)
    speech_ratio = len(speech_audio) / max(1, len(audio_16k))
    final_rms = rms_energy(speech_audio)
    peak = float(np.max(np.abs(speech_audio))) if speech_audio.size > 0 else 0.0

    debug = {
        **debug_base,
        "speech_sec": speech_sec,
        "speech_ratio": speech_ratio,
        "final_rms": final_rms,
        "peak": peak
    }

    if speech_sec < MIN_SPEECH_SEC:
        return reject_result("speech_too_short", debug)

    if speech_ratio < MIN_SPEECH_RATIO:
        return reject_result("speech_ratio_too_low", debug)

    if final_rms < MIN_FINAL_RMS:
        return reject_result("final_rms_too_low", debug)

    if peak < MIN_PEAK:
        return reject_result("peak_too_low", debug)

    if speech_ratio > MAX_SPEECH_RATIO_FOR_LOW_RMS and final_rms < 0.007:
        return reject_result("likely_constant_noise", debug)

    return {
        "has_speech": True,
        "speech_audio": speech_audio,
        "sr": TARGET_SR,
        "speech_ratio": float(speech_ratio),
        "reason": "ok",
        "debug": debug
    }