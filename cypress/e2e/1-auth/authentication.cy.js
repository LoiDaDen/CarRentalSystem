describe('Authentication Flow', () => {

  beforeEach(() => {
    // Clear session and cookies before each test
    cy.clearCookies();
    cy.clearLocalStorage();
    cy.visit('/Auth/Login');
  });

  it('1. Hiển thị báo lỗi khi đăng nhập sai thông tin', () => {
    cy.get('input[name="email"]').type('wrongemail@test.com');
    cy.get('input[name="password"]').type('wrongpassword123');
    cy.get('button[type="submit"]').contains('Đăng nhập').click();

    // Nên xuất hiện thông báo lỗi từ phía server / hiển thị SweetAlert
    cy.get('body').should('contain.text', 'Email hoặc mật khẩu không đúng')
      .or('contain.text', 'Invalid login attempt'); // Thay đổi string tuỳ thuộc vào localization UI
  });

  it('2. Đăng nhập thành công và ghi nhận phiên (Session)', () => {
    cy.get('input[name="email"]').type('hloi78274@gmail.com');
    cy.get('input[name="password"]').type('Admin@123');
    cy.get('button[type="submit"]').contains('Đăng nhập').click();

    // Đảm bảo URL thay đổi (trỏ về HomePage hoặc Profile tuỳ theo thiết lập ReturnUrl)
    cy.url().should('not.include', '/Auth/Login');
    cy.getCookie('.AspNetCore.Cookies').should('exist');
  });

  it('3. Kiểm tra tính năng Đăng xuất', () => {
    // Đăng nhập trước
    cy.get('input[name="email"]').type('hloi78274@gmail.com');
    cy.get('input[name="password"]').type('Admin@123');
    cy.get('button[type="submit"]').contains('Đăng nhập').click();

    // Yêu cầu logout qua Action
    cy.visit('/Auth/Logout');

    // Mất Cookie
    cy.getCookie('.AspNetCore.Cookies').should('be.null');
  });

  it.skip('4. Khách hàng thông thường bị chặn khi cố truy cập trang Admin', () => {
    // Đăng nhập tài khoản khách hàng thông thường (dùng 1 tài khoản chưa gán quyền Admin)
    cy.get('input[name="email"]').type('khachhangthuong@test.com');
    cy.get('input[name="password"]').type('123456');
    cy.get('button[type="submit"]').contains('Đăng nhập').click();

    // Giả lập hacker nhắm thẳng link Admin
    cy.request({
      url: '/Admin/Dashboard',
      failOnStatusCode: false
    }).then((response) => {
      // Vì là role Customer, nên server sẽ trả về 403 Forbidden hoặc redirect 302 về trang truy cập bị từ chối
      expect(response.status).to.be.oneOf([403, 302, 404]);
    });
  });

});
