import sounddevice as sd
import soundfile as sf
import numpy as np
from datetime import datetime

REPORT_FILE = "audio_analysis_report.txt"
WAV_FILE = "audio_analysis_recording.wav"

RECORD_SECONDS = 5
SAMPLE_RATE = 16000
CHANNELS = 1


def write_line(f, text=""):
    print(text)
    f.write(text + "\n")


def list_input_devices():
    devices = sd.query_devices()
    result = []
    for i, dev in enumerate(devices):
        if dev["max_input_channels"] > 0:
            result.append((i, dev))
    return result


def rms(x: np.ndarray) -> float:
    if x.size == 0:
        return 0.0
    return float(np.sqrt(np.mean(np.square(x))))


def peak(x: np.ndarray) -> float:
    if x.size == 0:
        return 0.0
    return float(np.max(np.abs(x)))


def zero_crossing_rate(x: np.ndarray) -> float:
    if x.size < 2:
        return 0.0
    signs = np.sign(x)
    return float(np.mean(signs[:-1] != signs[1:]))


def clipping_ratio(x: np.ndarray, threshold: float = 0.99) -> float:
    if x.size == 0:
        return 0.0
    return float(np.mean(np.abs(x) >= threshold))


def silence_ratio(x: np.ndarray, silence_threshold: float = 0.01) -> float:
    if x.size == 0:
        return 1.0
    return float(np.mean(np.abs(x) < silence_threshold))


def fft_features(x: np.ndarray, sample_rate: int):
    if x.size == 0:
        return 0.0, 0.0, 0.0, []

    x = x - np.mean(x)
    window = np.hanning(len(x))
    xw = x * window

    spectrum = np.fft.rfft(xw)
    mags = np.abs(spectrum)
    freqs = np.fft.rfftfreq(len(xw), d=1.0 / sample_rate)

    if mags.size == 0 or np.sum(mags) == 0:
        return 0.0, 0.0, 0.0, []

    dominant_freq = float(freqs[np.argmax(mags)])

    centroid = float(np.sum(freqs * mags) / np.sum(mags))

    bandwidth = float(
        np.sqrt(np.sum(((freqs - centroid) ** 2) * mags) / np.sum(mags))
    )

    top_n = min(10, len(mags))
    peak_indices = np.argsort(mags)[-top_n:][::-1]
    top_peaks = [(float(freqs[i]), float(mags[i])) for i in peak_indices]

    return dominant_freq, centroid, bandwidth, top_peaks


