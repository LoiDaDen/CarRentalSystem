from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import cm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "wwwroot" / "docs" / "owner" / "drivego-cam-nang-cho-thue-xe-an-toan.pdf"
FONT_REGULAR = Path(r"C:\Windows\Fonts\arial.ttf")
FONT_BOLD = Path(r"C:\Windows\Fonts\arialbd.ttf")


def register_fonts() -> tuple[str, str]:
    if FONT_REGULAR.exists() and FONT_BOLD.exists():
        pdfmetrics.registerFont(TTFont("ArialVN", str(FONT_REGULAR)))
        pdfmetrics.registerFont(TTFont("ArialVNBold", str(FONT_BOLD)))
        return "ArialVN", "ArialVNBold"

    return "Helvetica", "Helvetica-Bold"


def build_styles(font_name: str, bold_name: str):
    base = getSampleStyleSheet()
    return {
        "cover_brand": ParagraphStyle(
            "cover_brand",
            parent=base["Title"],
            fontName=bold_name,
            fontSize=28,
            leading=34,
            textColor=colors.HexColor("#0E5A7D"),
            alignment=TA_CENTER,
            spaceAfter=16,
        ),
        "cover_title": ParagraphStyle(
            "cover_title",
            parent=base["Title"],
            fontName=bold_name,
            fontSize=22,
            leading=29,
            textColor=colors.HexColor("#F37021"),
            alignment=TA_CENTER,
            spaceAfter=18,
        ),
        "cover_subtitle": ParagraphStyle(
            "cover_subtitle",
            parent=base["BodyText"],
            fontName=font_name,
            fontSize=12,
            leading=18,
            alignment=TA_CENTER,
            textColor=colors.HexColor("#334155"),
        ),
        "h1": ParagraphStyle(
            "h1",
            parent=base["Heading1"],
            fontName=bold_name,
            fontSize=16,
            leading=22,
            textColor=colors.HexColor("#0E5A7D"),
            spaceBefore=10,
            spaceAfter=8,
        ),
        "h2": ParagraphStyle(
            "h2",
            parent=base["Heading2"],
            fontName=bold_name,
            fontSize=12,
            leading=16,
            textColor=colors.HexColor("#F37021"),
            spaceBefore=8,
            spaceAfter=4,
        ),
        "body": ParagraphStyle(
            "body",
            parent=base["BodyText"],
            fontName=font_name,
            fontSize=10.5,
            leading=16,
            textColor=colors.HexColor("#1F2937"),
            spaceAfter=6,
        ),
        "bullet": ParagraphStyle(
            "bullet",
            parent=base["BodyText"],
            fontName=font_name,
            fontSize=10,
            leading=15,
            leftIndent=14,
            firstLineIndent=-10,
            textColor=colors.HexColor("#1F2937"),
            spaceAfter=3,
        ),
        "table_header": ParagraphStyle(
            "table_header",
            parent=base["BodyText"],
            fontName=bold_name,
            fontSize=9.5,
            leading=12,
            textColor=colors.white,
        ),
        "table_cell": ParagraphStyle(
            "table_cell",
            parent=base["BodyText"],
            fontName=font_name,
            fontSize=9,
            leading=12,
            textColor=colors.HexColor("#1F2937"),
        ),
        "note": ParagraphStyle(
            "note",
            parent=base["BodyText"],
            fontName=font_name,
            fontSize=9,
            leading=13,
            textColor=colors.HexColor("#475569"),
            backColor=colors.HexColor("#EEF7FA"),
            borderColor=colors.HexColor("#B7DEE8"),
            borderWidth=0.6,
            borderPadding=6,
            spaceBefore=6,
            spaceAfter=8,
        ),
    }


def p(text: str, style):
    return Paragraph(text, style)


def bullet(text: str, styles):
    return p(f"- {text}", styles["bullet"])


