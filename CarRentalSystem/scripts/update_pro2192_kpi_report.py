from __future__ import annotations

import shutil
from pathlib import Path

from docx import Document
from docx.enum.style import WD_STYLE_TYPE
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT, WD_TABLE_ALIGNMENT
from docx.enum.text import WD_BREAK, WD_LINE_SPACING, WD_PARAGRAPH_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor


SOURCE = Path(r"D:\CarRentalSystem_Report_PRO2192_FINAL.docx")
BACKUP = Path(r"D:\CarRentalSystem_Report_PRO2192_FINAL_BEFORE_KPI_6_THANG.docx")
DELIVERABLE = Path(r"D:\CarRentalSystem_Report_PRO2192_FINAL_KPI_6_THANG.docx")
SECTION_HEADING = "CHƯƠNG 1A. MÔ HÌNH STARTUP VÀ KPI SÁU THÁNG"
INSERT_BEFORE = "CHƯƠNG 2. KHẢO SÁT NHU CẦU, HIỆN TRẠNG VÀ GIẢI PHÁP"

BLUE = "0B5ED7"
LIGHT_BLUE = "EAF3FF"
LIGHT_GREEN = "EAF7EF"
LIGHT_ORANGE = "FFF4E5"
WHITE = "FFFFFF"


def shade_cell(cell, fill: str) -> None:
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = tc_pr.find(qn("w:shd"))
    if shd is None:
        shd = OxmlElement("w:shd")
        tc_pr.append(shd)
    shd.set(qn("w:fill"), fill)


def set_cell_text(cell, text: str, *, bold: bool = False, color: str = "000000") -> None:
    cell.text = ""
    paragraph = cell.paragraphs[0]
    paragraph.paragraph_format.space_after = Pt(0)
    run = paragraph.add_run(text)
    run.bold = bold
    run.font.name = "Times New Roman"
    run.font.size = Pt(10)
    run.font.color.rgb = RGBColor.from_string(color)
    cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER


def style_table(table, header_fill: str = BLUE) -> None:
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.style = "Table Grid"
    for cell in table.rows[0].cells:
        shade_cell(cell, header_fill)
        for run in cell.paragraphs[0].runs:
            run.bold = True
            run.font.color.rgb = RGBColor.from_string(WHITE)
            run.font.name = "Times New Roman"
            run.font.size = Pt(10)
    for row_index, row in enumerate(table.rows[1:], start=1):
        fill = "F7FAFC" if row_index % 2 == 0 else WHITE
        for cell in row.cells:
            shade_cell(cell, fill)
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            for paragraph in cell.paragraphs:
                paragraph.paragraph_format.space_after = Pt(0)
                for run in paragraph.runs:
                    run.font.name = "Times New Roman"
                    run.font.size = Pt(10)


def add_heading(doc: Document, text: str, level: int) -> None:
    paragraph = doc.add_heading(text, level=level)
    paragraph.paragraph_format.keep_with_next = True


def add_body(doc: Document, text: str, *, bold_prefix: str | None = None) -> None:
    paragraph = doc.add_paragraph()
    paragraph.alignment = WD_PARAGRAPH_ALIGNMENT.JUSTIFY
    paragraph.paragraph_format.line_spacing_rule = WD_LINE_SPACING.ONE_POINT_FIVE
    paragraph.paragraph_format.space_after = Pt(6)
    if bold_prefix and text.startswith(bold_prefix):
        first = paragraph.add_run(bold_prefix)
        first.bold = True
        paragraph.add_run(text[len(bold_prefix) :])
    else:
        paragraph.add_run(text)


def add_bullets(doc: Document, items: list[str]) -> None:
    for item in items:
        paragraph = doc.add_paragraph(style="List Bullet")
        paragraph.paragraph_format.line_spacing_rule = WD_LINE_SPACING.ONE_POINT_FIVE
        paragraph.paragraph_format.space_after = Pt(3)
        paragraph.add_run(item)


