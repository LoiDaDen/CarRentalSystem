using System.Reflection;
using CarRentalSystem.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using AdminAreaApiController = CarRentalSystem.Areas.Admin.Controllers.AreaApiController;
using AdminDisputeController = CarRentalSystem.Areas.Admin.Controllers.DisputeController;
using AdminPaymentController = CarRentalSystem.Areas.Admin.Controllers.PaymentController;
using AdminSettlementController = CarRentalSystem.Areas.Admin.Controllers.SettlementController;
using AdminSreController = CarRentalSystem.Areas.Admin.Controllers.SreController;
using AdminUserController = CarRentalSystem.Areas.Admin.Controllers.UserController;

namespace CarRentalSystem.Tests.UnitTests
{
    public class SecurityAttributeTests
    {
        [Theory]
        [InlineData(nameof(PaymentController.CancelHold))]
        [InlineData(nameof(PaymentController.CancelTransaction))]
        [InlineData(nameof(PaymentController.CreatePayment))]
        [Trait("Module", "Security")]
        [Trait("Type", "Regression")]
        public void PaymentStateChangingActions_RequirePostAndAntiForgery(string actionName)
        {
            var method = typeof(PaymentController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(m => m.Name == actionName);

            method.GetCustomAttribute<HttpPostAttribute>().Should().NotBeNull();
            method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>().Should().NotBeNull();
            method.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>().Should().BeNull();
        }

        [Fact]
        [Trait("Module", "Security")]
        [Trait("Type", "Regression")]
        public void PayOSWebhook_IsOnlyExplicitAntiForgeryExemption()
        {
            var action = typeof(PaymentController).GetMethod(nameof(PaymentController.Webhook))!;

            action.GetCustomAttribute<HttpPostAttribute>().Should().NotBeNull();
            action.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
            action.GetCustomAttribute<IgnoreAntiforgeryTokenAttribute>().Should().NotBeNull();
        }

        [Theory]
        [InlineData(typeof(AdminUserController), "Admin")]
        [InlineData(typeof(AdminPaymentController), "Admin")]
        [InlineData(typeof(AdminAreaApiController), "Admin,Manager")]
        [InlineData(typeof(AdminDisputeController), "Admin,Manager")]
        [InlineData(typeof(AdminSettlementController), "Admin,Manager")]
        [InlineData(typeof(AdminSreController), "Admin,Manager")]
        [Trait("Module", "Authorization")]
        [Trait("Type", "Regression")]
        public void CriticalAdminControllers_HaveExpectedRoleMatrix(Type controllerType, string roles)
        {
            var authorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();

            authorize.Should().NotBeNull($"{controllerType.Name} must be protected at controller level");
            authorize!.Roles.Should().Be(roles);
        }
    }
}
