from camera_bridge.plate_normalizer import looks_like_vietnam_plate, normalize_plate


def test_normalize_vietnam_plate_variants_match():
    assert normalize_plate("51A-123.45") == "51A12345"
    assert normalize_plate(" 51a 12345 ") == "51A12345"
    assert normalize_plate("51A.123-45") == "51A12345"


def test_vietnam_plate_shape():
    assert looks_like_vietnam_plate("51A12345")
    assert looks_like_vietnam_plate("30G99999")
    assert not looks_like_vietnam_plate("ABC123")
    assert not looks_like_vietnam_plate("1234")

