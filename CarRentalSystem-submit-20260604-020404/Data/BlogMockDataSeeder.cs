using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CarRentalSystem.Models;

namespace CarRentalSystem.Data
{
    public static class BlogMockDataSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            var categories = EnsureCategories(context);
            var tags = EnsureTags(context);

            if (context.Blogs.Any())
                return;

            var now = DateTime.UtcNow;
            var posts = new List<Blog>
            {
                CreatePost(
                    "Kinh nghiệm thuê xe tự lái lần đầu cho người mới",
                    "Những lưu ý quan trọng giúp người mới thuê xe tự lái tự tin hơn từ lúc chọn xe, đặt cọc đến khi nhận và trả xe.",
                    categories["kinh-nghiem-thue-xe"].BlogCategoryID,
                    "https://images.unsplash.com/photo-1503376780353-7e6692767b70?q=80&w=1200&auto=format&fit=crop",
                    now.AddDays(-2),
                    true,
                    new[] { "thuê xe tự lái", "kinh nghiệm thuê xe", "đặt xe online" },
                    Article("Người mới thuê xe tự lái thường lo nhất là giấy tờ, tiền cọc và cách kiểm tra xe. Thực tế, nếu chuẩn bị đúng vài bước cơ bản, quá trình nhận xe diễn ra khá nhẹ nhàng.",
                        "Chọn xe đúng nhu cầu", "Nếu chỉ đi trong thành phố hoặc công tác ngắn ngày, xe 4 chỗ tiết kiệm nhiên liệu là lựa chọn hợp lý. Với gia đình có trẻ nhỏ hoặc nhiều hành lý, xe 7 chỗ sẽ thoải mái hơn.",
                        "Chuẩn bị trước khi nhận xe", "Bạn nên kiểm tra lịch trình, thời gian nhận trả, điểm giao xe và giấy tờ cá nhân. Khi nhận xe, hãy chụp ảnh ngoại thất, nội thất, đồng hồ nhiên liệu và số km để hai bên có căn cứ rõ ràng.",
                        "Kinh nghiệm nhỏ khi trả xe", "Trả xe đúng giờ, giữ biên nhận thanh toán và thông báo sớm nếu lịch trình thay đổi. Điều này giúp tránh phí phát sinh và tạo lịch sử thuê xe tốt cho những lần sau.")),

                CreatePost(
                    "Nên thuê xe 4 chỗ hay 7 chỗ khi đi du lịch gia đình?",
                    "Gợi ý cách chọn xe 4 chỗ hoặc xe 7 chỗ theo số người, hành lý, cung đường và ngân sách chuyến đi.",
                    categories["so-sanh-xe"].BlogCategoryID,
                    "https://images.unsplash.com/photo-1519641471654-76ce0107ad1b?q=80&w=1200&auto=format&fit=crop",
                    now.AddDays(-4),
                    true,
                    new[] { "xe 4 chỗ", "xe 7 chỗ", "du lịch gia đình" },
                    Article("Một chuyến đi gia đình thoải mái bắt đầu từ việc chọn đúng chiếc xe. Không phải lúc nào xe lớn cũng tốt hơn, nhưng thiếu không gian sẽ khiến cả hành trình mệt mỏi.",
                        "Khi nào nên chọn xe 4 chỗ", "Xe 4 chỗ phù hợp nhóm 2-4 người, hành lý gọn và cung đường chủ yếu là cao tốc hoặc nội thành. Ưu điểm là dễ lái, dễ đỗ và tiết kiệm chi phí thuê xe.",
                        "Khi nào nên chọn xe 7 chỗ", "Xe 7 chỗ phù hợp gia đình đông người, có trẻ nhỏ, người lớn tuổi hoặc nhiều hành lý. Nếu đi Đà Lạt, Nha Trang hay các cung đường dài, không gian rộng giúp mọi người đỡ mệt hơn.",
                        "Cách cân đối ngân sách", "Đừng chỉ nhìn giá thuê theo ngày. Hãy tính thêm nhiên liệu, cầu đường, bãi đỗ và mức tiêu hao của từng dòng xe để chọn phương án thật sự tiết kiệm.")),

                CreatePost(
                    "Những giấy tờ cần chuẩn bị khi thuê xe tự lái",
                    "Danh sách giấy tờ và thông tin cần có để quá trình xác minh, đặt xe và nhận xe diễn ra nhanh chóng.",
                    categories["huong-dan-dat-xe"].BlogCategoryID,
                    "https://images.unsplash.com/photo-1450101499163-c8848c66ca85?q=80&w=1200&auto=format&fit=crop",
                    now.AddDays(-6),
                    false,
                    new[] { "thuê xe tự lái", "đặt xe online", "kinh nghiệm thuê xe" },
                    Article("Chuẩn bị giấy tờ đầy đủ giúp bạn tiết kiệm thời gian và giảm rủi ro khi nhận xe. Mỗi đơn vị có thể có quy định riêng, nhưng các nhóm thông tin dưới đây gần như luôn cần thiết.",
                        "Giấy tờ cá nhân", "Bạn cần căn cước công dân hoặc hộ chiếu còn hiệu lực, giấy phép lái xe phù hợp hạng xe và số điện thoại liên hệ chính chủ.",
                        "Thông tin đặt xe", "Hãy chuẩn bị sẵn thời gian nhận trả, địa điểm giao xe, nhu cầu thuê theo giờ, theo ngày hoặc dài hạn. Đặt xe online càng rõ thông tin thì báo giá càng chính xác.",
                        "Tiền cọc và xác nhận thanh toán", "Một số đơn cần phí giữ chỗ qua PayOS để khóa xe. Khoản này thường được ghi nhận tự động và trừ vào nghĩa vụ thanh toán khi nhận xe.")),

                CreatePost(
                    "Cách kiểm tra xe trước khi nhận bàn giao",
                    "Checklist kiểm tra ngoại thất, nội thất, giấy tờ xe, nhiên liệu và hình ảnh bàn giao để bảo vệ quyền lợi người thuê.",
                    categories["kinh-nghiem-thue-xe"].BlogCategoryID,
                    "https://images.unsplash.com/photo-1486262715619-67b85e0b08d3?q=80&w=1200&auto=format&fit=crop",
                    now.AddDays(-8),
                    false,
                    new[] { "kinh nghiệm thuê xe", "lái xe an toàn", "bảo hiểm xe" },
                    Article("Kiểm tra xe không phải để làm khó bên giao xe, mà để hai bên minh bạch ngay từ đầu. Chỉ cần dành 10-15 phút, bạn có thể tránh phần lớn tranh cãi khi trả xe.",
                        "Kiểm tra ngoại thất", "Đi một vòng quanh xe, ghi nhận vết trầy, móp, nứt kính, tình trạng lốp và mâm. Nên chụp ảnh hoặc quay video ở nơi đủ sáng.",
                        "Kiểm tra nội thất và tiện nghi", "Thử điều hòa, đèn, gạt mưa, màn hình, camera lùi, khóa cửa và dây an toàn. Nếu đi xa, hãy kiểm tra lốp dự phòng và bộ dụng cụ cơ bản.",
                        "Xác nhận biên bản bàn giao", "Biên bản nên thể hiện số km, mức nhiên liệu, phụ kiện đi kèm và tình trạng xe. Khi mọi thứ rõ ràng, chuyến đi sẽ nhẹ đầu hơn.")),

                CreatePost(
                    "Thuê xe tự lái theo giờ, theo ngày hay theo tháng: Nên chọn gói nào?",
                    "So sánh các gói thuê xe theo giờ, ngày, tuần và tháng để chọn đúng nhu cầu công tác, du lịch hoặc sử dụng dài hạn.",
                    categories["huong-dan-dat-xe"].BlogCategoryID,
                    "https://images.unsplash.com/photo-1449965408869-eaa3f722e40d?q=80&w=1200&auto=format&fit=crop",
                    now.AddDays(-10),
                    true,
                    new[] { "thuê xe tự lái", "tiết kiệm chi phí", "thuê xe cuối tuần" },
                    Article("Không phải chuyến đi nào cũng nên thuê trọn ngày. Chọn đúng gói thuê giúp bạn tối ưu chi phí mà vẫn đảm bảo xe luôn sẵn sàng cho lịch trình.",
                        "Thuê theo giờ", "Phù hợp khi cần xe đi họp, đưa đón sân bay, xử lý công việc ngắn trong ngày. Hãy kiểm tra kỹ giới hạn giờ và phí vượt giờ.",
                        "Thuê theo ngày hoặc cuối tuần", "Đây là lựa chọn phổ biến cho du lịch gia đình, về quê, đi biển hoặc đi Đà Lạt. Gói theo ngày thường dễ tính chi phí và linh hoạt hơn.",
                        "Thuê theo tháng", "Phù hợp nhân viên công tác dài ngày, doanh nghiệp cần xe tạm thời hoặc gia đình chưa muốn mua xe. Gói dài hạn thường có ưu đãi tốt hơn giá thuê lẻ.")),

                CreatePost(
                    "Top các dòng xe tiết kiệm nhiên liệu phù hợp đi xa",
                    "Gợi ý nhóm xe sedan, hatchback, MPV và SUV tiết kiệm nhiên liệu cho hành trình dài tại Việt Nam.",
                    categories["so-sanh-xe"].BlogCategoryID,
                    "https://images.unsplash.com/photo-1549317661-bd32c8ce0db2?q=80&w=1200&auto=format&fit=crop",
                    now.AddDays(-12),
                    false,
                    new[] { "xe 4 chỗ", "xe 7 chỗ", "tiết kiệm chi phí" },
                    Article("Khi đi xa, tiền nhiên liệu có thể chiếm phần đáng kể trong tổng chi phí. Một chiếc xe tiết kiệm, dễ lái và có khoang hành lý phù hợp sẽ giúp chuyến đi thoải mái hơn.",
                        "Sedan và hatchback", "Các dòng Vios, City, Accent, Grand i10 thường tiết kiệm, dễ điều khiển và phù hợp nhóm nhỏ. Đây là lựa chọn tốt cho cung đường cao tốc hoặc di chuyển nội tỉnh.",
                        "MPV 7 chỗ", "Xpander, Innova hoặc các mẫu MPV tương tự cân bằng giữa không gian và chi phí. Gia đình 5-7 người có thể đi xa thoải mái mà không cần chọn SUV quá lớn.",
                        "Lưu ý khi tính chi phí", "Hãy hỏi loại nhiên liệu, mức tiêu hao tham khảo và chính sách trả xe theo nhiên liệu. Cách lái ổn định cũng giúp tiết kiệm đáng kể.")),

                CreatePost(
                    "Những lỗi thường gặp khi thuê xe tự lái và cách tránh",
                    "Tổng hợp các lỗi phổ biến như đặt sát giờ, không đọc điều khoản, quên kiểm tra xe và cách xử lý trước khi phát sinh chi phí.",
                    categories["kinh-nghiem-thue-xe"].BlogCategoryID,
                    "https://images.unsplash.com/photo-1494976388531-d1058494cdd8?q=80&w=1200&auto=format&fit=crop",
                    now.AddDays(-14),
                    false,
                    new[] { "kinh nghiệm thuê xe", "thuê xe tự lái", "tiết kiệm chi phí" },
                    Article("Nhiều chi phí phát sinh không đến từ lỗi lớn, mà từ các chi tiết nhỏ bị bỏ qua. Biết trước những lỗi thường gặp sẽ giúp bạn chủ động hơn.",
                        "Đặt xe quá sát giờ", "Cuối tuần, lễ tết hoặc mùa du lịch, xe đẹp thường hết nhanh. Đặt sớm giúp bạn có nhiều lựa chọn hơn và tránh phải nâng hạng ngoài dự kiến.",
                        "Không đọc kỹ điều khoản", "Hãy xem kỹ giới hạn km, phí vượt giờ, phí vệ sinh, chính sách nhiên liệu và bảo hiểm. Những mục này ảnh hưởng trực tiếp tới tổng chi phí.",
                        "Bỏ qua bước chụp ảnh xe", "Ảnh bàn giao là bằng chứng quan trọng. Nên chụp đầy đủ bốn góc xe, nội thất, đồng hồ km và nhiên liệu trước khi xuất phát.")),

                CreatePost(
                    "Có nên mua thêm bảo hiểm khi thuê xe tự lái không?",
                    "Giải thích khi nào nên chọn gói bảo hiểm bổ sung và những điểm cần hỏi rõ trước khi nhận xe.",
                    categories["bao-hiem-quy-dinh"].BlogCategoryID,
                    "https://images.unsplash.com/photo-1554224155-6726b3ff858f?q=80&w=1200&auto=format&fit=crop",
                    now.AddDays(-16),
                    false,
                    new[] { "bảo hiểm xe", "thuê xe tự lái", "lái xe an toàn" },
                    Article("Bảo hiểm bổ sung không phải lúc nào cũng bắt buộc, nhưng có thể đáng tiền nếu bạn đi xa, đi đường lạ hoặc thuê xe có giá trị cao.",
                        "Hiểu phạm vi bảo hiểm", "Hãy hỏi rõ trường hợp nào được hỗ trợ, mức miễn thường, hồ sơ cần cung cấp và thời gian xử lý. Đừng chỉ nghe tên gói mà bỏ qua điều kiện chi tiết.",
                        "Khi nào nên mua thêm", "Người mới lái, gia đình đi đường dài, lịch trình qua đèo dốc hoặc khu vực đông xe nên cân nhắc gói bảo vệ cao hơn để giảm áp lực.",
                        "Vẫn cần lái xe cẩn thận", "Bảo hiểm không thay thế trách nhiệm lái xe an toàn. Tuân thủ tốc độ, nghỉ ngơi đủ và kiểm tra xe trước khi đi vẫn là ưu tiên số một.")),

                CreatePost(
                    "Kinh nghiệm thuê xe đi Đà Lạt, Vũng Tàu, Nha Trang",
                    "Các lưu ý chọn xe, lịch trình, nhiên liệu và thời gian xuất phát khi đi những điểm du lịch tự lái phổ biến.",
                    categories["du-lich-tu-lai"].BlogCategoryID,
                    "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?q=80&w=1200&auto=format&fit=crop",
                    now.AddDays(-18),
                    true,
                    new[] { "du lịch gia đình", "thuê xe cuối tuần", "lái xe an toàn" },
                    Article("Đà Lạt, Vũng Tàu và Nha Trang là các cung đường quen thuộc với khách thuê xe tự lái. Mỗi điểm đến lại có một kiểu chuẩn bị riêng.",
                        "Đi Đà Lạt", "Nên ưu tiên xe máy khỏe, phanh tốt và lốp còn sâu gai vì có nhiều đèo dốc. Người mới nên tránh chạy đêm nếu chưa quen cung đường.",
                        "Đi Vũng Tàu", "Cung đường ngắn hơn, phù hợp xe 4 chỗ hoặc 7 chỗ tùy số người. Hãy xuất phát sớm để tránh kẹt xe cuối tuần ở cửa ngõ thành phố.",
                        "Đi Nha Trang", "Quãng đường dài hơn nên cần kiểm tra lốp, dầu, nước làm mát và nghỉ giữa chặng. Nếu đi gia đình, xe rộng rãi sẽ giúp hành trình dễ chịu hơn.")),

                CreatePost(
                    "Cách đặt xe online nhanh chóng và an toàn",
                    "Hướng dẫn đặt xe online, kiểm tra thông tin xe, thanh toán giữ chỗ PayOS và theo dõi đơn thuê sau khi đặt.",
                    categories["huong-dan-dat-xe"].BlogCategoryID,
                    "https://images.unsplash.com/photo-1556742049-0cfed4f6a45d?q=80&w=1200&auto=format&fit=crop",
                    now.AddDays(-20),
                    false,
                    new[] { "đặt xe online", "thuê xe tự lái", "kinh nghiệm thuê xe" },
                    Article("Đặt xe online giúp bạn chủ động so sánh xe, giá và lịch thuê mà không cần gọi nhiều nơi. Điều quan trọng là kiểm tra thông tin trước khi thanh toán.",
                        "Tìm xe theo nhu cầu", "Chọn khu vực, thời gian nhận trả, số chỗ, loại hộp số và ngân sách. Bộ lọc càng rõ, kết quả càng sát lịch trình thật của bạn.",
                        "Kiểm tra chi tiết xe", "Xem ảnh, giá theo ngày, tiện nghi, địa điểm nhận xe và đánh giá nếu có. Với chuyến đi xa, hãy ưu tiên xe có lịch bảo dưỡng rõ ràng.",
                        "Thanh toán giữ chỗ", "PayOS giúp ghi nhận phí giữ chỗ tự động. Sau khi thanh toán thành công, bạn nên kiểm tra lại mục Chuyến xe của tôi để theo dõi trạng thái đơn.")),

                CreatePost(
                    "Các khoản phí có thể phát sinh khi thuê xe tự lái",
                    "Những loại phí người thuê cần biết trước: vượt giờ, vượt km, vệ sinh, nhiên liệu, cầu đường và xử lý vi phạm.",
                    categories["bao-hiem-quy-dinh"].BlogCategoryID,
                    "https://images.unsplash.com/photo-1554224154-26032ffc0d07?q=80&w=1200&auto=format&fit=crop",
                    now.AddDays(-22),
                    false,
                    new[] { "tiết kiệm chi phí", "thuê xe tự lái", "bảo hiểm xe" },
                    Article("Giá thuê niêm yết chưa phải toàn bộ chi phí của chuyến đi. Một số khoản chỉ phát sinh khi lịch trình thay đổi hoặc xe được trả không đúng điều kiện ban đầu.",
                        "Phí vượt giờ và vượt km", "Nếu trả xe trễ hoặc đi quá giới hạn km, hệ thống có thể tính phí theo quy định. Hãy thông báo sớm khi biết mình không kịp giờ trả.",
                        "Nhiên liệu và vệ sinh", "Thông thường xe cần được trả với mức nhiên liệu tương đương lúc nhận. Nếu xe quá bẩn sau chuyến đi biển hoặc cắm trại, có thể phát sinh phí vệ sinh.",
                        "Cầu đường và vi phạm", "ETC, bãi đỗ, phạt nguội hoặc lỗi giao thông trong thời gian thuê thường do người thuê chịu trách nhiệm. Lưu lại lịch trình giúp đối soát nhanh hơn.")),

                CreatePost(
                    "Mẹo lái xe đường dài an toàn cho người mới",
                    "Các mẹo chuẩn bị sức khỏe, giữ khoảng cách, nghỉ giữa chặng và xử lý cung đường lạ khi tự lái xe đường dài.",
                    categories["meo-lai-xe-an-toan"].BlogCategoryID,
                    "https://images.unsplash.com/photo-1533106418989-88406c7cc8ca?q=80&w=1200&auto=format&fit=crop",
                    now.AddDays(-24),
                    false,
                    new[] { "lái xe an toàn", "du lịch gia đình", "thuê xe cuối tuần" },
                    Article("Đường dài đòi hỏi sự tỉnh táo và nhịp lái ổn định hơn so với đi trong phố. Người mới nên chuẩn bị kỹ để chuyến đi an toàn và ít căng thẳng.",
                        "Chuẩn bị trước khi đi", "Ngủ đủ, kiểm tra tuyến đường, dự kiến điểm nghỉ và tránh xuất phát khi đang mệt. Trên xe nên có nước uống, sạc điện thoại và giấy tờ cần thiết.",
                        "Giữ nhịp lái ổn định", "Không bám đuôi, hạn chế chuyển làn đột ngột và quan sát gương thường xuyên. Khi trời mưa hoặc đường đèo, hãy giảm tốc sớm thay vì phanh gấp.",
                        "Nghỉ đúng lúc", "Sau mỗi 2-3 giờ lái, nên dừng nghỉ vài phút để giãn cơ và lấy lại tập trung. An toàn luôn quan trọng hơn việc đến nơi sớm vài chục phút."))
            };

