namespace CarRentalSystem.Models
{
    public class BlogTagMapping
    {
        public int BlogID { get; set; }
        public Blog? Blog { get; set; }

        public int BlogTagID { get; set; }
        public BlogTag? BlogTag { get; set; }
    }
}
