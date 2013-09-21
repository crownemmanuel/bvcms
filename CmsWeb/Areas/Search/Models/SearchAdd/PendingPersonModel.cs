using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CmsWeb.Areas.People.Models;
using CmsWeb.Code;
using NPOI.SS.Formula;
using UtilityExtensions;
using CmsData;
using System.Text;
using System.Data.Linq;
using CmsData.Codes;

namespace CmsWeb.Areas.Search.Models
{
    public class PendingPersonModel
    {
        public int index { get; set; }
        public string context { get; set; }

        [StringLength(25), Required(ErrorMessage = "required, or put 'na' if not known"), RemoveNA]
        public string FirstName { get; set; }

        [StringLength(15)]
        public string NickName { get; set; }

        [StringLength(15)]
        public string Middle { get; set; }

        [StringLength(100), Required(ErrorMessage = "required")]
        public string LastName { get; set; }

        public CodeInfo Title { get; set; }

        [StringLength(10)]
        public string SuffixCode { get; set; }

        [DisplayName("Birthday")]
        public string DOB { get; set; }

        [StringLength(20), RemoveNA]
        public string CellPhone { get; set; }

        [StringLength(20), EmailAddress, RemoveNA]
        public string EmailAddress { get; set; }

        [UnallowedCode("99", ErrorMessage = "specify gender (or unknown)")]
        public CodeInfo Gender { get; set; }

        [UnallowedCode("99", ErrorMessage = "specify marital status (or unknown)")]
        public CodeInfo MaritalStatus { get; set; }

        public CodeInfo Campus { get; set; }

        [UnallowedCode("0", ErrorMessage = "specify an Entry Point")]
        public CodeInfo EntryPoint { get; set; }

        [StringLength(20), RemoveNA]
        public string HomePhone { get; set; }

        private DateTime? birthday;
        public DateTime? Birthday
        {
            get
            {
                DateTime dt;
                if (!birthday.HasValue && DOB.NotEqual("na"))
                    if (Util.DateValid(DOB, out dt))
                        birthday = dt;
                return birthday;
            }
        }

        public int? Age
        {
            get
            {
                if (birthday.HasValue)
                    return birthday.Value.AgeAsOf(Util.Now);
                return null;
            }
        }

        public bool IsNew
        {
            get { return !PeopleId.HasValue; }
        }

        [NoUpdate]
        public int FamilyId { get; set; }
        private Family _family;
        public Family Family
        {
            get
            {
                if (_family == null && FamilyId > 0)
                    _family = DbUtil.Db.Families.Single(f => f.FamilyId == FamilyId);
                return _family;
            }
        }

        [NoUpdate]
        public int? PeopleId { get; set; }
        private Person person;
        public Person Person
        {
            get
            {
                if (person == null && PeopleId.HasValue)
                    person = DbUtil.Db.LoadPersonById(PeopleId.Value);
                return person;
            }
        }

        public AddressInfo AddressInfo { get; set; }

        public string PotentialDuplicate { get; set; }

        internal void CheckDuplicate()
        {
            var pids = DbUtil.Db.FindPerson(FirstName, LastName, birthday, null, CellPhone.GetDigits()).Select(pp => pp.PeopleId).ToList();
            var q = from p in DbUtil.Db.People
                    where pids.Contains(p.PeopleId)
                    select new { p.PeopleId, p.Name, p.PrimaryAddress, p.Age, };
            var sb = new StringBuilder();
            foreach (var p in q)
            {
                if (sb.Length == 0)
                    sb.AppendLine("<ul>\n");
                sb.AppendFormat("<li><a href=\"/Person2/{1}\" target=\"_blank\">{0}</a> ({1}), {2}, age:{3}</li>\n".Fmt(p.Name, p.PeopleId, p.PrimaryAddress, p.Age));
            }
            if (sb.Length > 0)
                PotentialDuplicate = sb + "</ul>\n";
        }

        public bool IsNewFamily { get; set; }

        internal void AddPerson(int originid, int? entrypointid, int? campusid)
        {
            Family f;
            if (FamilyId > 0)
                f = Family;
            else
            {
                f = new Family();
                AddressInfo.CopyPropertiesTo(f);
                f.HomePhone = HomePhone.GetDigits();
            }
            if (NickName != null)
                NickName = NickName.Trim();
            var position = PositionInFamily.Child;

            if (!birthday.HasValue)
                position = PositionInFamily.PrimaryAdult;
            if (Age >= 18)
                if (f.People.Count(per =>
                                   per.PositionInFamilyId == PositionInFamily.PrimaryAdult)
                    < 2)
                    position = PositionInFamily.PrimaryAdult;
                else
                    position = PositionInFamily.SecondaryAdult;

            person = Person.Add(f, position,
                                 null, FirstName.Trim(), NickName, LastName.Trim(), DOB, false, Gender.Value.ToInt(),
                                 originid, entrypointid);

            this.CopyPropertiesTo(Person);

            if (campusid == 0)
                campusid = null;
            Person.CampusId = Util.PickFirst(campusid.ToString(), DbUtil.Db.Setting("DefaultCampusId", "")).ToInt2();
            if (Person.CampusId == 0)
                Person.CampusId = null;

            DbUtil.Db.SubmitChanges();
            DbUtil.Db.Refresh(RefreshMode.OverwriteCurrentValues, Person);
            PeopleId = Person.PeopleId;
        }

        public void LoadAddress()
        {
            if (FamilyId <= 0)
            {
                AddressInfo = new AddressInfo();
                HomePhone = "";
#if DEBUG
                AddressInfo.AddressLineOne = "235 revere";
                AddressInfo.ZipCode = "38018";
#endif
                return;
            }
            var f = Family;
            AddressInfo = new AddressInfo(f.AddressLineOne, f.AddressLineTwo, f.CityName, f.StateCode, f.ZipCode, f.CountryName);
            HomePhone = f.HomePhone;
        }

        public string CityStateZip
        {
            get { return "{0}, {1} {2}".Fmt(AddressInfo.CityName, AddressInfo.StateCode.Value, AddressInfo.ZipCode); }
        }
    }
}
