using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CmsData;
using CmsWeb.Models;

namespace CmsWeb.Areas.People.Models.Person
{
    public class PreviousEnrollments : PagedTableModel<EnrollmentTransaction, OrgMemberInfo>
    {
        private int PeopleId;
        public CmsData.Person person { get; set; }
        public PreviousEnrollments(int id)
            : base("Org Name", "asc")
        {
            PeopleId = id;
            person = DbUtil.Db.LoadPersonById(id);
        }
        override public IQueryable<EnrollmentTransaction> DefineModelList()
        {
            var limitvisibility = Util2.OrgMembersOnly || Util2.OrgLeadersOnly
                                  || !HttpContext.Current.User.IsInRole("Access");
            var roles = DbUtil.Db.CurrentRoles();
            return from etd in DbUtil.Db.EnrollmentTransactions
                   let org = etd.Organization
                   where etd.TransactionStatus == false
                   where etd.PeopleId == PeopleId
                   where etd.TransactionTypeId >= 4
                   where !(limitvisibility && etd.Organization.SecurityTypeId == 3)
                   where org.LimitToRole == null || roles.Contains(org.LimitToRole)
                   select etd;
        }
        public override IEnumerable<OrgMemberInfo> DefineViewList(IQueryable<EnrollmentTransaction> q)
        {
            var q2 = from om in q
                     select new OrgMemberInfo
                     {
                         OrgId = om.OrganizationId,
                         PeopleId = om.PeopleId,
                         Name = om.OrganizationName,
                         MemberType = om.MemberType.Description,
                         EnrollDate = om.FirstTransaction.TransactionDate,
                         DropDate = om.TransactionDate,
                         AttendPct = om.AttendancePercentage,
                         DivisionName = om.Organization.Division.Program.Name + "/" + om.Organization.Division.Name,
                         OrgType = om.Organization.OrganizationType.Description ?? "Other"
                     };
            return q2;
        }
        override public IQueryable<EnrollmentTransaction> DefineModelSort(IQueryable<EnrollmentTransaction> q)
        {
            switch (Pager.SortExpression)
            {
                case "Enroll Date":
                case "Enroll Date desc":
                    return from om in q
                           orderby om.Organization.OrganizationType.Code ?? "z", om.EnrollmentDate
                           select om;
                case "Org Name":
                case "Org Name desc":
                    return from om in q
                           orderby om.Organization.OrganizationType.Code ?? "z", om.Organization.OrganizationName
                           select om;
            }
            return q;
        }
    }
}
