import io
import re
import traceback
import numpy as np
import soundfile as sf
from faster_whisper import WhisperModel


MODEL_NAME = "small"
_model = None


DOMAIN_PROMPT = """
Đây là hệ thống hướng dẫn viên ảo trong không gian 3D của Trường Đại học Bình Dương.

Wake word có 2 cách đọc chính:
1. Đọc theo tiếng Anh: B D U, có thể được nghe là bi đi u hoặc bi di u.
2. Đọc theo tiếng Việt: Bê Đê U, có thể được nghe là bê đê u hoặc be de u.

Một số địa điểm hợp lệ:
cổng trước, cổng sau, Khu A, Khu B, bãi xe, Khu công nghệ cao,
Viện AIDTI, Văn phòng Khoa FIRA, DSLAB, SMARTLAB, FABLAB,
vườn thông minh, thư viện, hội trường, phòng họp AI.

Wake word thường dùng:
xin chào BDU,
xin chào bi đi u,
xin chào bê đê u,
hi BDU,
hi bi đi u,
hi bê đê u,
hello BDU,
hello bi đi u,
hello bê đê u,
hey BDU,
hey bi đi u,
hey bê đê u,
BDU ơi,
bi đi u ơi,
bê đê u ơi,
hướng dẫn viên ơi,
trợ lý ơi.

Người dùng thường nói:
xin chào BDU hãy giới thiệu sơ lược về Trường Đại học Bình Dương,
xin chào bi đi u hãy giới thiệu sơ lược về Trường Đại học Bình Dương,
xin chào bê đê u hãy giới thiệu sơ lược về Trường Đại học Bình Dương,

xin chào BDU hãy giới thiệu ngành Công nghệ Thông tin,
xin chào bi đi u hãy giới thiệu ngành Công nghệ Thông tin,
xin chào bê đê u hãy giới thiệu ngành Công nghệ Thông tin,

xin chào BDU hãy giới thiệu Viện AIDTI,
xin chào bi đi u hãy giới thiệu Viện AIDTI,
xin chào bê đê u hãy giới thiệu Viện AIDTI,

xin chào BDU hãy giới thiệu thư viện,
xin chào bi đi u hãy giới thiệu thư viện,
xin chào bê đê u hãy giới thiệu thư viện,

xin chào BDU hãy đưa tôi đến thư viện,
xin chào bi đi u hãy đưa tôi đến thư viện,
xin chào bê đê u hãy đưa tôi đến thư viện,

hello BDU hãy đưa tôi đến Khu A,
hello bi đi u hãy đưa tôi đến Khu A,
hello bê đê u hãy đưa tôi đến Khu A,

BDU ơi hãy đưa tôi đến Viện AIDTI,
bi đi u ơi hãy đưa tôi đến Viện AIDTI,
bê đê u ơi hãy đưa tôi đến Viện AIDTI,

hướng dẫn viên ơi hãy đưa tôi đến thư viện,
trợ lý ơi giới thiệu Khu A,
gợi ý địa điểm tham quan.

Không hiểu các câu "xin chào", "hi", "hello" đơn lẻ là lệnh hệ thống.
Không tự sinh các câu kết video như:
cảm ơn mọi người, cảm ơn các bạn đã xem, hẹn gặp lại, đăng ký kênh, subscribe.
"""


def safe_print(text):
    try:
        print(text, flush=True)
    except UnicodeEncodeError:
        print(
            text.encode("utf-8", errors="ignore").decode("utf-8", errors="ignore"),
            flush=True
        )


def get_model():
    global _model

    if _model is None:
        safe_print(f"[STT] Loading Whisper model: {MODEL_NAME}")
        _model = WhisperModel(
            MODEL_NAME,
            device="cpu",
            compute_type="int8"
        )
        safe_print("[STT] Whisper model loaded.")

    return _model


def normalize_spaces(text: str) -> str:
    text = text or ""
    text = re.sub(r"\s+", " ", text)
    return text.strip()


def normalize_for_compare(text: str) -> str:
    if text is None:
        return ""

    text = text.strip().lower()
    text = re.sub(r"[,.!?;:()\[\]{}\"“”‘’]+", " ", text)
    text = re.sub(r"\s+", " ", text)
    return text.strip()


