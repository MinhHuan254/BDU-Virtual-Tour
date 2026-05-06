import os
import json
import re
import unicodedata
import random
from groq import Groq


GROQ_API_KEY = os.getenv("GROQ_API_KEY")
client = Groq(api_key=GROQ_API_KEY) if GROQ_API_KEY else None

# Ưu tiên local rule để demo ổn định, nhanh, không phụ thuộc internet.
# Nếu muốn dùng Groq cho câu hỏi ngoài phạm vi, đổi thành True.
USE_ONLINE_LLM_FOR_UNKNOWN = False


ALLOWED_LOCATIONS = [
    "cong_truoc",
    "cong_sau",
    "khu_a",
    "khu_b",
    "bai_xe",
    "khu_cong_nghe_cao",
    "vien_aidti",
    "van_phong_khoa_fira",
    "dslab",
    "smartlab",
    "fablab",
    "vuon_thong_minh",
    "thu_vien",
    "hoi_truong",
    "phong_hop_ai",
]


ALLOWED_ACTIONS = [
    "ignore",
    "answer_only",
    "suggest_locations",
    "describe_location",
    "teleport",
    "guide_route",
    "clarify",
]


def make_result(
    speech_text: str,
    action: str = "answer_only",
    target_location=None,
    locations=None,
    iot_device=None,
    iot_command=None,
    camera_id=None,
    confidence: float = 0.5,
):
    if locations is None:
        locations = []

    return {
        "speech_text": speech_text,
        "action": action,
        "target_location": target_location,
        "locations": locations,
        "iot_device": iot_device,
        "iot_command": iot_command,
        "camera_id": camera_id,
        "confidence": confidence,
    }


def pick_response(responses: list) -> str:
    if not responses:
        return "Tôi chưa có thông tin phù hợp để trả lời câu hỏi này."

    return random.choice(responses)


# ============================================================
# TRI THỨC NỀN CHO HƯỚNG DẪN VIÊN ẢO
# ============================================================

BDU_INFO = {
    "name": "Trường Đại học Bình Dương",
    "short_name": "BDU",
    "established": "ngày 24 tháng 9 năm 1997",
    "address": "số 504 Đại lộ Bình Dương, phường Phú Lợi, Thành phố Hồ Chí Minh",
    "responses": [
        (
            "Trường Đại học Bình Dương, còn gọi là BDU, được thành lập ngày 24 tháng 9 năm 1997. "
            "Trường là cơ sở giáo dục đại học đào tạo đa ngành, hướng đến mô hình giáo dục mở, "
            "gắn kiến thức với thực tiễn và phát triển năng lực nghề nghiệp cho người học. "
            "Trong hệ thống tham quan ba chiều này, tôi có thể giới thiệu thông tin về trường, "
            "ngành học, các khu vực trong khuôn viên và đưa bạn đến địa điểm cần tham quan."
        ),
        (
            "BDU là tên viết tắt của Trường Đại học Bình Dương. Đây là một cơ sở giáo dục đại học "
            "được thành lập từ năm 1997, có định hướng đào tạo đa ngành và chú trọng tính ứng dụng thực tế. "
            "Khi tham quan trong không gian ba chiều, bạn có thể hỏi tôi về các khu vực trong trường, "
            "ngành Công nghệ Thông tin, Viện AIDTI hoặc yêu cầu tôi đưa bạn đến một địa điểm cụ thể."
        ),
        (
            "Trường Đại học Bình Dương là một môi trường đào tạo đại học với định hướng kết hợp giữa kiến thức, "
            "kỹ năng và thực hành. Trong mô hình tham quan ảo này, tôi đóng vai trò là hướng dẫn viên, "
            "có thể hỗ trợ bạn tìm hiểu tổng quan về trường, giới thiệu các khu vực chính và hướng dẫn bạn di chuyển "
            "đến những điểm như thư viện, Khu A, hội trường hoặc Viện AIDTI."
        ),
        (
            "Đại học Bình Dương là một trường đại học đào tạo đa lĩnh vực, hướng đến việc giúp người học phát triển "
            "kiến thức chuyên môn, kỹ năng thực hành và khả năng thích ứng với môi trường làm việc hiện đại. "
            "Trong chuyến tham quan ba chiều này, tôi có thể giới thiệu sơ lược về trường, các ngành học và những khu vực "
            "tiêu biểu trong khuôn viên."
        ),
    ],
}


BDU_ADDRESS_RESPONSES = [
    (
        "Theo thông tin công khai của trường, cơ sở chính của Trường Đại học Bình Dương đặt tại "
        "số 504 Đại lộ Bình Dương, phường Phú Lợi, Thành phố Hồ Chí Minh."
    ),
    (
        "Cơ sở chính của BDU hiện được giới thiệu tại số 504 Đại lộ Bình Dương, phường Phú Lợi, "
        "Thành phố Hồ Chí Minh. Đây là thông tin địa chỉ chính để người học và khách tham quan liên hệ với trường."
    ),
]


BDU_HISTORY_RESPONSES = [
    (
        "Trường Đại học Bình Dương được thành lập ngày 24 tháng 9 năm 1997 theo Quyết định 791 của Thủ tướng Chính phủ. "
        "Từ khi thành lập, trường từng bước phát triển hoạt động đào tạo, nghiên cứu và phục vụ cộng đồng."
    ),
    (
        "BDU bắt đầu quá trình hình thành và phát triển từ năm 1997. Qua nhiều giai đoạn, trường mở rộng hoạt động đào tạo, "
        "phát triển cơ sở học tập và hướng đến việc gắn giáo dục với nhu cầu thực tiễn."
    ),
    (
        "Về lịch sử, Trường Đại học Bình Dương được thành lập vào năm 1997. Quá trình phát triển của trường gắn với định hướng "
        "mở rộng cơ hội học tập, đào tạo nguồn nhân lực và kết nối hoạt động giáo dục với thực tiễn xã hội."
    ),
]


