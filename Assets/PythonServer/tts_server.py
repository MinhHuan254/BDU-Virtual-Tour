import os
from flask import Flask, request, jsonify, send_file
from werkzeug.utils import secure_filename

from tts_module import text_file_to_cloned_wav

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
TEMP_DIR = os.path.join(BASE_DIR, "tts_temp")
os.makedirs(TEMP_DIR, exist_ok=True)

app = Flask(__name__)
app.config["MAX_CONTENT_LENGTH"] = 50 * 1024 * 1024


@app.route("/health", methods=["GET"])
def health():
    return jsonify({"success": True, "message": "TTS server is running"})


@app.route("/tts-from-files", methods=["POST"])
def tts_from_files():
    try:
        if "text_file" not in request.files:
            return jsonify({"success": False, "error": "Missing text_file"}), 400

        if "speaker_wav" not in request.files:
            return jsonify({"success": False, "error": "Missing speaker_wav"}), 400

        text_file = request.files["text_file"]
        speaker_wav = request.files["speaker_wav"]

        text_filename = secure_filename(text_file.filename or "input.txt")
        speaker_filename = secure_filename(speaker_wav.filename or "speaker.wav")

        text_path = os.path.join(TEMP_DIR, text_filename)
        speaker_path = os.path.join(TEMP_DIR, speaker_filename)
        output_path = os.path.join(TEMP_DIR, "tts_output.wav")

        text_file.save(text_path)
        speaker_wav.save(speaker_path)

        text_file_to_cloned_wav(
            text_file_path=text_path,
            speaker_wav_path=speaker_path,
            output_wav_path=output_path,
            language="vi"
        )

        return jsonify({
            "success": True,
            "audio_url": "http://127.0.0.1:5002/audio/latest",
            "audio_path": output_path
        })

    except Exception as e:
        return jsonify({
            "success": False,
            "error": str(e)
        }), 500


@app.route("/audio/latest", methods=["GET"])
def audio_latest():
    output_path = os.path.join(TEMP_DIR, "tts_output.wav")
    if not os.path.exists(output_path):
        return jsonify({"success": False, "error": "No audio generated"}), 404

    return send_file(output_path, mimetype="audio/wav")


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=5002, debug=False)