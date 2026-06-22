from __future__ import annotations

import json
from pathlib import Path

from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt, RGBColor
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE_DOCX = Path(r"D:\CarRentalSystem_Report_DOC_FIXED_WITH_FULL_UC_ERD.docx")
FINAL_DOCX = Path(r"D:\CarRentalSystem_Report_FINAL_FULL.docx")
CHANGELOG_DOCX = Path(r"D:\CHANGELOG_MOCKUP_API_DOC.docx")
INVENTORY_XLSX = Path(r"D:\api_inventory.xlsx")
LOGO = ROOT / "wwwroot" / "images" / "car_rental_logo.png"
SCREENSHOT_ROOT = ROOT / "docs" / "evidence" / "screenshots"
MOCKUP_ROOT = ROOT / "docs" / "evidence" / "mockups"
API_ROOT = ROOT / "docs" / "evidence" / "api"

BLUE = RGBColor(20, 73, 132)
ORANGE = RGBColor(245, 130, 32)
DARK = RGBColor(31, 41, 55)
MUTED = RGBColor(75, 85, 99)


def clear_paragraph(paragraph):
    element = paragraph._element
    for child in list(element):
        element.remove(child)


def set_cell_shading(cell, fill: str):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = tc_pr.find(qn("w:shd"))
    if shd is None:
        shd = OxmlElement("w:shd")
        tc_pr.append(shd)
    shd.set(qn("w:fill"), fill)


def set_cell_text(cell, text: str, *, bold=False, color=DARK, size=9):
    cell.text = ""
    paragraph = cell.paragraphs[0]
    run = paragraph.add_run(text)
    run.bold = bold
    run.font.name = "Times New Roman"
    run.font.size = Pt(size)
    run.font.color.rgb = color
    paragraph.paragraph_format.space_after = Pt(0)


def set_repeat_table_header(row):
    tr_pr = row._tr.get_or_add_trPr()
    tbl_header = OxmlElement("w:tblHeader")
    tbl_header.set(qn("w:val"), "true")
    tr_pr.append(tbl_header)


def prevent_row_split(row):
    tr_pr = row._tr.get_or_add_trPr()
    cant_split = OxmlElement("w:cantSplit")
    tr_pr.append(cant_split)


def add_cover_run(paragraph, text: str, size: float, color, *, bold=True):
    run = paragraph.add_run(text)
    run.bold = bold
    run.font.name = "Times New Roman"
    run.font.size = Pt(size)
    run.font.color.rgb = color
    return run


def format_cover(document: Document):
    cover = document.paragraphs[:10]
    for paragraph in cover:
        clear_paragraph(paragraph)
        paragraph.alignment = WD_ALIGN_PARAGRAPH.CENTER
        paragraph.paragraph_format.space_before = Pt(0)
        paragraph.paragraph_format.space_after = Pt(0)
        paragraph.paragraph_format.line_spacing = 1.05

    cover[0].add_run().add_picture(str(LOGO), width=Inches(1.25))
    cover[0].add_run("\n")
    add_cover_run(cover[0], "TRƯỜNG CAO ĐẲNG PHỔ THÔNG FPT", 15, BLUE)
    cover[0].paragraph_format.space_after = Pt(5)

    add_cover_run(cover[1], "BÁO CÁO DỰ ÁN MÔN HỌC", 14, ORANGE)
    cover[1].paragraph_format.space_after = Pt(7)

    add_cover_run(cover[2], "XÂY DỰNG HỆ THỐNG CHO THUÊ XE TỰ LÁI", 20, BLUE)
    cover[2].add_run("\n")
    add_cover_run(cover[2], "CARRENTALSYSTEM", 19, ORANGE)
    cover[2].paragraph_format.space_after = Pt(6)

    add_cover_run(cover[3], "DỰ ÁN MÔN HỌC PHÁT TRIỂN PHẦN MỀM", 13, DARK)
    cover[3].paragraph_format.space_after = Pt(10)

    add_cover_run(cover[4], "SINH VIÊN THỰC HIỆN", 12, BLUE)
    members = [
        "Huỳnh Lợi - Backend, PO",
        "Nguyễn Vũ Hoài Tâm - Frontend",
        "Nguyễn Hoài Phong - API",
        "Võ Anh Tuấn - Tester",
        "Lê Quốc Vũ - Khảo sát, Doc",
        "Nguyễn Đắc Gia Huy - Khảo sát, Doc",
    ]
    for member in members:
        cover[4].add_run("\n")
        add_cover_run(cover[4], member, 11, DARK, bold=False)
    cover[4].paragraph_format.space_after = Pt(5)

    add_cover_run(cover[5], "NHÓM THỰC HIỆN: 06 thành viên", 11, DARK)
    cover[5].paragraph_format.space_after = Pt(4)

    add_cover_run(cover[6], "GIẢNG VIÊN HƯỚNG DẪN", 11, BLUE)
    cover[6].add_run("\n")
    add_cover_run(cover[6], "PHẠM THỊ NGỌC THÙY", 13, ORANGE)
    cover[6].paragraph_format.space_after = Pt(8)

    add_cover_run(cover[7], "TP. Đồng Nai, 2026", 12, BLUE)
    cover[7].paragraph_format.space_before = Pt(4)

    cover[8].add_run().add_break(WD_BREAK.PAGE)


