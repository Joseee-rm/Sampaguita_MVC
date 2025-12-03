using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SeniorManagement.Controllers
{
    public class BaseController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            // Always set ViewBag properties for user info
            ViewBag.IsAdmin = HttpContext.Session.GetString("IsAdmin") == "True";
            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? "User";
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole") ?? "Staff";
            ViewBag.Name = HttpContext.Session.GetString("Name") ?? ViewBag.UserName;
        }
    }
}