from __future__ import annotations

import json
import re
import textwrap
import urllib.error
import urllib.request
from collections import Counter
from pathlib import Path

from openpyxl import Workbook
from openpyxl.styles import Alignment, Font, PatternFill
from openpyxl.utils import get_column_letter
from PIL import Image, ImageDraw, ImageFont


APP_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = APP_ROOT.parent
OUTPUT_ROOT = APP_ROOT / "docs" / "evidence"
MOCKUP_ROOT = OUTPUT_ROOT / "mockups"
API_ROOT = OUTPUT_ROOT / "api"
XLSX_PATH = Path(r"D:\api_inventory.xlsx")
BASE_URL = "http://localhost:5168"

FONT_REGULAR = Path(r"C:\Windows\Fonts\arial.ttf")
FONT_BOLD = Path(r"C:\Windows\Fonts\arialbd.ttf")

BLUE = "#0F3D69"
BLUE_2 = "#EAF2F8"
ORANGE = "#F47C20"
INK = "#1F2937"
MUTED = "#667085"
GREEN = "#16794D"
RED = "#B42318"
WHITE = "#FFFFFF"


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONT_BOLD if bold else FONT_REGULAR), size)


def ensure_dirs() -> None:
    for path in (MOCKUP_ROOT, API_ROOT):
        path.mkdir(parents=True, exist_ok=True)


def draw_wrapped(draw: ImageDraw.ImageDraw, text: str, xy: tuple[int, int], width: int, fnt, fill=INK, spacing=8) -> int:
    chars = max(18, int(width / max(fnt.size * 0.55, 1)))
    lines = []
    for paragraph in text.splitlines() or [""]:
        lines.extend(textwrap.wrap(paragraph, width=chars) or [""])
    y = xy[1]
    for line in lines:
        draw.text((xy[0], y), line, font=fnt, fill=fill)
        y += fnt.size + spacing
    return y


def rounded(draw, box, radius=18, fill=WHITE, outline="#D0D5DD", width=2):
    draw.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)


def create_mockup(item: dict) -> Path:
    role = item["role"]
    folder = MOCKUP_ROOT / role.lower().replace("/", "-")
    folder.mkdir(parents=True, exist_ok=True)
    target = folder / f'{item["slug"]}.png'

    canvas = Image.new("RGB", (1600, 1000), "#F5F7FA")
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((0, 0, 1600, 92), fill=BLUE)
    draw.rectangle((0, 92, 280, 1000), fill="#0A3157")
    draw.text((38, 27), "CarRentalSystem", font=font(30, True), fill=WHITE)
    draw.text((1215, 31), item["badge"], font=font(22, True), fill="#FFD7B5")

    draw.text((38, 130), f'{role} Portal', font=font(25, True), fill=WHITE)
    for i, nav in enumerate(item["nav"]):
        y = 205 + i * 62
        if i == 0:
            draw.rounded_rectangle((24, y - 12, 256, y + 38), radius=12, fill="#195584")
        draw.text((42, y), nav, font=font(19, i == 0), fill=WHITE)

    draw.text((330, 130), item["title"], font=font(38, True), fill=BLUE)
    draw.text((332, 182), f'Actor: {role}  |  Trạng thái: {item["badge"]}', font=font(20), fill=MUTED)

    cards = [
        ("Dữ liệu hiển thị", item["data"], "#EAF2F8"),
        ("Thao tác chính", item["actions"], "#FFF4E8"),
        ("Validation", item["validation"], "#ECFDF3"),
        ("Bảo mật & phân quyền", item["security"], "#FEF3F2"),
    ]
    positions = [(330, 245, 930, 475), (970, 245, 1540, 475), (330, 515, 930, 785), (970, 515, 1540, 785)]
    for (heading, body, bg), box in zip(cards, positions):
        rounded(draw, box, fill=bg, outline="#C8D3DF")
        draw.text((box[0] + 28, box[1] + 24), heading, font=font(24, True), fill=BLUE)
        draw_wrapped(draw, body, (box[0] + 28, box[1] + 72), box[2] - box[0] - 56, font(19), spacing=7)

    rounded(draw, (330, 830, 1540, 945), fill=WHITE, outline=ORANGE, width=3)
    draw.text((355, 852), "Ghi chú tài liệu", font=font(22, True), fill=ORANGE)
    draw_wrapped(draw, item["note"], (590, 850), 910, font(19), spacing=6)
    canvas.save(target)
    return target


