import re
import unicodedata
from typing import Tuple


WAKE_PATTERNS = [
    # B D U đọc kiểu tiếng Anh: bi đi u / bi di u
    r"^\s*xin\s+chào\s+bi\s+đi\s+u\b",
    r"^\s*xin\s+chao\s+bi\s+di\s+u\b",
    r"^\s*xin\s+giao\s+bi\s+di\s+u\b",
    r"^\s*xin\s+gào\s+bi\s+đi\s+u\b",
    r"^\s*xin\s+gao\s+bi\s+di\s+u\b",

    r"^\s*hi\s+bi\s+đi\s+u\b",
    r"^\s*hi\s+bi\s+di\s+u\b",
    r"^\s*hello\s+bi\s+đi\s+u\b",
    r"^\s*hello\s+bi\s+di\s+u\b",
    r"^\s*hey\s+bi\s+đi\s+u\b",
    r"^\s*hey\s+bi\s+di\s+u\b",
    r"^\s*alo\s+bi\s+đi\s+u\b",
    r"^\s*alo\s+bi\s+di\s+u\b",
    r"^\s*alô\s+bi\s+đi\s+u\b",

    r"^\s*bi\s+đi\s+u\s+ơi\b",
    r"^\s*bi\s+di\s+u\s+oi\b",

    # BDU đọc kiểu tiếng Việt: bê đê u / be de u
    r"^\s*xin\s+chào\s+bê\s+đê\s+u\b",
    r"^\s*xin\s+chao\s+be\s+de\s+u\b",
    r"^\s*xin\s+giao\s+be\s+de\s+u\b",
    r"^\s*xin\s+gào\s+bê\s+đê\s+u\b",
    r"^\s*xin\s+gao\s+be\s+de\s+u\b",

    r"^\s*hi\s+bê\s+đê\s+u\b",
    r"^\s*hi\s+be\s+de\s+u\b",
    r"^\s*hello\s+bê\s+đê\s+u\b",
    r"^\s*hello\s+be\s+de\s+u\b",
    r"^\s*hey\s+bê\s+đê\s+u\b",
    r"^\s*hey\s+be\s+de\s+u\b",
    r"^\s*alo\s+bê\s+đê\s+u\b",
    r"^\s*alo\s+be\s+de\s+u\b",
    r"^\s*alô\s+bê\s+đê\s+u\b",

    r"^\s*bê\s+đê\s+u\s+ơi\b",
    r"^\s*be\s+de\s+u\s+oi\b",

    # STT nhận đúng chữ BDU
    r"^\s*xin\s+chào\s+bdu\b",
    r"^\s*xin\s+chao\s+bdu\b",
    r"^\s*xin\s+giao\s+bdu\b",
    r"^\s*xin\s+gào\s+bdu\b",
    r"^\s*xin\s+gao\s+bdu\b",
    r"^\s*xin\s+trao\s+bdu\b",
    r"^\s*xin\s+trào\s+bdu\b",

    r"^\s*hi\s+bdu\b",
    r"^\s*hello\s+bdu\b",
    r"^\s*hey\s+bdu\b",
    r"^\s*alo\s+bdu\b",
    r"^\s*alô\s+bdu\b",

    r"^\s*bdu\s+ơi\b",
    r"^\s*bdu\s+oi\b",

    # STT tách thành B D U
    r"^\s*xin\s+chào\s+b\s+d\s+u\b",
    r"^\s*xin\s+chao\s+b\s+d\s+u\b",
    r"^\s*xin\s+giao\s+b\s+d\s+u\b",
    r"^\s*xin\s+gào\s+b\s+d\s+u\b",
    r"^\s*xin\s+gao\s+b\s+d\s+u\b",

    r"^\s*hi\s+b\s+d\s+u\b",
    r"^\s*hello\s+b\s+d\s+u\b",
    r"^\s*hey\s+b\s+d\s+u\b",
    r"^\s*alo\s+b\s+d\s+u\b",
    r"^\s*alô\s+b\s+d\s+u\b",

    r"^\s*b\s+d\s+u\s+ơi\b",
    r"^\s*b\s+d\s+u\s+oi\b",

    # Cụm gọi dài, không cần BDU
    r"^\s*hướng\s+dẫn\s+viên\s+ơi\b",
    r"^\s*huong\s+dan\s+vien\s+oi\b",
    r"^\s*trợ\s+lý\s+ơi\b",
    r"^\s*tro\s+ly\s+oi\b",
]


LOCATION_KEYWORDS = [
    "cổng trước", "cong truoc",
    "cổng sau", "cong sau",
    "khu a",
    "khu b",
    "bãi xe", "bai xe", "nhà xe", "nha xe",

    "khu công nghệ cao", "khu cong nghe cao",
    "khu công gài cao", "khu cong gai cao",
    "công nghệ cao", "cong nghe cao",

    "aidti", "viện aidti", "vien aidti",
    "appy", "viện appy", "vien appy",
    "át ti", "at ti",
    "áp ti", "ap ti",
    "ách ti", "ach ti",

    "fira", "văn phòng khoa", "van phong khoa",
    "dslab", "ds lab",
    "smartlab", "smart lab",
    "fablab", "fab lab",

    "vườn thông minh", "vuon thong minh",
    "thư viện", "thu vien",
    "hội trường", "hoi truong",
    "phòng họp ai", "phong hop ai",
]


