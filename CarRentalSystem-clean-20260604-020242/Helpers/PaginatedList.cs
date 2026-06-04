using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Helpers
{
    /// <summary>
    /// Generic paginated list helper — chuẩn enterprise.
    /// Hỗ trợ phân trang cho bất kỳ IQueryable nào.
    /// </summary>
    public class PaginatedList<T> : List<T>
    {
        public int PageIndex { get; private set; }
        public int TotalPages { get; private set; }
        public int TotalCount { get; private set; }
        public int PageSize { get; private set; }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        /// <summary>Trang đầu tiên</summary>
        public int FirstPage => 1;

        /// <summary>Trang cuối cùng</summary>
        public int LastPage => TotalPages;

        public PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            TotalCount = count;
            PageSize = pageSize;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);

            AddRange(items);
        }

        /// <summary>
        /// Tạo PaginatedList từ IQueryable — chỉ query đúng 1 page từ DB.
        /// </summary>
        public static async Task<PaginatedList<T>> CreateAsync(
            IQueryable<T> source, int pageIndex, int pageSize)
        {
            var count = await source.CountAsync();
            var items = await source
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return new PaginatedList<T>(items, count, pageIndex, pageSize);
        }

        /// <summary>
        /// Tạo PaginatedList từ List đã có sẵn (in-memory pagination).
        /// </summary>
        public static PaginatedList<T> Create(List<T> source, int pageIndex, int pageSize)
        {
            var count = source.Count;
            var items = source
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return new PaginatedList<T>(items, count, pageIndex, pageSize);
        }

        /// <summary>
        /// Tạo danh sách số trang để hiển thị (ví dụ: 1 2 3 ... 10).
        /// </summary>
        public IEnumerable<int> GetPageRange(int maxVisible = 5)
        {
            int start = Math.Max(1, PageIndex - maxVisible / 2);
            int end = Math.Min(TotalPages, start + maxVisible - 1);
            start = Math.Max(1, end - maxVisible + 1);

            for (int i = start; i <= end; i++)
                yield return i;
        }
    }
}
