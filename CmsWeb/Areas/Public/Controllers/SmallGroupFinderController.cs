﻿using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web.Mvc;
using CmsWeb.Areas.Public.Models;
using System.Linq;
using CmsData;
using System.Web;

namespace CmsWeb.Areas.Public.Controllers
{
    public class SmallGroupFinderController : Controller
    {
        public ActionResult Index(string id, bool useShell = true)
        {
            var check = (from e in DbUtil.Db.Contents
                         where e.Name == "SGF-" + id + ".xml"
                         select e).SingleOrDefault();

            if (check == null)
                return new HttpNotFoundResult("Page not found!");

            var sgfm = BuildSmallGroupFinderModel(id, useShell);

            // Process shell here
            if (sgfm.hasShell())
                return Content(sgfm.createFromShell());

            if (DbUtil.Db.Setting("SGF-UseEmbeddedMap", false))
            {
                var template = DbUtil.Db.ContentHtml("ShellDefaultSGF", "<!-- CONTAINER -->");
                ViewBag.Shell = template;
                return View("MapShell", sgfm);
            }

            return View("Index", sgfm);
        }

        [HttpPost]
        public ActionResult GetMapContent(string id)
        {
            var sgfm = BuildSmallGroupFinderModel(id);

            return PartialView("MapContent", sgfm);
        }

        private SmallGroupFinderModel BuildSmallGroupFinderModel(string id, bool useShell = true)
        {
            var sgfm = new SmallGroupFinderModel(this, useShell);
            sgfm.load(id);

            var search = new Dictionary<string, SearchItem>();

            var loadAllValues = DbUtil.Db.Setting("SGF-LoadAllExtraValues", false);

            if (Request.Form.Count != 0)
            {
                var encoded = Request.Form.ToString();

                foreach (var item in encoded.Split('&'))
                {
                    if (!item.StartsWith("SGF") && !loadAllValues) continue;

                    var parts = item.Split('=');
                    if (parts.Count() != 2) continue;

                    parts[0] = HttpUtility.UrlDecode(parts[0]);
                    parts[1] = HttpUtility.UrlDecode(parts[1]);

                    if (search.ContainsKey(parts[0]))
                    {
                        search[parts[0]].values.Add(parts[1]);
                        search[parts[0]].parse = true;
                    }
                    else
                    {
                        search.Add(parts[0], new SearchItem { name = parts[0], values = { parts[1] } });
                    }
                }
            }

            foreach (var query in Request.QueryString.AllKeys.Where(x => x.ToLower() != "id"))
            {
                if (!query.StartsWith("SGF") && !loadAllValues) continue;

                if (search.ContainsKey(query))
                {
                    search[query].values.Clear();
                    search[query].values.Add(Request.QueryString[query]);
                }
                else
                {
                    search.Add(query, new SearchItem { name = query, values = { Request.QueryString[query] } });
                }
            }

            sgfm.setSearch(search);
            return sgfm;
        }
    }
}