def fix_short_greeting_hallucination(text: str) -> str:
    if text is None:
        return ""

    clean = text.strip()
    lower = normalize_for_compare(clean)

    if not lower:
        return ""

    short_greeting_hallucinations = [
        "cảm ơn mọi người",
        "cảm ơn mội người",
        "cám ơn mọi người",
        "cảm ơn các bạn",
        "cám ơn các bạn",
        "cảm ơn bạn",
        "cám ơn bạn",
    ]

    word_count = len(lower.split())

    if word_count <= 5 and lower in short_greeting_hallucinations:
        safe_print(f"[STT FIX] Short hallucination fixed to 'xin chào bê đê u': {repr(clean)}")
        return "xin chào bê đê u"

    return clean


def filter_stt_hallucination(text: str) -> str:
    if text is None:
        return ""

    clean = text.strip()
    lower = clean.lower()

    if not clean:
        return ""

    fixed_short = fix_short_greeting_hallucination(clean)

    if fixed_short != clean:
        return fixed_short

    hallucination_patterns = [
        "hãy subscribe",
        "subscribe cho kênh",
        "la la school",
        "để không bỏ lỡ",
        "những video hấp dẫn",
        "like và subscribe",
        "nhấn chuông",
        "cảm ơn các bạn đã xem",
        "cảm ơn các bạn đã theo dõi",
        "cảm ơn mọi người đã xem",
        "cảm ơn mọi người đã theo dõi",
        "cám ơn mọi người đã xem",
        "hẹn gặp lại",
        "hẹn gặp lại các bạn",
        "hẹn gặp lại mọi người",
        "hẹn gặp lại trong",
        "trong phút này",
        "trong video tiếp theo",
        "video tiếp theo",
        "đăng ký kênh",
        "đừng quên đăng ký",
        "thank you for watching",
        "thanks for watching",
        "please subscribe",
        "subscribe to my channel",
    ]

    for pattern in hallucination_patterns:
        if pattern in lower:
            safe_print(f"[STT FILTER] Hallucination blocked: {repr(clean)}")
            return ""

    if len(lower) > 160:
        wake_words = [
            "xin chào bdu",
            "xin chào bi đi u",
            "xin chào bi di u",
            "xin chào bê đê u",
            "xin chao be de u",
            "hi bdu",
            "hi bi đi u",
            "hi bi di u",
            "hi bê đê u",
            "hello bdu",
            "hello bi đi u",
            "hello bi di u",
            "hello bê đê u",
            "hey bdu",
            "bdu ơi",
            "bi đi u ơi",
            "bi di u oi",
            "bê đê u ơi",
            "be de u oi",
            "hướng dẫn viên",
            "trợ lý",
        ]

        if not any(w in lower for w in wake_words):
            safe_print(f"[STT FILTER] Long text without wake word blocked: {repr(clean)}")
            return ""

    return clean


