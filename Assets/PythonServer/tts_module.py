import asyncio
import os
import re
import time
import traceback
import edge_tts


VOICE = "vi-VN-HoaiMyNeural"


def safe_print(text):
    try:
        print(text, flush=True)
    except UnicodeEncodeError:
        print(
            text.encode("utf-8", errors="ignore").decode("utf-8", errors="ignore"),
            flush=True
        )


def normalize_text_for_tts(text: str) -> str:
    if text is None:
        return ""

    text = str(text).strip()

    placeholder_map = {
        "__AIDTI__": "ây ai đi ti ai",
        "__FIRA__": "phi ra",
        "__DSLAB__": "đi ét lab",
        "__SMARTLAB__": "sờ mát lab",
        "__FABLAB__": "pháp lab",
        "__BDU__": "bi đi u",
        "__IOT__": "ai ô ti",
        "__RTSP__": "a ti ét pi",
        "__UNITY__": "iu ni ti",
        "__THREED__": "ba chiều",
    }

    long_replacements = {
        "AIDTI": "__AIDTI__",
        "A I D T I": "__AIDTI__",

        "FIRA": "__FIRA__",
        "F I R A": "__FIRA__",

        "DSLAB": "__DSLAB__",
        "D S Lab": "__DSLAB__",
        "D S LAB": "__DSLAB__",

        "SMARTLAB": "__SMARTLAB__",
        "SMART Lab": "__SMARTLAB__",
        "Smart Lab": "__SMARTLAB__",

        "FABLAB": "__FABLAB__",
        "FAB Lab": "__FABLAB__",
        "Fab Lab": "__FABLAB__",

        "BDU": "__BDU__",
        "B D U": "__BDU__",

        "IoT": "__IOT__",
        "IOT": "__IOT__",

        "RTSP": "__RTSP__",
        "Unity": "__UNITY__",
        "3D": "__THREED__",
    }

    for src in sorted(long_replacements.keys(), key=len, reverse=True):
        dst = long_replacements[src]
        text = re.sub(rf"\b{re.escape(src)}\b", dst, text, flags=re.IGNORECASE)

    text = re.sub(r"\bAI\b", "ây ai", text)

    for placeholder, spoken in placeholder_map.items():
        text = text.replace(placeholder, spoken)

    text = text.replace("\n", " ")
    text = text.replace("\r", " ")
    text = text.replace("\t", " ")

    text = re.sub(r"[^\w\sÀ-ỹà-ỹ.,!?;:()\-]", " ", text, flags=re.UNICODE)
    text = re.sub(r"\s+", " ", text).strip()

    if len(text) > 500:
        text = text[:500].strip()

    return text


def sanitize_text_for_retry(text: str) -> str:
    if text is None:
        return ""

    text = str(text)
    text = re.sub(r"[^\w\sÀ-ỹà-ỹ.,!?;:\-]", " ", text, flags=re.UNICODE)
    text = re.sub(r"\s+", " ", text).strip()

    return text


async def _tts_async(text: str, output_path: str):
    communicate = edge_tts.Communicate(
        text=text,
        voice=VOICE,
        rate="+0%",
        volume="+0%"
    )

    await communicate.save(output_path)


def text_to_speech_from_text(text: str, output_path: str):
    if text is None:
        text = ""

    text = normalize_text_for_tts(text)

    if not text:
        raise ValueError("TTS text is empty after normalization")

    output_dir = os.path.dirname(os.path.abspath(output_path))
    os.makedirs(output_dir, exist_ok=True)

    try:
        if os.path.exists(output_path):
            os.remove(output_path)
    except Exception:
        pass

    last_error = None

    for attempt in range(2):
        try:
            safe_print(f"[TTS] Generating audio... attempt={attempt + 1}")
            safe_print(f"[TTS] Text: {repr(text)}")

            asyncio.run(_tts_async(text, output_path))

            if os.path.exists(output_path) and os.path.getsize(output_path) > 0:
                safe_print(f"[TTS] Created: {output_path}")
                return

            raise RuntimeError("TTS output file was not created or is empty")

        except Exception as e:
            last_error = e
            safe_print(f"[TTS WARNING] attempt={attempt + 1} failed: {e}")
            safe_print(traceback.format_exc())
            time.sleep(0.5)

    retry_text = sanitize_text_for_retry(text)

    if retry_text and retry_text != text:
        try:
            safe_print("[TTS] Retrying with sanitized text...")
            safe_print(f"[TTS] Retry text: {repr(retry_text)}")

            asyncio.run(_tts_async(retry_text, output_path))

            if os.path.exists(output_path) and os.path.getsize(output_path) > 0:
                safe_print(f"[TTS] Created after sanitize retry: {output_path}")
                return

            raise RuntimeError("TTS sanitized output file was not created or is empty")

        except Exception as e:
            last_error = e
            safe_print(f"[TTS ERROR] sanitize retry failed: {e}")
            safe_print(traceback.format_exc())

    raise RuntimeError(f"TTS failed: {last_error}")


def text_file_to_speech(text_file_path: str, output_path: str):
    with open(text_file_path, "r", encoding="utf-8") as f:
        text = f.read().strip()

    text_to_speech_from_text(text, output_path)