def add_metric_table(doc: Document) -> None:
    headers = ["KPI", "Cách đo chính thức", "Mục tiêu 6 tháng"]
    rows = [
        ("Người dùng", "Tài khoản đăng ký trong kỳ đo", "100"),
        ("Xe đăng cho thuê", "Xe tự lái đã duyệt, không bị xóa", "30"),
        ("Đơn thuê hợp lệ", "Booking không ở Draft hoặc Unpaid", "80 kế hoạch; tối thiểu 50"),
        (
            "Giao dịch thành công",
            "Completed / (Completed + Cancelled + Rejected + Expired)",
            "Từ 70%",
        ),
        ("GMV", "Tổng TotalPrice của booking Completed", "250 triệu đồng"),
        (
            "Doanh thu DriveGo",
            "Tổng TotalPrice x CommissionRate của booking Completed",
            "50 triệu đồng",
        ),
        (
            "Hài lòng",
            "Review đã duyệt 4-5 sao / tổng review đã duyệt",
            "Trên 80%; tối thiểu 25 review",
        ),
    ]
    table = doc.add_table(rows=1, cols=3)
    for index, header in enumerate(headers):
        set_cell_text(table.rows[0].cells[index], header, bold=True, color=WHITE)
    for metric, definition, target in rows:
        cells = table.add_row().cells
        set_cell_text(cells[0], metric, bold=True)
        set_cell_text(cells[1], definition)
        set_cell_text(cells[2], target)
    style_table(table)


def add_growth_table(doc: Document) -> None:
    headers = ["Tháng", "User mới", "Xe duyệt", "Booking", "Completed", "GMV", "Hoa hồng"]
    rows = [
        ("1", "15", "5", "5", "3", "10 triệu", "2 triệu"),
        ("2", "15", "5", "8", "6", "20 triệu", "4 triệu"),
        ("3", "15", "5", "10", "7", "30 triệu", "6 triệu"),
        ("4", "20", "5", "14", "10", "45 triệu", "9 triệu"),
        ("5", "17", "5", "18", "12", "60 triệu", "12 triệu"),
        ("6", "18", "5", "25", "18", "85 triệu", "17 triệu"),
        ("Tổng", "100", "30", "80", "56", "250 triệu", "50 triệu"),
    ]
    table = doc.add_table(rows=1, cols=len(headers))
    for index, header in enumerate(headers):
        set_cell_text(table.rows[0].cells[index], header, bold=True, color=WHITE)
    for row_index, values in enumerate(rows):
        cells = table.add_row().cells
        for column_index, value in enumerate(values):
            set_cell_text(cells[column_index], value, bold=row_index == len(rows) - 1)
        if row_index == len(rows) - 1:
            for cell in cells:
                shade_cell(cell, LIGHT_GREEN)
    style_table(table)
    for cell in table.rows[-1].cells:
        shade_cell(cell, LIGHT_GREEN)


def add_evidence_table(doc: Document) -> None:
    headers = ["KPI", "Chức năng hệ thống", "Bằng chứng", "Cách nghiệm thu"]
    rows = [
        (
            "Người dùng",
            "Đăng ký, xác thực, quản trị tài khoản",
            "User.CreatedAt; Admin/User",
            "Đếm tài khoản trong kỳ sáu tháng",
        ),
        (
            "Xe được duyệt",
            "OwnerCars/Create; Admin/CarApproval",
            "Car.IsApproved, IsDeleted, RentalMode",
            "Đếm xe tự lái được duyệt và chưa xóa",
        ),
        (
            "Booking và thành công",
            "MyBookings; vòng đời booking; Staff/Handover",
            "Booking.Status, StartDate, EndDate",
            "Loại Draft/Unpaid; áp dụng mẫu số chính thức",
        ),
        (
            "GMV và hoa hồng",
            "Booking Completed; cấu hình CommissionRate",
            "Booking.TotalPrice; Car.CommissionRate",
            "Đối chiếu GMV, hoa hồng và ledger thành ba lớp riêng",
        ),
        (
            "Hài lòng",
            "Review sau chuyến và duyệt review",
            "Review.Rating, IsApproved",
            "Review 4-5 sao / tổng review đã duyệt",
        ),
        (
            "Dashboard",
            "Admin/StartupKpi",
            "Snapshot KPI + mục tiêu + kế hoạch tháng",
            "Admin đăng nhập, chọn kỳ đo, đối chiếu công thức",
        ),
    ]
    table = doc.add_table(rows=1, cols=4)
    for index, header in enumerate(headers):
        set_cell_text(table.rows[0].cells[index], header, bold=True, color=WHITE)
    for values in rows:
        cells = table.add_row().cells
        for column_index, value in enumerate(values):
            set_cell_text(cells[column_index], value, bold=column_index == 0)
    style_table(table)