def add_heading(document: Document, text: str, level=1):
    paragraph = document.add_heading(text, level=level)
    paragraph.paragraph_format.keep_with_next = True
    for run in paragraph.runs:
        run.font.name = "Times New Roman"
        run.font.color.rgb = BLUE if level == 1 else DARK
    return paragraph


def add_body(document: Document, text: str, *, bold=False, italic=False):
    paragraph = document.add_paragraph()
    paragraph.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
    paragraph.paragraph_format.space_after = Pt(5)
    paragraph.paragraph_format.line_spacing = 1.15
    run = paragraph.add_run(text)
    run.bold = bold
    run.italic = italic
    run.font.name = "Times New Roman"
    run.font.size = Pt(11)
    run.font.color.rgb = DARK
    return paragraph


def add_caption(document: Document, text: str):
    paragraph = document.add_paragraph()
    paragraph.alignment = WD_ALIGN_PARAGRAPH.CENTER
    paragraph.paragraph_format.keep_with_next = False
    run = paragraph.add_run(text)
    run.bold = True
    run.font.name = "Times New Roman"
    run.font.size = Pt(10)
    run.font.color.rgb = BLUE
    return paragraph


def add_page_break(document: Document):
    document.add_paragraph().add_run().add_break(WD_BREAK.PAGE)


def image_dimensions(path: Path, max_width=6.3, max_height=7.6):
    with Image.open(path) as image:
        width, height = image.size
    ratio = width / height
    target_width = max_width
    target_height = target_width / ratio
    if target_height > max_height:
        target_height = max_height
        target_width = target_height * ratio
    return Inches(target_width), Inches(target_height)


def add_image_page(document: Document, path: Path, caption: str, description: str):
    paragraph = document.add_paragraph()
    paragraph.alignment = WD_ALIGN_PARAGRAPH.CENTER
    width, height = image_dimensions(path)
    paragraph.add_run().add_picture(str(path), width=width, height=height)
    add_caption(document, caption)
    add_body(document, description)
    add_page_break(document)


def friendly_name(path: Path):
    return path.stem.replace("_", " ").title()


def add_summary_table(document: Document, rows):
    table = document.add_table(rows=1, cols=4)
    table.style = "Table Grid"
    headers = ["Hạng mục", "Kết quả", "Phân loại", "Ghi chú"]
    for index, header in enumerate(headers):
        set_cell_text(table.rows[0].cells[index], header, bold=True, color=RGBColor(255, 255, 255), size=10)
        set_cell_shading(table.rows[0].cells[index], "144984")
    set_repeat_table_header(table.rows[0])
    for values in rows:
        row = table.add_row()
        prevent_row_split(row)
        for index, value in enumerate(values):
            set_cell_text(row.cells[index], str(value), size=9)
            if len(table.rows) % 2 == 0:
                set_cell_shading(row.cells[index], "F3F6FA")
    return table


