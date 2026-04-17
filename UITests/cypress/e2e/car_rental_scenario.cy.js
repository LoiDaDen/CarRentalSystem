describe('Car Rental UI Automation', () => {
  it('Simulates a user browsing and booking a car', () => {
    // Độ trễ nhẹ giữa các thao tác để người quan sát dễ nhìn
    const DELAY = 1500;
    
    cy.log('1. Truy cập trang chủ');
    cy.visit('/');
    cy.wait(DELAY);

    cy.log('2. Click vào điều hướng Đội xe');
    cy.contains('Đội xe').click();
    cy.url().should('include', '/Car');
    cy.wait(DELAY);

    cy.log('3. Lọc xe theo Hãng');
    cy.contains('Toyota').click();
    cy.wait(DELAY);

    cy.log('4. Click xem chi tiết chiếc xe đầu tiên');
    // Bấm thẳng vào thẻ card xe không cần tìm chữ Xem chi tiết
    cy.get('a[href^="/Car/Details"]').first().click({force: true});
    cy.url().should('include', '/Car/Details');
    cy.wait(DELAY);

    cy.log('5. Nhập thời gian thuê và đổi khu vực');
    cy.get('#startDate').clear({force: true}).type('2026-05-01T10:00', {force: true});
    cy.get('#endDate').clear({force: true}).type('2026-05-05T10:00', {force: true});
    cy.wait(DELAY);
    
    cy.log('6. Click nút Thuê xe');
    cy.get('#bookingForm').submit();
    cy.wait(DELAY);

    cy.log('7. Bị yêu cầu đăng nhập (Redirect)');
    // Hệ thống bảo mật yêu cầu đăng nhập trước khi thanh toán
    cy.url().should('include', '/Auth/Login');
    cy.wait(DELAY);
    
    cy.log('8. Điền đơn đăng nhập');
    cy.get('input[name="email"]').last().clear({force: true}).type('demo@test.com', {force: true});
    cy.get('input[name="password"]').clear({force: true}).type('123456', {force: true});
    cy.contains('Đăng Nhập').click();
    cy.wait(DELAY);

    cy.log('=== KẾT THÚC KỊCH BẢN ===');
  });
});