def add_truth_table(doc: Document) -> None:
    headers = ["Cấp dữ liệu", "Ý nghĩa", "Quy tắc trình bày"]
    rows = [
        (
            "Mục tiêu",
            "Con số DriveGo cam kết hướng tới trong sáu tháng đầu",
            "Luôn ghi rõ là mục tiêu; không gọi là kết quả",
        ),
        (
            "Mô phỏng",
            "Dữ liệu seed/test để kiểm chứng công thức và luồng hệ thống",
            "Gắn nhãn dữ liệu local hoặc mô phỏng",
        ),
        (
            "Pilot",
            "Phản hồi và booking test có sự đồng ý trong một tuần",
            "Công bố mẫu, ngày, phương pháp và giới hạn",
        ),
        (
            "Kết quả thực tế",
            "Giao dịch thị trường đã phát sinh và được đối soát",
            "Chỉ tuyên bố khi có ledger, hợp đồng và bằng chứng đối soát",
        ),
    ]
    table = doc.add_table(rows=1, cols=3)
    for index, header in enumerate(headers):
        set_cell_text(table.rows[0].cells[index], header, bold=True, color=WHITE)
    for values in rows:
        cells = table.add_row().cells
        for column_index, value in enumerate(values):
            set_cell_text(cells[column_index], value, bold=column_index == 0)
    style_table(table)


def add_section(doc: Document) -> list[object]:
    body = doc._element.body
    original_children = list(body)

    page_break = doc.add_paragraph()
    page_break.add_run().add_break(WD_BREAK.PAGE)

    add_heading(doc, SECTION_HEADING, 1)
    add_body(
        doc,
        "DriveGo được tái định hướng thành nền tảng marketplace kết nối chủ xe với khách hàng "
        "thuê xe tự lái. Chuỗi tạo giá trị và doanh thu là: chủ xe đăng xe, khách đặt thuê, "
        "DriveGo kiểm soát giao dịch và bàn giao, sau đó ghi nhận 20% hoa hồng trên booking "
        "đã hoàn thành.",
    )

    add_heading(doc, "1A.1 Mục tiêu kinh doanh và nguyên tắc đo", 2)
    add_body(
        doc,
        "Mục tiêu sáu tháng đầu là đạt 100 người dùng đăng ký, 30 xe được duyệt cho thuê, "
        "phát sinh tối thiểu 50 đơn thuê hợp lệ, tỷ lệ giao dịch thành công từ 70%, mức độ "
        "hài lòng trên 80% và doanh thu nền tảng 50 triệu đồng. Để kế hoạch doanh thu có đủ "
        "dư địa, DriveGo đặt kế hoạch vận hành 80 booking hợp lệ, trong đó 56 booking Completed.",
    )
    add_metric_table(doc)

    add_heading(doc, "1A.2 Unit economics và công thức doanh thu", 2)
    add_body(
        doc,
        "Doanh thu mục tiêu 50 triệu đồng là hoa hồng của DriveGo, không phải toàn bộ giá trị "
        "booking. Với tỷ lệ hoa hồng mặc định 20%, hệ thống cần tạo ra 250 triệu đồng GMV từ "
        "các booking Completed.",
    )
    add_bullets(
        doc,
        [
            "250 triệu đồng GMV x 20% = 50 triệu đồng doanh thu hoa hồng.",
            "80 booking hợp lệ x 70% = 56 booking Completed theo kế hoạch.",
            "250 triệu đồng / 56 booking Completed = khoảng 4,46 triệu đồng cho mỗi booking Completed.",
            "Chiến lược cần kiểm chứng qua pilot là ưu tiên booking nhiều ngày thay vì phụ thuộc vào đơn một ngày.",
        ],
    )
    add_body(
        doc,
        "Dashboard KPI tách ba lớp tài chính để tránh diễn giải sai: GMV booking Completed; "
        "hoa hồng nền tảng được ghi nhận theo CommissionRate; và số tiền đã hạch toán trong "
        "payment ledger. Ba con số có thể khác nhau tại một thời điểm vì quy trình thanh toán "
        "và đối soát chưa diễn ra đồng thời.",
    )

    add_heading(doc, "1A.3 Kế hoạch tăng trưởng sáu tháng", 2)
    add_growth_table(doc)
    add_body(
        doc,
        "Kế hoạch tăng dần số booking và GMV qua từng tháng, đồng thời duy trì đều nguồn cung "
        "năm xe mới được duyệt mỗi tháng. Tháng 5 và tháng 6 đóng vai trò tăng tốc sau khi "
        "nhóm đã có dữ liệu pilot, quy trình vận hành và nhóm khách hàng quay lại.",
    )

    add_heading(doc, "1A.4 Ma trận KPI, chức năng và bằng chứng", 2)
    add_evidence_table(doc)
    add_body(
        doc,
        "Route Admin/StartupKpi là điểm tổng hợp để kiểm tra dữ liệu local hiện tại, mục tiêu "
        "sáu tháng, khoảng cách KPI, công thức doanh thu và kế hoạch vận hành. Route được bảo "
        "vệ bằng chính sách quyền Admin.",
    )

    add_heading(doc, "1A.5 Kế hoạch pilot một tuần", 2)
    add_bullets(
        doc,
        [
            "Phỏng vấn hoặc lấy xác nhận quan tâm từ ít nhất 5 chủ xe, tạo pipeline tối thiểu 8 xe.",
            "Thu ít nhất 20 phản hồi nhu cầu khách thuê và 10 đăng ký thử nghiệm.",
            "Thực hiện 3 booking test đầu-cuối bằng dữ liệu được đồng ý sử dụng.",
            "Lưu form, ngày thu thập, phương pháp và kết quả đã ẩn danh.",
            "Đánh giá giả thuyết booking nhiều ngày, mức giá chấp nhận và các nguyên nhân từ chối.",
        ],
    )
    add_body(
        doc,
        "Pilot chỉ là bằng chứng nhu cầu ban đầu và bằng chứng vận hành, không được gọi là đã "
        "đạt KPI sáu tháng. Sau pilot, nhóm phải cập nhật giả định tỷ lệ chuyển đổi, giá trị "
        "booking trung bình và kế hoạch thu hút chủ xe bằng dữ liệu quan sát được.",
    )

    add_heading(doc, "1A.6 Phân biệt mục tiêu, mô phỏng, pilot và kết quả thực tế", 2)
    add_truth_table(doc)

    add_heading(doc, "1A.7 Kịch bản chứng minh trong mười phút", 2)
    add_body(
        doc,
        "Phần trình bày chỉ mở bốn điểm chính để bảo đảm thời gian: MyBookings cho khách hàng, "
        "OwnerCars/Create cho chủ xe, Staff/Handover cho vận hành và Admin/StartupKpi cho việc "
        "đo mục tiêu. Tất cả tài khoản được đăng nhập trước và một người duy nhất điều khiển "
        "máy. Kiến trúc, bảo mật, API, SRE và QA chi tiết được chuyển sang phụ lục để dùng khi hỏi đáp.",
    )

    add_heading(doc, "1A.8 Giả định và nguồn tham chiếu thị trường", 2)
    add_bullets(
        doc,
        [
            "KPI được tính lũy kế trong sáu tháng đầu và mô hình doanh thu chỉ dùng hoa hồng 20%.",
            "Quy trình đăng và duyệt xe tham khảo mô hình marketplace công khai của Mioto: https://www.mioto.vn/carregistermode",
            "Giá thuê công khai tham khảo: https://www.mioto.vn/blog/bang-gia-thue-xe-tu-lai-tai-mioto/5GQMECH5",
            "Thông tin thị trường thuê xe nhiều ngày năm 2026 tham khảo: https://dantri.com.vn/o-to-xe-may/thi-truong-thue-o-to-tu-lai-tet-2026-ha-nhiet-xe-dien-duoc-ua-chuong-20260209174224591.htm",
        ],
    )

    new_children = [child for child in list(body) if child not in original_children]
    return new_children