def append_evidence(document: Document):
    add_page_break(document)
    add_heading(document, "PHỤ LỤC G. INVENTORY SOURCE, ROUTE VÀ API EVIDENCE", 1)
    add_body(
        document,
        "Phụ lục này đối chiếu source code và Swagger đang chạy thật tại ngày 12/06/2026. "
        "MVC action được phân biệt rõ với JSON/Swagger API; endpoint không tồn tại không được suy diễn hoặc ghi như đã triển khai.",
    )
    add_summary_table(
        document,
        [
            ("Controller/action trong source", "378 action", "MVC action và JSON action", "Trích trực tiếp từ controller/Area."),
            ("Swagger/OpenAPI chạy thật", "23 operation", "API có OpenAPI evidence", "Swagger tại môi trường Local, HTTP 200."),
            ("Portal theo vai trò", "Staff / Manager / Admin tách riêng", "RBAC/UI", "Mỗi portal có Area/layout và route riêng."),
            ("Driver", "Chưa có Area/controller độc lập", "Foundation/mock", "Không trình bày như module production-ready."),
            ("Inventory chi tiết", str(INVENTORY_XLSX), "XLSX bàn giao", "Có controller, action, route, role và trạng thái."),
        ],
    )
    add_body(
        document,
        "Các bằng chứng dưới đây ghi nhận method, URL, status code và response mẫu từ dịch vụ local/mock. "
        "Không gọi PayOS, eKYC, GPS hoặc ANPR thật.",
        italic=True,
    )
    api_images = sorted(API_ROOT.rglob("*.png"))
    for index, path in enumerate(api_images, 1):
        add_image_page(
            document,
            path,
            f"Hình PL-G.{index}. API evidence chạy thật - {friendly_name(path)}",
            "Nguồn: request HTTP/Swagger trên môi trường local. Dữ liệu minh họa không chứa secret hoặc dữ liệu cá nhân thật.",
        )

    add_heading(document, "PHỤ LỤC H. SCREENSHOT UI CHẠY THẬT THEO VAI TRÒ", 1)
    add_body(
        document,
        "Toàn bộ screenshot trong phụ lục này được chụp full-page từ ứng dụng chạy tại localhost bằng Playwright "
        "với dữ liệu seed/mock an toàn. Kết quả ghi nhận 28/28 route phản hồi HTTP 200. "
        "Ảnh được nhóm theo Public, Customer, Owner, Staff, Manager và Admin để thể hiện phân quyền và layout riêng.",
    )
    screenshot_paths = sorted(SCREENSHOT_ROOT.rglob("*.png"))
    for index, path in enumerate(screenshot_paths, 1):
        role = path.parent.name.title()
        add_image_page(
            document,
            path,
            f"Hình PL-H.{index}. Screenshot chạy thật - {role} - {friendly_name(path)}",
            "Nguồn: giao diện ứng dụng local sau khi đăng nhập đúng vai trò; không dùng dịch vụ vendor thật.",
        )

    add_heading(document, "PHỤ LỤC I. MOCKUP ĐỀ XUẤT VÀ FOUNDATION", 1)
    add_body(
        document,
        "Các hình trong phụ lục này là mockup đề xuất cho màn hình còn thiếu hoặc cần hoàn thiện. "
        "Mockup Driver được gắn nhãn foundation vì source hiện chưa có Driver Area/controller độc lập.",
    )
    mockup_paths = sorted(MOCKUP_ROOT.rglob("*.png"))
    for index, path in enumerate(mockup_paths, 1):
        group = path.parent.name.title()
        label = "Foundation/mock" if path.parent.name == "driver" or "foundation" in path.stem else "Mockup đề xuất"
        add_image_page(
            document,
            path,
            f"Hình PL-I.{index}. {label} - {group} - {friendly_name(path)}",
            "Nguồn: mockup enterprise bổ sung dựa trên nghiệp vụ và phân quyền đã đối chiếu trong source.",
        )

    add_heading(document, "PHỤ LỤC J. GIỚI HẠN EVIDENCE VÀ PHẠM VI TRUNG THỰC", 1)
    add_body(
        document,
        "1. Bộ test .NET đã chạy và đạt 354/354 test. Build ứng dụng đạt 0 warning, 0 error.",
    )
    add_body(
        document,
        "2. Playwright binary trên máy kiểm thử không thể khởi động do lỗi tham số nội bộ của binary. "
        "Vì vậy screenshot UI mới được chụp bằng Playwright; lỗi công cụ này không được che giấu hoặc ghi thành Playwright PASS.",
    )
    add_body(
        document,
        "3. PayOS, eKYC, GPS và ANPR chỉ được kiểm tra ở chế độ mock/foundation hoặc disabled; "
        "không có lời gọi tới dịch vụ hay thiết bị thật.",
    )
    add_body(
        document,
        "4. Driver chưa có portal độc lập trong source. Hai màn hình Driver trong tài liệu là foundation/mockup đề xuất.",
    )
    add_body(
        document,
        "5. Screenshot và API evidence chứng minh route/response tại môi trường local đã kiểm tra, "
        "không đồng nghĩa hệ thống đã production-ready.",
    )