IT_INFO = {
    "responses": [
        (
            "Ngành Công nghệ Thông tin đào tạo kiến thức về lập trình, cơ sở dữ liệu, mạng máy tính, "
            "trí tuệ nhân tạo, phát triển phần mềm và ứng dụng công nghệ vào thực tế. "
            "Sinh viên ngành này cần rèn luyện tư duy logic, kỹ năng lập trình, phân tích hệ thống, "
            "làm việc nhóm và khả năng tự học công nghệ mới."
        ),
        (
            "Công nghệ Thông tin là ngành học phù hợp với sinh viên yêu thích lập trình, phần mềm, dữ liệu, "
            "mạng máy tính và trí tuệ nhân tạo. Khi học ngành này, sinh viên không chỉ học lý thuyết mà còn cần "
            "thực hành xây dựng hệ thống, phát triển ứng dụng và giải quyết các bài toán công nghệ trong thực tế."
        ),
        (
            "Ngành Công nghệ Thông tin tập trung vào việc xây dựng và vận hành các hệ thống phần mềm, "
            "xử lý dữ liệu, phát triển ứng dụng, nghiên cứu trí tuệ nhân tạo và quản trị hệ thống mạng. "
            "Đây là ngành có tính ứng dụng cao, phù hợp với các hướng nghề nghiệp như lập trình viên, "
            "phân tích hệ thống, kỹ sư dữ liệu hoặc phát triển ứng dụng AI."
        ),
        (
            "Nếu bạn quan tâm đến ngành Công nghệ Thông tin, đây là ngành học liên quan nhiều đến lập trình, "
            "thiết kế phần mềm, dữ liệu, trí tuệ nhân tạo, mạng máy tính và hệ thống thông tin. "
            "Ngành này đòi hỏi khả năng tư duy logic, giải quyết vấn đề và cập nhật công nghệ liên tục."
        ),
    ],
}


AIDTI_INFO = {
    "responses": [
        (
            "Viện AIDTI là Viện Công nghệ Thông tin, Robot và Trí tuệ nhân tạo của Trường Đại học Bình Dương. "
            "Đây là đơn vị gắn với các hoạt động học tập, nghiên cứu và trải nghiệm công nghệ như lập trình, "
            "robot, trí tuệ nhân tạo và các mô hình ứng dụng thông minh. "
            "Nếu muốn, tôi có thể đưa bạn đến vị trí Viện AIDTI trong không gian ba chiều."
        ),
        (
            "AIDTI là viện liên quan đến các lĩnh vực Công nghệ Thông tin, Robot và Trí tuệ nhân tạo tại BDU. "
            "Khu vực này phù hợp để giới thiệu các hoạt động nghiên cứu, thực hành công nghệ, mô hình robot, "
            "AI và các sản phẩm ứng dụng thông minh trong môi trường học tập."
        ),
        (
            "Viện AIDTI có thể xem là một điểm nổi bật về công nghệ trong hệ thống tham quan này. "
            "Tại đây, người tham quan có thể tìm hiểu các hướng như lập trình, trí tuệ nhân tạo, robot, "
            "dữ liệu và các mô hình ứng dụng công nghệ hiện đại. Tôi có thể giới thiệu thêm hoặc đưa bạn đến Viện AIDTI."
        ),
        (
            "Viện AIDTI là nơi gắn với các nội dung công nghệ hiện đại như Công nghệ Thông tin, Robot và Trí tuệ nhân tạo. "
            "Trong chuyến tham quan ảo, đây là một điểm phù hợp để giới thiệu các hoạt động học tập, nghiên cứu, "
            "thực hành và ứng dụng công nghệ của nhà trường."
        ),
    ],
}


GREETING_RESPONSES = [
    (
        "Xin chào. Tôi là hướng dẫn viên ảo của Trường Đại học Bình Dương. "
        "Tôi có thể giới thiệu thông tin về trường, ngành Công nghệ Thông tin, Viện AIDTI, "
        "các khu vực trong khuôn viên, hoặc đưa bạn đến địa điểm cần tham quan."
    ),
    (
        "Chào bạn. Tôi là hướng dẫn viên ảo trong không gian tham quan ba chiều của BDU. "
        "Bạn có thể hỏi tôi về Trường Đại học Bình Dương, ngành Công nghệ Thông tin, Viện AIDTI, "
        "hoặc yêu cầu tôi đưa bạn đến một khu vực trong trường."
    ),
    (
        "Xin chào. Tôi có thể hỗ trợ bạn tham quan Trường Đại học Bình Dương. "
        "Bạn có thể yêu cầu giới thiệu về trường, hỏi thông tin ngành Công nghệ Thông tin, "
        "tìm hiểu Viện AIDTI hoặc chọn một địa điểm để tôi đưa bạn đến."
    ),
    (
        "Chào bạn. Tôi là trợ lý hướng dẫn tham quan trong mô hình ba chiều của BDU. "
        "Tôi có thể giới thiệu các điểm tham quan, cung cấp thông tin về trường, ngành Công nghệ Thông tin "
        "và hỗ trợ bạn di chuyển đến các khu vực như thư viện, hội trường hoặc Viện AIDTI."
    ),
]


