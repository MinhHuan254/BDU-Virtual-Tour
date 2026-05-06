import os
import sys
import json
import traceback
import logging
from datetime import datetime
from urllib.parse import quote

from flask import Flask, request, jsonify, send_file, Response

from stt_module import speech_to_text_from_bytes
from tts_module import text_to_speech_from_text
from llm_module import generate_response
from activation_module import remove_wake_word, is_supported_command


try:
    from vad_module import analyze_audio_bytes
    HAS_VAD = True
except Exception:
    HAS_VAD = False


try:
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
except Exception:
    pass


BASE_DIR = os.path.dirname(os.path.abspath(__file__))
TEMP_DIR = os.path.join(BASE_DIR, "temp")
os.makedirs(TEMP_DIR, exist_ok=True)


FIXED_STT_TEXT_FILE = os.path.join(TEMP_DIR, "stt_result.txt")
FIXED_COMMAND_TEXT_FILE = os.path.join(TEMP_DIR, "command_text.txt")
FIXED_LLM_TEXT_FILE = os.path.join(TEMP_DIR, "llm_response.txt")
FIXED_LLM_JSON_FILE = os.path.join(TEMP_DIR, "llm_result.json")
FIXED_WAV_FILE = os.path.join(TEMP_DIR, "last_received.wav")
FIXED_TTS_MP3_FILE = os.path.join(TEMP_DIR, "tts_output.mp3")


app = Flask(__name__)
app.config["MAX_CONTENT_LENGTH"] = 20 * 1024 * 1024


def safe_print(text):
    try:
        print(text, flush=True)
    except UnicodeEncodeError:
        print(
            text.encode("utf-8", errors="ignore").decode("utf-8", errors="ignore"),
            flush=True
        )


def save_text_file(text: str, path: str):
    with open(path, "w", encoding="utf-8") as f:
        f.write(text or "")


def save_json_file(data: dict, path: str):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)


def save_binary_file(data: bytes, path: str):
    with open(path, "wb") as f:
        f.write(data)


def empty_llm_result():
    return {
        "speech_text": "",
        "action": "ignore",
        "target_location": None,
        "locations": [],
        "iot_device": None,
        "iot_command": None,
        "camera_id": None,
        "confidence": 0.0
    }


def make_ignored_result(reason: str, user_text: str = ""):
    result = {
        "ignored": True,
        "reason": reason,
        "user_text": user_text or "",
        "command_text": "",
        "assistant_text": "",
        "llm_result": empty_llm_result(),
        "tts_path": None,
        "timestamp": datetime.now().isoformat()
    }

    save_json_file(result["llm_result"], FIXED_LLM_JSON_FILE)
    return result


