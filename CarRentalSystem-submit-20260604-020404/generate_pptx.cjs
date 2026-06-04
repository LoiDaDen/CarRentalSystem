'use strict';
const PptxGenJS = require('pptxgenjs');

// Color palette
const C = {
    primary:   '1a56db',
    success:   '16a34a',
    warning:   'f59e0b',
    danger:    'dc2626',
    purple:    '7c3aed',
    orange:    'ea580c',
    cyan:      '0891b2',
    bg:        '0d1117',
    bgCard:    '21262d',
    bgSec:     '161b22',
    text:      'e6edf3',
    muted:     '8b949e',
    border:    '30363d',
};

// Helpers
const mkShadow = () => ({ type: 'outer', color: '000000', blur: 8, offset: 3, angle: 135, opacity: 0.25 });
const mkCardShadow = () => ({ type: 'outer', color: '000000', blur: 6, offset: 2, angle: 135, opacity: 0.2 });

function addSlideHeader(slide, title, subtitle, accentColor) {
    accentColor = accentColor || C.primary;
    slide.addShape('rect', {
        x: 0, y: 0, w: 10, h: 1.0,
        fill: { color: C.bgSec },
        line: { color: C.border, width: 0.5 }
    });
    slide.addShape('rect', {
        x: 0, y: 0, w: 0.12, h: 1.0,
        fill: { color: accentColor }
    });
    slide.addText(title, {
        x: 0.3, y: 0.12, w: 9.4, h: 0.52,
        fontSize: 24, bold: true, color: C.text, fontFace: 'Inter', margin: 0
    });
    if (subtitle) {
        slide.addText(subtitle, {
            x: 0.3, y: 0.6, w: 9.4, h: 0.3,
            fontSize: 12, color: C.muted, fontFace: 'Inter', margin: 0
        });
    }
}

function addFooter(slide, pageNum) {
    slide.addText('Quá Trình Áp Dụng Scrum — Y5 | Quản Lý Dự Án Phần Mềm 2026', {
        x: 0.3, y: 7.1, w: 7, h: 0.3,
        fontSize: 8, color: C.muted, fontFace: 'Inter', margin: 0
    });
    slide.addText(`${pageNum} / 18`, {
        x: 8.5, y: 7.1, w: 1.2, h: 0.3,
        fontSize: 8, color: C.muted, fontFace: 'Inter', align: 'right', margin: 0
    });
}

function addCard(slide, x, y, w, h, fillColor, borderColor) {
    slide.addShape('roundRect', {
        x, y, w, h,
        fill: { color: fillColor || C.bgCard },
        line: { color: borderColor || C.border, width: 1 },
        rectRadius: 0.08,
        shadow: mkCardShadow()
    });
}

function addBadge(slide, x, y, text, bgColor, textColor) {
    slide.addShape('roundRect', {
        x, y, w: text.length * 0.09 + 0.2, h: 0.25,
        fill: { color: bgColor, transparency: 70 },
        line: { color: bgColor, width: 1 },
        rectRadius: 0.12
    });
    slide.addText(text, {
        x: x + 0.1, y: y + 0.02, w: text.length * 0.09 + 0.1, h: 0.22,
        fontSize: 9, bold: true, color: textColor || textColor, fontFace: 'Inter', margin: 0
    });
}

// ======================== BUILD PPTX ========================
const pptx = new PptxGenJS();
pptx.layout = 'LAYOUT_16x9';
pptx.title = 'Quá Trình Áp Dụng Scrum — Y5';
pptx.author = 'Nhóm Y5';
pptx.subject = 'Quản Lý Dự Án Phần Mềm';
pptx.defineLayout({ name: 'CUSTOM', width: 10, height: 5.63 });
pptx.layout = 'CUSTOM';

// Fonts
pptx.addSlide({ background: { color: C.bg }, color: C.text });

// ==================== SLIDE 1: TITLE ====================
let slide = pptx.addSlide({ background: { color: C.bg } });

// Gradient-like background shapes
slide.addShape('rect', {
    x: 0, y: 0, w: 10, h: 5.625,
    fill: { color: '111827' }
});
slide.addShape('rect', {
    x: 0, y: 3.5, w: 10, h: 2.125,
    fill: { color: C.bgSec }
});

// Logo circle
slide.addShape('ellipse', {
    x: 4.25, y: 0.6, w: 1.5, h: 1.5,
    fill: { color: C.primary },
    shadow: mkShadow()
});
slide.addText('Scrum', {
    x: 4.25, y: 0.95, w: 1.5, h: 0.4,
    fontSize: 11, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
});
slide.addText('Y5', {
    x: 4.25, y: 1.3, w: 1.5, h: 0.4,
    fontSize: 16, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
});

// Title
slide.addText('Quá Trình Áp Dụng Scrum', {
    x: 0.5, y: 2.2, w: 9, h: 0.8,
    fontSize: 38, bold: true, color: C.text, align: 'center', fontFace: 'Inter', margin: 0
});
slide.addText('Trong Quản Lý Dự Án Phần Mềm', {
    x: 0.5, y: 2.9, w: 9, h: 0.5,
    fontSize: 22, bold: false, color: C.muted, align: 'center', fontFace: 'Inter', margin: 0
});

// Accent line
slide.addShape('rect', {
    x: 3.5, y: 3.5, w: 3, h: 0.06,
    fill: { color: C.primary }
});

slide.addText('Khó Khăn, Thuận Lợi & Giải Pháp Đề Xuất Cải Tiến', {
    x: 0.5, y: 3.65, w: 9, h: 0.4,
    fontSize: 14, color: C.muted, align: 'center', fontFace: 'Inter', margin: 0
});

slide.addText('Nhóm Y5  |  Quản Lý Dự Án Phần Mềm  |  2026', {
    x: 0.5, y: 5.0, w: 9, h: 0.3,
    fontSize: 11, color: C.muted, align: 'center', fontFace: 'Inter', margin: 0
});

// ==================== SLIDE 2: TOC ====================
slide = pptx.addSlide({ background: { color: C.bg } });
addSlideHeader(slide, 'Mục Lục', '5 phần chính — Thời gian trình bày: 20-25 phút', C.primary);

const tocItems = [
    { num: '1', title: 'Scrum Essentials', desc: 'Kiến thức nền tảng\nScrum Framework', time: '3 phút', color: C.primary },
    { num: '2', title: 'Thuận Lợi', desc: 'Kết quả tích cực\nkhi áp dụng Scrum', time: '4 phút', color: C.success },
    { num: '3', title: 'Khó Khăn', desc: 'Thách thức & thất bại\nkhi triển khai', time: '6 phút', color: C.danger },
    { num: '4', title: 'Giải Pháp', desc: 'Cải tiến & đề xuất\ncải tiến mới', time: '6 phút', color: C.warning },
    { num: '5', title: 'Kết Luận & Q&A', desc: 'Tóm tắt &\ncâu hỏi thảo luận', time: '5 phút', color: C.purple },
];