LOCATION_RULES = [
    {
        "id": "cong_truoc",
        "name": "cổng trước",
        "tts_name": "cổng trước",
        "keywords": [
            "cổng trước", "cong truoc", "cỗng trước", "cổng chính", "cong chinh",
            "cửa trước", "cua truoc", "cửa chính", "cua chinh",
        ],
        "responses": [
            (
                "Cổng trước là khu vực lối vào chính, thường được dùng làm điểm bắt đầu khi tham quan trường. "
                "Từ đây, người tham quan có thể hình dung tổng quan hướng di chuyển đến các khu vực bên trong."
            ),
            (
                "Đây là khu vực cổng trước của trường, phù hợp để bắt đầu chuyến tham quan. "
                "Từ vị trí này, bạn có thể tiếp tục di chuyển đến Khu A, Khu B, thư viện hoặc các khu vực khác trong trường."
            ),
        ],
    },
    {
        "id": "cong_sau",
        "name": "cổng sau",
        "tts_name": "cổng sau",
        "keywords": ["cổng sau", "cong sau", "cỗng sau", "cửa sau", "cua sau"],
        "responses": [
            (
                "Cổng sau là một lối ra vào khác của khuôn viên trường, hỗ trợ việc di chuyển giữa các khu vực "
                "và giúp phân luồng giao thông trong trường thuận tiện hơn."
            ),
            (
                "Khu vực cổng sau giúp kết nối việc ra vào trường theo một hướng khác. "
                "Đây là điểm có thể dùng để định hướng khi tham quan hoặc di chuyển trong mô hình ba chiều."
            ),
        ],
    },
    {
        "id": "khu_a",
        "name": "Khu A",
        "tts_name": "Khu A",
        "keywords": ["khu a", "khu á", "khu ạ", "tòa a", "toa a", "dãy a", "day a"],
        "responses": [
            (
                "Khu A là một trong các khu vực học tập và làm việc chính của trường. "
                "Khu vực này có thể được dùng để giới thiệu các hoạt động đào tạo, phòng học, "
                "không gian làm việc và các điểm hỗ trợ sinh viên."
            ),
            (
                "Khu A là một điểm tham quan quan trọng trong khuôn viên BDU. "
                "Tại đây, người tham quan có thể hình dung các hoạt động học tập, làm việc và sinh hoạt học đường của sinh viên."
            ),
            (
                "Đây là Khu A, một khu vực tiêu biểu trong mô hình tham quan của trường. "
                "Nếu bạn muốn, tôi có thể đưa bạn đến Khu A hoặc giới thiệu thêm các khu vực gần đó."
            ),
        ],
    },
    {
        "id": "khu_b",
        "name": "Khu B",
        "tts_name": "Khu B",
        "keywords": ["khu b", "khu bê", "tòa b", "toa b", "dãy b", "day b"],
        "responses": [
            (
                "Khu B là khu vực phục vụ hoạt động học tập, sinh hoạt và di chuyển trong khuôn viên trường. "
                "Trong mô hình ba chiều, đây là một trong các điểm tham quan chính của hệ thống."
            ),
            (
                "Đây là Khu B của trường. Khu vực này có thể được giới thiệu như một phần trong không gian học tập "
                "và tham quan tổng thể của BDU."
            ),
        ],
    },
    {
        "id": "bai_xe",
        "name": "bãi xe",
        "tts_name": "bãi xe",
        "keywords": ["bãi xe", "bai xe", "nhà xe", "nha xe", "bãi đậu xe", "bai dau xe"],
        "responses": [
            (
                "Bãi xe là khu vực phục vụ nhu cầu gửi xe của sinh viên, giảng viên và khách đến trường. "
                "Đây là một điểm quan trọng trong việc tổ chức giao thông nội bộ."
            ),
            (
                "Khu vực bãi xe hỗ trợ việc gửi phương tiện và di chuyển trong khuôn viên trường. "
                "Trong mô hình tham quan, đây là một điểm tiện ích cần thiết đối với sinh viên và khách tham quan."
            ),
        ],
    },
    {
        "id": "khu_cong_nghe_cao",
        "name": "Khu công nghệ cao",
        "tts_name": "Khu công nghệ cao",
        "keywords": [
            "khu công nghệ cao", "khu cong nghe cao", "công nghệ cao", "cong nghe cao",
            "khu công gài cao", "khu công gái cao", "khu công ngại cao",
            "khu công nghẹ cao", "khu công nghệ câu", "khu công nghệ khao",
            "khu cong gai cao", "khu cong ngai cao", "khu cong nghe cau", "khu cnc", "cnc",
        ],
        "responses": [
            (
                "Khu công nghệ cao là khu vực phù hợp để giới thiệu các hoạt động liên quan đến công nghệ, "
                "nghiên cứu, thực hành và ứng dụng kỹ thuật hiện đại trong nhà trường."
            ),
            (
                "Đây là Khu công nghệ cao, một điểm tham quan phù hợp với các nội dung về kỹ thuật, thực hành, "
                "nghiên cứu và ứng dụng công nghệ. Khu vực này giúp người tham quan hình dung rõ hơn định hướng công nghệ của trường."
            ),
        ],
    },
    {
        "id": "vien_aidti",
        "name": "Viện AIDTI",
        "tts_name": "Viện AIDTI",
        "keywords": [
            "aidti", "viện aidti", "vien aidti", "a i d t i", "aid t i",
            "ây ai đi ti ai", "viện appy", "vien appy", "appy",
            "viện áp ti", "vien ap ti", "áp ti", "ap ti",
            "viện át ti", "vien at ti", "át ti", "at ti",
            "viện ách ti", "vien ach ti", "ách ti", "ach ti",
            "viện ái đi ti", "vien ai di ti",
            "viện công nghệ thông tin robot trí tuệ nhân tạo",
            "vien cong nghe thong tin robot tri tue nhan tao",
        ],
        "responses": AIDTI_INFO["responses"],
    },
    {
        "id": "van_phong_khoa_fira",
        "name": "Văn phòng Khoa FIRA",
        "tts_name": "Văn phòng Khoa FIRA",
        "keywords": [
            "fira", "văn phòng khoa", "van phong khoa", "khoa fira",
            "văn phòng fira", "van phong fira", "phi ra",
        ],
        "responses": [
            (
                "Văn phòng Khoa FIRA là khu vực hỗ trợ các hoạt động quản lý, trao đổi thông tin, "
                "học vụ và kết nối giữa sinh viên với khoa hoặc đơn vị chuyên môn."
            ),
            (
                "Đây là Văn phòng Khoa FIRA. Khu vực này có vai trò hỗ trợ sinh viên trong các hoạt động liên quan đến học vụ, "
                "trao đổi thông tin và kết nối với đơn vị chuyên môn."
            ),
        ],
    },
    {
        "id": "dslab",
        "name": "DSLAB",
        "tts_name": "DSLAB",
        "keywords": ["dslab", "ds lab", "d s lab", "dê ét lab", "dee es lab", "đi ét lab"],
        "responses": [
            (
                "DSLAB là không gian phòng thí nghiệm hoặc thực hành liên quan đến dữ liệu, phần mềm "
                "và các hoạt động nghiên cứu ứng dụng."
            ),
            (
                "DSLAB là khu vực phù hợp để giới thiệu các hoạt động thực hành về dữ liệu, phần mềm và công nghệ. "
                "Đây là một điểm tham quan có liên quan đến hướng học tập thực nghiệm."
            ),
        ],
    },
    {
        "id": "smartlab",
        "name": "SMARTLAB",
        "tts_name": "SMARTLAB",
        "keywords": ["smartlab", "smart lab", "phòng smartlab", "phong smartlab", "sờ mát lab"],
        "responses": [
            (
                "SMARTLAB là không gian thực hành thông minh, phù hợp để giới thiệu các mô hình ứng dụng công nghệ, "
                "thiết bị thông minh và hệ thống hỗ trợ học tập hiện đại."
            ),
            (
                "Đây là SMARTLAB, khu vực gắn với các mô hình công nghệ thông minh và hoạt động thực hành. "
                "Người tham quan có thể tìm hiểu thêm về các hệ thống ứng dụng công nghệ tại đây."
            ),
        ],
    },
    {
        "id": "fablab",
        "name": "FABLAB",
        "tts_name": "FABLAB",
        "keywords": ["fablab", "fab lab", "fablas", "phòng fablab", "phong fablab", "pháp lab"],
        "responses": [
            (
                "FABLAB là không gian chế tạo, thực hành và thử nghiệm ý tưởng. "
                "Khu vực này phù hợp với các hoạt động sáng tạo, mô hình hóa, thiết kế và phát triển sản phẩm mẫu."
            ),
            (
                "Đây là FABLAB, nơi phù hợp cho các hoạt động sáng tạo và thực hành chế tạo. "
                "Khu vực này giúp sinh viên thử nghiệm ý tưởng, phát triển mô hình và tiếp cận quy trình tạo sản phẩm mẫu."
            ),
        ],
    },
    {
        "id": "vuon_thong_minh",
        "name": "vườn thông minh",
        "tts_name": "vườn thông minh",
        "keywords": ["vườn thông minh", "vuon thong minh", "vườn", "vuon", "khu vườn", "khu vuon"],
        "responses": [
            (
                "Vườn thông minh là khu vực có thể giới thiệu các ứng dụng công nghệ trong nông nghiệp, "
                "môi trường hoặc mô hình IoT, giúp người tham quan hình dung việc áp dụng công nghệ vào đời sống."
            ),
            (
                "Đây là vườn thông minh, một điểm tham quan cho thấy cách công nghệ có thể được ứng dụng vào môi trường, "
                "nông nghiệp và các hệ thống giám sát tự động."
            ),
        ],
    },
    {
        "id": "thu_vien",
        "name": "thư viện",
        "tts_name": "thư viện",
        "keywords": ["thư viện", "thu vien", "thư viên", "thu viên", "thư việt", "library"],
        "responses": [
            (
                "Thư viện là nơi cung cấp tài liệu học tập, không gian tự học và hỗ trợ tra cứu thông tin cho sinh viên. "
                "Đây là một điểm tham quan phù hợp để giới thiệu môi trường học tập và nghiên cứu của nhà trường."
            ),
            (
                "Thư viện là không gian phục vụ việc học tập, đọc tài liệu và tự nghiên cứu. "
                "Đối với sinh viên, đây là nơi hỗ trợ quá trình học, tìm kiếm thông tin và rèn luyện khả năng tự học."
            ),
            (
                "Đây là thư viện của trường. Khu vực này thường được xem là một điểm quan trọng trong môi trường học tập, "
                "giúp sinh viên tiếp cận tài liệu, tra cứu thông tin và có không gian tự học."
            ),
        ],
    },
    {
        "id": "hoi_truong",
        "name": "hội trường",
        "tts_name": "hội trường",
        "keywords": ["hội trường", "hoi truong", "hội chường", "hồi trường", "auditorium"],
        "responses": [
            (
                "Hội trường là nơi tổ chức các sự kiện, hội thảo, sinh hoạt chuyên đề, lễ khai giảng, "
                "tổng kết hoặc các hoạt động quy mô lớn của trường."
            ),
            (
                "Đây là hội trường, khu vực phù hợp cho các sự kiện tập trung đông người như hội thảo, chương trình sinh hoạt, "
                "lễ khai giảng hoặc các hoạt động giao lưu trong nhà trường."
            ),
        ],
    },
    {
        "id": "phong_hop_ai",
        "name": "phòng họp AI",
        "tts_name": "phòng họp AI",
        "keywords": ["phòng họp ai", "phong hop ai", "phòng họp", "phong hop", "phòng ai", "phong ai", "phòng học ai"],
        "responses": [
            (
                "Phòng họp AI là khu vực phù hợp để giới thiệu hoạt động trao đổi học thuật, họp nhóm, "
                "thảo luận đề tài và các nội dung liên quan đến trí tuệ nhân tạo."
            ),
            (
                "Đây là phòng họp AI, nơi phù hợp cho các buổi trao đổi chuyên môn, làm việc nhóm và thảo luận các nội dung "
                "liên quan đến trí tuệ nhân tạo hoặc các đề tài công nghệ."
            ),
        ],
    },
]


