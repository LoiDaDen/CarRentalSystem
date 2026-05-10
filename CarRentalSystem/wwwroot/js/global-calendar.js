// global-calendar.js
// ============================================================
// FIX: Chuyển flatpickr sang inline: true gắn trực tiếp vào
// #calendarTriggerZone (div, không phải input ẩn).
// Mỗi lần modal mở lại -> destroy instance cũ -> tạo mới.
// Init chỉ chạy SAU event shown.bs.modal (đảm bảo DOM sẵn sàng).
// ============================================================

let g_flatpickrInst = null;
let g_targetStartInput = null;
let g_targetEndInput = null;
let g_onConfirmCallback = null;
let g_startDateSelected = null; // Ngày bắt đầu đã chọn

// Danh sách ngày lễ cố định Việt Nam (DD/MM)
const FIXED_HOLIDAYS = {
    '01/01': 'Tết Dương lịch',
    '30/04': 'Ngày Giải phóng',
    '01/05': 'Quốc Tế Lao Động',
    '02/09': 'Quốc Khánh',
    '10/03': 'Giỗ Tổ Hùng Vương',
};

// Tết Nguyên Đán
function getTetDates(year) {
    const tetDates = {
        2026: { start: '17/02', end: '23/02' },
        2027: { start: '08/02', end: '14/02' },
        2028: { start: '27/01', end: '02/02' },
        2029: { start: '15/02', end: '21/02' },
        2030: { start: '04/02', end: '10/02' },
    };

    const tet = tetDates[year] || tetDates[2026];
    const [sd, sm] = tet.start.split('/').map(Number);
    const [ed, em] = tet.end.split('/').map(Number);

    const startDate = new Date(year, sm - 1, sd);
    const endDate = new Date(em < sm ? year + 1 : year, em - 1, ed);

    return { start: startDate, end: endDate, name: `Tết Nguyên Đán ${year}` };
}

function isHoliday(dateOrStr) {
    // flatpickr có thể truyền string thay vì Date object vào onDayCreate
    const date = typeof dateOrStr === 'string' ? new Date(dateOrStr) : dateOrStr;
    if (!(date instanceof Date) || isNaN(date.getTime())) return false;

    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, '0');
    const d = String(date.getDate()).padStart(2, '0');
    const key = `${d}/${m}`;

    if (FIXED_HOLIDAYS[key]) return FIXED_HOLIDAYS[key];

    const tet = getTetDates(y);
    if (date >= tet.start && date <= tet.end) return tet.name;

    return false;
}

function isWeekend(dateOrStr) {
    const date = typeof dateOrStr === 'string' ? new Date(dateOrStr) : dateOrStr;
    if (!(date instanceof Date) || isNaN(date.getTime())) return false;
    return date.getDay() === 0 || date.getDay() === 6;
}

function updateSummaryDisplay(dates) {
    if (!dates || dates.length === 0) return;

    // Get values from select dropdowns (24h format)
    const startHour = document.getElementById('internalStartHour')?.value || '07';
    const startMinute = document.getElementById('internalStartMinute')?.value || '00';
    const endHour = document.getElementById('internalEndHour')?.value || '11';
    const endMinute = document.getElementById('internalEndMinute')?.value || '00';

    const startTime = `${startHour}:${startMinute}`;
    const endTime = `${endHour}:${endMinute}`;

    if (dates[0]) {
        const el = document.getElementById('summaryStartDisplay');
        if (el) el.innerText = formatDisplayCustom(dates[0], startTime);
    }
    if (dates.length > 1 && dates[1]) {
        const el = document.getElementById('summaryEndDisplay');
        if (el) el.innerText = formatDisplayCustom(dates[1], endTime);
    } else {
        const el = document.getElementById('summaryEndDisplay');
        if (el) el.innerText = '--/--/-- --:--';
    }
}