def process_audio_pipeline(audio_bytes: bytes):
    """
    audio binary
    -> VAD
    -> STT
    -> Wake Word Filter
    -> Command Filter
    -> LLM/local intent JSON
    -> TTS MP3
    """

    if not audio_bytes:
        raise ValueError("Empty audio bytes")

    safe_print("[PIPELINE] Received audio binary from Unity.")
    save_binary_file(audio_bytes, FIXED_WAV_FILE)
    safe_print(f"[PIPELINE] Saved received audio: {FIXED_WAV_FILE}")

    user_text = ""

    safe_print("[PIPELINE] Running VAD/STT...")

    if HAS_VAD:
        try:
            analysis = analyze_audio_bytes(audio_bytes)

            has_speech = bool(analysis.get("has_speech", False))
            speech_audio = analysis.get("speech_audio")
            sr = int(analysis.get("sr", 16000))
            speech_ratio = float(analysis.get("speech_ratio", 0.0))

            vad_reason = analysis.get("reason", "")
            vad_debug = analysis.get("debug", {})

            safe_print(
                f"[VAD] has_speech={has_speech}, "
                f"reason={vad_reason}, "
                f"speech_ratio={speech_ratio:.2f}, "
                f"debug={vad_debug}"
            )

            if not has_speech or speech_audio is None or speech_audio.size == 0:
                safe_print("[VAD] No speech detected. Ignore audio.")
                return make_ignored_result(
                    reason="no_speech_detected",
                    user_text=""
                )

            user_text = speech_to_text_from_bytes(
                preloaded_audio=speech_audio,
                preloaded_sr=sr
            )

        except Exception:
            safe_print("[VAD WARNING] VAD failed. Falling back to original audio.")
            safe_print(traceback.format_exc())

            user_text = speech_to_text_from_bytes(audio_bytes=audio_bytes)

    else:
        safe_print("[VAD] HAS_VAD=False. Using original audio for STT.")
        user_text = speech_to_text_from_bytes(audio_bytes=audio_bytes)

    user_text = (user_text or "").strip()
    save_text_file(user_text, FIXED_STT_TEXT_FILE)
    safe_print(f"[PIPELINE] STT text: {repr(user_text)}")

    if not user_text:
        safe_print("[PIPELINE] Empty STT text. Ignore audio.")
        return make_ignored_result(
            reason="empty_stt_text",
            user_text=""
        )

    activated, command_text = remove_wake_word(user_text)
    safe_print(f"[ACTIVATION] activated={activated}, command={repr(command_text)}")

    if not activated:
        safe_print("[ACTIVATION] Wake word not detected. Ignore audio.")
        return make_ignored_result(
            reason="wake_word_not_detected",
            user_text=user_text
        )

    command_text = (command_text or "").strip()

    # Nếu chỉ nói: "xin chào BDU", "hello BDU", "BDU ơi"
    if not command_text:
        command_text = "xin chào"

    # Nếu có wake word nhưng phía sau là tán gẫu không liên quan
    else:
        if not is_supported_command(command_text):
            safe_print(
                "[ACTIVATION] Wake word detected but command is not supported. Ignore audio."
            )
            return make_ignored_result(
                reason="wake_word_but_not_supported_command",
                user_text=user_text
            )

    save_text_file(command_text, FIXED_COMMAND_TEXT_FILE)

    safe_print(f"[PIPELINE] Command text: {repr(command_text)}")
    safe_print("[PIPELINE] Running LLM/local intent...")

    llm_result = generate_response(command_text)

    if not isinstance(llm_result, dict):
        safe_print("[PIPELINE] LLM result is not dict. Using fallback response.")
        llm_result = {
            "speech_text": "Tôi chưa hiểu rõ yêu cầu của bạn. Bạn có thể nói lại được không?",
            "action": "clarify",
            "target_location": None,
            "locations": [],
            "iot_device": None,
            "iot_command": None,
            "camera_id": None,
            "confidence": 0.0
        }

    assistant_text = str(llm_result.get("speech_text") or "").strip()

    if not assistant_text:
        assistant_text = "Tôi chưa tạo được câu trả lời. Bạn có thể hỏi lại được không?"
        llm_result["speech_text"] = assistant_text

    save_text_file(assistant_text, FIXED_LLM_TEXT_FILE)
    save_json_file(llm_result, FIXED_LLM_JSON_FILE)

    safe_print(f"[PIPELINE] LLM speech_text: {repr(assistant_text)}")
    safe_print(f"[PIPELINE] Action: {llm_result.get('action')}")
    safe_print(f"[PIPELINE] Target: {llm_result.get('target_location')}")

    safe_print("[PIPELINE] Running TTS...")

    text_to_speech_from_text(assistant_text, FIXED_TTS_MP3_FILE)

    if not os.path.exists(FIXED_TTS_MP3_FILE):
        raise RuntimeError("TTS output file was not created")

    if os.path.getsize(FIXED_TTS_MP3_FILE) <= 0:
        raise RuntimeError("TTS output file is empty")

    safe_print(f"[PIPELINE] TTS audio created: {FIXED_TTS_MP3_FILE}")

    return {
        "ignored": False,
        "reason": None,
        "user_text": user_text,
        "command_text": command_text,
        "assistant_text": assistant_text,
        "llm_result": llm_result,
        "tts_path": FIXED_TTS_MP3_FILE,
        "timestamp": datetime.now().isoformat()
    }


@app.route("/health", methods=["GET"])
def health():
    return jsonify({
        "success": True,
        "message": "Server is ready",
        "has_vad": HAS_VAD,
        "time": datetime.now().isoformat()
    })


@app.route("/chat-audio", methods=["POST"])
def chat_audio_json():
    """
    Endpoint debug:
    Unity gửi WAV binary.
    Server trả JSON.
    Unity gọi tiếp /tts/latest để lấy MP3 nếu cần.
    """

    try:
        audio_bytes = request.get_data()
        result = process_audio_pipeline(audio_bytes)

        if result.get("ignored"):
            return jsonify({
                "success": True,
                "ignored": True,
                "reason": result["reason"],
                "user_text": result["user_text"],
                "message": "Audio ignored.",
                "timestamp": result["timestamp"]
            })

        llm_result = result["llm_result"]

        return jsonify({
            "success": True,
            "ignored": False,

            "user_text": result["user_text"],
            "command_text": result["command_text"],
            "assistant_text": result["assistant_text"],

            "action": llm_result.get("action"),
            "target_location": llm_result.get("target_location"),
            "locations": llm_result.get("locations"),
            "iot_device": llm_result.get("iot_device"),
            "iot_command": llm_result.get("iot_command"),
            "camera_id": llm_result.get("camera_id"),
            "confidence": llm_result.get("confidence"),

            "audio_url": "http://127.0.0.1:5001/tts/latest",

            "stt_text_path": FIXED_STT_TEXT_FILE,
            "command_text_path": FIXED_COMMAND_TEXT_FILE,
            "llm_text_path": FIXED_LLM_TEXT_FILE,
            "llm_json_path": FIXED_LLM_JSON_FILE,
            "wav_path": FIXED_WAV_FILE,
            "tts_path": FIXED_TTS_MP3_FILE,

            "timestamp": result["timestamp"]
        })

    except Exception as e:
        safe_print("[CHAT AUDIO JSON ERROR]")
        safe_print(traceback.format_exc())

        return jsonify({
            "success": False,
            "error": str(e),
            "traceback": traceback.format_exc()
        }), 500