GENERAL_TOPICS = {
    "bdu_address": {
        "keywords": [
            "địa chỉ trường", "dia chi truong", "trường ở đâu", "truong o dau",
            "đại học bình dương ở đâu", "dai hoc binh duong o dau",
            "địa chỉ bdu", "dia chi bdu",
        ],
        "responses": BDU_ADDRESS_RESPONSES,
    },
    "bdu_history": {
        "keywords": [
            "lịch sử", "lich su", "thành lập", "thanh lap",
            "hình thành", "hinh thanh", "phát triển", "phat trien",
            "trường thành lập khi nào", "truong thanh lap khi nao",
        ],
        "responses": BDU_HISTORY_RESPONSES,
    },
    "it": {
        "keywords": [
            "công nghệ thông tin", "cong nghe thong tin", "cntt",
            "ngành công nghệ thông tin", "nganh cong nghe thong tin",
            "ngành cntt", "nganh cntt",
            "học công nghệ thông tin", "hoc cong nghe thong tin",
            "lập trình", "lap trinh", "phần mềm", "phan mem",
        ],
        "responses": IT_INFO["responses"],
    },
    "aidti": {
        "keywords": [
            "viện aidti", "vien aidti", "aidti", "viện appy", "vien appy",
            "viện át ti", "vien at ti", "át ti", "at ti",
            "viện công nghệ thông tin robot trí tuệ nhân tạo",
            "vien cong nghe thong tin robot tri tue nhan tao",
            "robot", "trí tuệ nhân tạo", "tri tue nhan tao",
        ],
        "responses": AIDTI_INFO["responses"],
    },
    "bdu": {
        "keywords": [
            "trường đại học bình dương", "truong dai hoc binh duong",
            "đại học bình dương", "dai hoc binh duong",
            "bdu", "giới thiệu trường", "gioi thieu truong",
            "giới thiệu sơ lược", "gioi thieu so luoc",
            "thông tin về trường", "thong tin ve truong",
            "trường này", "truong nay",
        ],
        "responses": BDU_INFO["responses"],
    },
}


