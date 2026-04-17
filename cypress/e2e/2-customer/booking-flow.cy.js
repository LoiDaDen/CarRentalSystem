describe('Booking Flow (Customer)', () => {

  const DELAY = 500;

  beforeEach(() => {
    // Setup độ phân giải PC
    cy.viewport(1280, 720);
    // Để bài test tự nhiên nhất: Chúng ta KHÔNG đăng nhập trước.
    // Khách hàng sẽ đi xem xe vãng lai, lúc nào bắt thanh toán mới bị ép đăng nhập!
    cy.wait(DELAY);
  });

  it('1. Trải nghiệm từ trang chủ, tìm kiếm xe và xem chi tiết', () => {
    cy.visit('/');
    cy.wait(DELAY);

    // Bấm nút Điều hướng Xe tự lái hoặc tìm nút Danh sách
    cy.contains('Danh sách xe').click();
    cy.url().should('include', '/Car');
    cy.wait(DELAY);

    // Thử tính năng Lọc tĩnh ở trang Car Index
    // Select box brandId
    cy.get('select[name="brandId"]').select('1').should('have.value', '1');
    cy.get('button[type="submit"]').contains('Tìm kiếm').click();
    cy.wait(DELAY);

    // Chuyển sang phần tử xe đầu tiên và xem chi tiết
    cy.get('.car-card').first().find('a').contains('Xem chi tiết').click();
    cy.url().should('include', '/Car/Details');
    cy.wait(DELAY);
  });

  it('2. Chọn gói thuê, điều chỉnh ngày giờ phẳng và thêm Addons', () => {
    // Nhảy trực tiếp vào trang chi tiết xe số 1 để test chức năng Booking Sidebar
    const carId = 1;
    cy.visit(`/Car/Details/${carId}`);
    cy.wait(DELAY * 2);

    // 2.1 Mua gói 24 giờ
    cy.get('button[data-hours="24"]').click();
    // Chờ hệ thống tự động cập nhật Form ẩn thông qua AJAX hoặc JS
    cy.wait(DELAY);

    // 2.2 Sửa thời gian thuê trên input Flatpickr
    // Do Flatpickr sử dụng input ẩn, đôi khi ta phải force update hoặc update value trực tiếp
    cy.get('input#startDate').then($input => {
      // Setup ngày bắt đầu là tương lai +2 ngày
      let futureDate = new Date();
      futureDate.setDate(futureDate.getDate() + 2);
      futureDate.setHours(10, 0, 0, 0);
      const tzoffset = (new Date()).getTimezoneOffset() * 60000; // offset in milliseconds
      const localISOTime = (new Date(futureDate - tzoffset)).toISOString().slice(0, -1).substring(0, 16);
      cy.wrap($input).type(localISOTime, { force: true });
    });

    cy.get('input#endDate').then($input => {
      // Thêm 3 ngày
      let futureDate = new Date();
      futureDate.setDate(futureDate.getDate() + 5);
      futureDate.setHours(10, 0, 0, 0);
      const tzoffset = (new Date()).getTimezoneOffset() * 60000;
      const localISOTime = (new Date(futureDate - tzoffset)).toISOString().slice(0, -1).substring(0, 16);
      cy.wrap($input).type(localISOTime, { force: true });
    });

    cy.wait(DELAY);

    // 2.3 Giao nhận xe
    cy.get('input#locOwner').check({force: true});
    cy.wait(DELAY);
    
    // 2.4 Tick chọn các Addon như Nước Suối / Combo
    // Đảm bảo checkbox có type="checkbox" và lấy cái đầu tiên
    cy.get('input[name="SelectedAddons"]').first().check({force: true});
    cy.wait(DELAY);

    // 2.5 Kiểm tra xem giá đã update lên UI hay chưa
    cy.get('#finalTotalAmount').invoke('text').should('match', /\d+/);
  });

  it('3. Thêm vào giỏ và tiến hành Checkout', () => {
    cy.visit('/Car/Details/1');
    cy.wait(DELAY);
    
    // Thêm vào giỏ
    cy.get('#addToCartBtn').click();
    cy.wait(DELAY * 2);

    // Chuyển sang Giỏ hàng
    cy.visit('/Cart');
    cy.wait(DELAY);
    cy.contains('1 chiếc').should('exist');

    // Chấp nhận và checkout
    cy.get('button').contains('Thanh toán ngay').click();
    cy.wait(DELAY);

    // Hệ thống sẽ chặn luồng chặn Khách vãng lai và yêu cầu Đăng Nhập
    cy.url().should('include', '/Auth/Login');
    cy.get('input[name="email"]').type('hloi78274@gmail.com');
    cy.get('input[name="password"]').type('Admin@123');
    cy.get('button[type="submit"]').contains('Đăng nhập').click();
    cy.wait(DELAY * 2);

    // Sau khi đăng nhập thành công, hệ thống phải tự đưa người dùng về lại luồng Thanh toán hoặc ấn Checkout lại
    // Nếu trang tự redirect lại Cart, ta tự bấm lại nút thanh toán
    cy.url().then((url) => {
        if (url.includes('Cart')) {
            cy.get('button').contains('Thanh toán ngay').click();
            cy.wait(DELAY);
        }
    });

    cy.url().should('include', '/Booking/Checkout');
    cy.wait(DELAY);

    // Chọn phương thức Payment PayOS và submit
    cy.get('input[name="PaymentMethod"]').first().check({force: true});
    cy.get('button').contains('Xác nhận & Thanh toán').click();
    cy.wait(DELAY * 2);
    
    // Tại đây, ứng dụng thường sẽ redirect qua cổng thanh toán PayOS bên thứ 3. 
    // Việc cross-origin test cần có flag ở cy config, ta chỉ cần expect URL chứa payos hoặc deposit
    cy.url().should('include', 'payos').or('include', 'Deposit');
  });

});