            foreach (var post in posts)
            {
                foreach (var mapping in post.BlogTagMappings)
                {
                    var tagName = mapping.BlogTag?.Name ?? string.Empty;
                    mapping.BlogTag = null;
                    mapping.BlogTagID = tags[Slugify(tagName)].BlogTagID;
                }
            }

            context.Blogs.AddRange(posts);
            context.SaveChanges();
        }

        private static Dictionary<string, BlogCategory> EnsureCategories(ApplicationDbContext context)
        {
            var seeds = new[]
            {
                ("Kinh nghiệm thuê xe", "kinh-nghiem-thue-xe"),
                ("Hướng dẫn đặt xe", "huong-dan-dat-xe"),
                ("Du lịch tự lái", "du-lich-tu-lai"),
                ("So sánh xe", "so-sanh-xe"),
                ("Bảo hiểm & quy định", "bao-hiem-quy-dinh"),
                ("Mẹo lái xe an toàn", "meo-lai-xe-an-toan"),
                ("Tin tức xe", "tin-tuc-xe")
            };

            var existing = context.BlogCategories.ToDictionary(c => c.Slug, StringComparer.OrdinalIgnoreCase);
            var order = 1;
            foreach (var (name, slug) in seeds)
            {
                if (!existing.ContainsKey(slug))
                {
                    var category = new BlogCategory
                    {
                        Name = name,
                        Slug = slug,
                        DisplayOrder = order,
                        Description = $"Các bài viết về {name.ToLower()} dành cho khách thuê xe tự lái."
                    };
                    context.BlogCategories.Add(category);
                    existing[slug] = category;
                }

                order++;
            }

            context.SaveChanges();
            return context.BlogCategories.ToDictionary(c => c.Slug, StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, BlogTag> EnsureTags(ApplicationDbContext context)
        {
            var names = new[]
            {
                "thuê xe tự lái", "kinh nghiệm thuê xe", "xe 4 chỗ", "xe 7 chỗ",
                "du lịch gia đình", "đặt xe online", "bảo hiểm xe", "lái xe an toàn",
                "tiết kiệm chi phí", "thuê xe cuối tuần"
            };

            var existing = context.BlogTags.ToDictionary(t => t.Slug, StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                var slug = Slugify(name);
                if (!existing.ContainsKey(slug))
                {
                    var tag = new BlogTag { Name = name, Slug = slug };
                    context.BlogTags.Add(tag);
                    existing[slug] = tag;
                }
            }

            context.SaveChanges();
            return context.BlogTags.ToDictionary(t => t.Slug, StringComparer.OrdinalIgnoreCase);
        }

        private static Blog CreatePost(string title, string summary, int categoryId, string imageUrl, DateTime publishedDate, bool featured, IEnumerable<string> tagNames, string content)
        {
            var blog = new Blog
            {
                Title = title,
                Slug = Slugify(title),
                Summary = summary,
                Content = content,
                ThumbnailUrl = imageUrl,
                CoverImageUrl = imageUrl,
                ThumbnailAlt = title,
                CoverImageAlt = title,
                CategoryID = categoryId,
                AuthorName = "CarRental Team",
                PublishedDate = publishedDate,
                ReadingTime = Math.Max(4, Regex.Matches(Regex.Replace(content, "<.*?>", " "), @"\S+").Count / 180),
                ViewCount = featured ? 280 : 90,
                IsFeatured = featured,
                Status = BlogStatusConstants.Published,
                MetaTitle = $"{title} | CarRental Blog",
                MetaDescription = summary
            };

            foreach (var tagName in tagNames)
                blog.BlogTagMappings.Add(new BlogTagMapping { BlogTag = new BlogTag { Name = tagName } });

            return blog;
        }

        private static string Article(string intro, string h2a, string pa, string h2b, string pb, string h2c, string pc)
        {
            return $@"<p>{intro}</p>
<h2>{h2a}</h2>
<p>{pa}</p>
<h3>Lưu ý thực tế</h3>
<p>Trước khi chốt đơn, bạn nên xem kỹ thời gian nhận trả xe, chi phí giữ chỗ, chính sách nhiên liệu và các khoản có thể phát sinh. Những thông tin này giúp việc thuê xe tự lái minh bạch hơn.</p>
<h2>{h2b}</h2>
<p>{pb}</p>
<h3>Mẹo dành cho khách thuê</h3>
<ul>
<li>Đặt xe sớm nếu đi vào cuối tuần hoặc ngày lễ.</li>
<li>Giữ liên lạc với bên giao xe trong ngày nhận xe.</li>
<li>Lưu lại ảnh bàn giao và hóa đơn thanh toán.</li>
</ul>
<h2>{h2c}</h2>
<p>{pc}</p>
<p>Với một chút chuẩn bị, chuyến đi bằng xe thuê sẽ chủ động, riêng tư và tiết kiệm hơn rất nhiều so với việc phụ thuộc vào phương tiện công cộng.</p>";
        }

        private static string Slugify(string text)
        {
            var normalized = text.Trim().ToLowerInvariant()
                .Replace("đ", "d")
                .Replace("Đ", "d")
                .Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    builder.Append(c);
            }

            var slug = Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"[^a-z0-9]+", "-");
            return Regex.Replace(slug, "-{2,}", "-").Trim('-');
        }
    }
}
