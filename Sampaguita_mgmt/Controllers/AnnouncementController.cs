using Microsoft.AspNetCore.Mvc;
using SeniorManagement.Helpers;

namespace SeniorManagement.Controllers
{
    public class AnnouncementController : BaseController
    {
        private readonly DatabaseHelper _dbHelper;

        public AnnouncementController(DatabaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        [HttpGet]
        public async Task<JsonResult> GetNotifications()
        {
            try
            {
                var notificationHelper = new NotificationHelper(_dbHelper);
                var userName = HttpContext.Session.GetString("UserName") ?? "System";
                var count = await notificationHelper.GetUnreadCountAsync(userName);
                var announcements = await notificationHelper.GetRecentAnnouncementsAsync(userName, 5);

                var result = new
                {
                    unreadCount = count,
                    announcements = announcements.Select(a => new
                    {
                        id = a.Id,
                        title = a.Title,
                        message = a.Message,
                        type = a.Type,
                        timeAgo = a.TimeAgo,
                        icon = a.Icon,
                        badgeColor = a.BadgeColor
                    })
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { unreadCount = 0, announcements = new object[] { } });
            }
        }



        [HttpPost]
        public JsonResult MarkAsRead(int id)
        {
            return Json(new { success = true });
        }

        public async Task<IActionResult> Index()
        {
            var notificationHelper = new NotificationHelper(_dbHelper);
            var userName = HttpContext.Session.GetString("UserName") ?? "System";
            var announcements = await notificationHelper.GetRecentAnnouncementsAsync(userName, 50);
            return View(announcements);
        }
    }
}