function formatDateWithTime(dateObj, timeStr) {
    const y = dateObj.getFullYear();
    const m = String(dateObj.getMonth() + 1).padStart(2, '0');
    const d = String(dateObj.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}T${timeStr}`;
}

/**
 * Disable giờ/phút đã qua so với giờ Việt Nam (UTC+7)
 * @param {Date} selectedDate - Ngày đã chọn trên calendar
 * @param {string} type - 'start' hoặc 'end'
 */
function disablePastTimes(selectedDate, type) {
    const now = new Date();
    // Chuyển sang giờ Việt Nam (UTC+7)
    const nowVN = new Date(now.getTime() + 7 * 60 * 60 * 1000);
    const todayVN = new Date(nowVN.getFullYear(), nowVN.getMonth(), nowVN.getDate());

    const hourEl = document.getElementById(`internal${type.charAt(0).toUpperCase() + type.slice(1)}Hour`);
    const minuteEl = document.getElementById(`internal${type.charAt(0).toUpperCase() + type.slice(1)}Minute`);

    if (!hourEl || !minuteEl) return;

    const selectedDayVN = new Date(selectedDate.getFullYear(), selectedDate.getMonth(), selectedDate.getDate());

    // Nếu chọn ngày hôm nay → disable giờ đã qua
    if (selectedDayVN.getTime() === todayVN.getTime()) {
        const currentHour = nowVN.getHours();
        const currentMinute = nowVN.getMinutes();

        // Disable các giờ đã qua
        hourEl.querySelectorAll('option').forEach(opt => {
            const hour = parseInt(opt.value);
            opt.disabled = hour < currentHour;
        });

        // Disable các phút đã qua trong giờ hiện tại
        minuteEl.querySelectorAll('option').forEach(opt => {
            const minute = parseInt(opt.value);
            const hour = parseInt(hourEl.value);
            opt.disabled = (hour === currentHour && minute < currentMinute);
        });
    } else {
        // Ngày khác hôm nay → enable tất cả
        hourEl.querySelectorAll('option').forEach(opt => opt.disabled = false);
        minuteEl.querySelectorAll('option').forEach(opt => opt.disabled = false);
    }

    // Đảm bảo giá trị được chọn không bị disabled
    if (hourEl.selectedOptions[0]?.disabled) {
        hourEl.value = String(nowVN.getHours()).padStart(2, '0');
    }
    if (minuteEl.selectedOptions[0]?.disabled) {
        minuteEl.value = String(nowVN.getMinutes()).padStart(2, '0');
    }
}

/**
 * Cập nhật trạng thái disable cho tất cả time dropdowns
 */
function updateTimeOptions() {
    if (g_startDateSelected) {
        disablePastTimes(g_startDateSelected, 'start');
    }
    disablePastTimes(new Date(), 'end');
}

function formatDisplayCustom(dateObj, timeStr) {
    const m = String(dateObj.getMonth() + 1).padStart(2, '0');
    const d = String(dateObj.getDate()).padStart(2, '0');
    return `${d}/${m} ${timeStr}`;
}

document.addEventListener('DOMContentLoaded', function () {
    // Time input listeners for select dropdowns
    document.getElementById('internalStartHour')?.addEventListener('change', function () {
        updateTimeOptions();
        if (g_flatpickrInst) updateSummaryDisplay(g_flatpickrInst.selectedDates);
    });
    document.getElementById('internalStartMinute')?.addEventListener('change', function () {
        if (g_flatpickrInst) updateSummaryDisplay(g_flatpickrInst.selectedDates);
    });
    document.getElementById('internalEndHour')?.addEventListener('change', function () {
        if (g_flatpickrInst) updateSummaryDisplay(g_flatpickrInst.selectedDates);
    });
    document.getElementById('internalEndMinute')?.addEventListener('change', function () {
        if (g_flatpickrInst) updateSummaryDisplay(g_flatpickrInst.selectedDates);
    });

    // Confirm button
    document.getElementById('btnConfirmCalendar')?.addEventListener('click', function () {
        const dates = g_flatpickrInst?.selectedDates;
        if (!dates || dates.length !== 2) {
            alert('Vui lòng chọn ngày nhận và ngày trả xe!');
            return;
        }

        // Get values from select dropdowns (24h format)
        const startHour = document.getElementById('internalStartHour')?.value || '07';
        const startMinute = document.getElementById('internalStartMinute')?.value || '00';
        const endHour = document.getElementById('internalEndHour')?.value || '11';
        const endMinute = document.getElementById('internalEndMinute')?.value || '00';

        const startTime = `${startHour}:${startMinute}`;
        const endTime = `${endHour}:${endMinute}`;

        const startStr = formatDateWithTime(dates[0], startTime);
        const endStr = formatDateWithTime(dates[1], endTime);

        if (g_targetStartInput) g_targetStartInput.value = startStr;
        if (g_targetEndInput) g_targetEndInput.value = endStr;

        const displayLabel = document.querySelector('[data-calendar-display]');
        if (displayLabel) {
            displayLabel.innerText = `${formatDisplayCustom(dates[0], startTime)} - ${formatDisplayCustom(dates[1], endTime)}`;
        }

        if (g_targetStartInput) g_targetStartInput.dispatchEvent(new Event('change'));
        if (g_targetEndInput) g_targetEndInput.dispatchEvent(new Event('change'));

        if (typeof g_onConfirmCallback === 'function') {
            g_onConfirmCallback(startStr, endStr);
        }

        const modalEl = document.getElementById('carCalendarModal');
        if (modalEl) {
            const modal = bootstrap.Modal.getInstance(modalEl);
            if (modal) modal.hide();
        }
    });

    // Responsive: adjust showMonths on resize (with debounce)
    let resizeTimeout;
    window.addEventListener('resize', function () {
        clearTimeout(resizeTimeout);
        resizeTimeout = setTimeout(function () {
            // g_flatpickrInst có thể là null nếu calendar chưa mở hoặc đã destroy
            if (g_flatpickrInst && typeof g_flatpickrInst.set === 'function') {
                const newMonths = window.innerWidth > 576 ? 2 : 1;
                g_flatpickrInst.set('showMonths', newMonths);
            }
        }, 250);
    });
});

/**
 * Mở modal calendar.
 *
 * CHAIN:
 * 1. Lưu target inputs + callback.
 * 2. Parse ngày mặc định từ opts hoặc hidden inputs.
 * 3. Pre-set time inputs (giờ nhận/trả).
 * 4. Tính disabledDates.
 * 5. Destroy flatpickr cũ nếu có (bước QUAN TRỌNG cho lifecycle).
 * 6. Lắng nghe shown.bs.modal — KHI modal thực sự visible MỚI tạo flatpickr.
 *    Việc init trong shown chứ không trong open/click đảm bảo
 *    #calendarTriggerZone đã visible trong DOM tree (modal backdrop đã appended).
 */
window.openCarCalendar = function (opts) {
    g_targetStartInput = document.getElementById(opts.startInputId);
    g_targetEndInput = document.getElementById(opts.endInputId);
    g_onConfirmCallback = opts.onConfirm || null;

    const modalEl = document.getElementById('carCalendarModal');
    if (!modalEl) return;

    // --- 1. Parse ngày mặc định ---
    let d1, d2;
    if (opts.defaultStart) {
        const parsed = new Date(opts.defaultStart);
        if (!isNaN(parsed.getTime())) d1 = parsed;
    } else if (g_targetStartInput && g_targetStartInput.value) {
        const p = new Date(g_targetStartInput.value);
        if (!isNaN(p.getTime())) d1 = p;
    }
    if (opts.defaultEnd) {
        const parsed = new Date(opts.defaultEnd);
        if (!isNaN(parsed.getTime())) d2 = parsed;
    } else if (g_targetEndInput && g_targetEndInput.value) {
        const p = new Date(g_targetEndInput.value);
        if (!isNaN(p.getTime())) d2 = p;
    }

    // Validate year - prevent invalid dates like year 2001
    // Sử dụng giờ Việt Nam (UTC+7) làm mốc
    const nowVN = new Date(Date.now() + 7 * 60 * 60 * 1000);

    if (!d1 || isNaN(d1.getTime()) || d1.getFullYear() < nowVN.getFullYear()) {
        d1 = new Date(nowVN.getTime() + 2 * 3600 * 1000); // Tối thiểu 2 tiếng sau
    }
    if (!d2 || isNaN(d2.getTime()) || d2.getFullYear() < nowVN.getFullYear()) {
        d2 = new Date(d1.getTime() + 24 * 3600 * 1000);
    }

    // --- 2. Pre-set time inputs ---
    const startHourEl = document.getElementById('internalStartHour');
    const startMinuteEl = document.getElementById('internalStartMinute');
    const endHourEl = document.getElementById('internalEndHour');
    const endMinuteEl = document.getElementById('internalEndMinute');

    if (startHourEl) startHourEl.value = String(d1.getHours()).padStart(2, '0');
    if (startMinuteEl) startMinuteEl.value = String(d1.getMinutes()).padStart(2, '0');
    if (endHourEl) endHourEl.value = String(d2.getHours()).padStart(2, '0');
    if (endMinuteEl) endMinuteEl.value = String(d2.getMinutes()).padStart(2, '0');

    // Disable giờ đã qua theo giờ Việt Nam
    g_startDateSelected = d1;
    disablePastTimes(d1, 'start');
    disablePastTimes(d2, 'end');

    // --- 3. Build disabled dates list ---
    const disabledDates = (opts.disabledDates || []).map(function (d) { return new Date(d); });
    if (window.carDisabledDates && window.carDisabledDates.length > 0) {
        window.carDisabledDates.forEach(function (d) { disabledDates.push(new Date(d)); });
    }

    // --- 4. Xử lý lifecycle: destroy flatpickr cũ trước khi tạo mới ---
    // KHÔNG destroy ở đây — destroy sẽ xảy ra TRONG shown.bs.modal handler
    // để tránh destroy khi modal chưa mở (lần đầu chỉ show(), chưa có instance)
    // Lưu trữ dữ liệu cần dùng trong shown handler
    window._calendarPendingConfig = {
        d1: d1,
        d2: d2,
        disabledDates: disabledDates,
        showMonths: window.innerWidth > 576 ? 2 : 1
    };

    // Luôn destroy trước khi tạo mới — gọi ngay để clean nếu đã có instance
    if (g_flatpickrInst) {
        try { g_flatpickrInst.destroy(); } catch (e) {}
        g_flatpickrInst = null;
    }

    // --- 5. Lắng nghe shown.bs.modal: init flatpickr SAU KHI modal visible ---
    // Dùng { once: true } để tự gỡ listener sau khi chạy 1 lần
    modalEl.addEventListener('shown.bs.modal', function initCalendarOnShown() {
        modalEl.removeEventListener('shown.bs.modal', initCalendarOnShown);

        // Đọc config đã lưu
        const cfg = window._calendarPendingConfig || {};
        window._calendarPendingConfig = null;

        const cfgD1 = cfg.d1 || new Date(Date.now() + 2 * 3600 * 1000);
        const cfgD2 = cfg.d2 || new Date(cfgD1.getTime() + 24 * 3600 * 1000);
        const cfgDisabled = cfg.disabledDates || [];
        const cfgMonths = cfg.showMonths || 1;

        const calendarInput = document.getElementById('internalFlatpickrRange');

        // Tạo flatpickr với inline: true
        // flatpickr sẽ append calendar vào parent của input (#calendarTriggerZone)
        // KHÔNG dùng static: true (gây conflict CSS với modal)
        // KHÔNG dùng appendTo: (flatpickr tự append vào parent của input)
        g_flatpickrInst = flatpickr(calendarInput, {
            inline: true,                    // Render inline trong DOM, không dùng overlay popup
            mode: 'range',
            showMonths: cfgMonths,
            dateFormat: 'Y-m-d',
            minDate: 'today',
            locale: 'vn',
            defaultDate: [cfgD1, cfgD2],
            disable: cfgDisabled,
            onChange: function (selectedDates) {
                updateSummaryDisplay(selectedDates);
                // Cập nhật disable time khi ngày thay đổi
                if (selectedDates && selectedDates.length > 0) {
                    g_startDateSelected = selectedDates[0];
                    disablePastTimes(selectedDates[0], 'start');
                    if (selectedDates.length > 1) {
                        disablePastTimes(selectedDates[1], 'end');
                    }
                }
            },
            onDayCreate: function (dObj, dStr, fp, dayElem) {
                const date = dayElem.dateObj;

                // Ngày đã qua (disabled) → giữ xám, không tô màu đặc biệt
                if (dayElem.classList.contains('flatpickr-disabled') || dayElem.classList.contains('disabled')) {
                    return;
                }

                const holidayName = isHoliday(date);
                const weekend = isWeekend(date);

                if (holidayName && holidayName.includes('Tết')) {
                    dayElem.classList.add('tet-day');
                    dayElem.title = holidayName;
                } else if (holidayName) {
                    dayElem.classList.add('holiday-day');
                    dayElem.title = holidayName;
                } else if (weekend) {
                    dayElem.classList.add('weekend-day');
                    dayElem.title = 'Cuối tuần';
                }
            }
        });

        // Disable giờ đã qua sau khi tạo flatpickr xong
        g_startDateSelected = cfgD1;
        disablePastTimes(cfgD1, 'start');
        disablePastTimes(cfgD2, 'end');

        updateSummaryDisplay([cfgD1, cfgD2]);
    }, { once: true });

    // Mở modal
    const modal = new bootstrap.Modal(modalEl);
    modal.show();
};
