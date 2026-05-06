import asyncio
import edge_tts

VOICE = "vi-VN-HoaiMyNeural"

async def _tts_async(text: str, output_path: str):
    communicate = edge_tts.Communicate(text=text, voice=VOICE)
    await communicate.save(output_path)

def text_file_to_speech(text_file_path: str, output_path: str):
    with open(text_file_path, "r", encoding="utf-8") as f:
        text = f.read().strip()

    if not text:
        raise ValueError("Text file is empty")

    asyncio.run(_tts_async(text, output_path))

if __name__ == "__main__":
    input_file = "input.txt"
    output_file = "output.mp3"
    text_file_to_speech(input_file, output_file)
    print(f"Đã tạo audio: {output_file}")