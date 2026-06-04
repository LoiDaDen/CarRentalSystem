namespace CarRentalSystem.Models
{
    public static class CarContractSigningModes
    {
        public const string Digital = "Digital";
        public const string PhysicalSelfManaged = "PhysicalSelfManaged";

        public static bool IsValid(string? mode)
        {
            return mode is Digital or PhysicalSelfManaged;
        }

        public static string Normalize(string? mode)
        {
            return mode == Digital ? Digital : PhysicalSelfManaged;
        }

        public static string DisplayName(string? mode)
        {
            return Normalize(mode) == Digital ? "Hợp đồng số" : "Hợp đồng giấy tự quản";
        }
    }
}