SYSTEM_PROMPT = """
Bạn là hướng dẫn viên ảo trong môi trường Unity 3D của Trường Đại học Bình Dương.
Luôn trả lời ngắn gọn, rõ ràng, tự nhiên, phù hợp để đọc bằng TTS.
Không mở camera. Không điều khiển IoT. Không bịa địa điểm ngoài danh sách.
Luôn trả về JSON hợp lệ.
"""


# ============================================================
# NORMALIZE / MATCHING
# ============================================================

def remove_vietnamese_accents(text: str) -> str:
    if text is None:
        return ""

    text = unicodedata.normalize("NFD", text)
    text = "".join(ch for ch in text if unicodedata.category(ch) != "Mn")
    text = text.replace("đ", "d").replace("Đ", "D")

    return text


def normalize_common_stt_errors(text: str) -> str:
    if text is None:
        return ""

    replacements = {
        "khu công gài cao": "khu công nghệ cao",
        "khu công gái cao": "khu công nghệ cao",
        "khu công ngại cao": "khu công nghệ cao",
        "khu công nghệ câu": "khu công nghệ cao",
        "khu công nghẹ cao": "khu công nghệ cao",
        "khu công nghệ khao": "khu công nghệ cao",
        "khu cong gai cao": "khu cong nghe cao",
        "khu cong ngai cao": "khu cong nghe cao",
        "khu cong nghe cau": "khu cong nghe cao",
        "thư viên": "thư viện",
        "thư việt": "thư viện",
        "thu vien": "thư viện",
        "hội chường": "hội trường",
        "hội trườn": "hội trường",
        "cỗng trước": "cổng trước",
        "cỗng sau": "cổng sau",
        "vườn thông mình": "vườn thông minh",
        "phòng hợp ai": "phòng họp ai",
        "phòng học ai": "phòng họp ai",
        "viện appy": "viện aidti",
        "vien appy": "vien aidti",
        "appy": "aidti",
        "viện áp ti": "viện aidti",
        "vien ap ti": "vien aidti",
        "áp ti": "aidti",
        "ap ti": "aidti",
        "viện át ti": "viện aidti",
        "vien at ti": "vien aidti",
        "át ti": "aidti",
        "at ti": "aidti",
        "viện ách ti": "viện aidti",
        "vien ach ti": "vien aidti",
        "ách ti": "aidti",
        "ach ti": "aidti",
    }

    fixed = text.lower()

    for wrong, right in replacements.items():
        fixed = fixed.replace(wrong, right)

    return fixed


def normalize_text(text: str) -> str:
    text = normalize_common_stt_errors(text)
    text = (text or "").strip().lower()
    text = re.sub(r"[,.!?;:()\[\]{}\"“”‘’]+", " ", text)
    text = re.sub(r"\s+", " ", text)
    return text.strip()