def fix_common_stt_errors(text: str) -> str:
    if text is None:
        return ""

    fixed = text.strip()
    lower = fixed.lower()

    replacements = {
        # =====================================================
        # Wake word BDU - đọc kiểu tiếng Anh: B D U
        # =====================================================
        "xin chào bi đi u": "xin chào BDU",
        "xin chào bi di u": "xin chào BDU",
        "xin chao bi di u": "xin chào BDU",
        "xin giao bi di u": "xin chào BDU",
        "xin gào bi đi u": "xin chào BDU",
        "xin gao bi di u": "xin chào BDU",
        "xin trao bi di u": "xin chào BDU",
        "xin trào bi đi u": "xin chào BDU",

        "hi bi đi u": "hi BDU",
        "hi bi di u": "hi BDU",
        "hello bi đi u": "hello BDU",
        "hello bi di u": "hello BDU",
        "hey bi đi u": "hey BDU",
        "hey bi di u": "hey BDU",
        "alo bi đi u": "alo BDU",
        "alo bi di u": "alo BDU",
        "alô bi đi u": "alo BDU",

        "bi đi u ơi": "BDU ơi",
        "bi di u oi": "BDU ơi",

        # =====================================================
        # Wake word BDU - đọc kiểu tiếng Việt: Bê Đê U
        # =====================================================
        "xin chào bê đê u": "xin chào BDU",
        "xin chao be de u": "xin chào BDU",
        "xin giao be de u": "xin chào BDU",
        "xin gào bê đê u": "xin chào BDU",
        "xin gao be de u": "xin chào BDU",
        "xin trao be de u": "xin chào BDU",
        "xin trào bê đê u": "xin chào BDU",

        "hi bê đê u": "hi BDU",
        "hi be de u": "hi BDU",
        "hello bê đê u": "hello BDU",
        "hello be de u": "hello BDU",
        "hey bê đê u": "hey BDU",
        "hey be de u": "hey BDU",
        "alo bê đê u": "alo BDU",
        "alo be de u": "alo BDU",
        "alô bê đê u": "alo BDU",

        "bê đê u ơi": "BDU ơi",
        "be de u oi": "BDU ơi",

        # =====================================================
        # Wake word BDU - STT nhận đúng hoặc tách chữ
        # =====================================================
        "xin chào bdu": "xin chào BDU",
        "xin chao bdu": "xin chào BDU",
        "xin giao bdu": "xin chào BDU",
        "xin gào bdu": "xin chào BDU",
        "xin gao bdu": "xin chào BDU",
        "xin trao bdu": "xin chào BDU",
        "xin trào bdu": "xin chào BDU",

        "hi bdu": "hi BDU",
        "hello bdu": "hello BDU",
        "hey bdu": "hey BDU",
        "alo bdu": "alo BDU",
        "alô bdu": "alo BDU",

        "bdu ơi": "BDU ơi",
        "bdu oi": "BDU ơi",

        "xin chào b d u": "xin chào BDU",
        "xin chao b d u": "xin chào BDU",
        "xin giao b d u": "xin chào BDU",
        "xin gào b d u": "xin chào BDU",
        "xin gao b d u": "xin chào BDU",

        "hi b d u": "hi BDU",
        "hello b d u": "hello BDU",
        "hey b d u": "hey BDU",
        "alo b d u": "alo BDU",
        "b d u ơi": "BDU ơi",
        "b d u oi": "BDU ơi",

        # Một số lỗi Whisper có thể sinh ra
        "xin chào bê đi u": "xin chào BDU",
        "xin chao be di u": "xin chào BDU",
        "hello bê đi u": "hello BDU",
        "hello be di u": "hello BDU",
        "bê đi u ơi": "BDU ơi",
        "be di u oi": "BDU ơi",

        # =====================================================
        # Tên riêng / địa điểm
        # =====================================================
        "khu công gài cao": "khu công nghệ cao",
        "khu công gái cao": "khu công nghệ cao",
        "khu công ngại cao": "khu công nghệ cao",
        "khu công nghẹ cao": "khu công nghệ cao",
        "khu công nghệ câu": "khu công nghệ cao",
        "khu công nghệ khao": "khu công nghệ cao",
        "khu cong gai cao": "khu công nghệ cao",
        "khu cong ngai cao": "khu công nghệ cao",
        "khu cong nghe cau": "khu công nghệ cao",
        "khu cong nghe cao": "khu công nghệ cao",

        "thư viên": "thư viện",
        "thư việt": "thư viện",
        "thư diễn": "thư viện",
        "thu vien": "thư viện",
        "thu viên": "thư viện",

        "hãy giới thiệu thư viên": "hãy giới thiệu thư viện",
        "hãy giới thiệu thư việt": "hãy giới thiệu thư viện",
        "giới thiệu thư viên": "giới thiệu thư viện",
        "giới thiệu thư việt": "giới thiệu thư viện",
        "hãy giới thiệu thu vien": "hãy giới thiệu thư viện",
        "gioi thieu thu vien": "giới thiệu thư viện",

        "hội chường": "hội trường",
        "hội trườn": "hội trường",
        "hồi trường": "hội trường",
        "hoi truong": "hội trường",

        "cỗng trước": "cổng trước",
        "cổng trức": "cổng trước",
        "cong truoc": "cổng trước",
        "cỗng sau": "cổng sau",
        "cong sau": "cổng sau",

        "bải xe": "bãi xe",
        "bãi se": "bãi xe",
        "bai xe": "bãi xe",
        "nhà se": "nhà xe",

        "vườn thông mình": "vườn thông minh",
        "vuon thong minh": "vườn thông minh",

        "phòng hợp ai": "phòng họp AI",
        "phòng học ai": "phòng họp AI",
        "phong hop ai": "phòng họp AI",

        # AIDTI
        "a i d t i": "AIDTI",
        "aidti": "AIDTI",
        "ây ai đi ti ai": "AIDTI",
        "viện appy": "viện AIDTI",
        "vien appy": "viện AIDTI",
        "viện áp ti": "viện AIDTI",
        "vien ap ti": "viện AIDTI",
        "viện át ti": "viện AIDTI",
        "vien at ti": "viện AIDTI",
        "viện ách ti": "viện AIDTI",
        "vien ach ti": "viện AIDTI",
        "viện ái đi ti": "viện AIDTI",
        "vien ai di ti": "viện AIDTI",
        "áp ti": "AIDTI",
        "ap ti": "AIDTI",
        "át ti": "AIDTI",
        "at ti": "AIDTI",
        "appy": "AIDTI",
        "ép ti": "AIDTI",
        "ep ti": "AIDTI",

        "fira": "FIRA",
        "phi ra": "FIRA",
        "ds lab": "DSLAB",
        "d s lab": "DSLAB",
        "dslab": "DSLAB",
        "smart lab": "SMARTLAB",
        "smartlab": "SMARTLAB",
        "fab lab": "FABLAB",
        "fablab": "FABLAB",
        "fablas": "FABLAB",
    }

    for wrong, right in replacements.items():
        lower = lower.replace(wrong.lower(), right)

    fixed = normalize_spaces(lower)

    name_replacements = {
        "bdu": "BDU",
        "aidti": "AIDTI",
        "fira": "FIRA",
        "dslab": "DSLAB",
        "smartlab": "SMARTLAB",
        "fablab": "FABLAB",
        "ai": "AI",
        "khu a": "Khu A",
        "khu b": "Khu B",
    }

    for wrong, right in name_replacements.items():
        fixed = re.sub(rf"\b{re.escape(wrong)}\b", right, fixed, flags=re.IGNORECASE)

    return fixed.strip()


