/**
 * SmartRental Map Library
 * A standard Leaflet wrapper for the entire Car Rental System
 * Uses OpenStreetMap tiles and Nominatim geocoding.
 */
const SmartMap = {
    // Shared configurations
    defaultLat: 10.7769,
    defaultLng: 106.7009,
    tileUrl: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
    tileAttr: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    geocodingBaseUrl: 'https://nominatim.openstreetmap.org',

    addTileLayer: function(map) {
        const tileLayer = L.tileLayer(this.tileUrl, {
            attribution: this.tileAttr,
            maxZoom: 19
        });
        tileLayer.on('tileerror', () => document.dispatchEvent(new CustomEvent('smartrental:tileerror')));
        tileLayer.addTo(map);
        return tileLayer;
    },

    createMap: function(containerId, lat, lng, zoom = 14, mapOptions = {}) {
        const el = document.getElementById(containerId);
        if (!el || typeof L === 'undefined') return null;

        const resolvedLat = Number.isFinite(Number(lat)) ? Number(lat) : this.defaultLat;
        const resolvedLng = Number.isFinite(Number(lng)) ? Number(lng) : this.defaultLng;
        const map = L.map(containerId, mapOptions).setView([resolvedLat, resolvedLng], zoom);
        const tileLayer = this.addTileLayer(map);
        return { map, tileLayer };
    },

    reverseGeocode: function(lat, lng) {
        const url = `${this.geocodingBaseUrl}/reverse?format=json&lat=${encodeURIComponent(lat)}&lon=${encodeURIComponent(lng)}&zoom=18&addressdetails=1`;
        return fetch(url).then(response => {
            if (!response.ok) throw new Error('Reverse geocoding failed.');
            return response.json();
        });
    },

    forwardGeocode: function(query) {
        const url = `${this.geocodingBaseUrl}/search?format=json&q=${encodeURIComponent(query)}&countrycodes=vn&limit=1`;
        return fetch(url).then(response => {
            if (!response.ok) throw new Error('Forward geocoding failed.');
            return response.json();
        });
    },

    getOpenStreetMapUrl: function(lat, lng, zoom = 16) {
        const resolvedLat = Number(lat);
        const resolvedLng = Number(lng);
        if (!Number.isFinite(resolvedLat) || !Number.isFinite(resolvedLng)) return '#';
        return `https://www.openstreetmap.org/?mlat=${resolvedLat}&mlon=${resolvedLng}#map=${zoom}/${resolvedLat}/${resolvedLng}`;
    },

    // Generic Icon (car icon)
    getCarIcon: function() {
        return L.divIcon({
            className: 'custom-div-icon border-0 bg-transparent',
            html: "<div style='background-color:#2563eb; width:34px; height:34px; border-radius:50%; border:3px solid white; box-shadow: 0 4px 8px rgba(0,0,0,0.3); display: flex; justify-content: center; align-items: center;'><i class='bi bi-car-front-fill text-white fs-6'></i></div>",
            iconSize: [34, 34],
            iconAnchor: [17, 17]
        });
    },

    // 1. Static Map (For displaying single location, e.g. Car Details, MyBookings)
    initStaticMap: function(containerId, lat, lng, popupHtml = '') {
        const baseMap = this.createMap(containerId, lat, lng, 15);
        if (!baseMap) return null;
        const { map, tileLayer } = baseMap;

        const marker = L.marker([lat || this.defaultLat, lng || this.defaultLng], { icon: this.getCarIcon() }).addTo(map);
        if (popupHtml) {
            marker.bindPopup(popupHtml).openPopup();
        }

        // Fix rendering issue in hidden tabs/modals
        setTimeout(() => map.invalidateSize(), 500);
        return { map, marker, tileLayer };
    },

    // 2. Map Picker (For Add/Edit Car where user drags marker to select location)
    initPickerMap: function(opts = {}) {
        /*
          opts = {
              containerId: 'mapPicker',
              latInputId: 'latInput',
              lngInputId: 'lngInput',
              addressInputId: 'carAddressInput',
              btnLocationId: 'btnGetCurrentLocation',
              locationDropdownId: 'LocationID' // Auto select region
          }
        */
        const el = document.getElementById(opts.containerId);
        if (!el || typeof L === 'undefined') return null;

        const latInput = document.getElementById(opts.latInputId);
        const lngInput = document.getElementById(opts.lngInputId);
        const addressInput = document.getElementById(opts.addressInputId);
        
        // Use existing lat/lng or default
        const startLat = (latInput && latInput.value) ? parseFloat(latInput.value) : this.defaultLat;
        const startLng = (lngInput && lngInput.value) ? parseFloat(lngInput.value) : this.defaultLng;

        const baseMap = this.createMap(opts.containerId, startLat, startLng, 14);
        if (!baseMap) return null;
        const { map, tileLayer } = baseMap;

        const marker = L.marker([startLat, startLng], { draggable: true }).addTo(map);

        const updateLocation = (lat, lng, doGeocode = true) => {
            marker.setLatLng([lat, lng]);
            map.panTo([lat, lng]);
            if (latInput) latInput.value = lat;
            if (lngInput) lngInput.value = lng;

            if (doGeocode && addressInput) {
                // Reverse Geocoding via Nominatim
                this.reverseGeocode(lat, lng)
                    .then(data => {
                        if (data && data.display_name) {
                            addressInput.value = data.display_name;
                            
                            // Auto select region
                            if (opts.locationDropdownId) {
                                const locationSelect = document.getElementById(opts.locationDropdownId);
                                if (locationSelect) {
                                    const addrStr = JSON.stringify(data.address || {}).toLowerCase();
                                    const options = Array.from(locationSelect.options);
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
                                    if (foundVal) {
                                        const targetOpt = options.find(o => o.text.trim() === foundVal);
                                        if (targetOpt) locationSelect.value = targetOpt.value;
                                    }
                                }
                            }
                        }
                    })
                    .catch(err => console.error("Geocoding failed:", err));
            }
        };

        // Forward Geocoding: If user types address manually, move the pin!
        if (addressInput) {
            addressInput.addEventListener('change', function() {
                const query = this.value.trim();
                if (query.length > 3) {
                    // Try to search Vietnam specifically, makes results more accurate
                    SmartMap.forwardGeocode(query)
                        .then(data => {
                            if (data && data.length > 0) {
                                const lat = parseFloat(data[0].lat);
                                const lng = parseFloat(data[0].lon);
                                // Call updateLocation, but pass doGeocode=true to automatically grab the full accurate address formatted and the Area combo box.
                                updateLocation(lat, lng, true);
                            }
                        })
                        .catch(err => console.error("Forward geocoding failed:", err));
                }
            });
        }

        // If no initial lat/lng, do a default update
        if (!latInput || !latInput.value) {
            updateLocation(startLat, startLng, false); // don't force address fetch on initial empty load
        }

        marker.on('dragend', function (event) {
            const position = marker.getLatLng();
            updateLocation(position.lat, position.lng, true);
        });

        map.on('click', function (e) {
            updateLocation(e.latlng.lat, e.latlng.lng, true);
        });

        // Current Location Button
        const btnLoc = document.getElementById(opts.btnLocationId);
        if (btnLoc) {
            btnLoc.addEventListener('click', function() {
                if ("geolocation" in navigator) {
                    const originalHtml = btnLoc.innerHTML;
                    btnLoc.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>';
                    navigator.geolocation.getCurrentPosition(
                        function(position) {
                            updateLocation(position.coords.latitude, position.coords.longitude, true);
                            btnLoc.innerHTML = originalHtml;
                        },
                        function(error) {
                            alert("Không thể lấy vị trí hiện tại. Vui lòng kiểm tra quyền truy cập vị trí trên trình duyệt.");
                            btnLoc.innerHTML = originalHtml;
                        },
                        { enableHighAccuracy: true, timeout: 5000, maximumAge: 0 }
                    );
                } else {
                    alert("Trình duyệt của bạn không hỗ trợ lấy vị trí.");
                }
            });
        }
        
        // Modal fixing
        setTimeout(() => map.invalidateSize(), 500);
        return { map, marker, tileLayer, updateLocation };
    },

    // 3. Cluster Map (For Search Page with multiple cars)
    initClusterMap: function(containerId, carsData, onMarkerClick) {
        const baseMap = this.createMap(containerId, this.defaultLat, this.defaultLng, 13, { zoomControl: false });
        if (!baseMap) return null;
        const { map, tileLayer } = baseMap;
        L.control.zoom({ position: 'bottomright' }).addTo(map);

        let activeMarkers = [];
        let markerMap = {};

        const renderMarkers = (cars) => {
            activeMarkers.forEach(m => map.removeLayer(m));
            activeMarkers = [];
            Object.keys(markerMap).forEach(key => delete markerMap[key]);

            cars.forEach(car => {
                const price = Number(car.PricePerDay || car.pricePerDay || 0);
                const lat = Number(car.Lat || car.lat || 0);
                const lng = Number(car.Lng || car.lng || 0);
                const id = car.CarID || car.carID || car.id;

                const div = document.createElement('div');
                div.innerHTML = `<div class="car-pin-price">${(price / 1000).toFixed(0)}K</div>`;
                const icon = L.divIcon({ html: div.innerHTML, className: '', iconSize: null, iconAnchor: [20, 15] });
                
                const marker = L.marker([lat, lng], { icon }).addTo(map);
                
                if (onMarkerClick) {
                    marker.on('click', () => onMarkerClick(id));
                }
                
                activeMarkers.push(marker);
                markerMap[id] = marker;
            });

            if (activeMarkers.length > 0) {
                const group = new L.featureGroup(activeMarkers);
                map.fitBounds(group.getBounds().pad(0.1));
            }
        };

        // Geolocation circle
        if (navigator.geolocation) {
            navigator.geolocation.getCurrentPosition(pos => {
                const { latitude, longitude } = pos.coords;
                L.circle([latitude, longitude], { radius: 500, color: '#3b82f6', fillOpacity: 0.1, weight: 2 }).addTo(map);
                L.marker([latitude, longitude], {
                    icon: L.divIcon({
                        html: '<div style="background:#3b82f6;width:14px;height:14px;border-radius:50%;border:2px solid white;box-shadow:0 0 10px rgba(59,130,246,0.5)"></div>',
                        className: '', iconSize: [14, 14], iconAnchor: [7, 7]
                    })
                }).addTo(map).bindPopup('Vị trí hiện tại của bạn');
            }, () => {});
        }

        renderMarkers(carsData);
        return { map, tileLayer, renderMarkers, markerMap };
    }
};

window.SmartMap = SmartMap;