def http_call(method: str, path: str, body: dict | None = None) -> dict:
    url = BASE_URL + path
    data = json.dumps(body).encode("utf-8") if body is not None else None
    headers = {"Content-Type": "application/json"} if body is not None else {}
    request = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            raw = response.read().decode("utf-8", errors="replace")
            return {"method": method, "path": path, "status": response.status, "response": raw[:900]}
    except urllib.error.HTTPError as exc:
        raw = exc.read().decode("utf-8", errors="replace")
        return {"method": method, "path": path, "status": exc.code, "response": raw[:900]}
    except Exception as exc:
        return {"method": method, "path": path, "status": "ERROR", "response": str(exc)}


def create_api_evidence(group: str, title: str, entries: list[dict], status_note: str) -> Path:
    folder = API_ROOT / group
    folder.mkdir(parents=True, exist_ok=True)
    target = folder / f"{group}_evidence.png"
    canvas = Image.new("RGB", (1800, 1120), "#F4F6F8")
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((0, 0, 1800, 115), fill=BLUE)
    draw.text((50, 30), title, font=font(38, True), fill=WHITE)
    draw.text((1330, 40), "API EVIDENCE - LOCAL/MOCK", font=font(22, True), fill="#FFD7B5")
    y = 155
    for entry in entries[:4]:
        rounded(draw, (55, y, 1745, y + 195), fill=WHITE, outline="#C7D2DF")
        status = str(entry["status"])
        status_color = GREEN if status.startswith("2") else RED if status.startswith(("4", "5")) else ORANGE
        draw.rounded_rectangle((80, y + 25, 175, y + 75), radius=10, fill=status_color)
        draw.text((101, y + 38), status, font=font(20, True), fill=WHITE)
        draw.text((205, y + 29), f'{entry["method"]} {entry["path"]}', font=font(25, True), fill=BLUE)
        response = re.sub(r"\s+", " ", entry["response"]).strip() or "(response body rỗng)"
        draw_wrapped(draw, f"Response mẫu: {response}", (205, y + 78), 1480, font(17), fill=INK, spacing=5)
        y += 220
    rounded(draw, (55, 1010, 1745, 1080), fill="#FFF4E8", outline=ORANGE)
    draw.text((80, 1032), status_note, font=font(18, True), fill=INK)
    canvas.save(target)
    return target


def infer_area(path: Path) -> str:
    parts = path.parts
    return parts[parts.index("Areas") + 1] if "Areas" in parts else ""


def infer_role(text: str, area: str) -> str:
    policy = re.search(r'Authorize\(Policy\s*=\s*PortalPolicyNames\.([A-Za-z0-9_]+)', text)
    roles = re.search(r'Authorize\(Roles\s*=\s*([^)]+)\)', text)
    if policy:
        return policy.group(1).replace("Require", "")
    if roles:
        return roles.group(1).replace('"', "").replace("UserRoleConstants.", "")
    return area or ("Authenticated" if "[Authorize" in text else "Guest/Public")


def controller_inventory() -> list[dict]:
    rows: list[dict] = []
    files = list((APP_ROOT / "Controllers").glob("*Controller.cs")) + list((APP_ROOT / "Areas").glob("*/Controllers/*Controller.cs"))
    action_re = re.compile(
        r'(?P<attrs>(?:\s*\[[^\]]+\]\s*)*)\s*public\s+(?:async\s+)?(?:Task<)?IActionResult>?\s+(?P<action>[A-Za-z0-9_]+)\s*\(',
        re.MULTILINE,
    )
    for file in sorted(files):
        text = file.read_text(encoding="utf-8", errors="replace")
        area = infer_area(file)
        controller = file.stem.removesuffix("Controller")
        role = infer_role(text[:3000], area)
        for match in action_re.finditer(text):
            attrs = match.group("attrs")
            action = match.group("action")
            if action in {"Dispose"}:
                continue
            method_match = re.search(r'\[Http(Get|Post|Put|Delete|Patch)', attrs)
            method = method_match.group(1).upper() if method_match else "GET"
            route_match = re.search(r'\[(?:Http(?:Get|Post|Put|Delete|Patch)|Route)\("([^"]+)"', attrs)
            if route_match and route_match.group(1).startswith("/"):
                route = route_match.group(1)
            elif route_match and route_match.group(1):
                route = f"/{route_match.group(1)}"
            else:
                prefix = f"/{area}" if area else ""
                route = f"{prefix}/{controller}/{action}"
            rows.append(
                {
                    "Module": controller,
                    "Controller/Area": f"{area + '/' if area else ''}{controller}Controller",
                    "Method": method,
                    "Action/Endpoint": route,
                    "View/API": "MVC action / JSON tùy ActionResult",
                    "Role": role,
                    "Trạng thái": "Có trong source",
                    "Ghi chú": str(file.relative_to(APP_ROOT)),
                }
            )
    return rows


