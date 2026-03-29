using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KarnelTravelGuide.Web.Attributes
{
    public class RoleAuthorizeAttribute : ActionFilterAttribute
    {
        private readonly string[] _roles;

        public RoleAuthorizeAttribute(params string[] roles)
        {
            _roles = roles;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var userRole = context.HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userRole))
            {
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "" });
                return;
            }

            if (_roles.Length > 0 && !_roles.Contains(userRole))
            {
                context.Result = new RedirectToActionResult("Index", "Home", new { area = "" });
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