tocItems.forEach((item, i) => {
    const x = 0.3 + i * 1.9;
    addCard(slide, x, 1.2, 1.75, 2.8, C.bgCard, item.color);
    // Number circle
    slide.addShape('ellipse', {
        x: x + 0.55, y: 1.35, w: 0.65, h: 0.65,
        fill: { color: item.color }
    });
    slide.addText(item.num, {
        x: x + 0.55, y: 1.45, w: 0.65, h: 0.5,
        fontSize: 18, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addText(item.title, {
        x: x + 0.05, y: 2.1, w: 1.65, h: 0.4,
        fontSize: 12, bold: true, color: C.text, align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addText(item.desc, {
        x: x + 0.05, y: 2.5, w: 1.65, h: 0.8,
        fontSize: 9, color: C.muted, align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addText('⏱ ' + item.time, {
        x: x + 0.05, y: 3.5, w: 1.65, h: 0.3,
        fontSize: 9, bold: true, color: item.color, align: 'center', fontFace: 'Inter', margin: 0
    });
});

slide.addShape('roundRect', {
    x: 0.3, y: 4.25, w: 9.4, h: 0.45,
    fill: { color: C.bgCard },
    line: { color: C.border, width: 1 },
    rectRadius: 0.08
});
slide.addText('📋 Nội dung thực tế có minh chứng  |  18 slides  |  20-25 phút  |  Tiếng Việt', {
    x: 0.5, y: 4.33, w: 9, h: 0.3,
    fontSize: 11, color: C.muted, align: 'center', fontFace: 'Inter', margin: 0
});

addFooter(slide, 2);

// ==================== SLIDE 3: SCRUM ESSENTIALS ====================
slide = pptx.addSlide({ background: { color: C.bg } });
addSlideHeader(slide, 'Scrum Essentials — Kiến Thức Nền Tảng', '3 Vai trò | 5 Sự kiện | 3 Artifacts | 5 Giá trị', C.primary);

// 3 columns
const essentials = [
    {
        color: C.primary, title: '3 Vai Trò', icon: '👥',
        items: [
            '👑 Product Owner — Tầm nhìn sản phẩm, ưu tiên backlog',
            '🎩 Scrum Master — Coaching, loại bỏ trở ngại',
            '💻 Dev Team — Tự tổ chức, 3-9 người cross-functional',
        ]
    },
    {
        color: C.success, title: '5 Sự Kiện', icon: '📅',
        items: [
            '🔄 Sprint — 2-4 tuần, container cho mọi sự kiện',
            '📋 Sprint Planning — Đầu Sprint, xác định Sprint Goal',
            '⚡ Daily Standup — 15 phút/ngày, đồng bộ hóa',
            '🎬 Sprint Review — Demo sản phẩm, thu feedback',
            '🔍 Retrospective — Cải tiến quá trình làm việc',
        ]
    },
    {
        color: C.warning, title: '3 Artifacts', icon: '📦',
        items: [
            '📝 Product Backlog — Danh sách feature, do PO quản lý',
            '📑 Sprint Backlog — Item được chọn cho Sprint hiện tại',
            '📊 Increment — Tổng hợp item đã hoàn thành',
        ]
    },
];

essentials.forEach((col, i) => {
    const x = 0.3 + i * 3.2;
    addCard(slide, x, 1.15, 3.0, 2.75, C.bgCard, C.border);
    slide.addShape('rect', {
        x, y: 1.15, w: 3.0, h: 0.06,
        fill: { color: col.color }
    });
    slide.addText(col.title, {
        x: x + 0.1, y: 1.28, w: 2.8, h: 0.35,
        fontSize: 13, bold: true, color: col.color, fontFace: 'Inter', margin: 0
    });
    col.items.forEach((item, j) => {
        slide.addText(item, {
            x: x + 0.1, y: 1.65 + j * 0.45, w: 2.8, h: 0.42,
            fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
        });
    });
});

// 5 Values
slide.addShape('rect', {
    x: 0.3, y: 4.05, w: 9.4, h: 0.45,
    fill: { color: C.bgCard },
    line: { color: C.border, width: 1 }
});
slide.addText('5 Giá Trị Cốt Lõi:', {
    x: 0.4, y: 4.1, w: 1.5, h: 0.35,
    fontSize: 10, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});
const values = [
    { v: 'Commitment', c: C.primary },
    { v: 'Focus', c: C.success },
    { v: 'Openness', c: C.purple },
    { v: 'Respect', c: C.orange },
    { v: 'Courage', c: C.danger },
];
values.forEach((val, i) => {
    slide.addShape('roundRect', {
        x: 1.9 + i * 1.45, y: 4.1, w: 1.35, h: 0.35,
        fill: { color: val.c, transparency: 75 },
        line: { color: val.c, width: 1 },
        rectRadius: 0.15
    });
    slide.addText(val.v, {
        x: 1.9 + i * 1.45, y: 4.12, w: 1.35, h: 0.32,
        fontSize: 9, bold: true, color: val.c, align: 'center', fontFace: 'Inter', margin: 0
    });
});

// 4P of Agile Manifesto
slide.addShape('rect', {
    x: 0.3, y: 4.65, w: 9.4, h: 1.1,
    fill: { color: C.bgCard },
    line: { color: C.border, width: 1 },
    shadow: mkCardShadow()
});
slide.addText('Agile Manifesto — 4 Nguyên Tắc:', {
    x: 0.4, y: 4.7, w: 3, h: 0.3,
    fontSize: 10, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});
const manifesto = [
    { left: 'Process & Tools', right: 'Individuals & Interactions', c1: C.muted, c2: C.success },
    { left: 'Comprehensive Docs', right: 'Working Software', c1: C.muted, c2: C.success },
    { left: 'Contract Negotiation', right: 'Customer Collaboration', c1: C.muted, c2: C.success },
    { left: 'Following a Plan', right: 'Responding to Change', c1: C.muted, c2: C.success },
];
manifesto.forEach((m, i) => {
    const col = i < 2 ? 0 : 1;
    const row = i % 2;
    slide.addText(m.left + '  →  ', {
        x: 0.4 + col * 4.7, y: 4.95 + row * 0.28, w: 2.3, h: 0.25,
        fontSize: 8, color: m.c1, fontFace: 'Inter', margin: 0
    });
    slide.addText(m.right, {
        x: 2.65 + col * 4.7, y: 4.95 + row * 0.28, w: 2, h: 0.25,
        fontSize: 8, bold: true, color: m.c2, fontFace: 'Inter', margin: 0
    });
});

addFooter(slide, 3);

// ==================== SLIDE 4: SCRUM FRAMEWORK ====================
slide = pptx.addSlide({ background: { color: C.bg } });
addSlideHeader(slide, 'Scrum Framework — Tổng Quan', 'Sơ đồ vai trò & vòng đời Sprint', C.primary);

// Left: Roles diagram
addCard(slide, 0.3, 1.15, 4.4, 4.3, C.bgCard, C.border);

// Center dot
slide.addShape('ellipse', {
    x: 2.0, y: 2.9, w: 1.0, h: 1.0,
    fill: { color: C.primary },
    shadow: mkShadow()
});
slide.addText('Scrum', {
    x: 2.0, y: 3.1, w: 1.0, h: 0.35,
    fontSize: 10, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
});
slide.addText('Framework', {
    x: 2.0, y: 3.4, w: 1.0, h: 0.25,
    fontSize: 8, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
});

// PO
slide.addShape('roundRect', {
    x: 1.65, y: 1.35, w: 1.7, h: 1.0,
    fill: { color: C.bgSec },
    line: { color: C.warning, width: 2 },
    rectRadius: 0.1,
    shadow: mkCardShadow()
});
slide.addText('👑 Product Owner', {
    x: 1.65, y: 1.45, w: 1.7, h: 0.3,
    fontSize: 10, bold: true, color: C.warning, align: 'center', fontFace: 'Inter', margin: 0
});
slide.addText('Tầm nhìn & Backlog', {
    x: 1.65, y: 1.75, w: 1.7, h: 0.5,
    fontSize: 8, color: C.muted, align: 'center', fontFace: 'Inter', margin: 0
});

// SM
slide.addShape('roundRect', {
    x: 0.45, y: 3.15, w: 1.5, h: 1.0,
    fill: { color: C.bgSec },
    line: { color: C.success, width: 2 },
    rectRadius: 0.1,
    shadow: mkCardShadow()
});
slide.addText('🎩 Scrum Master', {
    x: 0.45, y: 3.25, w: 1.5, h: 0.3,
    fontSize: 10, bold: true, color: C.success, align: 'center', fontFace: 'Inter', margin: 0
});
slide.addText('Coaching\nLoại bỏ trở ngại', {
    x: 0.45, y: 3.55, w: 1.5, h: 0.55,
    fontSize: 8, color: C.muted, align: 'center', fontFace: 'Inter', margin: 0
});

// Dev Team
slide.addShape('roundRect', {
    x: 3.05, y: 3.15, w: 1.5, h: 1.0,
    fill: { color: C.bgSec },
    line: { color: C.primary, width: 2 },
    rectRadius: 0.1,
    shadow: mkCardShadow()
});
slide.addText('💻 Dev Team', {
    x: 3.05, y: 3.25, w: 1.5, h: 0.3,
    fontSize: 10, bold: true, color: C.primary, align: 'center', fontFace: 'Inter', margin: 0
});
slide.addText('Tự tổ chức\nCross-functional', {
    x: 3.05, y: 3.55, w: 1.5, h: 0.55,
    fontSize: 8, color: C.muted, align: 'center', fontFace: 'Inter', margin: 0
});

// Right: Sprint Lifecycle timeline
addCard(slide, 4.9, 1.15, 4.8, 4.3, C.bgCard, C.border);
slide.addText('Vòng Đời Sprint', {
    x: 5.1, y: 1.3, w: 4.4, h: 0.35,
    fontSize: 13, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});

const sprintSteps = [
    { icon: '📋', title: 'Sprint Planning', desc: 'Chọn PBI, xác định Sprint Goal, tạo Sprint Backlog', color: C.primary },
    { icon: '⚡', title: 'Daily Standup', desc: '15 phút/ngày, đồng bộ hóa, loại bỏ blockers', color: C.success },
    { icon: '🔨', title: 'Development', desc: 'Code, test, tích hợp, liên tục integrate', color: C.warning },
    { icon: '🎬', title: 'Sprint Review', desc: 'Demo sản phẩm, thu thập feedback stakeholder', color: C.orange },
    { icon: '🔍', title: 'Retrospective', desc: 'Cải tiến quá trình làm việc, action items', color: C.purple },
];

sprintSteps.forEach((step, i) => {
    const y = 1.7 + i * 0.78;
    slide.addShape('ellipse', {
        x: 5.1, y, w: 0.45, h: 0.45,
        fill: { color: step.color }
    });
    slide.addText((i + 1).toString(), {
        x: 5.1, y: y + 0.05, w: 0.45, h: 0.38,
        fontSize: 12, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addText(step.icon + ' ' + step.title, {
        x: 5.65, y, w: 3.8, h: 0.25,
        fontSize: 11, bold: true, color: step.color, fontFace: 'Inter', margin: 0
    });
    slide.addText(step.desc, {
        x: 5.65, y: y + 0.25, w: 3.8, h: 0.4,
        fontSize: 8.5, color: C.muted, fontFace: 'Inter', margin: 0
    });
    if (i < sprintSteps.length - 1) {
        slide.addShape('rect', {
            x: 5.31, y: y + 0.47, w: 0.03, h: 0.28,
            fill: { color: C.border }
        });
    }
});

addFooter(slide, 4);

// ==================== SLIDE 5: WATERFALL vs SCRUM ====================
slide = pptx.addSlide({ background: { color: C.bg } });
addSlideHeader(slide, 'Agile Manifesto & So Sánh Waterfall vs Scrum', '4 Nguyên tắc cốt lõi của Agile', C.primary);

// 4 Manifesto cards
const manifestoItems = [
    { left: 'Process & Tools', right: 'Individuals & Interactions', desc: 'Tương tác con người hơn quy trình' },
    { left: 'Comprehensive Docs', right: 'Working Software', desc: 'Phần mềm chạy được hơn tài liệu' },
    { left: 'Contract Negotiation', right: 'Customer Collaboration', desc: 'Cộng tác khách hàng hơn đàm phán' },
    { left: 'Following a Plan', right: 'Responding to Change', desc: 'Đáp ứng thay đổi hơn tuân theo kế hoạch' },
];

manifestoItems.forEach((item, i) => {
    const col = i % 2;
    const row = Math.floor(i / 2);
    const x = 0.3 + col * 4.7;
    const y = 1.15 + row * 1.1;
    addCard(slide, x, y, 4.5, 1.0, C.bgCard, C.border);
    slide.addText(item.left, {
        x: x + 0.15, y: y + 0.1, w: 2.0, h: 0.35,
        fontSize: 9, color: C.muted, strikethrough: true, fontFace: 'Inter', margin: 0
    });
    slide.addText('→', {
        x: x + 2.15, y: y + 0.1, w: 0.3, h: 0.35,
        fontSize: 14, bold: true, color: C.primary, fontFace: 'Inter', margin: 0
    });
    slide.addText(item.right, {
        x: x + 2.45, y: y + 0.1, w: 1.9, h: 0.35,
        fontSize: 9, bold: true, color: C.success, fontFace: 'Inter', margin: 0
    });
    slide.addText(item.desc, {
        x: x + 0.15, y: y + 0.55, w: 4.2, h: 0.35,
        fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
    });
});

// Comparison table
addCard(slide, 0.3, 3.45, 5.5, 2.6, C.bgCard, C.border);
slide.addText('So Sánh Chi Tiết', {
    x: 0.45, y: 3.55, w: 5.2, h: 0.3,
    fontSize: 11, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});

const tableData = [
    [{ text: 'Tiêu chí', options: { fill: { color: C.bgSec }, color: C.text, bold: true } },
     { text: 'Waterfall', options: { fill: { color: C.bgSec }, color: C.muted, bold: true } },
     { text: 'Scrum', options: { fill: { color: C.bgSec }, color: C.primary, bold: true } }],
    [{ text: 'Phương pháp', options: { color: C.muted } }, { text: 'Sequential', options: { color: C.muted } }, { text: '✓ Iterative', options: { color: C.success, bold: true } }],
    [{ text: 'Thay đổi', options: { color: C.muted } }, { text: 'Rất khó', options: { color: C.muted } }, { text: '✓ Dễ thích ứng', options: { color: C.success, bold: true } }],
    [{ text: 'Feedback', options: { color: C.muted } }, { text: 'Cuối dự án', options: { color: C.muted } }, { text: '✓ Liên tục', options: { color: C.success, bold: true } }],
    [{ text: 'Giao hàng', options: { color: C.muted } }, { text: 'Một lần', options: { color: C.muted } }, { text: '✓ Liên tục', options: { color: C.success, bold: true } }],
    [{ text: 'Quản lý rủi ro', options: { color: C.muted } }, { text: 'Tập trung cuối', options: { color: C.muted } }, { text: '✓ Phân bố đều', options: { color: C.success, bold: true } }],
    [{ text: 'Stakeholder', options: { color: C.muted } }, { text: 'Cuối cùng', options: { color: C.muted } }, { text: '✓ Xuyên suốt', options: { color: C.success, bold: true } }],
];

slide.addTable(tableData, {
    x: 0.4, y: 3.9, w: 5.3, h: 2.0,
    fontFace: 'Inter', fontSize: 9,
    color: C.muted,
    border: { pt: 0.5, color: C.border },
    colW: [1.6, 1.85, 1.85],
});

// Waterfall vs Scrum visual
addCard(slide, 6.0, 3.45, 3.7, 2.6, C.bgCard, C.border);
slide.addText('Timeline So Sánh', {
    x: 6.15, y: 3.55, w: 3.4, h: 0.3,
    fontSize: 11, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});

// Waterfall bars
slide.addText('Waterfall', {
    x: 6.15, y: 3.88, w: 1.6, h: 0.25,
    fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
});
const wfBars = [0.7, 0.55, 0.45, 0.3, 0.2];
const wfLabels = ['Req', 'Design', 'Dev', 'Test', 'Deploy'];
wfBars.forEach((h, i) => {
    slide.addShape('rect', {
        x: 6.15 + i * 0.3, y: 4.15 + (0.7 - h) * 0.7, w: 0.25, h: h * 0.7,
        fill: { color: C.muted }
    });
    slide.addText(wfLabels[i], {
        x: 6.15 + i * 0.3, y: 4.9, w: 0.25, h: 0.2,
        fontSize: 6, color: C.muted, align: 'center', fontFace: 'Inter', margin: 0
    });
});

// Scrum sprints
slide.addText('Scrum', {
    x: 8.1, y: 3.88, w: 1.4, h: 0.25,
    fontSize: 9, color: C.primary, fontFace: 'Inter', margin: 0
});
const scColors = [C.primary, C.success, C.warning, C.purple];
scColors.forEach((color, i) => {
    slide.addShape('rect', {
        x: 8.1 + i * 0.38, y: 4.15, w: 0.32, h: 0.7,
        fill: { color }
    });
    slide.addText('S' + (i + 1), {
        x: 8.1 + i * 0.38, y: 4.9, w: 0.32, h: 0.2,
        fontSize: 6, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
    });
});

addFooter(slide, 5);

// ==================== SLIDE 6: THUẬN LỢI OVERVIEW ====================
slide = pptx.addSlide({ background: { color: C.bg } });

// Section header with green accent
slide.addShape('rect', {
    x: 0, y: 0, w: 10, h: 1.0,
    fill: { color: C.bgSec },
    line: { color: C.border, width: 0.5 }
});
slide.addShape('rect', {
    x: 0, y: 0, w: 0.12, h: 1.0,
    fill: { color: C.success }
});
slide.addText('Thuận Lợi — Khi Scrum Được Áp Dụng Đúng Cách', {
    x: 0.3, y: 0.12, w: 9.4, h: 0.52,
    fontSize: 22, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});
slide.addText('Những kết quả tích cực, lợi ích mang lại khi triển khai thành công', {
    x: 0.3, y: 0.6, w: 9.4, h: 0.3,
    fontSize: 11, color: C.muted, fontFace: 'Inter', margin: 0
});

// 6 benefit cards
const benefits = [
    { icon: '🚀', title: 'Tăng Tốc Độ Giao Hàng', value: '+30-50%', desc: 'Delivery Speed', color: C.primary },
    { icon: '🛡️', title: 'Chất Lượng Cao Hơn', value: '-40-60%', desc: 'Bug Rate', color: C.success },
    { icon: '😊', title: 'Stakeholder Satisfaction', value: '+25-35%', desc: 'Customer Score', color: C.warning },
    { icon: '❤️', title: 'Team Morale Cao', value: '+20%', desc: 'Retention, -30% OT', color: C.purple },
    { icon: '🔄', title: 'Thích Ứng Thay Đổi', value: '-40%', desc: 'Time-to-market', color: C.cyan },
    { icon: '👁️', title: 'Visibility Cao', value: '100%', desc: 'Burndown, Kanban', color: C.orange },
];

benefits.forEach((b, i) => {
    const col = i % 3;
    const row = Math.floor(i / 3);
    const x = 0.3 + col * 3.2;
    const y = 1.15 + row * 1.5;
    addCard(slide, x, y, 3.0, 1.35, C.bgCard, C.border);
    slide.addShape('rect', {
        x, y, w: 3.0, h: 0.06,
        fill: { color: b.color }
    });
    slide.addText(b.icon + ' ' + b.title, {
        x: x + 0.1, y: y + 0.12, w: 2.8, h: 0.35,
        fontSize: 10, bold: true, color: C.text, fontFace: 'Inter', margin: 0
    });
    slide.addText(b.value, {
        x: x + 0.1, y: y + 0.45, w: 1.0, h: 0.45,
        fontSize: 22, bold: true, color: b.color, fontFace: 'Inter', margin: 0
    });
    slide.addText(b.desc, {
        x: x + 1.1, y: y + 0.55, w: 1.8, h: 0.35,
        fontSize: 8, color: C.muted, fontFace: 'Inter', margin: 0
    });
});

// 2 bottom cards
addCard(slide, 0.3, 4.3, 4.65, 1.1, C.bgCard, C.border);
slide.addShape('rect', { x: 0.3, y: 4.3, w: 4.65, h: 0.06, fill: { color: C.danger } });
slide.addText('📉 Giảm Rủi Ro', {
    x: 0.45, y: 4.42, w: 4.3, h: 0.3,
    fontSize: 11, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});
slide.addText('-60% project failure cost nhờ phát hiện sớm — Early failures chỉ tổn thất 1-2 Sprint thay vì cả dự án', {
    x: 0.45, y: 4.72, w: 4.3, h: 0.55,
    fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
});

addCard(slide, 5.15, 4.3, 4.55, 1.1, C.bgCard, C.border);
slide.addShape('rect', { x: 5.15, y: 4.3, w: 4.55, h: 0.06, fill: { color: C.orange } });
slide.addText('💡 Khuyến Khích Innovation', {
    x: 5.3, y: 4.42, w: 4.2, h: 0.3,
    fontSize: 11, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});
slide.addText('+15-20% ý tưởng mới từ team mỗi Sprint — Spike research, 20% innovation time, Technical excellence', {
    x: 5.3, y: 4.72, w: 4.2, h: 0.55,
    fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
});

addFooter(slide, 6);

// ==================== SLIDE 7: THUẬN LỢI CHI TIẾT ====================
slide = pptx.addSlide({ background: { color: C.bg } });
addSlideHeader(slide, 'Thuận Lợi Chi Tiết — 8 Kết Quả Chính', 'Biểu đồ Velocity & Kanban Board minh họa', C.success);

const detailBenefits = [
    { icon: '🚀', title: 'Delivery Speed', val: '+30-50%', desc: 'Sprint ngắn 2 tuần tạo rhythm rõ ràng. MVP sau Sprint 1, core features Sprint 2-3.', color: C.primary },
    { icon: '🛡️', title: 'Bug Rate', val: '-40-60%', desc: 'Sprint Review + Retrospective phát hiện sớm. CI/CD tự động hóa kiểm thử.', color: C.success },
    { icon: '😊', title: 'Customer Satisfaction', val: '+25-35%', desc: 'Demo mỗi Sprint, feedback sớm, transparency tuyệt đối với stakeholder.', color: C.warning },
    { icon: '❤️', title: 'Team Retention', val: '+20%', desc: 'Self-organizing team, mỗi thành viên có tiếng nói. Cross-functional growth.', color: C.purple },
];

detailBenefits.forEach((b, i) => {
    const col = i % 2;
    const row = Math.floor(i / 2);
    const x = 0.3 + col * 4.75;
    const y = 1.15 + row * 1.55;
    addCard(slide, x, y, 4.55, 1.4, C.bgCard, C.border);
    slide.addShape('rect', { x, y, w: 4.55, h: 0.06, fill: { color: b.color } });
    slide.addText(b.icon + ' ' + b.title, {
        x: x + 0.1, y: y + 0.12, w: 2.5, h: 0.3,
        fontSize: 11, bold: true, color: C.text, fontFace: 'Inter', margin: 0
    });
    slide.addText(b.val, {
        x: x + 3.0, y: y + 0.05, w: 1.4, h: 0.5,
        fontSize: 22, bold: true, color: b.color, fontFace: 'Inter', align: 'right', margin: 0
    });
    slide.addText(b.desc, {
        x: x + 0.1, y: y + 0.55, w: 4.35, h: 0.75,
        fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
    });
});

// Kanban board
addCard(slide, 0.3, 4.35, 9.4, 1.15, C.bgCard, C.border);
slide.addText('Scrum Board (Kanban)', {
    x: 0.45, y: 4.42, w: 3, h: 0.25,
    fontSize: 10, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});

const kanbanCols = [
    { label: 'To Do', color: C.danger, cards: ['Login API', 'Dashboard UI'] },
    { label: 'In Progress', color: C.warning, cards: ['Search Feature'] },
    { label: 'Review', color: C.purple, cards: ['Auth Module'] },
    { label: 'Done', color: C.success, cards: ['DB Schema', 'API Config'] },
];

kanbanCols.forEach((col, i) => {
    const x = 0.45 + i * 2.3;
    slide.addShape('roundRect', {
        x, y: 4.72, w: 2.15, h: 0.25,
        fill: { color: col.color, transparency: 75 },
        line: { color: col.color, width: 1 },
        rectRadius: 0.1
    });
    slide.addText(col.label, {
        x, y: 4.73, w: 2.15, h: 0.22,
        fontSize: 8, bold: true, color: col.color, align: 'center', fontFace: 'Inter', margin: 0
    });
    col.cards.forEach((card, j) => {
        slide.addShape('roundRect', {
            x, y: 5.0 + j * 0.22, w: 2.15, h: 0.2,
            fill: { color: C.bgSec },
            line: { color: C.border, width: 0.5 },
            rectRadius: 0.08
        });
        slide.addText(card, {
            x: x + 0.08, y: 5.01 + j * 0.22, w: 2.0, h: 0.18,
            fontSize: 7, color: C.muted, fontFace: 'Inter', margin: 0
        });
    });
});

addFooter(slide, 7);

// ==================== SLIDE 8: THUẬN LỢI MIND MAP ====================
slide = pptx.addSlide({ background: { color: C.bg } });
addSlideHeader(slide, 'Thuận Lợi — Mind Map Tổng Quan', '6 nhánh chính từ Scrum thành công', C.success);

// Center circle
slide.addShape('ellipse', {
    x: 4.0, y: 2.3, w: 2.0, h: 2.0,
    fill: { color: C.success },
    shadow: mkShadow()
});
slide.addText('Scrum\nThành\nCông', {
    x: 4.0, y: 2.75, w: 2.0, h: 1.1,
    fontSize: 14, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
});

const mindBranches = [
    { icon: '🚀', title: 'Delivery', points: ['Tăng tốc 30-50%', 'Time-to-market -40%', 'Liên tục deploy'], color: C.primary, x: 0.3, y: 1.2 },
    { icon: '🛡️', title: 'Quality', points: ['Bug rate -40-60%', 'Early detection', 'CI/CD automation'], color: C.success, x: 3.4, y: 1.2 },
    { icon: '❤️', title: 'People', points: ['Retention +20%', 'Morale cao', 'Self-organizing'], color: C.warning, x: 6.6, y: 1.2 },
    { icon: '📉', title: 'Risk', points: ['Failure cost -60%', 'Early validation', 'Small batches'], color: C.danger, x: 0.3, y: 4.1 },
    { icon: '👁️', title: 'Visibility', points: ['Burndown chart', '100% transparency', 'Stakeholder trust'], color: C.purple, x: 3.4, y: 4.1 },
    { icon: '💡', title: 'Innovation', points: ['15-20% ideas', 'Spike for research', '20% innovation time'], color: C.orange, x: 6.6, y: 4.1 },
];

mindBranches.forEach((branch) => {
    addCard(slide, branch.x, branch.y, 3.0, 1.35, C.bgCard, C.border);
    slide.addShape('rect', { x: branch.x, y: branch.y, w: 3.0, h: 0.06, fill: { color: branch.color } });
    slide.addText(branch.icon + ' ' + branch.title, {
        x: branch.x + 0.1, y: branch.y + 0.12, w: 2.8, h: 0.3,
        fontSize: 12, bold: true, color: branch.color, fontFace: 'Inter', margin: 0
    });
    branch.points.forEach((pt, i) => {
        slide.addText('• ' + pt, {
            x: branch.x + 0.1, y: branch.y + 0.48 + i * 0.28, w: 2.8, h: 0.26,
            fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
        });
    });
});

addFooter(slide, 8);

// ==================== SLIDE 9: KHÓ KHĂN OVERVIEW ====================
slide = pptx.addSlide({ background: { color: C.bg } });

slide.addShape('rect', { x: 0, y: 0, w: 10, h: 1.0, fill: { color: C.bgSec }, line: { color: C.border, width: 0.5 } });
slide.addShape('rect', { x: 0, y: 0, w: 0.12, h: 1.0, fill: { color: C.danger } });
slide.addText('Khó Khăn — Những Thách Thức Thật Sự', {
    x: 0.3, y: 0.12, w: 9.4, h: 0.52,
    fontSize: 22, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});
slide.addText('5 danh mục: Con người | Quá trình | Kỹ thuật | Tổ chức | Tools', {
    x: 0.3, y: 0.6, w: 9.4, h: 0.3,
    fontSize: 11, color: C.muted, fontFace: 'Inter', margin: 0
});

// Pie chart placeholder
addCard(slide, 0.3, 1.15, 4.8, 3.5, C.bgCard, C.border);
slide.addText('Phân Bổ Khó Khăn Theo Danh Mục', {
    x: 0.45, y: 1.25, w: 4.5, h: 0.3,
    fontSize: 11, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});

// Chart using shapes (pie approximation)
const pieData = [
    { label: 'Con Người', pct: '85%', color: C.danger, startAngle: 0 },
    { label: 'Quá Trình', pct: '80%', color: C.warning, startAngle: 85 },
    { label: 'Tổ Chức', pct: '75%', color: C.purple, startAngle: 155 },
    { label: 'Kỹ Thuật', pct: '70%', color: C.primary, startAngle: 215 },
    { label: 'Tools', pct: '65%', color: C.muted, startAngle: 270 },
];
// Draw as horizontal bars instead (cleaner in pptxgenjs)
pieData.forEach((item, i) => {
    slide.addShape('rect', {
        x: 0.5, y: 1.65 + i * 0.5, w: 4.3, h: 0.35,
        fill: { color: item.color, transparency: 70 },
        line: { color: item.color, width: 1 }
    });
    slide.addShape('rect', {
        x: 0.5, y: 1.65 + i * 0.5, w: 4.3 * (item.pct.replace('%', '') / 100), h: 0.35,
        fill: { color: item.color }
    });
    slide.addText(item.label + ' (' + item.pct + ')', {
        x: 0.6, y: 1.68 + i * 0.5, w: 4.0, h: 0.3,
        fontSize: 10, bold: true, color: 'FFFFFF', fontFace: 'Inter', margin: 0
    });
});

// Stats on right
const statsCards = [
    { label: 'Con Người', val: '85%', color: C.danger, level: 'Rất phổ biến', levelColor: C.danger },
    { label: 'Quá Trình', val: '80%', color: C.warning, level: 'Phổ biến', levelColor: C.warning },
    { label: 'Tổ Chức', val: '75%', color: C.purple, level: 'Cao', levelColor: C.purple },
    { label: 'Kỹ Thuật', val: '70%', color: C.primary, level: 'TB - Cao', levelColor: C.primary },
    { label: 'Tools', val: '65%', color: C.muted, level: 'Thấp - TB', levelColor: C.muted },
];

statsCards.forEach((s, i) => {
    const y = 1.15 + i * 0.7;
    addCard(slide, 5.3, y, 4.4, 0.6, C.bgCard, C.border);
    slide.addShape('rect', { x: 5.3, y, w: 0.1, h: 0.6, fill: { color: s.color } });
    slide.addText(s.label, {
        x: 5.5, y: y + 0.08, w: 2.0, h: 0.25,
        fontSize: 11, bold: true, color: C.text, fontFace: 'Inter', margin: 0
    });
    slide.addText(s.val, {
        x: 8.3, y: y + 0.05, w: 1.2, h: 0.5,
        fontSize: 22, bold: true, color: s.color, align: 'right', fontFace: 'Inter', margin: 0
    });
    slide.addText(s.level, {
        x: 5.5, y: y + 0.32, w: 2.0, h: 0.22,
        fontSize: 8, color: s.levelColor, fontFace: 'Inter', margin: 0
    });
});

slide.addShape('roundRect', {
    x: 0.3, y: 4.8, w: 9.4, h: 0.5,
    fill: { color: C.danger, transparency: 85 },
    line: { color: C.danger, width: 1 },
    rectRadius: 0.1
});
slide.addText('⚠️ Khó khăn lớn nhất: CON NGƯỜI (85%) và TỔ CHỨC (75%) — không phải kỹ thuật', {
    x: 0.5, y: 4.88, w: 9, h: 0.35,
    fontSize: 11, bold: true, color: C.danger, align: 'center', fontFace: 'Inter', margin: 0
});

addFooter(slide, 9);

// ==================== SLIDE 10: KHÓ KHĂN CON NGƯỜI & QUÁ TRÌNH ====================
slide = pptx.addSlide({ background: { color: C.bg } });
addSlideHeader(slide, 'Khó Khăn — Con Người & Quá Trình', 'A1-B5: 10 thách thức chính', C.danger);

// Left: Con Người
addCard(slide, 0.3, 1.15, 4.65, 5.3, C.bgCard, C.border);
slide.addShape('rect', { x: 0.3, y: 1.15, w: 4.65, h: 0.06, fill: { color: C.danger } });
slide.addText('👤 Con Người (85%) — Rất phổ biến', {
    x: 0.45, y: 1.28, w: 4.35, h: 0.3,
    fontSize: 12, bold: true, color: C.danger, fontFace: 'Inter', margin: 0
});

const peopleChallenges = [
    { code: 'A1', name: 'Resistance to Change', pct: '80%', desc: 'Team quen Waterfall, không muốn thay đổi' },
    { code: 'A2', name: 'Lack of Scrum Knowledge', pct: '70%', desc: 'Không được đào tạo đủ, roles lẫn lộn' },
    { code: 'A3', name: 'PO Absence', pct: '60%', desc: 'PO bận/ không có thẩm quyền' },
    { code: 'A4', name: 'Team Turnover', pct: '50%', desc: 'Mất người giữa chừng, velocity drop 30-50%' },
    { code: 'A5', name: 'Remote/Hybrid Challenges', pct: '75%', desc: 'Timezone khác, Zoom fatigue, culture barrier' },
];
peopleChallenges.forEach((c, i) => {
    const y = 1.65 + i * 0.88;
    slide.addShape('roundRect', {
        x: 0.4, y, w: 0.45, h: 0.35,
        fill: { color: C.danger },
        rectRadius: 0.08
    });
    slide.addText(c.code, {
        x: 0.4, y: y + 0.03, w: 0.45, h: 0.3,
        fontSize: 9, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addText(c.name, {
        x: 0.95, y, w: 2.5, h: 0.25,
        fontSize: 10, bold: true, color: C.text, fontFace: 'Inter', margin: 0
    });
    slide.addText(c.pct, {
        x: 3.9, y, w: 0.9, h: 0.25,
        fontSize: 12, bold: true, color: C.danger, align: 'right', fontFace: 'Inter', margin: 0
    });
    slide.addText(c.desc, {
        x: 0.95, y: y + 0.28, w: 3.85, h: 0.5,
        fontSize: 8.5, color: C.muted, fontFace: 'Inter', margin: 0
    });
});

// Right: Quá Trình
addCard(slide, 5.15, 1.15, 4.55, 5.3, C.bgCard, C.border);
slide.addShape('rect', { x: 5.15, y: 1.15, w: 4.55, h: 0.06, fill: { color: C.warning } });
slide.addText('⚙️ Quá Trình (80%) — Phổ biến', {
    x: 5.3, y: 1.28, w: 4.25, h: 0.3,
    fontSize: 12, bold: true, color: C.warning, fontFace: 'Inter', margin: 0
});

const processChallenges = [
    { code: 'B1', name: 'Unrealistic Sprint Commitments', pct: '90%', desc: 'Cam kết 50 SP nhưng chỉ làm được 30' },
    { code: 'B2', name: 'Scope Creep', pct: '85%', desc: '"Chỉ 1 item nhỏ thôi" — scope tăng liên tục' },
    { code: 'B3', name: 'Poorly Written User Stories', pct: '75%', desc: '"As a user, I want to login" — không có gì' },
    { code: 'B4', name: 'Ineffective Daily Standup', pct: '70%', desc: 'Kéo dài 45 phút, chỉ status update' },
    { code: 'B5', name: 'Sprint Overlap with Holidays', pct: '60%', desc: 'Nghỉ lễ phép, velocity không đo chính xác' },
];
processChallenges.forEach((c, i) => {
    const y = 1.65 + i * 0.88;
    slide.addShape('roundRect', {
        x: 5.25, y, w: 0.45, h: 0.35,
        fill: { color: C.warning },
        rectRadius: 0.08
    });
    slide.addText(c.code, {
        x: 5.25, y: y + 0.03, w: 0.45, h: 0.3,
        fontSize: 9, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addText(c.name, {
        x: 5.8, y, w: 2.4, h: 0.25,
        fontSize: 10, bold: true, color: C.text, fontFace: 'Inter', margin: 0
    });
    slide.addText(c.pct, {
        x: 8.75, y, w: 0.8, h: 0.25,
        fontSize: 12, bold: true, color: C.warning, align: 'right', fontFace: 'Inter', margin: 0
    });
    slide.addText(c.desc, {
        x: 5.8, y: y + 0.28, w: 3.75, h: 0.5,
        fontSize: 8.5, color: C.muted, fontFace: 'Inter', margin: 0
    });
});

addFooter(slide, 10);

// ==================== SLIDE 11: KHÓ KHĂN KỸ THUẬT & TỔ CHỨC ====================
slide = pptx.addSlide({ background: { color: C.bg } });
addSlideHeader(slide, 'Khó Khăn — Kỹ Thuật & Tổ Chức', 'C1-D4: 8 thách thức chính + biểu đồ tần suất', C.danger);

// Left: Kỹ Thuật
addCard(slide, 0.3, 1.15, 4.65, 2.7, C.bgCard, C.border);
slide.addShape('rect', { x: 0.3, y: 1.15, w: 4.65, h: 0.06, fill: { color: C.primary } });
slide.addText('🔧 Kỹ Thuật (70%) — TB đến Cao', {
    x: 0.45, y: 1.28, w: 4.35, h: 0.3,
    fontSize: 12, bold: true, color: C.primary, fontFace: 'Inter', margin: 0
});

const techChallenges = [
    { code: 'C1', name: 'Lack of Technical Practices', pct: '65%', desc: 'Không CI/CD, manual deploy mất 2-3 ngày' },
    { code: 'C2', name: 'Technical Debt Accumulation', pct: '80%', desc: '"Sau này tính sau" — debt tích lũy, velocity giảm' },
    { code: 'C3', name: 'Integration Issues', pct: '60%', desc: 'API thay đổi không thông báo, "integration hell"' },
    { code: 'C4', name: 'Legacy System Integration', pct: '50%', desc: 'API cũ không docs, không continuous deploy' },
];
techChallenges.forEach((c, i) => {
    const y = 1.65 + i * 0.6;
    slide.addShape('roundRect', {
        x: 0.4, y, w: 0.4, h: 0.3,
        fill: { color: C.primary },
        rectRadius: 0.08
    });
    slide.addText(c.code, {
        x: 0.4, y: y + 0.02, w: 0.4, h: 0.26,
        fontSize: 8, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addText(c.name, {
        x: 0.9, y, w: 2.4, h: 0.25,
        fontSize: 9, bold: true, color: C.text, fontFace: 'Inter', margin: 0
    });
    slide.addText(c.pct, {
        x: 3.85, y, w: 0.7, h: 0.25,
        fontSize: 11, bold: true, color: C.primary, align: 'right', fontFace: 'Inter', margin: 0
    });
    slide.addText(c.desc, {
        x: 0.9, y: y + 0.25, w: 3.65, h: 0.3,
        fontSize: 8, color: C.muted, fontFace: 'Inter', margin: 0
    });
});

// Right: Tổ Chức
addCard(slide, 5.15, 1.15, 4.55, 2.7, C.bgCard, C.border);
slide.addShape('rect', { x: 5.15, y: 1.15, w: 4.55, h: 0.06, fill: { color: C.purple } });
slide.addText('🏢 Tổ Chức (75%) — Cao', {
    x: 5.3, y: 1.28, w: 4.25, h: 0.3,
    fontSize: 12, bold: true, color: C.purple, fontFace: 'Inter', margin: 0
});

const orgChallenges = [
    { code: 'D1', name: 'Management Pressure (metrics cũ)', pct: '80%', desc: '"Sao còn 50% mà chưa xong?" — hỏi Gantt chart' },
    { code: 'D2', name: 'Siloed Teams / No Cross-Functional', pct: '70%', desc: '"Chỉ frontend" và "chỉ backend" — knowledge silos' },
    { code: 'D3', name: 'Lack of Organizational Support', pct: '50%', desc: '"Scrum là buzzword" — không cấp tool, workspace' },
    { code: 'D4', name: 'Contractual / Compliance Issues', pct: '40%', desc: 'Fixed price contract, SRS document required' },
];
orgChallenges.forEach((c, i) => {
    const y = 1.65 + i * 0.6;
    slide.addShape('roundRect', {
        x: 5.25, y, w: 0.4, h: 0.3,
        fill: { color: C.purple },
        rectRadius: 0.08
    });
    slide.addText(c.code, {
        x: 5.25, y: y + 0.02, w: 0.4, h: 0.26,
        fontSize: 8, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addText(c.name, {
        x: 5.75, y, w: 2.4, h: 0.25,
        fontSize: 9, bold: true, color: C.text, fontFace: 'Inter', margin: 0
    });
    slide.addText(c.pct, {
        x: 8.75, y, w: 0.7, h: 0.25,
        fontSize: 11, bold: true, color: C.purple, align: 'right', fontFace: 'Inter', margin: 0
    });
    slide.addText(c.desc, {
        x: 5.75, y: y + 0.25, w: 3.75, h: 0.3,
        fontSize: 8, color: C.muted, fontFace: 'Inter', margin: 0
    });
});

// Bar chart
addCard(slide, 0.3, 4.0, 9.4, 1.5, C.bgCard, C.border);
slide.addText('Tần Suất Khó Khăn Theo Mã', {
    x: 0.45, y: 4.08, w: 4, h: 0.25,
    fontSize: 10, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});
const barItems = [
    { code: 'B1', pct: 90, color: C.warning },
    { code: 'B2', pct: 85, color: C.warning },
    { code: 'A1', pct: 80, color: C.danger },
    { code: 'C2', pct: 80, color: C.primary },
    { code: 'D1', pct: 80, color: C.purple },
    { code: 'A5', pct: 75, color: C.danger },
    { code: 'B3', pct: 75, color: C.warning },
    { code: 'A2', pct: 70, color: C.danger },
    { code: 'D2', pct: 70, color: C.purple },
    { code: 'E2', pct: 70, color: C.muted },
];
barItems.forEach((item, i) => {
    const x = 0.45 + i * 0.93;
    slide.addShape('rect', {
        x, y: 4.4, w: 0.75, h: 0.95 * (item.pct / 100),
        fill: { color: item.color }
    });
    slide.addText(item.code, {
        x, y: 4.4 + 0.95 * (item.pct / 100), w: 0.75, h: 0.2,
        fontSize: 7, color: C.muted, align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addText(item.pct + '%', {
        x, y: 4.35, w: 0.75, h: 0.18,
        fontSize: 7, bold: true, color: item.color, align: 'center', fontFace: 'Inter', margin: 0
    });
});

addFooter(slide, 11);

// ==================== SLIDE 12: KHÓ KHĂN TOOLS & CASE STUDIES ====================
slide = pptx.addSlide({ background: { color: C.bg } });
addSlideHeader(slide, 'Khó Khăn — Tools & Case Studies Thất Bại', 'E1-E2: Tools | 3 case study thực tế', C.danger);

// Left column
addCard(slide, 0.3, 1.15, 4.65, 1.5, C.bgCard, C.border);
slide.addShape('rect', { x: 0.3, y: 1.15, w: 4.65, h: 0.06, fill: { color: C.muted } });
slide.addText('🛠️ Tools (65%) — Thấp đến Trung bình', {
    x: 0.45, y: 1.28, w: 4.35, h: 0.3,
    fontSize: 11, bold: true, color: C.muted, fontFace: 'Inter', margin: 0
});
slide.addText('E1: Tool Overload (60%) — Jira, Confluence, Slack, Zoom, GitHub, Jenkins... quá nhiều tool không tích hợp', {
    x: 0.45, y: 1.6, w: 4.35, h: 0.4,
    fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
});
slide.addText('E2: Poor Configuration (70%) — Jira cấu hình như Waterfall, Stories có 20 subtasks, 90% backlog là junk', {
    x: 0.45, y: 2.05, w: 4.35, h: 0.45,
    fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
});

// Case studies
const cases = [
    {
        title: 'Startup XYZ — Thất Bại Vì Không Có PO',
        color: C.danger,
        points: ['5 dev, không có PO, tự viết backlog random', 'Sprint 1-3: progress chậm, stakeholder không thấy giá trị', 'Kết quả: Project bị HỦY sau 3 tháng']
    },
    {
        title: 'Enterprise ABC — "Scrum Theater"',
        color: C.warning,
        points: ['Chỉ đổi tên "Phase" → "Sprint"', 'Vẫn giữ Gantt chart, weekly report', 'Daily standup bị cấm "không phải meeting"', 'Kết quả: Không mang lại giá trị']
    },
    {
        title: 'Offshore Team DEF — Chênh Lệch Múi Giờ',
        color: C.primary,
        points: ['3 dev VN, 1 PO Mỹ (2 tiếng chênh lệch)', 'Sprint Planning online kéo dài 4 giờ', 'Daily standup chỉ 2 lần/tuần', 'Kết quả: Velocity giảm 40%, quality tệ']
    },
];

cases.forEach((c, i) => {
    const col = i;
    const x = 0.3 + i * 3.2;
    addCard(slide, x, 2.8, 3.05, 2.55, C.bgCard, C.border);
    slide.addShape('rect', { x, y: 2.8, w: 3.05, h: 0.06, fill: { color: c.color } });
    slide.addText(c.title, {
        x: x + 0.1, y: 2.92, w: 2.85, h: 0.5,
        fontSize: 10, bold: true, color: c.color, fontFace: 'Inter', margin: 0
    });
    c.points.forEach((pt, j) => {
        slide.addText('• ' + pt, {
            x: x + 0.1, y: 3.48 + j * 0.45, w: 2.85, h: 0.42,
            fontSize: 8.5, color: C.muted, fontFace: 'Inter', margin: 0
        });
    });
});

// Lesson box
slide.addShape('roundRect', {
    x: 0.3, y: 5.5, w: 9.4, h: 0.5,
    fill: { color: C.danger, transparency: 85 },
    line: { color: C.danger, width: 1 },
    rectRadius: 0.1
});
slide.addText('📌 Bài học chung: Khó khăn lớn nhất là CON NGƯỜI và TỔ CHỨC, không phải kỹ thuật. 80% thất bại đến từ yếu tố con người.', {
    x: 0.5, y: 5.57, w: 9, h: 0.38,
    fontSize: 10, bold: true, color: C.danger, align: 'center', fontFace: 'Inter', margin: 0
});

addFooter(slide, 12);

// ==================== SLIDE 13: GIẢI PHÁP OVERVIEW ====================
slide = pptx.addSlide({ background: { color: C.bg } });

slide.addShape('rect', { x: 0, y: 0, w: 10, h: 1.0, fill: { color: C.bgSec }, line: { color: C.border, width: 0.5 } });
slide.addShape('rect', { x: 0, y: 0, w: 0.12, h: 1.0, fill: { color: C.warning } });
slide.addText('Giải Pháp Đề Xuất Cải Tiến', {
    x: 0.3, y: 0.12, w: 9.4, h: 0.52,
    fontSize: 22, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});
slide.addText('5 danh mục giải pháp — Từ khó khăn đến hành động cụ thể', {
    x: 0.3, y: 0.6, w: 9.4, h: 0.3,
    fontSize: 11, color: C.muted, fontFace: 'Inter', margin: 0
});

const solutions = [
    { icon: '👤', title: 'Con Người', items: 'Training, Embedded PO, Team Charter, Psychological Safety', color: C.primary, num: '5 solutions' },
    { icon: '⚙️', title: 'Quá Trình', items: 'DoD/DoR, Sprint Precision, Backlog Refinement, Timeboxed Events', color: C.success, num: '4 solutions' },
    { icon: '🔧', title: 'Kỹ Thuật', items: 'XP Practices, Tech Debt, API Testing, CI/CD Pipeline', color: C.warning, num: '4 solutions' },
    { icon: '🏢', title: 'Tổ Chức', items: 'Agile Metrics, Cross-Functional, Executive Sponsorship', color: C.purple, num: '3 solutions' },
    { icon: '🛠️', title: 'Tools', items: 'Simplification, Dashboard, Real-Time Reporting', color: C.muted, num: '2 solutions' },
    { icon: '🎯', title: 'Priority #1', items: 'DoD & DoR — Nền tảng mọi cải tiến', color: C.danger, num: 'DoD + DoR' },
];

solutions.forEach((s, i) => {
    const col = i % 3;
    const row = Math.floor(i / 3);
    const x = 0.3 + col * 3.2;
    const y = 1.15 + row * 1.9;
    addCard(slide, x, y, 3.0, 1.75, C.bgCard, C.border);
    slide.addShape('rect', { x, y, w: 3.0, h: 0.06, fill: { color: s.color } });
    slide.addText(s.icon + ' ' + s.title, {
        x: x + 0.1, y: y + 0.12, w: 2.8, h: 0.3,
        fontSize: 12, bold: true, color: s.color, fontFace: 'Inter', margin: 0
    });
    slide.addText(s.num, {
        x: x + 0.1, y: y + 0.45, w: 2.8, h: 0.25,
        fontSize: 9, bold: true, color: C.text, fontFace: 'Inter', margin: 0
    });
    slide.addText(s.items, {
        x: x + 0.1, y: y + 0.72, w: 2.8, h: 0.9,
        fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
    });
});

// Priority bars
addCard(slide, 0.3, 5.05, 9.4, 0.5, C.bgCard, C.border);
slide.addText('Ưu tiên:', {
    x: 0.4, y: 5.12, w: 0.7, h: 0.35,
    fontSize: 9, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});
const priorities = [
    { label: 'DoD/DoR', pct: 95, color: C.danger },
    { label: 'Embedded PO', pct: 90, color: C.danger },
    { label: 'Scrum Training', pct: 88, color: C.danger },
    { label: 'Sprint Precision', pct: 75, color: C.warning },
    { label: 'Agile Metrics', pct: 68, color: C.warning },
    { label: 'XP Practices', pct: 60, color: C.success },
];
priorities.forEach((p, i) => {
    const x = 1.2 + i * 1.4;
    slide.addShape('rect', {
        x, y: 5.12, w: 1.2, h: 0.3,
        fill: { color: p.color, transparency: 70 }
    });
    slide.addShape('rect', {
        x, y: 5.12, w: 1.2 * (p.pct / 100), h: 0.3,
        fill: { color: p.color }
    });
    slide.addText(p.label, {
        x, y: 5.13, w: 1.2, h: 0.28,
        fontSize: 7, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
    });
});

addFooter(slide, 13);

// ==================== SLIDE 14: GIẢI PHÁP CON NGƯỜI & QUÁ TRÌNH ====================
slide = pptx.addSlide({ background: { color: C.bg } });
addSlideHeader(slide, 'Giải Pháp — Con Người & Quá Trình', 'G1: Con người | G2: Quá trình', C.success);

// Left: Con Người
addCard(slide, 0.3, 1.15, 4.65, 3.0, C.bgCard, C.border);
slide.addShape('rect', { x: 0.3, y: 1.15, w: 4.65, h: 0.06, fill: { color: C.primary } });
slide.addText('👤 Giải Pháp về Con Người', {
    x: 0.45, y: 1.28, w: 4.35, h: 0.3,
    fontSize: 12, bold: true, color: C.primary, fontFace: 'Inter', margin: 0
});

const peopleSolutions = [
    { g: 'G1.1', name: 'Scrum Training', desc: '2-day workshop + CSM/PSM certification, pair programming Sprint đầu' },
    { g: 'G1.2', name: 'Embedded PO', desc: '50% thời gian PO trong team, quyền ra quyết định rõ ràng' },
    { g: 'G1.3', name: 'Team Charter', desc: 'Working agreements do chính team viết, buddy system, T-shaped skills' },
    { g: 'G1.4', name: 'Psychological Safety', desc: 'Prime directive mỗi Retro, anonymous feedback, SMART action items' },
];
peopleSolutions.forEach((s, i) => {
    const y = 1.65 + i * 0.62;
    slide.addShape('roundRect', {
        x: 0.4, y, w: 0.45, h: 0.3,
        fill: { color: C.primary },
        rectRadius: 0.08
    });
    slide.addText(s.g, {
        x: 0.4, y: y + 0.02, w: 0.45, h: 0.26,
        fontSize: 7, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addText(s.name, {
        x: 0.95, y, w: 1.5, h: 0.25,
        fontSize: 10, bold: true, color: C.text, fontFace: 'Inter', margin: 0
    });
    slide.addText(s.desc, {
        x: 0.95, y: y + 0.25, w: 3.85, h: 0.35,
        fontSize: 8.5, color: C.muted, fontFace: 'Inter', margin: 0
    });
});

// Retro formats
slide.addText('Retro Formats:', {
    x: 0.4, y: 4.25, w: 2, h: 0.25,
    fontSize: 10, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});
const retroFormats = ['Start-Stop-Continue', 'Mad-Sad-Glad', '4Ls'];
retroFormats.forEach((f, i) => {
    slide.addShape('roundRect', {
        x: 0.4 + i * 1.5, y: 4.5, w: 1.4, h: 0.35,
        fill: { color: C.purple, transparency: 75 },
        line: { color: C.purple, width: 1 },
        rectRadius: 0.12
    });
    slide.addText(f, {
        x: 0.4 + i * 1.5, y: 4.52, w: 1.4, h: 0.32,
        fontSize: 8, color: C.purple, align: 'center', fontFace: 'Inter', margin: 0
    });
});

// Right: Quá Trình
addCard(slide, 5.15, 1.15, 4.55, 3.0, C.bgCard, C.border);
slide.addShape('rect', { x: 5.15, y: 1.15, w: 4.55, h: 0.06, fill: { color: C.success } });
slide.addText('⚙️ Giải Pháp về Quá Trình', {
    x: 5.3, y: 1.28, w: 4.25, h: 0.3,
    fontSize: 12, bold: true, color: C.success, fontFace: 'Inter', margin: 0
});

const processSolutions = [
    { g: 'G2.1', name: 'DoD & DoR', desc: 'Tiêu chuẩn ready & done rõ ràng từ Sprint 0' },
    { g: 'G2.2', name: 'Sprint Commitment Precision', desc: 'Velocity-based planning, buffer 20%, "Sprint Goal first"' },
    { g: 'G2.3', name: 'Backlog Refinement', desc: '2 sessions/sprint, INVEST criteria, Given-When-Then' },
    { g: 'G2.4', name: 'Timeboxed Events', desc: '15 phút standup, parking lot, timer bắt buộc' },
];
processSolutions.forEach((s, i) => {
    const y = 1.65 + i * 0.62;
    slide.addShape('roundRect', {
        x: 5.25, y, w: 0.45, h: 0.3,
        fill: { color: C.success },
        rectRadius: 0.08
    });
    slide.addText(s.g, {
        x: 5.25, y: y + 0.02, w: 0.45, h: 0.26,
        fontSize: 7, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addText(s.name, {
        x: 5.8, y, w: 2.0, h: 0.25,
        fontSize: 10, bold: true, color: C.text, fontFace: 'Inter', margin: 0
    });
    slide.addText(s.desc, {
        x: 5.8, y: y + 0.25, w: 3.75, h: 0.35,
        fontSize: 8.5, color: C.muted, fontFace: 'Inter', margin: 0
    });
});

// INVEST grid
slide.addText('INVEST Criteria:', {
    x: 5.3, y: 4.25, w: 2, h: 0.25,
    fontSize: 10, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});
const invest = [
    { l: 'I', w: 'Independent' }, { l: 'N', w: 'Negotiable' }, { l: 'V', w: 'Valuable' },
    { l: 'E', w: 'Estimable' }, { l: 'S', w: 'Small' }, { l: 'T', w: 'Testable' },
];
invest.forEach((item, i) => {
    const col = i % 3;
    const row = Math.floor(i / 3);
    const x = 5.25 + col * 1.45;
    const y = 4.5 + row * 0.35;
    slide.addShape('roundRect', {
        x, y, w: 1.35, h: 0.3,
        fill: { color: C.primary, transparency: 80 },
        line: { color: C.primary, width: 1 },
        rectRadius: 0.1
    });
    slide.addText(item.l + ' — ' + item.w, {
        x, y: y + 0.02, w: 1.35, h: 0.26,
        fontSize: 8, bold: true, color: C.primary, align: 'center', fontFace: 'Inter', margin: 0
    });
});

// DoD box
addCard(slide, 0.3, 4.85, 9.4, 0.7, C.bgCard, C.border);
slide.addShape('rect', { x: 0.3, y: 4.85, w: 0.1, h: 0.7, fill: { color: C.success } });
slide.addText('Definition of Done (DoD):', {
    x: 0.5, y: 4.9, w: 2.2, h: 0.25,
    fontSize: 10, bold: true, color: C.success, fontFace: 'Inter', margin: 0
});
slide.addText('✓ Code reviewed (PR approved)  ✓ Unit test pass (coverage >70%)  ✓ Integration test pass  ✓ UAT signed off  ✓ Deployed to staging  ✓ No critical/high bugs', {
    x: 0.5, y: 5.15, w: 9.0, h: 0.35,
    fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
});

addFooter(slide, 14);

// ==================== SLIDE 15: GIẢI PHÁP KỸ THUẬT & TỔ CHỨC ====================
slide = pptx.addSlide({ background: { color: C.bg } });
addSlideHeader(slide, 'Giải Pháp — Kỹ Thuật & Tổ Chức', 'G3: Kỹ thuật | G4: Tổ chức', C.success);

// XP Practices (6 cards)
const xpPractices = [
    { icon: '🧪', name: 'TDD', desc: 'Red-Green-Refactor', color: C.primary },
    { icon: '♾️', name: 'CI/CD', desc: 'Auto build & deploy', color: C.success },
    { icon: '👥', name: 'Pair Programming', desc: '2 dev 1 máy, 90 phút', color: C.warning },
    { icon: '🔀', name: 'Code Review', desc: 'PR require 1 approve', color: C.danger },
    { icon: '🧹', name: 'Refactoring', desc: '20% Sprint cho debt', color: C.purple },
    { icon: '📄', name: 'Contract Testing', desc: 'Pact framework', color: C.cyan },
];

xpPractices.forEach((p, i) => {
    const col = i % 3;
    const row = Math.floor(i / 3);
    const x = 0.3 + col * 3.2;
    const y = 1.15 + row * 0.95;
    addCard(slide, x, y, 3.0, 0.82, C.bgCard, C.border);
    slide.addShape('rect', { x, y, w: 3.0, h: 0.05, fill: { color: p.color } });
    slide.addText(p.icon + ' ' + p.name, {
        x: x + 0.1, y: y + 0.1, w: 2.8, h: 0.3,
        fontSize: 11, bold: true, color: p.color, fontFace: 'Inter', margin: 0
    });
    slide.addText(p.desc, {
        x: x + 0.1, y: y + 0.42, w: 2.8, h: 0.3,
        fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
    });
});

// Tech Debt
addCard(slide, 0.3, 3.15, 4.65, 0.6, C.bgCard, C.border);
slide.addText('🔧 Tech Debt Management:', {
    x: 0.45, y: 3.22, w: 2.5, h: 0.25,
    fontSize: 10, bold: true, color: C.text, fontFace: 'Inter', margin: 0
});
slide.addText('Backlog riêng | Spike 1-3 ngày | Boy scout rule | Monthly Tech Debt Day', {
    x: 0.45, y: 3.47, w: 4.35, h: 0.25,
    fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
});

// Org Solutions
const orgSolutions = [
    { icon: '📊', name: 'Agile-Friendly Metrics', desc: 'Velocity, Lead time, Cycle time, DoD compliance', color: C.primary },
    { icon: '🗺️', name: 'Cross-Functional Teams', desc: 'Feature teams, PODS model, "You build it, you run it"', color: C.success },
    { icon: '👑', name: 'Executive Sponsorship', desc: 'Scrum Champion, Agile CoE, Kotter\'s 8-Step', color: C.purple },
    { icon: '🤝', name: 'T&M Contracts', desc: 'Time & Materials thay vì Fixed Price', color: C.warning },
];
orgSolutions.forEach((s, i) => {
    const col = i % 2;
    const row = Math.floor(i / 2);
    const x = 5.15 + col * 2.3;
    const y = 1.15 + row * 1.35;
    addCard(slide, x, y, 2.2, 1.2, C.bgCard, C.border);
    slide.addShape('rect', { x, y, w: 2.2, h: 0.05, fill: { color: s.color } });
    slide.addText(s.icon + ' ' + s.name, {
        x: x + 0.08, y: y + 0.1, w: 2.04, h: 0.45,
        fontSize: 10, bold: true, color: s.color, fontFace: 'Inter', margin: 0
    });
    slide.addText(s.desc, {
        x: x + 0.08, y: y + 0.6, w: 2.04, h: 0.5,
        fontSize: 8.5, color: C.muted, fontFace: 'Inter', margin: 0
    });
});

// Kotter
addCard(slide, 0.3, 3.9, 9.4, 1.05, C.bgCard, C.border);
slide.addText('Kotter\'s 8-Step Change Model:', {
    x: 0.45, y: 3.97, w: 4, h: 0.25,
    fontSize: 10, bold: true, color: C.purple, fontFace: 'Inter', margin: 0
});
const kotter = [
    '1. Create urgency', '2. Form coalition', '3. Create vision', '4. Communicate',
    '5. Remove obstacles', '6. Short-term wins', '7. Build on change', '8. Anchor in culture',
];
kotter.forEach((step, i) => {
    const col = i % 4;
    const row = Math.floor(i / 4);
    slide.addShape('roundRect', {
        x: 0.45 + col * 2.3, y: 4.25 + row * 0.35, w: 2.2, h: 0.3,
        fill: { color: C.purple, transparency: 80 },
        line: { color: C.purple, width: 1 },
        rectRadius: 0.1
    });
    slide.addText(step, {
        x: 0.45 + col * 2.3, y: 4.27 + row * 0.35, w: 2.2, h: 0.27,
        fontSize: 8, color: C.purple, align: 'center', fontFace: 'Inter', margin: 0
    });
});

addFooter(slide, 15);

// ==================== SLIDE 16: IMPLEMENTATION ROADMAP ====================
slide = pptx.addSlide({ background: { color: C.bg } });
addSlideHeader(slide, 'Implementation Roadmap — Lộ Trình Triển Khai', '4 giai đoạn từ Sprint 0 đến Month 12+', C.warning);

const phases = [
    {
        num: '1', title: 'Foundation', time: 'Sprint 0 - Sprint 3', color: C.primary,
        items: ['Scrum training toàn team', 'Cài đặt DoD & DoR', 'Setup Jira/ADO', 'Embedded PO', 'Timeboxed ceremonies'],
    },
    {
        num: '2', title: 'Stabilization', time: 'Sprint 4 - Sprint 8', color: C.success,
        items: ['Velocity baseline', 'Backlog grooming discipline', 'Agile metrics reporting', 'Retro action tracking', 'Team Charter finalized'],
    },
    {
        num: '3', title: 'Optimization', time: 'Sprint 9 - Sprint 16', color: C.warning,
        items: ['XP engineering practices', 'Tech debt backlog', 'Cross-functional skill dev', 'Tool integration', 'CI/CD pipeline'],
    },
    {
        num: '4', title: 'Scaling', time: 'Month 6+', color: C.purple,
        items: ['Scrum of Scrums', 'Executive reporting', 'Org change management', 'Continuous improvement', 'Agile Center of Excellence'],
    },
];

phases.forEach((phase, i) => {
    const x = 0.3 + i * 2.4;
    addCard(slide, x, 1.15, 2.25, 3.9, C.bgCard, C.border);
    slide.addShape('ellipse', {
        x: x + 0.75, y: 1.25, w: 0.75, h: 0.75,
        fill: { color: phase.color },
        shadow: mkCardShadow()
    });
    slide.addText(phase.num, {
        x: x + 0.75, y: 1.4, w: 0.75, h: 0.5,
        fontSize: 18, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addText(phase.title, {
        x: x + 0.08, y: 2.08, w: 2.1, h: 0.3,
        fontSize: 12, bold: true, color: phase.color, align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addShape('roundRect', {
        x: x + 0.2, y: 2.4, w: 1.85, h: 0.28,
        fill: { color: phase.color, transparency: 75 },
        line: { color: phase.color, width: 1 },
        rectRadius: 0.12
    });
    slide.addText(phase.time, {
        x: x + 0.2, y: 2.42, w: 1.85, h: 0.25,
        fontSize: 8, bold: true, color: phase.color, align: 'center', fontFace: 'Inter', margin: 0
    });
    phase.items.forEach((item, j) => {
        slide.addText('✓ ' + item, {
            x: x + 0.1, y: 2.75 + j * 0.4, w: 2.05, h: 0.38,
            fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
        });
    });
});

// Timeline bar
addCard(slide, 0.3, 5.2, 9.4, 0.35, C.bgCard, C.border);
slide.addText('🗓️ Sprint 0', {
    x: 0.4, y: 5.22, w: 1.0, h: 0.3,
    fontSize: 8, bold: true, color: C.primary, fontFace: 'Inter', margin: 0
});
slide.addShape('rect', {
    x: 1.4, y: 5.27, w: 7.5, h: 0.2,
    fill: { color: C.border }
});
const phaseBarColors = [C.primary, C.success, C.warning, C.purple];
let accW = 0;
phases.forEach((p, i) => {
    const w = [1.8, 1.8, 1.8, 2.1][i];
    slide.addShape('rect', {
        x: 1.4 + accW, y: 5.27, w, h: 0.2,
        fill: { color: p.color }
    });
    accW += w;
});
slide.addText('Sprint 0 → Sprint 3 → Sprint 8 → Sprint 16 → Month 6+', {
    x: 1.5, y: 5.27, w: 7.4, h: 0.2,
    fontSize: 7, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
});

addFooter(slide, 16);

// ==================== SLIDE 17: KEY TAKEAWAYS ====================
slide = pptx.addSlide({ background: { color: C.bg } });
addSlideHeader(slide, 'Key Takeaways — 3 Điểm Quan Trọng Cần Nhớ', '', C.warning);

const takeaways = [
    {
        num: '1', color: C.primary,
        title: 'Scrum mang lại lợi ích lớn nhưng cần sự kiên nhẫn',
        desc: 'Tăng tốc độ giao hàng 30-50%, chất lượng cao hơn, stakeholder satisfaction tăng 25-35%. Cần 3-4 Sprint đầu để team ổn định velocity và hiểu rõ quy trình.',
    },
    {
        num: '2', color: C.danger,
        title: 'Khó khăn lớn nhất là CON NGƯỜI và TỔ CHỨC, không phải kỹ thuật',
        desc: '85% thất bại đến từ yếu tố con người: Resistance to Change (80%), PO Absence (60%), Management Pressure (80%). Kỹ thuật chỉ chiếm 70% với mức độ trung bình.',
    },
    {
        num: '3', color: C.success,
        title: 'Giải pháp hiệu quả nhất: DoD/DoR, Embedded PO, Scrum Training, Agile Metrics',
        desc: 'Priority 1: DoD & DoR (chi phí thấp, ảnh hưởng lớn). Priority 2: Embedded PO + Scrum Training. Ưu tiên những gì có thể triển khai ngay trong Sprint 0-1.',
    },
];

takeaways.forEach((t, i) => {
    const y = 1.15 + i * 1.55;
    addCard(slide, 0.3, y, 9.4, 1.4, C.bgCard, C.border);
    slide.addShape('rect', { x: 0.3, y, w: 0.12, h: 1.4, fill: { color: t.color } });
    slide.addShape('ellipse', {
        x: 0.55, y: y + 0.45, w: 0.6, h: 0.6,
        fill: { color: t.color }
    });
    slide.addText(t.num, {
        x: 0.55, y: y + 0.55, w: 0.6, h: 0.45,
        fontSize: 18, bold: true, color: 'FFFFFF', align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addText(t.title, {
        x: 1.35, y: y + 0.1, w: 8.15, h: 0.4,
        fontSize: 13, bold: true, color: C.text, fontFace: 'Inter', margin: 0
    });
    slide.addText(t.desc, {
        x: 1.35, y: y + 0.55, w: 8.15, h: 0.75,
        fontSize: 10, color: C.muted, fontFace: 'Inter', margin: 0
    });
});

// Quote box
slide.addShape('roundRect', {
    x: 0.3, y: 5.95, w: 9.4, h: 0.5,
    fill: { color: C.warning, transparency: 85 },
    line: { color: C.warning, width: 1.5 },
    rectRadius: 0.1
});
slide.addText('💬 "Scrum không phải giải pháp cho tất cả, nhưng là nền tảng tốt để xây dựng."', {
    x: 0.5, y: 6.02, w: 9, h: 0.38,
    fontSize: 12, bold: true, color: C.warning, align: 'center', fontFace: 'Inter', margin: 0
});

addFooter(slide, 17);

// ==================== SLIDE 18: Q&A ====================
slide = pptx.addSlide({ background: { color: C.bg } });

// Background
slide.addShape('rect', { x: 0, y: 0, w: 10, h: 5.625, fill: { color: '111827' } });
slide.addShape('rect', { x: 0, y: 3.0, w: 10, h: 2.625, fill: { color: C.bgSec } });

slide.addText('💬', {
    x: 4.3, y: 0.5, w: 1.4, h: 1.4,
    fontSize: 64, align: 'center', fontFace: 'Inter', margin: 0
});
slide.addText('Cảm Ơn & Hỏi Đáp', {
    x: 0.5, y: 1.9, w: 9, h: 0.7,
    fontSize: 36, bold: true, color: C.text, align: 'center', fontFace: 'Inter', margin: 0
});
slide.addText('Cảm ơn thầy/cô và các bạn đã lắng nghe! Mọi câu hỏi xin gửi về phần Q&A.', {
    x: 0.5, y: 2.6, w: 9, h: 0.4,
    fontSize: 12, color: C.muted, align: 'center', fontFace: 'Inter', margin: 0
});

// Resources
const resources = [
    { icon: '📖', title: 'Scrum Guide', sub: 'scrumguides.org' },
    { icon: '📜', title: 'Agile Manifesto', sub: 'agilemanifesto.org' },
    { icon: '📚', title: 'Scrum & XP from the Trenches', sub: '— Henrik Kniberg' },
    { icon: '👤', title: 'User Stories Applied', sub: '— Mike Cohn' },
    { icon: '👥', title: 'Team Topologies', sub: '— Skelton & Pais' },
    { icon: '♟️', title: 'Kotter\'s 8-Step', sub: 'Change Model' },
];

resources.forEach((r, i) => {
    const col = i % 3;
    const row = Math.floor(i / 3);
    const x = 0.5 + col * 3.1;
    const y = 3.15 + row * 1.05;
    addCard(slide, x, y, 2.9, 0.9, C.bgCard, C.border);
    slide.addText(r.icon, {
        x: x + 0.1, y: y + 0.15, w: 0.6, h: 0.6,
        fontSize: 20, align: 'center', fontFace: 'Inter', margin: 0
    });
    slide.addText(r.title, {
        x: x + 0.7, y: y + 0.15, w: 2.1, h: 0.35,
        fontSize: 10, bold: true, color: C.text, fontFace: 'Inter', margin: 0
    });
    slide.addText(r.sub, {
        x: x + 0.7, y: y + 0.5, w: 2.1, h: 0.3,
        fontSize: 9, color: C.muted, fontFace: 'Inter', margin: 0
    });
});

slide.addText('Nhóm Y5 — Quản Lý Dự Án Phần Mềm 2026', {
    x: 0.5, y: 5.3, w: 9, h: 0.25,
    fontSize: 10, color: C.muted, align: 'center', fontFace: 'Inter', margin: 0
});

// Save
pptx.writeFile({ fileName: 'SCRUM_Y5_Presentation.pptx' })
    .then(fileName => {
        console.log('✅ PPTX created: ' + fileName);
    })
    .catch(err => {
        console.error('❌ Error:', err);
    });