def swagger_inventory() -> tuple[dict, list[dict]]:
    swagger = json.load(urllib.request.urlopen(BASE_URL + "/swagger/v1/swagger.json", timeout=15))
    rows = []
    for path, operations in swagger.get("paths", {}).items():
        for method, operation in operations.items():
            rows.append(
                {
                    "Nhóm API": ", ".join(operation.get("tags") or ["Untagged"]),
                    "Method": method.upper(),
                    "Endpoint": path,
                    "Actor": "Theo authorization trong controller",
                    "Request mẫu": ", ".join((operation.get("requestBody") or {}).get("content", {}).keys()) or "-",
                    "Response mẫu": ", ".join((operation.get("responses") or {}).keys()),
                    "Evidence image": "",
                    "Trạng thái": "Swagger/OpenAPI thật",
                }
            )
    return swagger, rows


def style_sheet(ws) -> None:
    fill = PatternFill("solid", fgColor="0F3D69")
    for cell in ws[1]:
        cell.fill = fill
        cell.font = Font(color="FFFFFF", bold=True)
        cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
    ws.freeze_panes = "A2"
    ws.auto_filter.ref = ws.dimensions
    for column in range(1, ws.max_column + 1):
        values = [str(ws.cell(row, column).value or "") for row in range(1, min(ws.max_row, 250) + 1)]
        ws.column_dimensions[get_column_letter(column)].width = min(max(max(map(len, values)) + 2, 13), 55)
    for row in ws.iter_rows(min_row=2):
        for cell in row:
            cell.alignment = Alignment(vertical="top", wrap_text=True)


def write_inventory(controller_rows: list[dict], swagger_rows: list[dict]) -> None:
    wb = Workbook()
    summary = wb.active
    summary.title = "Summary"
    summary.append(["Hạng mục", "Số lượng", "Ghi chú"])
    summary.append(["Controller actions", len(controller_rows), "Trích từ Controllers và Areas/*/Controllers"])
    summary.append(["Swagger endpoints", len(swagger_rows), "Chỉ các endpoint xuất hiện trong swagger.json"])
    summary.append(["Staff portal", "Riêng", "Areas/Staff có controller, view và policy riêng"])
    summary.append(["Manager portal", "Riêng", "Areas/Manager có controller, view và policy riêng"])
    summary.append(["Admin portal", "Riêng", "Areas/Admin có controller, view và policy riêng"])
    summary.append(["Driver portal", "Foundation/mock", "Không có Driver Area/controller độc lập; /Driver redirect OwnerCars"])
    style_sheet(summary)

    for title, rows in (("Controller Actions", controller_rows), ("Swagger API", swagger_rows)):
        ws = wb.create_sheet(title)
        headers = list(rows[0].keys()) if rows else ["Không có dữ liệu"]
        ws.append(headers)
        for row in rows:
            ws.append([row.get(header, "") for header in headers])
        style_sheet(ws)

    modules = wb.create_sheet("Module Status")
    modules.append(["Module", "Trạng thái", "Evidence/ghi chú"])
    statuses = [
        ("Auth/User/Profile", "Implemented", "AuthController, Views/Auth"),
        ("Car/Catalog/Owner", "Implemented", "CarController, CarCatalogController, OwnerCarsController"),
        ("Booking/Hold/Cart", "Implemented", "BookingController, CartController, reservation services"),
        ("Payment/PayOS", "Foundation + mock/sandbox evidence", "Không gọi PayOS live"),
        ("eKYC", "Foundation + mock evidence", "Không gọi vendor thật"),
        ("Contract/Handover", "Implemented", "MVC actions và views hiện có"),
        ("Tracking/GPS/ANPR", "Foundation + mock evidence", "Không gọi thiết bị thật"),
        ("Staff", "Implemented portal riêng", "Areas/Staff"),
        ("Manager", "Implemented portal riêng", "Areas/Manager"),
        ("Admin", "Implemented portal riêng", "Areas/Admin"),
        ("Driver", "Foundation/mockup", "Không có Driver controller/area riêng"),
        ("AI Copilot", "Foundation/mock", "Swagger POST /api/ai/chat; provider local disabled"),
    ]
    for row in statuses:
        modules.append(row)
    style_sheet(modules)
    wb.save(XLSX_PATH)