def section(title: str, items: list[str], styles):
    story = [p(title, styles["h1"])]
    story.extend(bullet(item, styles) for item in items)
    story.append(Spacer(1, 6))
    return story


def table(rows: list[list[str]], styles):
    formatted = []
    for row_index, row in enumerate(rows):
        style_name = "table_header" if row_index == 0 else "table_cell"
        formatted.append([p(cell, styles[style_name]) for cell in row])

    result = Table(formatted, colWidths=[4.2 * cm, 11.2 * cm], repeatRows=1)
    result.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#0E5A7D")),
                ("GRID", (0, 0), (-1, -1), 0.35, colors.HexColor("#CBD5E1")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 7),
                ("RIGHTPADDING", (0, 0), (-1, -1), 7),
                ("TOPPADDING", (0, 0), (-1, -1), 6),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
                ("BACKGROUND", (0, 1), (-1, -1), colors.HexColor("#F8FAFC")),
            ]
        )
    )
    return result


def draw_page(canvas, doc):
    canvas.saveState()
    width, height = A4
    canvas.setStrokeColor(colors.HexColor("#0E5A7D"))
    canvas.setLineWidth(1.0)
    canvas.rect(1.05 * cm, 1.05 * cm, width - 2.1 * cm, height - 2.1 * cm)
    canvas.setFont("ArialVN", 8)
    canvas.setFillColor(colors.HexColor("#64748B"))
    canvas.drawString(1.35 * cm, 0.7 * cm, "DriveGo - Cẩm nang cho thuê xe an toàn")
    canvas.drawRightString(width - 1.35 * cm, 0.7 * cm, f"Trang {doc.page}")
    canvas.restoreState()