def decode_audio_from_bytes(audio_bytes: bytes):
    if not audio_bytes:
        raise ValueError("audio_bytes is empty")

    audio_buffer = io.BytesIO(audio_bytes)

    audio_data, sample_rate = sf.read(
        audio_buffer,
        dtype="float32",
        always_2d=False
    )

    if not isinstance(audio_data, np.ndarray):
        audio_data = np.array(audio_data, dtype=np.float32)

    if audio_data.ndim > 1:
        audio_data = np.mean(audio_data, axis=1).astype(np.float32)

    if audio_data.size == 0:
        raise ValueError("decoded audio is empty")

    peak = float(np.max(np.abs(audio_data))) if audio_data.size > 0 else 0.0

    if peak > 1.0:
        audio_data = audio_data / peak

    return audio_data.astype(np.float32), int(sample_rate)


def speech_to_text_from_bytes(
    audio_bytes: bytes = None,
    preloaded_audio=None,
    preloaded_sr=None
) -> str:
    try:
        if preloaded_audio is not None and preloaded_sr is not None:
            audio_data = np.asarray(preloaded_audio, dtype=np.float32)
            sample_rate = int(preloaded_sr)
            safe_print("[STT] Using VAD-filtered speech audio...")
        else:
            safe_print("[STT] Decoding audio bytes in memory...")
            audio_data, sample_rate = decode_audio_from_bytes(audio_bytes)

        if audio_data.size == 0:
            return ""

        duration_sec = len(audio_data) / float(sample_rate)
        safe_print(f"[STT] Duration: {duration_sec:.2f}s")

        if duration_sec < 0.25:
            safe_print("[STT] Audio too short. Ignored.")
            return ""

        model = get_model()

        segments_generator, info = model.transcribe(
            audio_data,
            language="vi",
            task="transcribe",
            beam_size=5,
            vad_filter=False,
            condition_on_previous_text=False,
            temperature=0.0,
            initial_prompt=DOMAIN_PROMPT,
            compression_ratio_threshold=2.0,
            log_prob_threshold=-0.8,
            no_speech_threshold=0.75
        )

        safe_print(f"[STT] Detected language: {info.language}")
        safe_print(f"[STT] Language probability: {info.language_probability}")

        segments = list(segments_generator)

        text_parts = []

        for segment in segments:
            seg_text = segment.text.strip()
            if seg_text:
                text_parts.append(seg_text)

        final_text = " ".join(text_parts).strip()

        safe_print(f"[STT] Raw final text: {repr(final_text)}")

        final_text = filter_stt_hallucination(final_text)
        final_text = fix_common_stt_errors(final_text)

        safe_print(f"[STT] Fixed final text: {repr(final_text)}")

        return final_text

    except Exception as e:
        safe_print("[STT ERROR]")
        safe_print(traceback.format_exc())
        raise RuntimeError(f"speech_to_text_from_bytes failed: {e}")