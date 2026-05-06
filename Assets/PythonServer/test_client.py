import requests

url = "http://127.0.0.1:5000/audio"

with open("test.wav", "rb") as f:
    audio_bytes = f.read()

resp = requests.post(
    url,
    data=audio_bytes,
    headers={"Content-Type": "audio/wav"}
)

print("Status:", resp.status_code)
print("Content-Type:", resp.headers.get("Content-Type"))
print("Response length:", len(resp.content))

if resp.status_code == 200:
    with open("server_reply.mp3", "wb") as f:
        f.write(resp.content)
    print("Saved: server_reply.mp3")
else:
    print("Response text:")
    print(resp.text)