def normalize_text_no_accent(text: str) -> str:
    text = normalize_text(text)
    text = remove_vietnamese_accents(text)
    text = re.sub(r"\s+", " ", text)
    return text.strip()


def contains_any(text: str, text_no_accent: str, keywords: list) -> bool:
    for kw in keywords:
        kw_norm = normalize_text(kw)
        kw_no_acc = normalize_text_no_accent(kw)

        if kw_norm and kw_norm in text:
            return True

        if kw_no_acc and kw_no_acc in text_no_accent:
            return True

    return False


def is_greeting(text: str, text_no_accent: str) -> bool:
    greetings = [
        "xin chào",
        "chào",
        "chào bạn",
        "hi",
        "hello",
        "alo",
        "alô",
        "hey",
    ]

    greeting_no_accents = [normalize_text_no_accent(g) for g in greetings]

    return text in greetings or text_no_accent in greeting_no_accents


def is_route_request(text: str, text_no_accent: str) -> bool:
    return contains_any(
        text,
        text_no_accent,
        [
            "hướng dẫn tôi đến",
            "huong dan toi den",
            "hướng dẫn tôi tới",
            "huong dan toi toi",

            "hướng dẫn đường",
            "huong dan duong",

            "chỉ đường",
            "chi duong",

            "đường đi",
            "duong di",

            "cách đi",
            "cach di",

            "lối đi",
            "loi di",

            "dẫn đường",
            "dan duong",

            "đi như thế nào",
            "di nhu the nao",

            "đi đường nào",
            "di duong nao",

            "hướng đi",
            "huong di",

            # Ưu tiên hiểu "dẫn tôi..." là chỉ đường, không teleport.
            "dẫn tôi đến",
            "dan toi den",
            "dẫn tôi tới",
            "dan toi toi",
        ],
    )


def is_move_request(text: str, text_no_accent: str) -> bool:
    # Nếu câu có ngữ nghĩa hướng dẫn/chỉ đường,
    # tuyệt đối không hiểu là teleport.
    route_like_phrases = [
        "hướng dẫn tôi đến",
        "huong dan toi den",
        "hướng dẫn tôi tới",
        "huong dan toi toi",

        "hướng dẫn đường",
        "huong dan duong",

        "chỉ đường",
        "chi duong",

        "cách đi",
        "cach di",

        "đường đi",
        "duong di",

        "lối đi",
        "loi di",

        "dẫn đường",
        "dan duong",

        "dẫn tôi đến",
        "dan toi den",
        "dẫn tôi tới",
        "dan toi toi",
    ]

    if contains_any(text, text_no_accent, route_like_phrases):
        return False

    return contains_any(
        text,
        text_no_accent,
        [
            "hãy đưa tôi đến",
            "hay dua toi den",
            "hãy đưa tôi tới",
            "hay dua toi toi",

            "đưa tôi tới",
            "dua toi toi",
            "đưa tôi đến",
            "dua toi den",

            "cho tôi tới",
            "cho toi toi",
            "cho tôi đến",
            "cho toi den",

            "tôi muốn tới",
            "toi muon toi",
            "tôi muốn đến",
            "toi muon den",

            "muốn tới",
            "muon toi",
            "muốn đến",
            "muon den",

            "dịch chuyển",
            "dich chuyen",

            "di chuyển",
            "di chuyen",

            "đi tới",
            "di toi",
            "đi đến",
            "di den",
        ],
    )


def is_suggest_request(text: str, text_no_accent: str) -> bool:
    return contains_any(
        text,
        text_no_accent,
        [
            "tham quan",
            "địa điểm",
            "đi đâu",
            "có gì",
            "gợi ý",
            "một điểm",
            "1 điểm",
            "một nơi",
            "điểm nào",
            "nơi nào",
        ],
    )


def is_describe_request(text: str, text_no_accent: str) -> bool:
    return contains_any(
        text,
        text_no_accent,
        [
            "giới thiệu",
            "thông tin",
            "nói về",
            "cho tôi biết",
            "mô tả",
            "kể về",
            "sơ lược",
            "tổng quan",
            "là gì",
            "la gi",
            "học gì",
            "hoc gi",
            "có gì",
            "co gi",
        ],
    )


def contains_unsupported_request(text: str, text_no_accent: str) -> bool:
    return contains_any(
        text,
        text_no_accent,
        [
            "camera",
            "mở camera",
            "xem camera",
            "bật đèn",
            "tắt đèn",
            "máy lạnh",
            "điều khiển",
            "iot",
        ],
    )


def find_location(text: str, text_no_accent: str):
    for loc in LOCATION_RULES:
        if contains_any(text, text_no_accent, loc["keywords"]):
            return loc

    return None


def find_general_topic(text: str, text_no_accent: str):
    for topic_id, topic in GENERAL_TOPICS.items():
        if contains_any(text, text_no_accent, topic["keywords"]):
            return topic_id, topic

    return None, None


def display_name_for_tts(loc: dict) -> str:
    return loc.get("tts_name") or loc.get("name") or ""


def location_description(loc: dict) -> str:
    if "responses" in loc and isinstance(loc["responses"], list):
        return pick_response(loc["responses"])

    return loc.get("description") or "Đây là một địa điểm trong khuôn viên Trường Đại học Bình Dương."


# ============================================================
# RESPONSE HELPERS
# ============================================================

