using System.Reflection;
using CarRentalSystem.Authorization;
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
        [InlineData(nameof(PaymentController.ConfirmHold))]
        [InlineData(nameof(PaymentController.ConfirmTransaction))]
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

        [Fact]
        [Trait("Module", "Security")]
        [Trait("Type", "Regression")]
        public void Logout_RequiresPostAndAntiForgery()
        {
            var action = typeof(AuthController).GetMethod(nameof(AuthController.Logout))!;

            action.GetCustomAttribute<HttpPostAttribute>().Should().NotBeNull();
            action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>().Should().NotBeNull();
            action.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        }

        [Theory]
        [InlineData(nameof(PaymentController.Hold))]
        [InlineData(nameof(PaymentController.Transaction))]
        [InlineData(nameof(PaymentController.Success))]
        [Trait("Module", "Security")]
        [Trait("Type", "Regression")]
        public void PaymentReturnPages_AreNotAnonymous(string actionName)
        {
            var action = typeof(PaymentController).GetMethod(actionName)!;

            action.GetCustomAttribute<HttpGetAttribute>().Should().NotBeNull();
            action.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
        }

        [Theory]
        [InlineData(typeof(AdminUserController))]
        [InlineData(typeof(AdminPaymentController))]
        [InlineData(typeof(AdminAreaApiController))]
        [InlineData(typeof(AdminDisputeController))]
        [InlineData(typeof(AdminSettlementController))]
        [InlineData(typeof(AdminSreController))]
        [Trait("Module", "Authorization")]
        [Trait("Type", "Regression")]
        public void CriticalAdminControllers_RequireAdminPolicy(Type controllerType)
        {
            var authorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();

            authorize.Should().NotBeNull($"{controllerType.Name} must be protected at controller level");
            authorize!.Policy.Should().Be(PortalPolicyNames.RequireAdmin);
            authorize.Roles.Should().BeNullOrWhiteSpace();
        }

        [Theory]
        [InlineData("CarRentalSystem.Areas.Admin.Controllers", PortalPolicyNames.RequireAdmin)]
        [InlineData("CarRentalSystem.Areas.Manager.Controllers", PortalPolicyNames.RequireManager)]
        [InlineData("CarRentalSystem.Areas.Staff.Controllers", PortalPolicyNames.RequireStaff)]
        [Trait("Module", "Authorization")]
        [Trait("Type", "Contract")]
        public void InternalPortalControllers_RequireAreaPolicy(string controllerNamespace, string expectedPolicy)
        {
            var controllers = typeof(AdminUserController).Assembly.GetTypes()
                .Where(type => !type.IsAbstract &&
                    typeof(ControllerBase).IsAssignableFrom(type) &&
                    type.Namespace == controllerNamespace)
                .ToList();

            controllers.Should().NotBeEmpty();
            foreach (var controller in controllers)
            {
                controller.GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(
                    expectedPolicy,
                    $"{controller.FullName} must require {expectedPolicy}");
            }
        }

        [Fact]
        [Trait("Module", "Authorization")]
        [Trait("Type", "Contract")]
        public void StaffAndManagerViews_DoNotReferenceAdminLayoutsOrRoutes()
        {
            var appRoot = FindAppRoot();
            var files = new[] { "Staff", "Manager" }
                .SelectMany(area => Directory.EnumerateFiles(
                    Path.Combine(appRoot, "Areas", area),
                    "*.*",
                    SearchOption.AllDirectories))
                .Where(path => path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .ToList();

            files.Should().NotBeEmpty();
            foreach (var file in files)
            {
                var source = File.ReadAllText(file);
                source.Should().NotContain("_AdminLayout", file);
                source.Should().NotContain("_AreaManagerLayout", file);
                source.Should().NotContain("/Admin/", file);
            }
        }

        private static string FindAppRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, "CarRentalSystem", "Areas");
                if (Directory.Exists(candidate))
                    return Path.Combine(current.FullName, "CarRentalSystem");
                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate the CarRentalSystem application root.");
        }
    }
}
