﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CmsData;

namespace CmsWeb.Areas.Dialog.Models
{
    public class TransactionHistoryModel
    {
        public int id { get; set; }
        public int oid { get; set; }
        public string Name { get; set; }
        public string Org { get; set; }

        public TransactionHistoryModel(int id, int oid)
        {
            this.id = id;
            this.oid = oid;
            var q = from o in DbUtil.Db.Organizations
                    from p in DbUtil.Db.People
                    where o.OrganizationId == oid
                    where p.PeopleId == id
                    select new { p.Name, o.FullName };
            var j = q.Single();
            Name = j.Name;
            Org = j.FullName;
        }
        public class AttendInfo
        {
            public string Indicator { get; set; }
            public string AttendanceFlag { get; set; }
            public DateTime MeetingDate { get; set; }
            public int MeetingId { get; set; }
            public string AttendType { get; set; }
            public string MemberType { get; set; }
            public int OtherAttends { get; set; }
            public int? OtherOrgId { get; set; }
        }
        public IEnumerable<AttendInfo> FetchAttends()
        {
            var q = from a in DbUtil.Db.Attends
                    where a.PeopleId == id && a.OrganizationId == oid
                    orderby a.MeetingDate descending
                    select new AttendInfo
                    {
                        Indicator = Indicator(a.AttendanceTypeId, a.EffAttendFlag),
                        AttendanceFlag = a.EffAttendFlag.HasValue ? (a.EffAttendFlag.Value ? "1" : "0") : "_",
                        MeetingDate = a.MeetingDate,
                        MeetingId = a.MeetingId,
                        AttendType = a.AttendType.Description,
                        MemberType = a.MemberType.Description,
                        OtherAttends = a.OtherAttends,
                        OtherOrgId = a.OtherOrgId,
                    };
            return q.Take(60);
        }

        public class TransactionInfo
        {
            public int TransactionId { get; set; }
            public int? EnrollmentTransactionId { get; set; }
            public DateTime TransactionDate { get; set; }
            public string TransactionType { get; set; }
            public string MemberType { get; set; }
            public string Pending { get; set; }
            public DateTime? EnrollmentDate { get; set; }
            public DateTime? NextTranChangeDate { get; set; }
        }
        public IEnumerable<TransactionInfo> FetchHistory()
        {
            var q2 = from et in DbUtil.Db.EnrollmentTransactions
                     where et.PeopleId == id && et.OrganizationId == oid
                     where et.TransactionStatus == false
                     orderby et.TransactionDate descending
                     select new TransactionInfo
                     {
                         TransactionId = et.TransactionId,
                         EnrollmentTransactionId = et.EnrollmentTransactionId,
                         TransactionDate = et.TransactionDate,
                         TransactionType = et.TransactionTypeId == 1 ? "Join" : et.TransactionTypeId == 3 ? "Change" : "Drop",
                         MemberType = et.MemberType.Description,
                         Pending = et.Pending == true ? "pending" : "",
                         EnrollmentDate = et.EnrollmentDate,
                         NextTranChangeDate = et.NextTranChangeDate
                     };
            return q2;
        }
        private string Indicator(int? type, bool? flag)
        {
            if (flag == null) // attended elsewhere or Group
                switch (type)
                {
                    case 20: return "V";
                    case 70: return "I";
                    case 90: return "G";
                    case 80: return "O";
                    case 110: return "*";
                    default: return "*";
                }
            else if (flag.Value) // attended here
                return "P";
            else // absent
                return ".";
        }

    }
}