def configure_styles(doc: Document) -> None:
    for style_name in ("Normal", "List Bullet"):
        try:
            style = doc.styles[style_name]
        except KeyError:
            continue
        style.font.name = "Times New Roman"
        style.font.size = Pt(13)
        style._element.rPr.rFonts.set(qn("w:eastAsia"), "Times New Roman")
        style.paragraph_format.line_spacing = 1.2 if style_name == "Normal" else 1.2
        style.paragraph_format.space_after = Pt(2)

    for style_name in ("Heading 1", "Heading 2"):
        style = doc.styles[style_name]
        style.font.name = "Times New Roman"
        style._element.rPr.rFonts.set(qn("w:eastAsia"), "Times New Roman")

    if "KPI Formula" not in [style.name for style in doc.styles]:
        style = doc.styles.add_style("KPI Formula", WD_STYLE_TYPE.PARAGRAPH)
        style.font.name = "Times New Roman"
        style.font.size = Pt(14)
        style.font.bold = True
        style.font.color.rgb = RGBColor.from_string(BLUE)


def compact_main_content(doc: Document) -> None:
    start = next(
        (index for index, p in enumerate(doc.paragraphs) if p.text.strip().startswith("CHƯƠNG 1.")),
        None,
    )
    end = next(
        (index for index, p in enumerate(doc.paragraphs) if p.text.strip() == "TÀI LIỆU THAM KHẢO"),
        None,
    )
    if start is None or end is None:
        return

    for paragraph in doc.paragraphs[start:end]:
        if paragraph.style.name.startswith("Heading"):
            continue
        paragraph.paragraph_format.line_spacing = 1.15
        paragraph.paragraph_format.space_after = Pt(2)


