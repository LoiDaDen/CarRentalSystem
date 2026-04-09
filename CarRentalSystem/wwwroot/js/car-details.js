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
                        
                        deliveryDistanceKm = calculateDistance(carData.lat, carData.lng, lat, lng);
                        updatePrices();
                        
                        fetch(`https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lng}&zoom=18&addressdetails=1`)
                            .then(res => res.json())
                            .then(data => {
                                if (data && data.address) {
                                    const addrStr = JSON.stringify(data.address).toLowerCase();
                                    const regionTarget = (carData.region || "").toLowerCase();
                                    
                                    const matchMapping = [
                                        { keys: ["hồ chí minh", "ho chi minh", "hcmc", "sg"], val: "TP. Hồ Chí Minh" },
                                        { keys: ["hà nội", "ha noi"], val: "Hà Nội" },
                                        { keys: ["đà nẵng", "da nang"], val: "Đà Nẵng" },
                                        { keys: ["cần thơ", "can tho"], val: "Cần Thơ" },
                                        { keys: ["bình dương", "binh duong"], val: "Bình Dương" },
                                        { keys: ["đồng nai", "dong nai"], val: "Đồng Nai" },
                                        { keys: ["nha trang", "khánh hòa", "khanh hoa"], val: "Nha Trang" },
                                        { keys: ["hải phòng", "hai phong"], val: "Hải Phòng" },
                                        { keys: ["vũng tàu", "vung tau", "bà rịa"], val: "Bà Rịa - Vũng Tàu" },
                                        { keys: ["cà mau", "ca mau"], val: "Cà Mau" },
                                        { keys: ["lâm đồng", "lam dong", "đà lạt", "da lat"], val: "Lâm Đồng" },
                                        { keys: ["phú quốc", "phu quoc", "kiên giang", "kien giang"], val: "Phú Quốc" }
                                    ];
                                    
                                    let foundVal = null;
                                    for (const m of matchMapping) {
                                        if (m.keys.some(k => addrStr.includes(k))) {
                                            foundVal = m.val; break;
                                        }
                                    }
                                    
                                    let isValid = false;
                                    if (foundVal && regionTarget.includes(foundVal.toLowerCase())) isValid = true;
                                    else if (!foundVal && addrStr.includes(regionTarget)) isValid = true;
                                    if (regionTarget.includes("hồ chí minh") && addrStr.includes("ho chi minh")) isValid = true;
                                    
                                    if (!isValid) {
                                        Swal.fire({
                                            icon: 'error',
                                            title: 'Ngoài khu vực hỗ trợ',
                                            text: `BonbonCar chỉ hỗ trợ giao xe trong khu vực ${carData.region}. Vui lòng chọn điểm nhận xe khác.`,
                                            confirmButtonColor: '#10b981'
                                        });
                                        pickerMapObj.marker.setLatLng([carData.lat, carData.lng]);
                                        pickerMapObj.map.panTo([carData.lat, carData.lng]);
                                        deliveryDistanceKm = 0;
                                        if (pickupCoords) pickupCoords.value = carData.address || coordsStr;
                                        if (finalLoc) finalLoc.value = carData.address || coordsStr;
                                        updatePrices();
                                        return;
                                    }
                                    
                                    const displayName = data.display_name;
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
        }
        updatePrices();
    }

    if (locCustomer && locOwner) {
        locCustomer.addEventListener('change', handleLocationToggle);
        locOwner.addEventListener('change', handleLocationToggle);
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

    // Calculate shipping fee: 3000đ/km + fuel (distance * 2 * 8L/100km * 25000đ/L)
    function calculateShippingFee(distanceKm) {
        if (distanceKm <= 0) return 0;
        const kmFee = Math.ceil(distanceKm) * 3000;
        const fuelFee = Math.ceil(distanceKm * 2 * 8 / 100 * 25000);
        return kmFee + fuelFee;
    }

    // 3. Dynamic Price Calculation - LINEAR PRICING between packages
    function calculateLinearPrice(hours) {
        if (hours <= 0) return 0;

        const p4 = carData.price4h > 0 ? carData.price4h : 4 * carData.pricePerHour;
        const p8 = carData.price8h > 0 ? carData.price8h : 8 * carData.pricePerHour;
        const p12 = carData.price12h > 0 ? carData.price12h : 12 * carData.pricePerHour;
        const p24 = carData.price24h > 0 ? carData.price24h : 24 * carData.pricePerHour;
        
        // Ensure 24h fallback for multi-day multiplier
        const maxDayPrice = p24 > 0 ? p24 : carData.price;

        if (hours <= 4) return Math.min(hours * carData.pricePerHour, p4);
        if (hours <= 8) return Math.min(p4 + (hours - 4) * carData.pricePerHour, p8);
        if (hours <= 12) return Math.min(p8 + (hours - 8) * carData.pricePerHour, p12);
        if (hours <= 24) return Math.min(p12 + (hours - 12) * carData.pricePerHour, maxDayPrice);
        
        const fullDays = Math.floor(hours / 24);
        const rem = hours % 24;
        let remP = 0;
        if (rem > 0) {
            if (rem <= 4) remP = Math.min(rem * carData.pricePerHour, p4);
            else if (rem <= 8) remP = Math.min(p4 + (rem - 4) * carData.pricePerHour, p8);
            else if (rem <= 12) remP = Math.min(p8 + (rem - 8) * carData.pricePerHour, p12);
            else remP = Math.min(p12 + (rem - 12) * carData.pricePerHour, maxDayPrice);
        }
        return (fullDays * maxDayPrice) + remP;
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
        
        if (isOwnerDelivery) {
            deliveryFee = calculateShippingFee(deliveryDistanceKm);
            if (deliveryFee < 50000) deliveryFee = 50000; // Minimum delivery fee
            if (deliveryFeeRow) {
                deliveryFeeRow.classList.remove('d-none');
                deliveryFeeRow.classList.add('d-flex');
                const feeSpan = deliveryFeeRow.querySelector('span:last-child');
                if (feeSpan) {
                    if (deliveryDistanceKm > 0) {
                        feeSpan.textContent = `+${deliveryFee.toLocaleString('en-US')} đ (${deliveryDistanceKm.toFixed(1)}km)`;
                    } else {
                        feeSpan.textContent = `+50,000 đ`;
                    }
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
        
        if (baseRent) baseRent.innerText = totalPrice.toLocaleString('en-US') + ' đ';
        if (discountAmountSpan) discountAmountSpan.innerText = '-' + discountVal.toLocaleString('en-US') + ' đ';
        
        // Grand total = rental - discount + VAT + delivery + addons
        const grandTotal = totalPrice - discountVal + 200000 + deliveryFee + addonTotal;
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
        btn.addEventListener('click', function() {
            const hours = parseInt(this.getAttribute('data-hours'));
            const startInput = document.getElementById('startDate');
            const endInput = document.getElementById('endDate');
            if (startInput && endInput && startInput.value) {
                const start = new Date(startInput.value);
                if (!isNaN(start)) {
                    const newEnd = new Date(start.getTime() + hours * 3600000);
                    if (endInput._flatpickr) {
                        endInput._flatpickr.setDate(newEnd, true);
                    } else {
                        const pad = num => String(num).padStart(2, '0');
                        endInput.value = `${newEnd.getFullYear()}-${pad(newEnd.getMonth() + 1)}-${pad(newEnd.getDate())}T${pad(newEnd.getHours())}:${pad(newEnd.getMinutes())}`;
                    }
                    updatePrices();
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

    const bookingForm = document.getElementById('bookingForm');
    if (bookingForm) {
        bookingForm.addEventListener('submit', function(e) {
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
                const res = await fetch('/Cart/AddToCart', {
                    method: 'POST',
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
