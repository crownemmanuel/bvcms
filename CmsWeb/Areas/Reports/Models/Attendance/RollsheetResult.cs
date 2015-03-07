/* Author: David Carroll
 * Copyright (c) 2008, 2009 Bellevue Baptist Church 
 * Licensed under the GNU General Public License (GPL v2)
 * you may not use this code except in compliance with the License.
 * You may obtain a copy of the License at http://bvcms.codeplex.com/license 
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using CmsData;
using UtilityExtensions;
using System.Web.Mvc;
using CmsWeb.Areas.Org2.Dialog.Models;
using CmsData.Codes;
using CmsWeb.Areas.Search.Models;

namespace CmsWeb.Areas.Reports.Models
{
    public class RollsheetResult : ActionResult
    {
        public class PersonVisitorInfo
        {
            public int PeopleId { get; set; }
            public string Name2 { get; set; }
            public string BirthDate { get; set; }
            public DateTime? LastAttended { get; set; }
            public string NameParent1 { get; set; }
            public string NameParent2 { get; set; }
            public string VisitorType { get; set; }
        }

        public OrgSearchModel OrgSearchModel;
        public NewMeetingInfo NewMeetingInfo;
        public int? meetingid, orgid;
        bool pageSetStarted;
        private bool hasRows;

        public override void ExecuteResult(ControllerContext context)
        {
            var Response = context.HttpContext.Response;

            CmsData.Meeting meeting = null;
            if (meetingid.HasValue)
            {
                meeting = DbUtil.Db.Meetings.Single(mt => mt.MeetingId == meetingid);
                Debug.Assert(meeting.MeetingDate != null, "meeting.MeetingDate != null");
                NewMeetingInfo = new NewMeetingInfo {MeetingDate = meeting.MeetingDate.Value};
                orgid = meeting.OrganizationId;
            }

            var list1 = NewMeetingInfo.ByGroup == true ? ReportList2().ToList() : ReportList().ToList();

            if (!list1.Any())
            {
                Response.Write("no data found");
                return;
            }
            Response.ContentType = "application/pdf";
            Response.AddHeader("content-disposition", "filename=foo.pdf");

            doc = new Document(PageSize.LETTER.Rotate(), 36, 36, 64, 64);
            var w = PdfWriter.GetInstance(doc, Response.OutputStream);
            w.PageEvent = pageEvents;
            doc.Open();
            dc = w.DirectContent;

            box = new PdfPCell();
            box.Border = PdfPCell.NO_BORDER;
            box.CellEvent = new CellEvent();
            PdfPTable table = null;

            OrgInfo lasto = null;
            foreach (var o in list1)
            {
                lasto = o;
                table = new PdfPTable(1);
                table.DefaultCell.Border = PdfPCell.NO_BORDER;
                table.DefaultCell.Padding = 0;
                table.WidthPercentage = 100;
                var co = DbUtil.Db.CurrentOrg;
                if (meeting != null)
                {
                    var Groups = o.Groups;
                    if (!Groups.HasValue())
                    {
                        var q = from at in meeting.Attends
                                where at.AttendanceFlag == true || at.Commitment == AttendCommitmentCode.Attending || at.Commitment == AttendCommitmentCode.Substitute
                                orderby at.Person.LastName, at.Person.FamilyId, at.Person.Name2
                                select new
                                           {
                                               at.MemberType.Code,
                                               Name2 = NewMeetingInfo.UseAltNames && at.Person.AltName.HasValue() ? at.Person.AltName : at.Person.Name2,
                                               at.PeopleId,
                                               at.Person.DOB,
                                           };
                        if (q.Any())
                            StartPageSet(o);
                        foreach (var a in q)
                            table.AddCell(AddRow(a.Code, a.Name2, a.PeopleId, a.DOB, "", font));
                    }
                    else
                    {
                        var comm = new[] {AttendCommitmentCode.Attending, AttendCommitmentCode.Substitute};
                        var q = from at in DbUtil.Db.Attends
                                where at.MeetingId == meeting.MeetingId
                                join om in DbUtil.Db.OrgMember(orgid, GroupSelectCode.Member, null, null, Groups, showhidden: false) on at.PeopleId equals om.PeopleId into mm
                                from om in mm.DefaultIfEmpty()
                                where at.AttendanceFlag || comm.Contains(at.Commitment ?? 0 )
                                orderby at.Person.LastName, at.Person.FamilyId, at.Person.Name2
                                select new
                                           {
                                               at.MemberType.Code,
                                               Name2 = NewMeetingInfo.UseAltNames && at.Person.AltName.HasValue() ? at.Person.AltName : at.Person.Name2,
                                               at.PeopleId,
                                               at.Person.DOB,
                                           };
                        if (q.Any())
                            StartPageSet(o);
                        foreach (var a in q)
                            table.AddCell(AddRow(a.Code, a.Name2, a.PeopleId, a.DOB, "", font));
                    }
                }
                else if (OrgSearchModel != null)
                {
                    var q = from om in DbUtil.Db.OrganizationMembers
                            where om.OrganizationId == o.OrgId
                        join m in DbUtil.Db.OrgPeople(o.OrgId, o.Groups) on om.PeopleId equals m.PeopleId
                        where om.EnrollmentDate <= Util.Now
                        orderby om.Person.LastName, om.Person.FamilyId, om.Person.Name2
                        let p = om.Person
                        let ch = NewMeetingInfo.UseAltNames && p.AltName != null && p.AltName.Length > 0
                        select new
                        {
                            PeopleId = p.PeopleId,
                            Name2 = ch ? p.AltName : p.Name2,
                            BirthDate = Util.FormatBirthday(
                                p.BirthYear,
                                p.BirthMonth,
                                p.BirthDay),
                            MemberTypeCode = om.MemberType.Code,
                            ch,
                            highlight =
                                om.OrgMemMemTags.Any(mm => mm.MemberTag.Name == NewMeetingInfo.HighlightGroup)
                                    ? NewMeetingInfo.HighlightGroup
                                    : ""
                        };
                    if (q.Any())
                        StartPageSet(o);
                    foreach (var m in q)
                        table.AddCell(AddRow(m.MemberTypeCode, m.Name2, m.PeopleId, m.BirthDate, m.highlight, m.ch ? china : font));

                }
                else if(co.GroupSelect == GroupSelectCode.Member)
                {
                    var Groups = NewMeetingInfo.ByGroup == true ? o.Groups : co.SgFilter;
                    var q = from om in DbUtil.Db.OrganizationMembers
                        where om.OrganizationId == orgid
                        join m in DbUtil.Db.OrgPeople(orgid, co.GroupSelect,
                            co.First(), co.Last(), Groups, co.ShowHidden,
                            co.FilterIndividuals, co.FilterTag) on om.PeopleId equals m.PeopleId
                            where om.EnrollmentDate <= Util.Now
                            orderby om.Person.LastName, om.Person.FamilyId, om.Person.Name2
                            let p = om.Person
                        let ch = NewMeetingInfo.UseAltNames && p.AltName != null && p.AltName.Length > 0
                            select new
                            {
                                PeopleId = p.PeopleId,
                                Name2 = ch ? p.AltName : p.Name2,
                                BirthDate = Util.FormatBirthday(
                                    p.BirthYear,
                                    p.BirthMonth,
                                    p.BirthDay),
                                MemberTypeCode = om.MemberType.Code,
                                ch,
                            highlight =
                                om.OrgMemMemTags.Any(mm => mm.MemberTag.Name == NewMeetingInfo.HighlightGroup)
                                    ? NewMeetingInfo.HighlightGroup
                                    : ""
                        };
                    if (q.Any())
                        StartPageSet(o);
                    foreach (var m in q)
                        table.AddCell(AddRow(m.MemberTypeCode, m.Name2, m.PeopleId, m.BirthDate, m.highlight, m.ch ? china : font));
                }
                else
                {
                        var q = from m in DbUtil.Db.OrgPeople(orgid, co.GroupSelect,
                                co.First(), co.Last(), co.SgFilter, co.ShowHidden,
                                co.FilterIndividuals, co.FilterTag)
                            orderby m.Name2
                            let p = DbUtil.Db.People.Single(pp => pp.PeopleId == m.PeopleId)
                            let om = p.OrganizationMembers.SingleOrDefault(mm => mm.OrganizationId == orgid)
                            let ch = NewMeetingInfo.UseAltNames && p.AltName != null && p.AltName.Length > 0
                            select new
                            {
                                p.PeopleId,
                                Name2 = ch ? p.AltName : p.Name2,
                                BirthDate = Util.FormatBirthday(
                                    p.BirthYear,
                                    p.BirthMonth,
                                    p.BirthDay),
                                MemberTypeCode = om == null ? "Guest" : om.MemberType.Code,
                                ch,
                                highlight = om.OrgMemMemTags.Any(mm => mm.MemberTag.Name == NewMeetingInfo.HighlightGroup) ? NewMeetingInfo.HighlightGroup : ""
                            };
                    if (q.Any())
                        StartPageSet(o);
                    foreach (var m in q)
                        table.AddCell(AddRow(m.MemberTypeCode, m.Name2, m.PeopleId, m.BirthDate, m.highlight, m.ch ? china : font));
                }
                if (OrgSearchModel != null
                   || (co != null 
                        && co.GroupSelect == GroupSelectCode.Member 
                        && meeting == null
                        && !co.SgFilter.HasValue() 
                        && !co.NameFilter.HasValue() 
                        && !co.FilterIndividuals
                        && !co.FilterTag
                        && NewMeetingInfo.ByGroup == false))
                {
                    foreach ( var m in RollsheetModel.FetchVisitors(o.OrgId, NewMeetingInfo.MeetingDate, NoCurrentMembers: true, UseAltNames: NewMeetingInfo.UseAltNames))
                    {
                        if(table.Rows.Count == 0)
                            StartPageSet(o);
                        table.AddCell(AddRow(m.VisitorType, m.Name2, m.PeopleId, m.BirthDate, "", boldfont));
                    }
                }
                if (!pageSetStarted)
                    continue;

                var col = 0;
                float gutter = 20f;
                float colwidth = (doc.Right - doc.Left - gutter) / 2;
                var ct = new ColumnText(w.DirectContent);
                ct.AddElement(table);

                int status = 0;

                while (ColumnText.HasMoreText(status))
                {
                    if(col == 0)
                        ct.SetSimpleColumn(doc.Left, doc.Bottom, doc.Left + colwidth, doc.Top);
                    else
                        ct.SetSimpleColumn(doc.Right - colwidth, doc.Bottom, doc.Right, doc.Top);
                    status = ct.Go();
                    ++col;
                    if (col > 1)
                    {
                        col = 0;
                        doc.NewPage();
                    }
                }
            }
            if (!hasRows)
            {
                if (!pageSetStarted)
                    StartPageSet(lasto);
                doc.Add(new Paragraph("no members as of this meeting date and time to show on rollsheet"));
            }
            doc.Close();
        }

        public class MemberInfo
        {
            public string Name { get; set; }
            public int Id { get; set; }
            public string Organization { get; set; }
            public string Location { get; set; }
            public string MemberType { get; set; }
        }

        private PdfPCell box;
        private Font boldfont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD);
        private Font font = FontFactory.GetFont(FontFactory.HELVETICA);
        private Font smallfont = FontFactory.GetFont(FontFactory.HELVETICA, 7);
        private Font medfont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
        private Font china = null;
        private PageEvent pageEvents = new PageEvent();
        private Document doc;
        private PdfContentByte dc;


        private void StartPageSet(OrgInfo o)
        {
            doc.NewPage();
            pageSetStarted = true;
//            if (altnames == true)
//            {
//                BaseFont.AddToResourceSearch(HttpContext.Current.Server.MapPath("/Content/iTextAsian.dll"));
//                var bfchina = BaseFont.CreateFont("MHei-Medium",
//                    "UniCNS-UCS2-H", BaseFont.EMBEDDED);
//                china = new Font(bfchina, 12, Font.NORMAL);
//            }
            pageEvents.StartPageSet(
                                    "{0}: {1}, {2} ({3})".Fmt(o.Division, o.Name, o.Location, o.Teacher),
                                    "{0:f} ({1})".Fmt(NewMeetingInfo.MeetingDate, o.OrgId),
                                    "M.{0}.{1:MMddyyHHmm}".Fmt(o.OrgId, NewMeetingInfo.MeetingDate));
        }
        private PdfPTable AddRow(string Code, string name, int pid, string dob, string highlight, Font font)
        {
            var t = new PdfPTable(4);
            //t.SplitRows = false;
            t.WidthPercentage = 100;
            t.SetWidths(new int[] { 30, 4, 6, 30 });
            t.DefaultCell.Border = PdfPCell.NO_BORDER;

            var bc = new Barcode39();
            bc.X = 1.2f;
            bc.Font = null;
            bc.Code = pid.ToString();
            var img = bc.CreateImageWithBarcode(dc, null, null);
            var c = new PdfPCell(img, false);
            c.PaddingTop = 3f;
            c.Border = PdfPCell.NO_BORDER;
            c.HorizontalAlignment = PdfPCell.ALIGN_RIGHT;
            t.AddCell(c);

            t.AddCell("");
            t.AddCell(box);

            DateTime bd;
            DateTime.TryParse(dob, out bd);

            var p = new Phrase(name, font);
            p.Add("\n");
            p.Add(new Chunk(" ", medfont));
            p.Add(new Chunk("({0}) {1:MMM d}".Fmt(Code, bd), smallfont));
            if (highlight.HasValue())
                p.Add("\n" + highlight);
            t.AddCell(p);
            hasRows = true;
            return t;
        }
        private class OrgInfo
        {
            public int OrgId { get; set; }
            public string Division { get; set; }
            public string Name { get; set; }
            public string Teacher { get; set; }
            public string Location { get; set; }
            public string Groups { get; set; }
        }
        private IEnumerable<OrgInfo> ReportList()
        {
            var orgs = OrgSearchModel == null
                ? DbUtil.Db.Organizations.AsQueryable()
                : OrgSearchModel.FetchOrgs();
            var roles = DbUtil.Db.CurrentRoles();
            var q = from o in orgs
                    where o.LimitToRole == null || roles.Contains(o.LimitToRole)
                    where o.OrganizationId == orgid || (orgid ?? 0) == 0
                    orderby o.Division.Name, o.OrganizationName
                    select new OrgInfo
                    {
                        OrgId = o.OrganizationId,
                        Division = o.Division.Name,
                        Name = o.OrganizationName,
                        Teacher = o.LeaderName,
                        Location = o.Location,
                        Groups = NewMeetingInfo.GroupFilterPrefix
                    };
            return q;
        }
        private IEnumerable<OrgInfo> ReportList2()
        {
            var orgs = OrgSearchModel == null
                ? DbUtil.Db.Organizations.AsQueryable()
                : OrgSearchModel.FetchOrgs();
            var roles = DbUtil.Db.CurrentRoles();
            var q = from o in orgs
                    where o.LimitToRole == null || roles.Contains(o.LimitToRole)
                    from sg in o.MemberTags
                    where (NewMeetingInfo.GroupFilterPrefix ?? "") == "" || sg.Name.StartsWith(NewMeetingInfo.GroupFilterPrefix)
                    where o.OrganizationId == orgid || (orgid ?? 0) == 0
                    select new OrgInfo
                    {
                        OrgId = o.OrganizationId,
                        Division = o.OrganizationName,
                        Name = sg.Name,
                        Teacher = "",
                        Location = o.Location,
                        Groups = sg.Name
                    };
            return q;
        }
        class CellEvent : IPdfPCellEvent
        {
            public void CellLayout(PdfPCell cell, Rectangle pos, PdfContentByte[] canvases)
            {
                var cb = canvases[PdfPTable.BACKGROUNDCANVAS];
                cb.SetGrayStroke(0f);
                cb.SetLineWidth(.2f);
                cb.RoundRectangle(pos.Left + 4, pos.Bottom, pos.Width - 8, pos.Height - 4, 4);
                cb.Stroke();
                cb.ResetRGBColorStroke();
            }
        }
        class PageEvent : PdfPageEventHelper
        {
            class NPages
            {
                public NPages(PdfContentByte dc)
                {
                    template = dc.CreateTemplate(50, 50);
                }
                public bool juststartednewset;
                public PdfTemplate template;
                public int n;
            }
            private NPages npages;
            private int pg;

            private PdfWriter writer;
            private Document document;
            private PdfContentByte dc;
            private BaseFont font;
            private string HeadText;
            private string HeadText2;
            private string Barcode;

            public override void OnOpenDocument(PdfWriter writer, Document document)
            {
                this.writer = writer;
                this.document = document;
                base.OnOpenDocument(writer, document);
                font = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                dc = writer.DirectContent;
                npages = new NPages(dc);
            }
            public void EndPageSet()
            {
                if (npages == null)
                    return;
                npages.template.BeginText();
                npages.template.SetFontAndSize(font, 8);
                npages.template.ShowText(npages.n.ToString());
                pg = 1;
                npages.template.EndText();
                npages = new NPages(dc);
            }
            public void StartPageSet(string header1, string header2, string barcode)
            {
                this.HeadText = header1;
                this.HeadText2 = header2;
                this.Barcode = barcode;
                npages.juststartednewset = true;
            }
            public override void OnEndPage(PdfWriter writer, Document document)
            {
                base.OnEndPage(writer, document);
                if (npages.juststartednewset)
                    EndPageSet();

                string text;
                float len;

                //---Header left
                text = HeadText;
                const float HeadFontSize = 11f;
                len = font.GetWidthPoint(text, HeadFontSize);
                dc.BeginText();
                dc.SetFontAndSize(font, HeadFontSize);
                dc.SetTextMatrix(30, document.PageSize.Height - 30);
                dc.ShowText(text);
                dc.EndText();
                dc.BeginText();
                dc.SetFontAndSize(font, HeadFontSize);
                dc.SetTextMatrix(30, document.PageSize.Height - 30 - (HeadFontSize * 1.5f));
                dc.ShowText(HeadText2);
                dc.EndText();

                //---Barcode right
                var bc = new Barcode39();
                bc.Font = null;
                bc.Code = Barcode;
                bc.X = 1.2f;
                var img = bc.CreateImageWithBarcode(dc, null, null);
                var h = font.GetAscentPoint(text, HeadFontSize);
                img.SetAbsolutePosition(document.PageSize.Width - img.Width - 30, document.PageSize.Height - 30 - img.Height + h);
                dc.AddImage(img);

                //---Column 1
                text = "Page " + (pg) + " of ";
                len = font.GetWidthPoint(text, 8);
                dc.BeginText();
                dc.SetFontAndSize(font, 8);
                dc.SetTextMatrix(30, 30);
                dc.ShowText(text);
                dc.EndText();
                dc.AddTemplate(npages.template, 30 + len, 30);
                npages.n = pg++;

                //---Column 2
                text = "Attendance Rollsheet";
                len = font.GetWidthPoint(text, 8);
                dc.BeginText();
                dc.SetFontAndSize(font, 8);
                dc.SetTextMatrix(document.PageSize.Width / 2 - len / 2, 30);
                dc.ShowText(text);
                dc.EndText();

                //---Column 3
                text = Util.Now.ToShortDateString();
                len = font.GetWidthPoint(text, 8);
                dc.BeginText();
                dc.SetFontAndSize(font, 8);
                dc.SetTextMatrix(document.PageSize.Width - 30 - len, 30);
                dc.ShowText(text);
                dc.EndText();

            }
            public override void OnCloseDocument(PdfWriter writer, Document document)
            {
                base.OnCloseDocument(writer, document);
                EndPageSet();
            }
            private float PutText(string text, BaseFont font, float size, float x, float y)
            {
                dc.BeginText();
                dc.SetFontAndSize(font, size);
                dc.SetTextMatrix(x, y);
                dc.ShowText(text);
                dc.EndText();
                return font.GetWidthPoint(text, size);
            }
        }
    }
}