def move_range_before(
    doc: Document,
    start_text: str,
    end_text: str,
    destination_text: str,
) -> None:
    body = doc._element.body
    paragraphs = {p.text.strip(): p._p for p in doc.paragraphs if p.text.strip()}
    start = paragraphs.get(start_text)
    end = paragraphs.get(end_text)
    destination = paragraphs.get(destination_text)
    if start is None or end is None or destination is None:
        return

    children = list(body)
    start_index = children.index(start)
    end_index = children.index(end)
    destination_index = children.index(destination)
    if end_index <= start_index or destination_index <= start_index:
        return

    for element in children[start_index:end_index]:
        destination.addprevious(element)


def replace_paragraph_text(paragraph, old: str, new: str) -> None:
    if old not in paragraph.text:
        return
    for run in paragraph.runs:
        if old in run.text:
            run.text = run.text.replace(old, new)
            return
    paragraph.text = paragraph.text.replace(old, new)


def refresh_test_evidence(doc: Document) -> None:
    replacements = [
        ("371/371", "375/375"),
        ("371 passed", "375 passed"),
        ("thời gian khoảng 16 giây", "thời gian khoảng 23 giây"),
        ("thời gian khoảng 20 giây", "thời gian khoảng 23 giây"),
        ("Lệnh kiểm chứng ngày 14/06/2026", "Lệnh kiểm chứng ngày 15/06/2026"),
        ("Lệnh test ngày 14/06/2026", "Lệnh test ngày 15/06/2026"),
        (
            "Playwright hiện có 49 spec file.",
            "Playwright có các spec E2E, nhưng chưa có log full regression mới.",
        ),
        (
            "Playwright có 49 spec file nhưng",
            "Playwright có các spec E2E nhưng",
        ),
        (
            "Có 49 spec; không claim full pass phiên này",
            "Có E2E spec; không claim full pass phiên này",
        ),
    ]
    for paragraph in doc.paragraphs:
        for old, new in replacements:
            replace_paragraph_text(paragraph, old, new)
    for table in doc.tables:
        for row in table.rows:
            row_text = " | ".join(cell.text for cell in row.cells)
            for cell in row.cells:
                for paragraph in cell.paragraphs:
                    for old, new in replacements:
                        replace_paragraph_text(paragraph, old, new)
            if "Playwright spec" in row_text:
                for cell in row.cells:
                    if cell.text.strip() == "49":
                        set_cell_text(cell, "Chưa xác minh bằng log mới")


def insert_before(doc: Document, elements: list[object], target_text: str) -> None:
    target = next((p for p in doc.paragraphs if p.text.strip() == target_text), None)
    if target is None:
        return
    for element in elements:
        target._p.addprevious(element)


def main() -> None:
    if not SOURCE.exists():
        raise FileNotFoundError(SOURCE)
    if not BACKUP.exists():
        shutil.copy2(SOURCE, BACKUP)

    doc = Document(SOURCE)
    configure_styles(doc)

    if not any(p.text.strip() == SECTION_HEADING for p in doc.paragraphs):
        elements = add_section(doc)
        insert_before(doc, elements, INSERT_BEFORE)
    move_range_before(
        doc,
        "4.8 External provider và trạng thái kiểm chứng",
        "CHƯƠNG 5. THỰC HIỆN VÀ QUẢN LÝ DỰ ÁN",
        "PHỤ LỤC E. SCREENSHOT CHẠY LOCAL ĐƯỢC CHỌN LỌC",
    )
    move_range_before(
        doc,
        "6.4 Giới hạn kiểm thử",
        "CHƯƠNG 7. ĐÓNG GÓI, TRIỂN KHAI VÀ VẬN HÀNH",
        "PHỤ LỤC F. TÀI LIỆU BÀN GIAO VÀ ASSETS",
    )
    refresh_test_evidence(doc)
    compact_main_content(doc)

    core = doc.core_properties
    core.subject = "DriveGo marketplace, KPI 6 tháng, unit economics và bằng chứng hệ thống"
    core.comments = (
        "Bổ sung định hướng KPI ngày 15/06/2026. Dữ liệu mục tiêu, mô phỏng, pilot và kết quả "
        "thực tế phải được phân biệt rõ."
    )

    doc.save(SOURCE)
    shutil.copy2(SOURCE, DELIVERABLE)
    print(
        {
            "updated": str(SOURCE),
            "backup": str(BACKUP),
            "deliverable": str(DELIVERABLE),
            "paragraphs": len(doc.paragraphs),
            "tables": len(doc.tables),
        }
    )


if __name__ == "__main__":
    main()