def main() -> None:
    ensure_dirs()
    controllers = controller_inventory()
    swagger, swagger_rows = swagger_inventory()
    write_inventory(controllers, swagger_rows)

    mockups = [
        dict(role="Driver", slug="driver_dashboard_foundation", title="Driver Dashboard", badge="FOUNDATION / MOCKUP ĐỀ XUẤT",
             nav=["Dashboard", "Chuyến được giao", "Sự cố", "Hồ sơ"],
             data="Danh sách chuyến được phân công, thời gian đón, điểm đến và trạng thái chuyến.",
             actions="Nhận chuyến, bắt đầu chuyến, báo sự cố và cập nhật trạng thái.",
             validation="Chỉ cập nhật chuyến được phân công; chặn trạng thái nhảy sai.",
             security="Driver chỉ xem chuyến của mình; không xem dữ liệu tài chính hoặc quản trị.",
             note="Source hiện không có Driver Area/controller độc lập; /Driver redirect về OwnerCars."),
        dict(role="Driver", slug="driver_assigned_trip", title="Assigned Trip Detail", badge="FOUNDATION / MOCKUP ĐỀ XUẤT",
             nav=["Chuyến được giao", "Chi tiết", "Lịch sử"],
             data="Thông tin chuyến đã che PII, lộ trình mock, checklist và liên hệ hỗ trợ.",
             actions="Xác nhận đến điểm đón, bắt đầu, kết thúc, báo sự cố.",
             validation="Yêu cầu đúng thứ tự trạng thái và evidence tối thiểu.",
             security="Không hiển thị CCCD/GPLX; GPS chỉ mock.",
             note="Mockup dùng để mô tả hướng phát triển, không phải screenshot chạy thật."),
        dict(role="Staff", slug="staff_handover_checklist", title="Handover Checklist", badge="MOCKUP ĐỀ XUẤT THEO ROUTE THẬT",
             nav=["Dashboard", "Booking", "Bàn giao", "Nhận xe trả", "Hỗ trợ"],
             data="Booking được giao, tình trạng xe, odo, nhiên liệu và inventory.",
             actions="Chụp evidence mock, xác nhận checklist, bàn giao xe.",
             validation="Không hoàn tất nếu thiếu trường bắt buộc hoặc booking không thuộc phạm vi.",
             security="Staff chỉ thao tác booking được phân công/khu vực.",
             note="Đối chiếu Areas/Staff/Handover và Handover workflow hiện có."),
        dict(role="Manager", slug="manager_settlement_approval", title="Settlement Approval", badge="FOUNDATION / MOCKUP ĐỀ XUẤT",
             nav=["Dashboard KPI", "Booking", "Nhân viên", "Vận hành", "Báo cáo"],
             data="Damage/late/fuel review, số tiền đề xuất và evidence liên quan.",
             actions="Xem xét, chuyển cấp Admin, ghi nhận nhận xét.",
             validation="Không tự phê duyệt settlement ngoài quyền hiện tại.",
             security="Manager xem dữ liệu khu vực; quyền approve cuối hiện là Admin-only.",
             note="Source hiện có Admin Settlement; mockup này mô tả màn hình Manager escalation."),
        dict(role="Manager", slug="manager_staff_assignment", title="Staff Assignment", badge="MOCKUP ĐỀ XUẤT THEO ROUTE THẬT",
             nav=["Dashboard KPI", "Booking", "Nhân viên", "Vận hành", "Báo cáo"],
             data="Nhân sự theo khu vực, tải công việc và booking chưa phân công.",
             actions="Gán/gỡ nhân viên khỏi booking hoặc khu vực.",
             validation="Nhân viên phải thuộc phạm vi khu vực hợp lệ.",
             security="Manager không thay đổi role hệ thống hoặc quyền Admin.",
             note="Đối chiếu Areas/Manager/Staff và Areas/Manager/Booking."),
        dict(role="Admin", slug="admin_api_key_management", title="API Key Management", badge="MOCKUP ĐỀ XUẤT THEO ROUTE THẬT",
             nav=["Dashboard", "Người dùng", "Xe", "Booking", "Hệ thống"],
             data="Key prefix, owner, scopes, trạng thái, ngày hết hạn; không hiển thị raw key sau tạo.",
             actions="Tạo API key, thu hồi, xem audit.",
             validation="Scopes hợp lệ; owner tồn tại; ngày hết hạn hợp lệ.",
             security="Admin-only; raw secret chỉ trả một lần và không ghi vào tài liệu.",
             note="Đối chiếu Areas/Admin/ApiKeysController."),
        dict(role="Admin", slug="admin_settlement_approval", title="Settlement Approval", badge="MOCKUP ĐỀ XUẤT THEO ROUTE THẬT",
             nav=["Dashboard", "Booking", "Thanh toán", "Đối soát", "Audit"],
             data="Settlement review, booking, evidence, phí damage/late/fuel và trạng thái.",
             actions="Tạo, duyệt, từ chối kèm lý do.",
             validation="Lý do bắt buộc; booking/evidence phải tồn tại.",
             security="Admin-only; audit mọi quyết định.",
             note="Đối chiếu Areas/Admin/SettlementController."),
        dict(role="Customer", slug="customer_support_ticket", title="Support Ticket", badge="MOCKUP ĐỀ XUẤT THEO ROUTE THẬT",
             nav=["Trang chủ", "Xe", "Booking", "Hỗ trợ", "Hồ sơ"],
             data="Danh sách ticket của chính khách hàng và trạng thái xử lý.",
             actions="Tạo ticket, chọn booking liên quan, xem phản hồi.",
             validation="Subject/description bắt buộc; booking phải thuộc người dùng.",
             security="Không xem ticket của người khác.",
             note="Đối chiếu SupportController.Mine/Create."),
    ]
    for item in mockups:
        create_mockup(item)

    calls = {
        "system": [
            http_call("GET", "/live"),
            http_call("GET", "/ready"),
            http_call("GET", "/swagger/v1/swagger.json"),
        ],
        "catalog": [
            http_call("GET", "/api/blogs/latest"),
            http_call("GET", "/api/Voucher/check?code=DOCSAFE"),
            http_call("GET", "/FAQ"),
        ],
        "security": [
            http_call("GET", "/admin/api/users"),
            http_call("GET", "/admin/api/locations"),
            http_call("POST", "/api/ai/chat", {"message": "Tóm tắt vận hành bằng dữ liệu mock"}),
            http_call("POST", "/integrations/camera/anpr-events", {"plate": "51K-***.12"}),
        ],
    }
    evidence_map = [
        ("system", "System Health & Swagger API", calls["system"], "Evidence chạy thật trên Local; không chứa secret."),
        ("catalog", "Catalog / Blog / Voucher API", calls["catalog"], "Request GET an toàn; dữ liệu phản hồi được rút gọn."),
        ("security", "Protected / Foundation API", calls["security"], "401/403/302 là bằng chứng phân quyền hợp lệ; không gọi vendor hoặc thiết bị thật."),
    ]
    for group, title, entries, note in evidence_map:
        create_api_evidence(group, title, entries, note)

    summary = {
        "controllerActions": len(controllers),
        "swaggerEndpoints": len(swagger_rows),
        "mockups": len(mockups),
        "apiEvidenceGroups": len(evidence_map),
        "swaggerTags": dict(Counter(row["Nhóm API"] for row in swagger_rows)),
    }
    (OUTPUT_ROOT / "generation_summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False))


if __name__ == "__main__":
    main()