# Lệnh dịch chuyển thật sự.
# Lưu ý: KHÔNG để "dẫn tôi đến" ở đây,
# vì sẽ bị nhầm với "hướng dẫn tôi đến".
MOVE_PHRASES = [
    "đưa tôi tới", "dua toi toi",
    "đưa tôi đến", "dua toi den",

    "cho tôi tới", "cho toi toi",
    "cho tôi đến", "cho toi den",

    "tôi muốn tới", "toi muon toi",
    "tôi muốn đến", "toi muon den",

    "dịch chuyển tới", "dich chuyen toi",
    "dịch chuyển đến", "dich chuyen den",
    "dịch chuyển", "dich chuyen",

    "di chuyển tới", "di chuyen toi",
    "di chuyển đến", "di chuyen den",
]


# Lệnh hướng dẫn / chỉ đường.
# Có "hướng dẫn tôi đến", "dẫn tôi đến" ở đây.
ROUTE_PHRASES = [
    "chỉ đường tới", "chi duong toi",
    "chỉ đường đến", "chi duong den",

    "hướng dẫn đường tới", "huong dan duong toi",
    "hướng dẫn đường đến", "huong dan duong den",

    "hướng dẫn tôi tới", "huong dan toi toi",
    "hướng dẫn tôi đến", "huong dan toi den",

    "dẫn tôi tới", "dan toi toi",
    "dẫn tôi đến", "dan toi den",

    "đường đi tới", "duong di toi",
    "đường đi đến", "duong di den",

    "cách đi tới", "cach di toi",
    "cách đi đến", "cach di den",

    "lối đi tới", "loi di toi",
    "lối đi đến", "loi di den",
]


DESCRIBE_PHRASES = [
    "giới thiệu", "gioi thieu",
    "thông tin", "thong tin",
    "nói về", "noi ve",
    "cho tôi biết", "cho toi biet",
    "mô tả", "mo ta",
    "sơ lược", "so luoc",
    "tổng quan", "tong quan",
    "là gì", "la gi",
    "học gì", "hoc gi",
    "có gì", "co gi",
]


SUGGEST_PHRASES = [
    "gợi ý", "goi y",
    "tham quan",
    "địa điểm", "dia diem",
    "đi đâu", "di dau",
    "có gì tham quan", "co gi tham quan",
    "một điểm", "mot diem",
    "1 điểm",
    "một nơi", "mot noi",
]


INFO_PHRASES = [
    "công nghệ thông tin",
    "cong nghe thong tin",
    "cntt",
    "ngành học",
    "nganh hoc",
    "ngành công nghệ thông tin",
    "nganh cong nghe thong tin",

    "trường đại học bình dương",
    "truong dai hoc binh duong",
    "đại học bình dương",
    "dai hoc binh duong",
    "bdu",
    "giới thiệu trường",
    "gioi thieu truong",
    "giới thiệu sơ lược",
    "gioi thieu so luoc",
    "thông tin về trường",
    "thong tin ve truong",
    "lịch sử trường",
    "lich su truong",
    "địa chỉ trường",
    "dia chi truong",

    "viện aidti",
    "vien aidti",
    "aidti",
    "viện công nghệ thông tin robot trí tuệ nhân tạo",
    "vien cong nghe thong tin robot tri tue nhan tao",
    "robot",
    "trí tuệ nhân tạo",
    "tri tue nhan tao",
]


def remove_vietnamese_accents(text: str) -> str:
    if text is None:
        return ""

    text = unicodedata.normalize("NFD", text)
    text = "".join(ch for ch in text if unicodedata.category(ch) != "Mn")
    text = text.replace("đ", "d").replace("Đ", "D")
    return text


def normalize_text(text: str) -> str:
    if text is None:
        return ""

    text = text.strip().lower()
    text = re.sub(r"[,.!?;:()\[\]{}\"“”‘’]+", " ", text)
    text = re.sub(r"\s+", " ", text)
    return text.strip()


def normalize_no_accent(text: str) -> str:
    return normalize_text(remove_vietnamese_accents(text))


def contains_any(text: str, keywords: list) -> bool:
    text_norm = normalize_text(text)
    text_no_acc = normalize_no_accent(text)

    for keyword in keywords:
        kw_norm = normalize_text(keyword)
        kw_no_acc = normalize_no_accent(keyword)

        if kw_norm and kw_norm in text_norm:
            return True

        if kw_no_acc and kw_no_acc in text_no_acc:
            return True

    return False


def is_activated(user_text: str) -> bool:
    text = normalize_text(user_text)

    if not text:
        return False

    for pattern in WAKE_PATTERNS:
        if re.search(pattern, text, flags=re.IGNORECASE):
            return True

    return False


def remove_wake_word(user_text: str) -> Tuple[bool, str]:
    text = normalize_text(user_text)

    if not text:
        return False, ""

    for pattern in WAKE_PATTERNS:
        match = re.search(pattern, text, flags=re.IGNORECASE)

        if match:
            cleaned = text[match.end():].strip()
            cleaned = re.sub(r"^[,.\-:;!? ]+", "", cleaned).strip()
            return True, cleaned

    return False, text


def has_location(text: str) -> bool:
    return contains_any(text, LOCATION_KEYWORDS)


def is_info_command(text: str) -> bool:
    return contains_any(text, INFO_PHRASES)


def is_move_command(text: str) -> bool:
    return contains_any(text, MOVE_PHRASES) and has_location(text)


def is_route_command(text: str) -> bool:
    return contains_any(text, ROUTE_PHRASES) and has_location(text)


def is_describe_command(text: str) -> bool:
    return contains_any(text, DESCRIBE_PHRASES) and (has_location(text) or is_info_command(text))


def is_suggest_command(text: str) -> bool:
    return contains_any(text, SUGGEST_PHRASES)


def is_supported_command(command_text: str) -> bool:
    text = normalize_text(command_text)

    if not text:
        return False

    if is_move_command(text):
        return True

    if is_route_command(text):
        return True

    if is_describe_command(text):
        return True

    if is_suggest_command(text):
        return True

    if is_info_command(text):
        return True

    return False