def suggest_locations_result(one_location: bool = False) -> dict:
    if one_location:
        responses = [
            (
                "Tôi gợi ý bạn tham quan thư viện. Đây là nơi phù hợp để tìm hiểu không gian học tập, "
                "tài liệu và khu vực tự học của sinh viên. Nếu muốn đi đến đó, bạn có thể nói: đưa tôi tới thư viện."
            ),
            (
                "Bạn có thể bắt đầu với thư viện. Đây là một điểm tham quan dễ giới thiệu, vì nó gắn với hoạt động học tập, "
                "tra cứu tài liệu và tự nghiên cứu của sinh viên."
            ),
            (
                "Tôi đề xuất bạn tham quan Viện AIDTI. Đây là khu vực phù hợp nếu bạn quan tâm đến Công nghệ Thông tin, "
                "Robot, Trí tuệ nhân tạo và các hoạt động ứng dụng công nghệ."
            ),
        ]

        return make_result(
            speech_text=pick_response(responses),
            action="suggest_locations",
            target_location=None,
            locations=["thu_vien", "vien_aidti"],
            confidence=0.85,
        )

    responses = [
        (
            "Bạn có thể tham quan cổng trước, cổng sau, Khu A, Khu B, bãi xe, "
            "Khu công nghệ cao, Viện AIDTI, Văn phòng Khoa FIRA, DSLAB, SMARTLAB, "
            "FABLAB, vườn thông minh, thư viện, hội trường và phòng họp AI."
        ),
        (
            "Một số điểm tham quan chính gồm Khu A, Khu B, thư viện, hội trường, Khu công nghệ cao, "
            "Viện AIDTI, DSLAB, SMARTLAB, FABLAB và vườn thông minh. Bạn có thể yêu cầu tôi giới thiệu "
            "hoặc đưa bạn đến một trong các địa điểm này."
        ),
        (
            "Trong mô hình ba chiều này, bạn có thể tham quan các khu vực như cổng trước, thư viện, hội trường, "
            "Khu công nghệ cao, Viện AIDTI, phòng họp AI và các phòng thực hành như DSLAB, SMARTLAB, FABLAB."
        ),
    ]

    return make_result(
        speech_text=pick_response(responses),
        action="suggest_locations",
        locations=ALLOWED_LOCATIONS,
        confidence=0.85,
    )


# ============================================================
# MAIN LOCAL INTENT
# ============================================================

def local_intent_response(user_text: str):
    text = normalize_text(user_text)
    text_no_accent = normalize_text_no_accent(user_text)

    print(f"[LOCAL INTENT] text={repr(text)}", flush=True)
    print(f"[LOCAL INTENT] text_no_accent={repr(text_no_accent)}", flush=True)

    if not text:
        return make_result(
            speech_text="Tôi chưa nghe rõ. Bạn có thể nói lại được không?",
            action="clarify",
            confidence=0.0,
        )

    if is_greeting(text, text_no_accent):
        return make_result(
            speech_text=pick_response(GREETING_RESPONSES),
            action="answer_only",
            confidence=0.95,
        )

    if contains_unsupported_request(text, text_no_accent):
        responses = [
            (
                "Hiện tại tôi chưa hỗ trợ mở camera hoặc điều khiển thiết bị. "
                "Tôi có thể giới thiệu thông tin về trường, ngành học, Viện AIDTI, "
                "hoặc đưa bạn đến một địa điểm trong khuôn viên."
            ),
            (
                "Chức năng camera và điều khiển thiết bị hiện chưa được kích hoạt. "
                "Tôi có thể hỗ trợ bạn theo hướng tham quan, giới thiệu địa điểm, chỉ đường hoặc cung cấp thông tin về BDU."
            ),
        ]

        return make_result(
            speech_text=pick_response(responses),
            action="clarify",
            confidence=0.85,
        )

    loc = find_location(text, text_no_accent)

    route_request = is_route_request(text, text_no_accent)
    move_request = is_move_request(text, text_no_accent)
    describe_request = is_describe_request(text, text_no_accent)

    print(
        f"[LOCAL INTENT] route={route_request}, move={move_request}, describe={describe_request}, "
        f"loc={loc['id'] if loc else None}",
        flush=True,
    )

    if loc is not None:
        loc_id = loc["id"]
        loc_name = display_name_for_tts(loc)

        # Ưu tiên route trước move.
        # "hướng dẫn tôi đến..." phải ra guide_route.
        if route_request:
            responses = [
                f"Tôi sẽ hướng dẫn bạn đi đến {loc_name}.",
                f"Được, tôi sẽ chỉ đường đến {loc_name}.",
                f"Tôi đã xác định điểm đến là {loc_name}. Tôi sẽ hướng dẫn bạn di chuyển đến đó.",
            ]

            return make_result(
                speech_text=pick_response(responses),
                action="guide_route",
                target_location=loc_id,
                confidence=0.9,
            )

        if move_request:
            responses = [
                f"Được, tôi sẽ đưa bạn đến {loc_name}.",
                f"Tôi sẽ chuyển bạn đến {loc_name}.",
                f"Đã rõ. Tôi sẽ đưa bạn tới {loc_name} trong không gian tham quan ba chiều.",
            ]

            return make_result(
                speech_text=pick_response(responses),
                action="teleport",
                target_location=loc_id,
                confidence=0.95,
            )

        if describe_request:
            return make_result(
                speech_text=location_description(loc),
                action="describe_location",
                target_location=loc_id,
                confidence=0.9,
            )

        return make_result(
            speech_text=(
                f"Bạn đang nhắc đến {loc_name}. {location_description(loc)} "
                "Bạn muốn tôi giới thiệu thêm, chỉ đường hay đưa bạn đến địa điểm này?"
            ),
            action="clarify",
            target_location=loc_id,
            confidence=0.8,
        )

    if route_request:
        responses = [
            (
                "Bạn muốn tôi chỉ đường đến địa điểm nào? "
                "Bạn có thể nói: chỉ đường tới thư viện, Khu A, hội trường hoặc Viện AIDTI."
            ),
            (
                "Tôi có thể chỉ đường, nhưng bạn cần cho biết điểm đến cụ thể. "
                "Ví dụ: chỉ đường đến thư viện hoặc chỉ đường đến Viện AIDTI."
            ),
            (
                "Tôi hiểu là bạn cần hướng dẫn đường đi. Bạn hãy nói rõ địa điểm cần đến, ví dụ: "
                "hướng dẫn tôi đến thư viện hoặc hướng dẫn tôi đến Khu A."
            ),
        ]

        return make_result(
            speech_text=pick_response(responses),
            action="clarify",
            confidence=0.75,
        )

    if move_request:
        responses = [
            (
                "Bạn muốn tôi đưa bạn đến địa điểm nào? "
                "Bạn có thể nói: đưa tôi tới thư viện, Khu A, hội trường, Viện AIDTI hoặc phòng họp AI."
            ),
            (
                "Tôi đã hiểu là bạn muốn di chuyển. Bạn hãy nói rõ điểm đến, ví dụ: đưa tôi tới Khu A, "
                "đưa tôi đến thư viện hoặc đưa tôi đến Viện AIDTI."
            ),
        ]

        return make_result(
            speech_text=pick_response(responses),
            action="clarify",
            confidence=0.75,
        )

    if is_suggest_request(text, text_no_accent):
        one_location = contains_any(
            text,
            text_no_accent,
            ["một điểm", "1 điểm", "một nơi", "một địa điểm"],
        )
        return suggest_locations_result(one_location=one_location)

    topic_id, topic = find_general_topic(text, text_no_accent)

    if topic is not None:
        return make_result(
            speech_text=pick_response(topic["responses"]),
            action="answer_only",
            confidence=0.9,
        )

    return None