@app.route("/chat-audio-binary", methods=["POST"])
def chat_audio_binary():
    """
    Endpoint chính:
    Unity gửi WAV binary.
    Server trả MP3 binary.
    Metadata gửi qua HTTP headers.
    """

    try:
        audio_bytes = request.get_data()
        result = process_audio_pipeline(audio_bytes)

        if result.get("ignored"):
            response = Response(status=204)
            response.headers["X-Ignored"] = "true"
            response.headers["X-Reason"] = result["reason"]
            response.headers["X-User-Text"] = quote(result["user_text"] or "")
            response.headers["X-Timestamp"] = result["timestamp"]
            return response

        llm_result = result["llm_result"]

        with open(result["tts_path"], "rb") as f:
            mp3_bytes = f.read()

        response = Response(
            mp3_bytes,
            mimetype="audio/mpeg"
        )

        response.headers["X-Ignored"] = "false"
        response.headers["X-User-Text"] = quote(result["user_text"])
        response.headers["X-Command-Text"] = quote(result["command_text"])
        response.headers["X-Assistant-Text"] = quote(result["assistant_text"])

        response.headers["X-Action"] = str(llm_result.get("action") or "")
        response.headers["X-Target-Location"] = str(llm_result.get("target_location") or "")
        response.headers["X-Locations"] = quote(
            json.dumps(llm_result.get("locations") or [], ensure_ascii=False)
        )

        response.headers["X-Iot-Device"] = str(llm_result.get("iot_device") or "")
        response.headers["X-Iot-Command"] = str(llm_result.get("iot_command") or "")
        response.headers["X-Camera-Id"] = str(llm_result.get("camera_id") or "")
        response.headers["X-Confidence"] = str(llm_result.get("confidence") or 0.0)
        response.headers["X-Timestamp"] = result["timestamp"]

        return response

    except Exception as e:
        safe_print("[CHAT AUDIO BINARY ERROR]")
        safe_print(traceback.format_exc())

        return jsonify({
            "success": False,
            "error": str(e),
            "traceback": traceback.format_exc()
        }), 500


@app.route("/tts/latest", methods=["GET"])
def tts_latest():
    if not os.path.exists(FIXED_TTS_MP3_FILE):
        return jsonify({
            "success": False,
            "error": "No TTS audio yet"
        }), 404

    return send_file(
        FIXED_TTS_MP3_FILE,
        mimetype="audio/mpeg"
    )


@app.route("/debug/latest", methods=["GET"])
def debug_latest():
    data = {
        "success": True,
        "files": {
            "stt_text": FIXED_STT_TEXT_FILE,
            "command_text": FIXED_COMMAND_TEXT_FILE,
            "llm_text": FIXED_LLM_TEXT_FILE,
            "llm_json": FIXED_LLM_JSON_FILE,
            "wav": FIXED_WAV_FILE,
            "tts_mp3": FIXED_TTS_MP3_FILE
        }
    }

    if os.path.exists(FIXED_STT_TEXT_FILE):
        with open(FIXED_STT_TEXT_FILE, "r", encoding="utf-8") as f:
            data["stt_text"] = f.read()

    if os.path.exists(FIXED_COMMAND_TEXT_FILE):
        with open(FIXED_COMMAND_TEXT_FILE, "r", encoding="utf-8") as f:
            data["command_text"] = f.read()

    if os.path.exists(FIXED_LLM_TEXT_FILE):
        with open(FIXED_LLM_TEXT_FILE, "r", encoding="utf-8") as f:
            data["llm_text"] = f.read()

    if os.path.exists(FIXED_LLM_JSON_FILE):
        with open(FIXED_LLM_JSON_FILE, "r", encoding="utf-8") as f:
            data["llm_json"] = json.load(f)

    return jsonify(data)


if __name__ == "__main__":
    log = logging.getLogger("werkzeug")
    log.setLevel(logging.ERROR)

    safe_print("[SERVER] Running on http://127.0.0.1:5001")

    app.run(
        host="127.0.0.1",
        port=5001,
        debug=False,
        use_reloader=False,
        threaded=True
    )