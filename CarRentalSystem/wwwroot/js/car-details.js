document.addEventListener('DOMContentLoaded', function () {
    // 1. Initialize Map Left Column
    if (typeof SmartMap !== 'undefined') {
        SmartMap.initStaticMap('carLocationMap', carData.lat, carData.lng, `<b>${carData.brand} ${carData.name}</b>`);
    }

    // 2. Right Sidebar Logic
    const locCustomer = document.getElementById('locCustomer');
    const locOwner = document.getElementById('locOwner');
    const ownerDeliverySection = document.getElementById('ownerDeliverySection');
    let pickerMapObj = null;
    let deliveryDistanceKm = 0;
    let deliveryDurationMinutes = 0;
    let estimatedDeliveryTime = null;
    let deliveryInScope = true;
    let deliveryScopeMessage = '';

    // Delivery info display element
    let deliveryInfoEl = null;

    function useFallbackDeliveryQuote(lat, lng, message) {
        deliveryInScope = true;
        deliveryScopeMessage = message || '';
        deliveryDistanceKm = calculateDistance(carData.lat, carData.lng, lat, lng);
        deliveryDurationMinutes = deliveryDistanceKm > 0 ? (deliveryDistanceKm / 35) * 60 : 0;
        estimatedDeliveryTime = new Date(Date.now() + (60 + deliveryDurationMinutes) * 60000);
    }

    async function refreshDeliveryQuote(lat, lng) {
        try {
            const response = await fetch(`/Car/CalculateDelivery?carId=${encodeURIComponent(carData.id)}&customerLat=${encodeURIComponent(lat)}&customerLng=${encodeURIComponent(lng)}`);
            const data = await response.json();
            if (!data.success || data.isWithinDeliveryScope === false) {
                deliveryInScope = false;
                deliveryScopeMessage = data.message || 'Địa điểm giao xe nằm ngoài phạm vi hỗ trợ.';
                deliveryDistanceKm = 0;
                deliveryDurationMinutes = 0;
            } else {
                deliveryInScope = true;
                deliveryScopeMessage = '';
                deliveryDistanceKm = Number(data.distanceKm) || 0;
                deliveryDurationMinutes = Number(data.durationMinutes) || 0;
            }
        } catch (err) {
            console.error('Delivery quote error', err);
            useFallbackDeliveryQuote(lat, lng, 'Đang dùng ước tính tạm thời do chưa kết nối được dịch vụ kiểm tra phạm vi.');
        }

        updatePrices();
        updateDeliveryInfoDisplay();
    }

    function handleLocationToggle() {
        if (locOwner && locOwner.checked) {
            ownerDeliverySection.style.display = 'block';
            if (!pickerMapObj && typeof SmartMap !== 'undefined') {
                pickerMapObj = SmartMap.initPickerMap({
                    containerId: 'bookingMap',
                    btnLocationId: 'getCurrentPoint'
                });

                if (pickerMapObj) {
                    pickerMapObj.map.setView([carData.lat, carData.lng], 14);
                    pickerMapObj.marker.setLatLng([carData.lat, carData.lng]);

                    const syncCoords = (lat, lng) => {
                        const coordsStr = `${lat.toFixed(6)}, ${lng.toFixed(6)}`;
                        const pickupCoords = document.getElementById('pickupCoords');
                        const finalLoc = document.getElementById('finalPickupLocation');
                        if (pickupCoords) pickupCoords.value = "Đang lấy địa chỉ...";
                        refreshDeliveryQuote(lat, lng);
                        
                        // 1. Dùng OSRM API để lấy khoảng cách và thời gian lái xe thật
                        fetch(`https://router.project-osrm.org/route/v1/driving/${carData.lng},${carData.lat};${lng},${lat}?overview=false`)
                            .then(res => res.json())
                            .then(data => {
                                if (data.routes && data.routes.length > 0) {
                                    const route = data.routes[0];
                                    deliveryDistanceKm = route.distance / 1000;
                                    const durationSec = route.duration;
                                    
                                    // Thời gian giao xe = Thời gian chạy xe + 1 giờ chuẩn bị
                                    const now = new Date();
                                    const totalHoursNeeded = 1 + (durationSec / 3600);
                                    deliveryDurationMinutes = durationSec / 60;
                                    estimatedDeliveryTime = new Date(now.getTime() + totalHoursNeeded * 3600000);

                                    // Tính thời điểm sớm nhất có thể giao xe
                                    const minStart = new Date(now.getTime() + totalHoursNeeded * 3600000);
                                    
                                    const startInput = document.getElementById('startDate');
                                    if (startInput) {
                                        const currentStart = new Date(startInput.value);
                                        // Nếu giờ bắt đầu hiện tại nhỏ hơn giờ bắt buộc tối thiểu -> Đẩy giờ lên
                                        if (isNaN(currentStart) || currentStart < minStart) {
                                            if (startInput._flatpickr) {
                                                startInput._flatpickr.setDate(minStart, true);
                                                startInput._flatpickr.set('minDate', minStart);
                                            } else {
                                                const pad = num => String(num).padStart(2, '0');
                                                startInput.value = `${minStart.getFullYear()}-${pad(minStart.getMonth() + 1)}-${pad(minStart.getDate())}T${pad(minStart.getHours())}:${pad(minStart.getMinutes())}`;
                                                startInput.min = startInput.value;
                                            }
                                            
                                            // Thông báo nhẹ cho người dùng biết giờ đã được điều chỉnh
                                            if (typeof Swal !== 'undefined' && totalHoursNeeded > 1.5) {
                                                Swal.fire({
                                                    toast: true,
                                                    position: 'top-end',
                                                    icon: 'info',
                                                    title: `Giờ nhận xe được dời xuống do thời gian giao xe mất khoảng ${Math.ceil(totalHoursNeeded)} tiếng!`,
                                                    showConfirmButton: false,
                                                    timer: 4000
                                                });
                                            }
                                        } else if (startInput._flatpickr) {
                                            startInput._flatpickr.set('minDate', minStart);
                                        }
                                    }
                                } else {
                                    // Dự phòng dùng khoảng cách đường chim bay nếu API lỗi
                                    deliveryDistanceKm = calculateDistance(carData.lat, carData.lng, lat, lng);
                                }
                                updatePrices();
                                updateDeliveryInfoDisplay();
                            })
                            .catch(err => {
                                console.error("OSRM error", err);
                                deliveryDistanceKm = calculateDistance(carData.lat, carData.lng, lat, lng);
                                updatePrices();
                                updateDeliveryInfoDisplay();
                            });
                        
                        // 2. Lấy tên địa chỉ (Bỏ chặn khu vực, cho phép khách hàng mọi nơi đều thuê được)
                        fetch(`https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lng}&zoom=18&addressdetails=1`)
                            .then(res => res.json())
                            .then(data => {
                                if (data && data.address) {
                                    // San pham display_name de tranh XSS
                                    const displayName = data.display_name
                                        ? data.display_name.replace(/</g, '&lt;').replace(/>/g, '&gt;')
                                        : coordsStr;
                                    if (pickupCoords) pickupCoords.value = displayName;
                                    if (finalLoc) finalLoc.value = displayName;
                                } else {
                                    if (pickupCoords) pickupCoords.value = coordsStr;
                                    if (finalLoc) finalLoc.value = coordsStr;
                                }
                            })
                            .catch(err => {
                                console.error("Geocoding error", err);
                                if (pickupCoords) pickupCoords.value = coordsStr;
                                if (finalLoc) finalLoc.value = coordsStr;
                            });
                    };

                    pickerMapObj.marker.on('dragend', function () {
                        const pos = pickerMapObj.marker.getLatLng();
                        syncCoords(pos.lat, pos.lng);
                    });
                    
                    pickerMapObj.map.on('click', function (e) {
                        syncCoords(e.latlng.lat, e.latlng.lng);
                    });

                    const locationBtn = document.getElementById('getCurrentPoint');
                    if (locationBtn) {
                        locationBtn.addEventListener('click', function() {
                            setTimeout(() => {
                                const pos = pickerMapObj.marker.getLatLng();
                                syncCoords(pos.lat, pos.lng);
                            }, 500);
                        });
                    }
                }
            }
            if (pickerMapObj) setTimeout(() => pickerMapObj.map.invalidateSize(), 200);
            document.getElementById('finalPickupLocation').value = document.getElementById('pickupCoords')?.value || '';
        } else {
            // CUSTOMER PICKUP - no delivery fee, reset everything
            if(ownerDeliverySection) ownerDeliverySection.style.display = 'none';
            document.getElementById('finalPickupLocation').value = carData.address;
            deliveryDistanceKm = 0;
            deliveryDurationMinutes = 0;
        }
        updatePrices();
        updateDeliveryInfoDisplay();
    }

    if (locCustomer && locOwner) {
        locCustomer.addEventListener('change', handleLocationToggle);
        locOwner.addEventListener('change', handleLocationToggle);
        // Initial call to update delivery info display
        handleLocationToggle();
    }

    // Haversine distance calculation
    function calculateDistance(lat1, lon1, lat2, lon2) {
        const R = 6371;
        const dLat = (lat2 - lat1) * Math.PI / 180;
        const dLon = (lon2 - lon1) * Math.PI / 180;
        const a = Math.sin(dLat/2) * Math.sin(dLat/2) +
                  Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
                  Math.sin(dLon/2) * Math.sin(dLon/2);
        const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1-a));
        return R * c;
    }

    // Calculate shipping fee: 3000đ/km
    function calculateShippingFee(distanceKm) {
        if (distanceKm <= 0) return 0;
        const kmFee = Math.ceil(distanceKm) * 3000;
        return kmFee;
    }

    // Update delivery info display
    function updateDeliveryInfoDisplay() {
        const distanceEl = document.getElementById('deliveryDistanceDisplay');
        const timeEl = document.getElementById('deliveryTimeDisplay');
        const locOwnerEl = document.getElementById('locOwner');
        
        if (!distanceEl || !timeEl) return;
        
        if (locOwnerEl && locOwnerEl.checked && !deliveryInScope) {
            distanceEl.textContent = deliveryScopeMessage || 'Địa điểm giao xe nằm ngoài phạm vi hỗ trợ.';
            timeEl.textContent = '-';
        } else if (locOwnerEl && locOwnerEl.checked && deliveryDistanceKm > 0) {
            distanceEl.textContent = deliveryDistanceKm.toFixed(1) + ' km';
            const totalHours = 1 + (deliveryDurationMinutes / 60);
            if (totalHours >= 1) {
                timeEl.textContent = '~' + Math.ceil(totalHours) + ' tiếng';
            } else {
                timeEl.textContent = '~' + Math.ceil(deliveryDurationMinutes) + ' phút';
            }
        } else {
            distanceEl.textContent = 'Chọn vị trí để tính';
            timeEl.textContent = '-';
        }
    }

    function calculateLinearPrice(hours) {
        if (hours <= 0) return 0;

        const p4 = carData.price4h > 0 ? carData.price4h : 4 * carData.pricePerHour;
        const p8 = carData.price8h > 0 ? carData.price8h : 8 * carData.pricePerHour;
        const p12 = carData.price12h > 0 ? carData.price12h : 12 * carData.pricePerHour;
        const p24 = carData.price24h > 0 ? carData.price24h : 24 * carData.pricePerHour;
        
        // Ensure 24h fallback for multi-day multiplier
        const maxDayPrice = p24 > 0 ? p24 : carData.price;
        
        const fullDays = Math.floor(hours / 24);
        const remHours = hours % 24;

        const blocks = [
            { h: 1, cost: carData.pricePerHour },
            { h: 4, cost: p4 },
            { h: 8, cost: p8 },
            { h: 12, cost: p12 },
            { h: 24, cost: maxDayPrice }
        ];
        
        let dp = new Array(25).fill(Infinity);
        dp[0] = 0;
        for (let i = 1; i <= 24; i++) {
            for (let b of blocks) {
                if (i >= b.h) {
                    dp[i] = Math.min(dp[i], dp[i - b.h] + b.cost);
                }
            }
        }
        for (let i = 23; i >= 1; i--) {
            dp[i] = Math.min(dp[i], dp[i + 1]);
        }
        
        return (fullDays * maxDayPrice) + dp[remHours];
    }

    function updatePrices() {
        const startInput = document.getElementById('startDate');
        const endInput = document.getElementById('endDate');
        if (!startInput || !endInput) return;
        
        const start = new Date(startInput.value);
        const end = new Date(endInput.value);
        
        if (isNaN(start) || isNaN(end) || end <= start) return;
        
        const diffMs = end - start;
        const diffHours = Math.ceil(diffMs / (1000 * 60 * 60));
        
        // Calculate price using linear pricing
        let totalPrice = calculateLinearPrice(diffHours);
        
        // Auto-select the matching package button
        const pkgBtns = document.querySelectorAll('.pkg-btn');
        if (pkgBtns.length > 0) {
            pkgBtns.forEach(b => {
                b.classList.remove('active', 'bg-success', 'text-white');
                b.classList.add('text-success');
            });
            let bestVal = String(diffHours);
            const exactBtn = document.querySelector(`.pkg-btn[data-hours="${bestVal}"]`);
            if (exactBtn) {
                exactBtn.classList.remove('text-success', 'btn-outline-success');
                exactBtn.classList.add('active', 'bg-success', 'text-white');
            }
        }
        
        // ====== DELIVERY FEE ======
        // ONLY charge delivery fee when "locOwner" (BonbonCar giao & nhận xe) is selected
        let deliveryFee = 0;
        const locOwnerEl = document.getElementById('locOwner');
        const deliveryFeeRow = document.getElementById('deliveryFeeRow');
        
        const isOwnerDelivery = locOwnerEl && locOwnerEl.checked;
        
        if (isOwnerDelivery && !deliveryInScope) {
            deliveryFee = 0;
            if (deliveryFeeRow) {
                deliveryFeeRow.classList.remove('d-none');
                deliveryFeeRow.classList.add('d-flex');
                const feeSpan = deliveryFeeRow.querySelector('span:last-child');
                if (feeSpan) feeSpan.textContent = deliveryScopeMessage || 'Địa điểm giao xe nằm ngoài phạm vi hỗ trợ.';
            }
        } else if (isOwnerDelivery) {
            deliveryFee = calculateShippingFee(deliveryDistanceKm);
            if (deliveryFeeRow) {
                deliveryFeeRow.classList.remove('d-none');
                deliveryFeeRow.classList.add('d-flex');
                const feeSpan = deliveryFeeRow.querySelector('span:last-child');
                if (feeSpan) {
                    feeSpan.textContent = `+${deliveryFee.toLocaleString('en-US')} đ (${deliveryDistanceKm.toFixed(1)}km)`;
                }
            }
        } else {
            // Customer pickup - NO delivery fee at all
            deliveryFee = 0;
            if (deliveryFeeRow) {
                deliveryFeeRow.classList.remove('d-flex');
                deliveryFeeRow.classList.add('d-none');
            }
        }

        // ====== ADD-ON TOTAL + RENDER IN ORDER ======
        let addonTotal = 0;
        const addonItemsContainer = document.getElementById('addonItemsInOrder');
        let addonHtml = '';
        document.querySelectorAll('.addon-check').forEach(cb => {
            if (cb.checked) {
                const parts = cb.value.split('|');
                if (parts.length > 1) {
                    const price = parseInt(parts[1]) || 0;
                    addonTotal += price;
                    addonHtml += `<div class="d-flex justify-content-between mb-1 small text-muted"><span><i class="bi bi-dot"></i>${parts[0]}</span><span class="text-success fw-bold">+${price.toLocaleString('en-US')} đ</span></div>`;
                }
            }
        });
        if (addonItemsContainer) addonItemsContainer.innerHTML = addonHtml;
        
        const baseRent = document.getElementById('baseRentAmount');
        const finalTotal = document.getElementById('finalTotalAmount');
        const addonRow = document.getElementById('addonTotalRow');
        const addonAmount = document.getElementById('addonTotalAmount');
        const discountInput = document.getElementById('contractDiscount');
        const discountAmountSpan = document.getElementById('discountAmount');
        
        let discountPercent = discountInput ? parseFloat(discountInput.value) || 0 : 0;
        let discountVal = (totalPrice * discountPercent) / 100;
        
        const memDiscountInput = document.getElementById('membershipDiscount');
        const memDiscountAmountSpan = document.getElementById('memDiscountAmount');
        let memDiscountPercent = memDiscountInput ? parseFloat(memDiscountInput.value) || 0 : 0;
        let memDiscountVal = (totalPrice * memDiscountPercent) / 100;

        if (baseRent) baseRent.innerText = totalPrice.toLocaleString('en-US') + ' đ';
        if (discountAmountSpan) discountAmountSpan.innerText = '-' + discountVal.toLocaleString('en-US') + ' đ';
        if (memDiscountAmountSpan) memDiscountAmountSpan.innerText = '-' + memDiscountVal.toLocaleString('en-US') + ' đ';
        
        // Grand total = rental - contract_discount - mem_discount + VAT + delivery + addons
        const grandTotal = totalPrice - discountVal - memDiscountVal + 200000 + deliveryFee + addonTotal;
        if (finalTotal) finalTotal.innerText = grandTotal.toLocaleString('en-US') + ' đ';

        // Show/hide addon total row in pricing summary
        if (addonRow) {
            if (addonTotal > 0) {
                addonRow.classList.remove('d-none');
                addonRow.classList.add('d-flex');
            } else {
                addonRow.classList.remove('d-flex');
                addonRow.classList.add('d-none');
            }
        }
        if (addonAmount) addonAmount.innerText = '+' + addonTotal.toLocaleString('en-US') + ' đ';
    }

    // Auto-update end date when a package button is clicked
    document.querySelectorAll('.pkg-btn').forEach(btn => {
        btn.addEventListener('click', function(e) {
            e.preventDefault();
            const hours = parseInt(this.getAttribute('data-hours'));
            const startInput = document.getElementById('startDate');
            const endInput = document.getElementById('endDate');
            const displayObj = document.getElementById('bookingCalendarDisplay');
            if (startInput && endInput && startInput.value) {
                const start = new Date(startInput.value);
                if (!isNaN(start)) {
                    const newEnd = new Date(start.getTime() + hours * 3600000);
                    if (endInput._flatpickr) {
                        endInput._flatpickr.setDate(newEnd, true);
                    }
                    const pad = num => String(num).padStart(2, '0');
                    endInput.value = newEnd.getFullYear() + '-' + pad(newEnd.getMonth()+1) + '-' + pad(newEnd.getDate()) + 'T' + pad(newEnd.getHours()) + ':' + pad(newEnd.getMinutes());
                    if (displayObj && startInput.value) {
                        const s = new Date(startInput.value);
                        const m1 = pad(s.getMonth()+1); const d1 = pad(s.getDate());
                        const h1 = pad(s.getHours()) + ':' + pad(s.getMinutes());
                        const m2 = pad(newEnd.getMonth()+1); const d2 = pad(newEnd.getDate());
                        const h2 = pad(newEnd.getHours()) + ':' + pad(newEnd.getMinutes());
                        displayObj.innerText = `${d1}/${m1} ${h1} - ${d2}/${m2} ${h2}`;
                    }
                    updatePrices();
                    updateDeliveryInfoDisplay();
                }
            }
        });
    });

    // Listen for add-on checkbox changes
    document.querySelectorAll('.addon-check').forEach(cb => {
        cb.addEventListener('change', updatePrices);
    });

    // Listen for date changes (including flatpickr)
    const startObj = document.getElementById('startDate');
    const endObj = document.getElementById('endDate');
    if (startObj && endObj) {
        startObj.addEventListener('change', updatePrices);
        endObj.addEventListener('change', updatePrices);
        // Also listen for input events (manual typing)
        startObj.addEventListener('input', updatePrices);
        endObj.addEventListener('input', updatePrices);
    }
    
    // Initial price update
    updatePrices();
    updateDeliveryInfoDisplay();

    const bookingForm = document.getElementById('bookingForm');
    if (bookingForm) {
        bookingForm.addEventListener('submit', function(e) {
            if (locOwner && locOwner.checked && !deliveryInScope) {
                e.preventDefault();
                if (typeof Swal !== 'undefined') {
                    Swal.fire('Thông báo', deliveryScopeMessage || 'Địa điểm giao xe nằm ngoài phạm vi hỗ trợ.', 'warning');
                } else {
                    alert(deliveryScopeMessage || 'Địa điểm giao xe nằm ngoài phạm vi hỗ trợ.');
                }
                return;
            }
            const btn = document.getElementById('confirmBookingBtn');
            if(btn) {
                btn.disabled = true;
                btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>ĐANG XỬ LÝ...';
            }
        });
    }

    // Re-enable submit button when user navigates back via browser history
    window.addEventListener('pageshow', function(event) {
        const btn = document.getElementById('confirmBookingBtn');
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = 'Thuê xe';
        }
    });

    // 4. Add to Cart Logic
    const addToCartBtn = document.getElementById('addToCartBtn');
    if (addToCartBtn) {
        addToCartBtn.addEventListener('click', async function() {
            const startInput = document.getElementById('startDate');
            const endInput = document.getElementById('endDate');
            let startVal = startInput?.value;
            let endVal = endInput?.value;
            
            let start, end, estimatedPrice;
            if (!startVal || !endVal) {
                // Default: Now to Tomorrow
                start = new Date();
                end = new Date(start.getTime() + 24 * 60 * 60 * 1000);
                
                // Format dates to simple ISO strings for the backend
                // Add padding so timezone matches local roughly, or just use string
                start.setMinutes(start.getMinutes() - start.getTimezoneOffset());
                end.setMinutes(end.getMinutes() - end.getTimezoneOffset());
                startVal = start.toISOString().slice(0,16);
                endVal = end.toISOString().slice(0,16);
                
                estimatedPrice = carData.price24h > 0 ? carData.price24h : (carData.price || carData.pricePerHour * 24);
            } else {
                start = new Date(startVal);
                end = new Date(endVal);
                if (end <= start) {
                    Swal.fire('Lỗi', 'Ngày kết thúc phải sau ngày bắt đầu.', 'error');
                    return;
                }
                const diffHours = Math.ceil((end - start) / 3600000);
                estimatedPrice = calculateLinearPrice(diffHours);
            }

            addToCartBtn.disabled = true;
            addToCartBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';

            const formData = new FormData();
            formData.append('carId', carData.id);
            formData.append('startDate', startVal);
            formData.append('endDate', endVal);
            formData.append('withDriver', false);
            formData.append('estimatedPrice', estimatedPrice);

            try {
                // Lấy AntiForgery token để gửi kèm request
                const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

                const res = await fetch('/Cart/AddToCart', {
                    method: 'POST',
                    headers: {
                        'RequestVerificationToken': csrfToken || ''
                    },
                    body: formData
                });
                const data = await res.json();
                if (data.success) {
                    Swal.fire({
                        icon: 'success',
                        title: 'Thành công',
                        text: 'Đã thêm xe vào giỏ hàng của bạn.',
                        showCancelButton: true,
                        confirmButtonText: 'Đến giỏ hàng',
                        cancelButtonText: 'Tiếp tục xem xe',
                        confirmButtonColor: '#10b981',
                        cancelButtonColor: '#6e7881'
                    }).then((result) => {
                        if (result.isConfirmed) {
                            window.location.href = '/Cart';
                        }
                    });
                    if (typeof updateCartBadge === 'function') updateCartBadge();
                } else {
                    Swal.fire('Thông báo', data.message || 'Lỗi khi thêm vào giỏ hàng', 'warning');
                }
            } catch (e) {
                Swal.fire('Lỗi', 'Có lỗi xảy ra khi kết nối đến hệ thống.', 'error');
            } finally {
                addToCartBtn.disabled = false;
                addToCartBtn.innerHTML = '<i class="bi bi-cart-plus"></i>';
            }
        });
    }
});