def build_story(styles):
    story = [
        Spacer(1, 2.2 * cm),
        p("DriveGo", styles["cover_brand"]),
        p("Cẩm nang cho thuê xe an toàn", styles["cover_title"]),
        p("Tài liệu hướng dẫn dành cho chủ xe đăng ký cho thuê trên hệ thống DriveGo", styles["cover_subtitle"]),
        Spacer(1, 0.8 * cm),
        p(
            "Mục tiêu của cẩm nang là giúp chủ xe chuẩn bị hồ sơ rõ ràng, đăng tin minh bạch, bàn giao có bằng chứng và xử lý rủi ro theo quy trình nhất quán.",
            styles["note"],
        ),
        Spacer(1, 4.2 * cm),
        p("Phiên bản 2026 - dùng cho vận hành nội bộ và demo hệ thống", styles["cover_subtitle"]),
        PageBreak(),
    ]

    story.extend(
        section(
            "1. Chuẩn bị hồ sơ xe",
            [
                "Đảm bảo xe có đăng ký xe, giấy đăng kiểm còn hiệu lực, bảo hiểm trách nhiệm dân sự và hồ sơ bảo dưỡng gần nhất.",
                "Biển số, số VIN, số chỗ, hãng xe, mẫu xe và năm sản xuất cần nhập đúng như giấy tờ.",
                "Không đăng xe đang tranh chấp, xe bị cầm cố không có quyền cho thuê, xe hết hạn đăng kiểm hoặc xe chưa đủ điều kiện lưu thông.",
                "Chuẩn bị ảnh giấy tờ ở nơi đủ sáng, che các thông tin không cần công khai nhưng vẫn giữ phần hệ thống cần đối chiếu.",
            ],
            styles,
        )
    )
    story.extend(
        section(
            "2. Tiêu chuẩn ảnh đăng xe",
            [
                "Tải tối thiểu bốn ảnh: trước xe, sau xe, hai bên thân xe. Nên bổ sung nội thất, khoang lái, cốp xe và đồng hồ km.",
                "Ảnh cần rõ nét, không dùng filter quá mạnh, không che biển số trong ảnh xác minh nội bộ.",
                "Chụp xe sau khi rửa sạch, nền gọn, không để vật riêng tư hoặc giấy tờ cá nhân lộ trong khoang xe.",
                "Nếu có trầy xước hoặc móp sẵn, chụp cận cảnh để tránh tranh chấp khi trả xe.",
            ],
            styles,
        )
    )

    story.append(p("3. Định giá và điều khoản cho thuê", styles["h1"]))
    story.append(
        table(
            [
                ["Hạng mục", "Khuyến nghị vận hành"],
                ["Giá theo ngày", "So sánh với xe cùng phân khúc, đời xe, khu vực và mùa cao điểm. Không đặt giá quá thấp nếu xe có chi phí bảo dưỡng cao."],
                ["Giảm giá tuần/tháng", "Dùng để tăng tỷ lệ lấp lịch, nhưng vẫn giữ biên an toàn cho bảo dưỡng, rửa xe và khấu hao."],
                ["Giới hạn km", "Đặt giới hạn rõ ràng theo ngày. Nêu phí vượt km để khách biết trước khi đặt."],
                ["Giao nhận xe", "Công khai bán kính giao xe, phí mỗi km và khung giờ hỗ trợ. Nếu không tiện giao xe, chọn khách tự đến nhận."],
                ["Điều khoản riêng", "Ghi rõ quy định hút thuốc, thú cưng, đi tỉnh, đổ nhiên liệu, vệ sinh và xử lý vi phạm."],
            ],
            styles,
        )
    )
    story.append(Spacer(1, 8))
    story.append(
        p(
            "Không thu thêm khoản ngoài hệ thống nếu khoản đó chưa được ghi nhận trong thỏa thuận với khách. Điều này giúp DriveGo có đủ dữ liệu để hỗ trợ khi phát sinh tranh chấp.",
            styles["note"],
        )
    )

    story.extend(
        section(
            "4. Gửi duyệt xe",
            [
                "Kiểm tra lại biển số và số VIN trước khi bấm gửi duyệt. Hai thông tin này là định danh chính của xe trong hệ thống.",
                "Sau khi gửi, xe ở trạng thái chờ duyệt. Staff khu vực kiểm tra hồ sơ, Manager khu vực rà soát chất lượng, Admin xác nhận lần cuối theo quy trình cấu hình.",
                "Nếu bị yêu cầu bổ sung, cập nhật đúng phần được góp ý thay vì tạo xe mới trùng biển số.",
                "Không tự ý thay đổi thông tin định danh sau khi xe đã có booking đang chờ hoặc đang hoạt động.",
            ],
            styles,
        )
    )
    story.extend(
        section(
            "5. Bàn giao xe",
            [
                "Đối chiếu người nhận xe với thông tin đặt xe và giấy phép lái xe đã xác thực trong hệ thống.",
                "Chụp ảnh bốn phía xe, nội thất, đồng hồ km, mức nhiên liệu hoặc pin, phụ kiện đi kèm và các vết xước có sẵn.",
                "Ghi nhận tài sản thế chấp, phí giữ chỗ, tiền cọc và các khoản đã thanh toán theo đúng trạng thái trên hệ thống.",
                "Nhắc khách kiểm tra phanh, đèn, lốp, giấy tờ xe bản sao, dụng cụ khẩn cấp và số liên hệ hỗ trợ.",
            ],
            styles,
        )
    )

    story.extend(
        section(
            "6. Theo dõi chuyến đi",
            [
                "Giữ kênh liên lạc lịch sự và ngắn gọn; ưu tiên trao đổi qua luồng hỗ trợ được DriveGo ghi nhận.",
                "Nếu khách muốn gia hạn, yêu cầu thao tác trên hệ thống để cập nhật thời gian, phí và trạng thái bảo hiểm.",
                "Không tự ý can thiệp xe khi chưa có tình huống khẩn cấp hoặc chưa thống nhất với khách và bộ phận hỗ trợ.",
                "Với xe có thiết bị GPS, chỉ dùng dữ liệu theo dõi cho mục đích bảo vệ tài sản và xử lý sự cố.",
            ],
            styles,
        )
    )
    story.extend(
        section(
            "7. Trả xe và quyết toán",
            [
                "Kiểm tra xe theo cùng checklist lúc bàn giao: ngoại thất, nội thất, km, nhiên liệu, phụ kiện, giấy tờ và tình trạng vệ sinh.",
                "Chụp ảnh sau chuyến, đặc biệt ở các điểm có dấu hiệu mới phát sinh.",
                "Ghi nhận phí phát sinh như vượt km, trả muộn, vệ sinh, nhiên liệu thiếu hoặc thiệt hại sau khi có bằng chứng rõ ràng.",
                "Không hoàn tất giao dịch nếu còn tranh chấp chưa ghi nhận hoặc thiếu biên bản xác nhận với khách.",
            ],
            styles,
        )
    )
    story.extend(
        section(
            "8. Xử lý sự cố và phạt nguội",
            [
                "Khi có va chạm, ưu tiên an toàn người trên xe, giữ hiện trường khi cần thiết và liên hệ cơ quan chức năng hoặc bảo hiểm theo quy định.",
                "Chủ xe nên lưu toàn bộ ảnh, biên bản, tin nhắn và hóa đơn sửa chữa để DriveGo có căn cứ hỗ trợ đối soát.",
                "DriveGo chưa có API phạt nguội tự động và không tự khẳng định dữ liệu vi phạm là dữ liệu thật.",
                "Khi cần kiểm tra phạt nguội, mở cổng Cục Cảnh sát giao thông và nhập biển số trực tiếp tại: https://csgt.bocongan.gov.vn/tra-cuu-phat-nguoi",
            ],
            styles,
        )
    )

    story.append(p("9. Checklist nhanh trước khi xe lên sàn", styles["h1"]))
    story.append(
        table(
            [
                ["Việc cần kiểm tra", "Trạng thái mong muốn"],
                ["Hồ sơ", "Đăng ký, đăng kiểm, bảo hiểm và giấy tờ liên quan đã sẵn sàng."],
                ["Thông tin định danh", "Biển số và số VIN nhập đúng, không trùng với xe khác."],
                ["Ảnh xe", "Đủ ảnh ngoài xe, nội thất, đồng hồ km và vết xước nếu có."],
                ["Giá và phí", "Giá ngày, giới hạn km, phụ phí và giao nhận xe rõ ràng."],
                ["Điều khoản", "Quy định nhiên liệu, vệ sinh, trả muộn, hút thuốc, thú cưng được ghi cụ thể."],
                ["Bàn giao", "Có checklist ảnh, km, nhiên liệu, phụ kiện và xác nhận của khách."],
                ["Sự cố", "Biết cách liên hệ hỗ trợ, bảo hiểm và cổng tra cứu CSGT khi cần."],
            ],
            styles,
        )
    )
    story.append(Spacer(1, 8))
    story.append(
        p(
            "Cẩm nang này là tài liệu vận hành của DriveGo. Chủ xe nên cập nhật theo quy định pháp luật, chính sách bảo hiểm và hướng dẫn mới nhất trong hệ thống.",
            styles["note"],
        )
    )
    return story


def main():
    font_name, bold_name = register_fonts()
    styles = build_styles(font_name, bold_name)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    doc = SimpleDocTemplate(
        str(OUTPUT),
        pagesize=A4,
        leftMargin=1.7 * cm,
        rightMargin=1.7 * cm,
        topMargin=1.7 * cm,
        bottomMargin=1.5 * cm,
        title="DriveGo - Cẩm nang cho thuê xe an toàn",
        author="DriveGo",
    )
    doc.build(build_story(styles), onFirstPage=draw_page, onLaterPages=draw_page)
    print(OUTPUT)


if __name__ == "__main__":
    main()