def fallback_response(user_text: str) -> dict:
    local = local_intent_response(user_text)

    if local is not None:
        return local

    responses = [
        (
            "Tôi chưa hiểu rõ yêu cầu của bạn. "
            "Bạn có thể hỏi: giới thiệu Trường Đại học Bình Dương, giới thiệu ngành Công nghệ Thông tin, "
            "giới thiệu Viện AIDTI, gợi ý một điểm tham quan, hoặc đưa tôi tới thư viện."
        ),
        (
            "Yêu cầu này hiện chưa rõ với tôi. Bạn có thể nói theo mẫu: giới thiệu BDU, giới thiệu ngành Công nghệ Thông tin, "
            "đưa tôi đến Khu A, hoặc chỉ đường đến thư viện."
        ),
        (
            "Tôi chưa xác định được bạn muốn hỏi thông tin hay muốn di chuyển. "
            "Bạn có thể yêu cầu tôi giới thiệu một địa điểm, gợi ý nơi tham quan hoặc đưa bạn đến một khu vực cụ thể."
        ),
    ]

    return make_result(
        speech_text=pick_response(responses),
        action="clarify",
        confidence=0.5,
    )


def normalize_result(data: dict) -> dict:
    if not isinstance(data, dict):
        return fallback_response("")

    speech_text = str(data.get("speech_text") or "").strip()
    action = data.get("action")
    target_location = data.get("target_location")
    locations = data.get("locations") or []

    if action not in ALLOWED_ACTIONS:
        action = "answer_only"

    if target_location not in ALLOWED_LOCATIONS:
        target_location = None

    clean_locations = []

    if isinstance(locations, list):
        for loc in locations:
            if loc in ALLOWED_LOCATIONS and loc not in clean_locations:
                clean_locations.append(loc)

    if action in ["open_camera", "iot_control"]:
        action = "clarify"
        target_location = None
        speech_text = (
            "Hiện tại tôi chưa hỗ trợ mở camera hoặc điều khiển thiết bị. "
            "Tôi có thể đưa bạn đến một địa điểm trong trường hoặc hướng dẫn đường đi."
        )

    if not speech_text:
        speech_text = "Tôi chưa tạo được câu trả lời. Bạn có thể hỏi lại được không?"

    try:
        confidence = float(data.get("confidence", 0.5))
    except Exception:
        confidence = 0.5

    confidence = max(0.0, min(1.0, confidence))

    return {
        "speech_text": speech_text,
        "action": action,
        "target_location": target_location,
        "locations": clean_locations,
        "iot_device": None,
        "iot_command": None,
        "camera_id": None,
        "confidence": confidence,
    }


def generate_response(user_text: str) -> dict:
    user_text = (user_text or "").strip()

    local = local_intent_response(user_text)

    if local is not None:
        print("[LLM] Local intent matched. Skip online LLM.", flush=True)
        return local

    if not USE_ONLINE_LLM_FOR_UNKNOWN:
        print("[LLM] Online LLM disabled. Using fallback.", flush=True)
        return fallback_response(user_text)

    if client is None:
        print("[LLM] No GROQ_API_KEY. Using fallback.", flush=True)
        return fallback_response(user_text)

    try:
        completion = client.chat.completions.create(
            model="openai/gpt-oss-20b",
            messages=[
                {
                    "role": "system",
                    "content": SYSTEM_PROMPT,
                },
                {
                    "role": "user",
                    "content": user_text,
                },
            ],
            temperature=0.2,
            max_tokens=260,
            response_format={"type": "json_object"},
            timeout=8,
        )

        raw = completion.choices[0].message.content
        data = json.loads(raw)

        return normalize_result(data)

    except Exception as e:
        print(f"[LLM ERROR] {e}", flush=True)
        return fallback_response(user_text)