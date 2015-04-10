using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Security;
using CmsData;
using CmsData.Registration;
using CmsWeb.Models;
using Elmah;
using UtilityExtensions;
using System.Collections.Generic;
using CmsData.Codes;

namespace CmsWeb.Areas.OnlineReg.Controllers
{
    public partial class OnlineRegController : CmsController
    {
        [HttpGet]
        public ActionResult Continue(int id)
        {
            var m = OnlineRegModel.GetRegistrationFromDatum(id);
            if (m == null)
                return Message("no existing registration available");
            var n = m.List.Count - 1;
            m.HistoryAdd("continue");
            m.UpdateDatum();
            SetHeaders(m);
            return View("Index", m);
        }
        
        [HttpGet]
        public ActionResult StartOver(int id)
        {
            var pid = (int)TempData["er"];
            if (pid == 0)
                return Message("not logged in");
            var m = OnlineRegModel.GetRegistrationFromDatum(id);
            if (m == null)
                return Message("no existing registration available");
            m.HistoryAdd("startover");
            m.UpdateDatum(abandoned: true);
            return Redirect(m.URL);
        }

        [HttpPost]
        public ActionResult SaveProgress(OnlineRegModel m)
        {
            m.HistoryAdd("saveprogress");
            if (m.UserPeopleId == null)
                m.UserPeopleId = Util.UserPeopleId;
            m.UpdateDatum();
            var p = m.UserPeopleId.HasValue ? DbUtil.Db.LoadPersonById(m.UserPeopleId.Value) : m.List[0].person;

            if (p == null)
                return Content("We have not found your record yet, cannot save progress, sorry");
            if (m.masterorgid == null && m.Orgid == null)
                return Content("Registration is not far enough along to save, sorry.");

            var registerLink = EmailReplacements.CreateRegisterLink(m.masterorgid ?? m.Orgid, "Resume registration for {0}".Fmt(m.Header));
            var msg = "<p>Hi {first},</p>\n<p>Here is the link to continue your registration:</p>\n" + registerLink;
            var notifyids = DbUtil.Db.NotifyIds((m.masterorgid ?? m.Orgid).Value, (m.masterorg ?? m.org).NotifyIds);
            DbUtil.Db.Email(notifyids[0].FromEmail, p, "Continue your registration for {0}".Fmt(m.Header), msg);

            /* We use Content as an ActionResult instead of Message because we want plain text sent back
             * This is an HttpPost ajax call and will have a SiteLayout wrapping this.
             */
            return Content("We have saved your progress. An email with a link to finish this registration will come to you shortly.");
        }

        [HttpGet]
        public ActionResult Existing(int id)
        {
            var pid = (int)TempData["er"];
            if (pid == 0)
                return Message("not logged in");
            var m = OnlineRegModel.GetRegistrationFromDatum(id);
            if (m == null)
                return Message("no existing registration available");
            if (m.UserPeopleId != m.Datum.UserPeopleId)
                return Message("incorrect user");
            TempData["er"] = pid;
            return View(m);
        }


    }
}