def estimate_f0_autocorr(x: np.ndarray, sample_rate: int, fmin: float = 80.0, fmax: float = 350.0) -> float:
    """
    Ước lượng tần số cơ bản F0 bằng autocorrelation, phù hợp tương đối cho giọng nói.
    """
    if x.size == 0:
        return 0.0

    x = x - np.mean(x)
    if np.allclose(x, 0):
        return 0.0

    corr = np.correlate(x, x, mode="full")
    corr = corr[len(corr) // 2:]

    min_lag = int(sample_rate / fmax)
    max_lag = int(sample_rate / fmin)

    if max_lag >= len(corr) or min_lag >= max_lag:
        return 0.0

    search = corr[min_lag:max_lag]
    peak_idx = np.argmax(search) + min_lag

    if peak_idx <= 0:
        return 0.0

    return float(sample_rate / peak_idx)


def voice_band_energy_ratio(x: np.ndarray, sample_rate: int, low: float = 80.0, high: float = 4000.0) -> float:
    if x.size == 0:
        return 0.0

    x = x - np.mean(x)
    window = np.hanning(len(x))
    xw = x * window

    spectrum = np.fft.rfft(xw)
    power = np.abs(spectrum) ** 2
    freqs = np.fft.rfftfreq(len(xw), d=1.0 / sample_rate)

    total_energy = np.sum(power)
    if total_energy <= 0:
        return 0.0

    mask = (freqs >= low) & (freqs <= high)
    band_energy = np.sum(power[mask])

    return float(band_energy / total_energy)


def estimate_noise_and_snr(x: np.ndarray):
    """
    Ước lượng thô:
    - noise floor = rms của 20% frame yên lặng nhất
    - signal rms = rms toàn cục
    - snr = 20log10(signal/noise)
    """
    if x.size == 0:
        return 0.0, 0.0

    frame_size = 512
    if len(x) < frame_size:
        signal_rms = rms(x)
        noise_rms = signal_rms
        snr_db = 0.0
        return noise_rms, snr_db

    frames = []
    for i in range(0, len(x) - frame_size + 1, frame_size):
        frame = x[i:i + frame_size]
        frames.append(rms(frame))

    frames = np.array(frames)
    if frames.size == 0:
        return 0.0, 0.0

    sorted_frames = np.sort(frames)
    k = max(1, int(0.2 * len(sorted_frames)))
    noise_rms = float(np.mean(sorted_frames[:k]))
    signal_rms = rms(x)

    if noise_rms <= 1e-9:
        snr_db = 999.0
    else:
        snr_db = float(20 * np.log10(max(signal_rms, 1e-9) / noise_rms))

    return noise_rms, snr_db


def main():
    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    devices = list_input_devices()
    if not devices:
        print("Không tìm thấy microphone input.")
        return

    print("=== INPUT DEVICES ===")
    for idx, dev in devices:
        print(f"[{idx}] {dev['name']}")
        print(f"    max_input_channels: {dev['max_input_channels']}")
        print(f"    default_samplerate: {dev['default_samplerate']}")

    raw = input("\nNhập device index muốn ghi: ").strip()
    try:
        device_index = int(raw)
    except ValueError:
        print("Device index không hợp lệ.")
        return

    try:
        device_info = sd.query_devices(device_index)
    except Exception as e:
        print("Không lấy được device:", e)
        return

    print(f"\nGhi âm {RECORD_SECONDS} giây tại {SAMPLE_RATE} Hz...")
    print("Hãy nói rõ vào mic, ví dụ:")
    print('"Xin chào. Tôi muốn tìm hiểu ngành công nghệ thông tin."')

    audio = sd.rec(
        int(RECORD_SECONDS * SAMPLE_RATE),
        samplerate=SAMPLE_RATE,
        channels=CHANNELS,
        dtype="float32",
        device=device_index
    )
    sd.wait()

    audio = np.squeeze(audio)
    sf.write(WAV_FILE, audio, SAMPLE_RATE)

    # Tính chỉ số
    duration = len(audio) / SAMPLE_RATE
    rms_value = rms(audio)
    peak_value = peak(audio)
    crest = peak_value / rms_value if rms_value > 1e-9 else 0.0
    zcr = zero_crossing_rate(audio)
    clip_ratio = clipping_ratio(audio)
    sil_ratio = silence_ratio(audio)
    dom_freq, centroid, bandwidth, top_peaks = fft_features(audio, SAMPLE_RATE)
    est_f0 = estimate_f0_autocorr(audio, SAMPLE_RATE)
    voice_ratio = voice_band_energy_ratio(audio, SAMPLE_RATE)
    noise_floor, snr_db = estimate_noise_and_snr(audio)

    with open(REPORT_FILE, "w", encoding="utf-8") as f:
        write_line(f, "=== AUDIO QUALITY ANALYSIS REPORT ===")
        write_line(f, f"Time: {now}")
        write_line(f)

        write_line(f, "=== DEVICE INFO ===")
        write_line(f, f"Device index: {device_index}")
        write_line(f, f"Device name: {device_info['name']}")
        write_line(f, f"Device default samplerate: {device_info['default_samplerate']}")
        write_line(f)

        write_line(f, "=== RECORDING INFO ===")
        write_line(f, f"Saved wav: {WAV_FILE}")
        write_line(f, f"Sample rate: {SAMPLE_RATE}")
        write_line(f, f"Channels: {CHANNELS}")
        write_line(f, f"Duration: {duration:.3f} s")
        write_line(f, f"Recorded samples: {len(audio)}")
        write_line(f)

        write_line(f, "=== TIME-DOMAIN FEATURES ===")
        write_line(f, f"RMS: {rms_value:.6f}")
        write_line(f, f"Peak: {peak_value:.6f}")
        write_line(f, f"Crest factor: {crest:.3f}")
        write_line(f, f"Zero crossing rate: {zcr:.6f}")
        write_line(f, f"Clipping ratio: {clip_ratio:.6f}")
        write_line(f, f"Silence ratio: {sil_ratio:.6f}")
        write_line(f)

        write_line(f, "=== FREQUENCY-DOMAIN FEATURES ===")
        write_line(f, f"Dominant frequency: {dom_freq:.2f} Hz")
        write_line(f, f"Estimated F0: {est_f0:.2f} Hz")
        write_line(f, f"Spectral centroid: {centroid:.2f} Hz")
        write_line(f, f"Spectral bandwidth: {bandwidth:.2f} Hz")
        write_line(f, f"Voice band energy ratio (80-4000 Hz): {voice_ratio:.6f}")
        write_line(f)

        write_line(f, "Top spectral peaks:")
        for freq, mag in top_peaks:
            write_line(f, f"  - {freq:.2f} Hz | magnitude={mag:.4f}")
        write_line(f)

        write_line(f, "=== NOISE ESTIMATION ===")
        write_line(f, f"Noise floor estimate (RMS): {noise_floor:.6f}")
        write_line(f, f"SNR estimate (dB): {snr_db:.2f}")
        write_line(f)

        write_line(f, "=== INTERPRETATION GUIDE ===")
        write_line(f, "- RMS quá thấp (< 0.005): mic thu yếu, STT dễ hụt âm.")
        write_line(f, "- Clipping ratio > 0.01: tín hiệu quá to, có thể méo.")
        write_line(f, "- Silence ratio quá cao: audio có quá nhiều khoảng yên lặng.")
        write_line(f, "- Voice band energy ratio càng cao càng tốt cho STT.")
        write_line(f, "- SNR estimate càng cao càng tốt. Nếu dưới ~10 dB thì môi trường khá ồn.")
        write_line(f, "- Estimated F0 chỉ là ước lượng tần số cơ bản, không dùng trực tiếp để chỉnh model Whisper.")
        write_line(f, "- Thứ nên tối ưu cho STT là: đúng mic, audio sạch, mono, 16kHz, ít nhiễu, VAD hợp lý.")

    print(f"\nĐã lưu audio: {WAV_FILE}")
    print(f"Đã lưu báo cáo: {REPORT_FILE}")


if __name__ == "__main__":
    main()