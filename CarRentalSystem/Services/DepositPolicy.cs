namespace CarRentalSystem.Services
{
    public static class DepositPolicy
    {
        public const decimal Rate = 0.2m;

        public static decimal Calculate(decimal amount)
        {
            return Math.Round(Math.Max(0m, amount) * Rate, 0, MidpointRounding.AwayFromZero);
        }
    }
}