def build_changelog():
    document = Document()
    title = document.add_heading("CHANGELOG MOCKUP, API EVIDENCE VÀ TÀI LIỆU", 0)
    title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    add_body(document, "Ngày cập nhật: 12/06/2026. Tài liệu nền được bảo toàn và đã sao lưu trước khi chỉnh sửa.")
    rows = [
        ("1", "Trang bìa", "Thiết kế lại bìa sáng xanh/cam FPT; bỏ lớp; giữ 6 thành viên và GVHD; đổi địa điểm.", "Trang bìa", "TP. Đồng Nai, 2026"),
        ("2", "Inventory source/API", "Trích 378 controller action và 23 Swagger operation; phân loại MVC/API/foundation.", "Phụ lục G; api_inventory.xlsx", "Không bịa endpoint."),
        ("3", "API evidence", "Bổ sung 3 nhóm ảnh evidence HTTP/Swagger local.", "Phụ lục G", "Không gọi vendor thật."),
        ("4", "Screenshot UI", "Bổ sung 28 screenshot chạy thật theo vai trò, tất cả route HTTP 200.", "Phụ lục H", "Chụp bằng Playwright."),
        ("5", "Mockup", "Bổ sung 8 mockup enterprise cho màn hình thiếu/foundation.", "Phụ lục I", "Driver được ghi foundation/mock."),
        ("6", "QA evidence", "Ghi nhận build PASS và 354/354 .NET test PASS.", "Phụ lục J", "Playwright binary lỗi được ghi trung thực."),
        ("7", "Phạm vi an toàn", "Ghi rõ không dùng PayOS/eKYC/GPS/ANPR thật; không chèn secret.", "Phụ lục J", "Dữ liệu local/mock."),
        ("8", "Xuất bản", "Cập nhật field/mục lục bằng Word COM và xuất PDF.", "Toàn tài liệu", "DOCX/PDF bàn giao."),
    ]
    table = document.add_table(rows=1, cols=5)
    table.style = "Table Grid"
    for i, text in enumerate(["STT", "Hạng mục", "Đã thêm/sửa", "Vị trí trong tài liệu", "Ghi chú"]):
        set_cell_text(table.rows[0].cells[i], text, bold=True, color=RGBColor(255, 255, 255), size=10)
        set_cell_shading(table.rows[0].cells[i], "144984")
    set_repeat_table_header(table.rows[0])
    for values in rows:
        row = table.add_row()
        prevent_row_split(row)
        for index, value in enumerate(values):
            set_cell_text(row.cells[index], value, size=9)
            if len(table.rows) % 2 == 0:
                set_cell_shading(row.cells[index], "F3F6FA")
    document.save(CHANGELOG_DOCX)


def main():
    if not SOURCE_DOCX.exists():
        raise FileNotFoundError(SOURCE_DOCX)
    for required in (LOGO, INVENTORY_XLSX, SCREENSHOT_ROOT, MOCKUP_ROOT, API_ROOT):
        if not required.exists():
            raise FileNotFoundError(required)

    document = Document(SOURCE_DOCX)
    format_cover(document)
    append_evidence(document)
    document.save(FINAL_DOCX)
    build_changelog()

    results_path = SCREENSHOT_ROOT / "capture_results.json"
    results = json.loads(results_path.read_text(encoding="utf-8"))
    summary = {
        "finalDocx": str(FINAL_DOCX),
        "changelogDocx": str(CHANGELOG_DOCX),
        "screenshots": len(list(SCREENSHOT_ROOT.rglob("*.png"))),
        "mockups": len(list(MOCKUP_ROOT.rglob("*.png"))),
        "apiEvidence": len(list(API_ROOT.rglob("*.png"))),
        "screenshotResults": len(results),
    }
    print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
