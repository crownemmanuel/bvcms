﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Xml.Linq;
using System.Configuration;
using System.IO;
using System.Collections.Specialized;
using System.Xml.Serialization;

namespace CmsCheckin
{
    public partial class ListFamily : UserControl
    {
        private const int ExtraPixelsName = 15;
        private const string STR_CheckMark = "ü";
        public ListFamily()
        {
            InitializeComponent();
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            const int WM_KEYDOWN = 0x100;
            const int WM_SYSKEYDOWN = 0x104;

            if ((msg.Msg == WM_KEYDOWN) || (msg.Msg == WM_SYSKEYDOWN))
            {
                switch (keyData)
                {
                    case Keys.PageUp:
                        if (pgup.Visible)
                            ShowPage(page - 1);
                        return true;
                    case Keys.PageDown:
                        if (pgdn.Visible)
                            ShowPage(page + 1);
                        return true;
                    case Keys.Escape:
                        Program.TimerStop();
                        this.GoHome(string.Empty);
                        return true;
                    case Keys.Return:
                        Program.TimerStop();
                        DoPrinting(null, null);
                        this.GoHome(string.Empty);
                        return true;
                    case Keys.S | Keys.Alt:
                        Program.TimerReset();
                        Program.CursorShow();
                        foreach (var c in sucontrols)
                        {
                            c.Enabled = true;
                            c.BackColor = Color.Coral;
                        }
                        return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool hasprinter;
        DoPrinting doprint = new DoPrinting();
        List<AttendLabel> list;
        int page = 1;
        List<Control> controls = new List<Control>();
        List<Control> sucontrols = new List<Control>();
        XDocument xdoc;
        const string Verdana = "Verdana";
        Font pfont = new Font(Verdana, 14f, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));

        public void ShowFamily(int fid)
        {
            page = 1;
            xdoc = this.GetDocument("Checkin2/Family/" + fid + Program.QueryString);
            ShowFamily(xdoc);
        }
        public void ShowFamily(XDocument x)
        {
            xdoc = x;
            hasprinter = PrintRawHelper.HasPrinter(Program.Printer);
            this.Focus();

            Program.FamilyId = x.Root.Attribute("familyid").Value.ToInt();

            if (!Program.SecurityCode.HasValue())
                Program.SecurityCode = x.Root.Attribute("securitycode").Value;
            label3.Text = Program.SecurityCode;

            Program.MaxLabels = x.Root.Attribute("maxlabels").Value.ToInt();

            list = new List<AttendLabel>();
            if (x.Descendants("attendee").Count() == 0)
            {
                ClearControls();
                var lab = new Label();
                lab.Font = pfont;
                lab.Location = new Point(15, 200);
                lab.AutoSize = true;
                PrintAll.Visible = false;
                PrintAll1.Visible = false;
                PrintAll2.Visible = false;
                pgup.Visible = false;
                pgdn.Visible = false;
                lab.Text = "Not Found, try another phone number?";
                this.Controls.Add(lab);
                Return.Text = "Try again";
                controls.Add(lab);
                return;
            }

            foreach (var e in x.Descendants("attendee"))
            {
                var a = new AttendLabel
                {
                    cinfo = new ClassInfo
                    {
                        oid = e.Attribute("orgid").Value.ToInt(),
                        pid = e.Attribute("id").Value.ToInt(),
                        mv = e.Attribute("mv").Value,
                    },
                    name = e.Attribute("name").Value,
                    first = e.Attribute("first").Value,
                    last = e.Attribute("last").Value,
                    dob = e.Attribute("dob").Value,
                    church = e.Attribute("church").Value,

                    goesby = e.Attribute("goesby").Value,
                    email = e.Attribute("email").Value,
                    addr = e.Attribute("addr").Value,
                    zip = e.Attribute("zip").Value,
                    home = e.Attribute("home").Value.FmtFone(),
                    cell = e.Attribute("cell").Value.FmtFone(),
                    gender = e.Attribute("gender").Value.ToInt(),
                    marital = e.Attribute("marital").Value.ToInt(),
                    emphone = e.Attribute("emphone").Value.FmtFone(),
                    emfriend = e.Attribute("emfriend").Value,
                    allergies = e.Attribute("allergies").Value,
                    grade = e.Attribute("grade").Value,
                    activeother = e.Attribute("activeother").Value,
                    parent = e.Attribute("parent").Value,

                    org = e.Attribute("org").Value,
                    orgname = e.Attribute("orgname").Value,
                    custody = bool.Parse(e.Attribute("custody").Value),
                    transport = bool.Parse(e.Attribute("transport").Value),
                    location = e.Attribute("loc").Value,
                    leader = e.Attribute("leader").Value,
                    NumLabels = int.Parse(e.Attribute("numlabels").Value),
                    Row = list.Count,
                    CheckedIn = bool.Parse(e.Attribute("checkedin").Value),
                    HasPicture = bool.Parse(e.Attribute("haspicture").Value),
                    RequiresSecurityLabel = bool.Parse(e.Attribute("requiressecuritylabel").Value),
                    leadtime = double.Parse(e.Attribute("leadtime").Value),
                };
                DateTime dt;
                if (DateTime.TryParse(e.Attribute("hour").Value, out dt))
                    a.cinfo.hour = dt;
                list.Add(a);
            }
            ShowPage(1);
        }
        public void ShowPage(int page)
        {
            ClearControls();
            this.page = page;

            const int sep = 10;
            const int rowheight = 50;
            int top = 50;
            const int bsize = 45;
            const int bwid = 65;
            const int mwid = 80;

            var points = 14F;
            var g = this.CreateGraphics();

            Font font;
            Font labfont;
            string Present = "Attend";
            string Labels = "Labels";
            Return.Text = "Print Labels, Return";

            var cols = new int[6];

            const int PageSize = 10;

            int srow = (page - 1) * PageSize;
            int erow = srow + PageSize;
            if (erow > list.Count)
                erow = list.Count;
            pgdn.Visible = list.Count > erow;
            pgup.Visible = srow > 0;

            int maxheight;
            int twidab, widname, widorg, twidlb;
            int totalwid;

            while (true)
            {
                twidab = widname = widorg = twidlb = maxheight = 0;
                font = new Font(Verdana, points, FontStyle.Regular,
                    GraphicsUnit.Point, ((byte)(0)));
                labfont = new Font(Verdana, points,
                    ((FontStyle)((FontStyle.Italic | FontStyle.Underline))),
                    GraphicsUnit.Point, ((byte)(0)));
                maxheight = 0;
                foreach (var c in list)
                {
                    var size = g.MeasureString(Present, labfont);
                    twidab = Math.Max(twidab, (int)Math.Ceiling(size.Width));
                    twidab = Math.Max(twidab, bwid);

                    size = g.MeasureString(c.name, font);
                    widname = Math.Max(widname, (int)Math.Ceiling(size.Width) + ExtraPixelsName);

                    size = g.MeasureString(Labels, labfont);
                    twidlb = Math.Max(twidlb, (int)Math.Ceiling(size.Width));
                    twidlb = Math.Max(twidlb, mwid);

                    size = g.MeasureString("{0:h:mm tt} {1}".Fmt(c.cinfo.hour, c.org), font);
                    widorg = Math.Max(widorg, (int)Math.Ceiling(size.Width));

                    size = g.MeasureString("|", labfont);
                    maxheight = Math.Max(maxheight, (int)Math.Ceiling(size.Height));
                }

                totalwid = sep + twidab + sep + widname + sep + widorg
                                    + sep + twidlb + sep;
                if (totalwid > 1024)
                {
                    points -= 1F;
                    continue;
                }
                break;
            }
            var labtop = top - rowheight;
            var LeftEdge = (1024 - totalwid) / 2;

            var head = new Label();
            LeftEdge += sep;
            head.Location = new Point(LeftEdge, labtop);
            head.Size = new Size(twidab + 5, maxheight);
            head.Font = labfont;
            head.Text = Present;
            this.Controls.Add(head);
            controls.Add(head);

            head = new Label();
            LeftEdge += twidab + sep;
            head.Location = new Point(LeftEdge, labtop);
            head.Size = new Size(widname + 5, maxheight);
            head.Font = labfont;
            head.Text = "Name";
            this.Controls.Add(head);
            controls.Add(head);

            head = new Label();
            LeftEdge += mwid + sep + widname + sep;
            head.Location = new Point(LeftEdge, labtop);
            head.Size = new Size(widorg + 5, maxheight);
            head.Font = labfont;
            head.Text = "Meeting";
            this.Controls.Add(head);
            controls.Add(head);

            for (var r = srow; r < erow; r++)
            {
                var c = list[r];
                if (c.cinfo.mv == "V")
                    c.cinfo.mv = "G";
                if (classlist.Count > 0)
                {
                    var q = from cl in classlist
                            where cl.oid == c.cinfo.oid && cl.pid == c.cinfo.pid
                            select cl;
                    if (q.Count() > 0 && c.CheckedIn)
                        c.WasChecked = true;
                }

                LeftEdge = (1024 - totalwid) / 2;
                top += rowheight;

                var ab = new Button();
                LeftEdge += sep;
                ab.Location = new Point(LeftEdge, top - 5);
                ab.Size = new Size(bwid, bsize);

                ab.FlatStyle = FlatStyle.Flat;
                ab.FlatAppearance.BorderSize = 1;

                ab.BackColor = Color.CornflowerBlue;
                ab.FlatAppearance.BorderColor = Color.Black;

                double howlate = -(Program.EarlyCheckin / 60d);
                if (c.cinfo.oid == 0
                    || c.leadtime > Program.LeadTime
                    || c.leadtime < howlate)
                {
                    ab.Enabled = false;
                    ab.BackColor = SystemColors.Control;
                    ab.FlatAppearance.BorderColor = SystemColors.ButtonShadow;
                }

                ab.ForeColor = Color.White;
                ab.Font = new Font("Wingdings", 24, FontStyle.Bold,
                    GraphicsUnit.Point, ((byte)(2)));
                ab.Name = "attend" + c.Row;
                ab.TextAlign = ContentAlignment.TopCenter;
                ab.UseVisualStyleBackColor = false;
                this.Controls.Add(ab);
                ab.KeyDown += new KeyEventHandler(AttendButton_KeyDown);
                ab.Click += new EventHandler(Attend_Click);
                ab.Text = c.CheckedIn ? STR_CheckMark : String.Empty;
                ab.Tag = c.Row;
                controls.Add(ab);

                var nam = new Button();
                LeftEdge += twidab + sep;
                nam.UseVisualStyleBackColor = false;
                nam.FlatStyle = FlatStyle.Flat;
                nam.FlatAppearance.BorderSize = 1;
                nam.FlatAppearance.BorderColor = Color.Black;
                if (c.HasPicture)
                    nam.BackColor = Color.FromArgb(0xFF, 0xCC, 0x99);
                else
                    nam.BackColor = Color.White;

                nam.Location = new Point(LeftEdge, top - 5);
                nam.Size = new Size(widname, bsize);

                nam.Font = font;
                nam.UseMnemonic = false;
                nam.Text = c.name;
                nam.Name = "name" + c.Row;
                nam.TextAlign = ContentAlignment.MiddleLeft;
                if (c.cinfo.oid != 0)
                    if (c.cinfo.mv == "M")
                        nam.ForeColor = Color.Blue;
                    else
                        nam.ForeColor = Color.DarkGreen;
                nam.Click += new EventHandler(ShowPic_Click);
                nam.Enabled = false;
                nam.Tag = c.Row;
                this.Controls.Add(nam);
                controls.Add(nam);
                sucontrols.Add(nam);

                var menu = new Button();
                LeftEdge += widname + 5 + sep;
                menu.Location = new Point(LeftEdge, top - 5);
                menu.Size = new Size(mwid, bsize);
                menu.Text = "menu";
                menu.BackColor = SystemColors.Control;
                menu.Enabled = false;
                menu.Font = pfont;
                menu.Name = "menu" + c.Row;
                menu.Tag = c.Row;
                menu.TextAlign = ContentAlignment.TopCenter;
                menu.UseVisualStyleBackColor = false;
                this.Controls.Add(menu);
                menu.Click += new EventHandler(Menu_Click);
                controls.Add(menu);
                sucontrols.Add(menu);

                var org = new Label();
                LeftEdge += mwid + 5 + sep;
                org.Location = new Point(LeftEdge, top);
                org.Size = new Size(widorg + 5, maxheight);
                org.Font = font;
                org.UseMnemonic = false;
                org.Text = "{0:h:mm tt} {1}".Fmt(c.cinfo.hour, c.org);
                org.TextAlign = ContentAlignment.MiddleLeft;
                org.Name = "org" + c.Row;
                if (c.cinfo.oid != 0)
                    if (c.cinfo.mv == "M")
                        org.ForeColor = Color.Blue;
                    else
                        org.ForeColor = Color.DarkGreen;
                this.Controls.Add(org);
                controls.Add(org);
            }
            Program.TimerStart(timer1_Tick);
            ComputeLabels();
        }

        void timer1_Tick(object sender, EventArgs e)
        {
            Program.TimerStop();
            Util.UnLockFamily();
            Program.ClearFields();
            this.GoHome("");
        }

        void AttendButton_KeyDown(object sender, KeyEventArgs e)
        {
            Program.TimerReset();
            if (e.KeyValue == 27)
            {
                Program.TimerStop();
                Util.UnLockFamily();
                this.GoHome(string.Empty);
            }
        }

        void PrintLabel_Click(object sender, EventArgs e)
        {
            Program.TimerReset();
            var c = list[(int)menu.Tag];
            var li = new LabelInfo
            {
                allergies = c.allergies,
                pid = c.cinfo.pid,
                mv = c.cinfo.mv,
                n = c.NumLabels,
                first = c.first,
                last = c.last,
                location = c.location,
                hour = c.cinfo.hour,
                org = c.org,
                custody = c.custody,
                transport = c.transport,
                requiressecuritylabel = c.RequiresSecurityLabel,
            };
            using (var ms = new MemoryStream())
            {
                if (Program.TwoInchLabel)
                    ms.LabelKiosk2(li);
                else
                    ms.LabelKiosk(li);
                PrintRawHelper.SendDocToPrinter(Program.Printer, ms);
            }

            RemoveMenu();
        }

        void Attend_Click(object sender, EventArgs e)
        {
            Attend_Click((Button)sender);
        }
        public void Attend_Click(Button ab)
        {
            Program.TimerReset();
            var c = list[(int)ab.Tag];
            if (c.lastpress.HasValue && DateTime.Now.Subtract(c.lastpress.Value).TotalSeconds < 1)
                return;
            if (c.cinfo.oid == 0)
                return;
            Cursor.Current = Cursors.WaitCursor;
            Program.CursorShow();
            if (ab.Text == String.Empty)
            {
                ab.Text = STR_CheckMark;
                c.CheckedIn = true;
                c.WasChecked = true;
            }
            else
            {
                ab.Text = String.Empty;
                c.CheckedIn = false;
                c.WasChecked = false;
            }
            var info = new Util.ClassCheckedInfo { c = c.cinfo, ischecked = c.CheckedIn };
            c.lastpress = DateTime.Now;
            ComputeLabels();
            var bw = new BackgroundWorker();
            bw.DoWork += CheckUnCheckDoWork;
            bw.RunWorkerCompleted += CheckUncheckCompleted;
            bw.RunWorkerAsync(info);
        }

        void ShowPic_Click(object sender, EventArgs e)
        {
            Program.TimerReset();
            var eb = sender as Button;
            var ab = this.Controls[this.Controls.IndexOfKey("attend" + eb.Tag.ToString())] as Button;
            var c = list[(int)ab.Tag];
            Program.PeopleId = c.cinfo.pid;
            var f = new Picture();
            f.ShowDialog();
        }

        public List<ClassInfo> classlist = new List<ClassInfo>();
        private Label mask;
        private Menu menu;

        void Menu_Click(object sender, EventArgs e)
        {
            Program.TimerReset();
            var MenuButton = sender as Button;
            menu = new Menu();
            menu.Tag = MenuButton.Tag;
            menu.Parent = this;
            menu.Location = new Point(MenuButton.Location.X - 100, MenuButton.Location.Y + MenuButton.Height);
            mask = new Label();
            mask.BackColor = this.BackColor;
            mask.Size = this.Size;
            //mask.Location = this.Location;
            mask.Parent = this;
            mask.BringToFront();
            var nam = this.Controls[this.Controls.IndexOfKey("name" + menu.Tag.ToString())] as Button;
            var org = this.Controls[this.Controls.IndexOfKey("org" + menu.Tag.ToString())] as Label;
            nam.BringToFront();
            org.BringToFront();

            mask.Show();
            menu.VisitClass += Visit_Click;
            var c = list[(int)menu.Tag];
            nam.Enabled = false;
            menu.EditRecord += EditRecord_Click;
            menu.PrintLabel += PrintLabel_Click;
            menu.AddFamily += AddToFamily_Click;
            menu.JoinClass += Join_Click;
            if (c.cinfo.oid != 0)
            {
                menu.DropJoin.Visible = true;
                if (c.cinfo.mv == "M")
                {
                    menu.DropJoinClass += DropThis_Click;
                    menu.DropJoin.Text = "Drop This Class";
                }
                else
                {
                    menu.DropJoinClass += JoinThis_Click;
                    menu.DropJoin.Text = "Join This Class";
                }
            }
            else
                menu.DropJoin.Visible = false;
            menu.CancelMenu += new EventHandler(CancelMenu_Click);
            menu.Show();
            menu.BringToFront();
        }

        void Join_Click(object sender, EventArgs e)
        {
            var c = list[(int)menu.Tag];
            SaveClasses();
            RemoveMenu();
            this.Swap(Program.classes);
            Program.classes.JoiningNotAttending = true;
            Program.classes.ShowResults(c.cinfo.pid);
        }
        void DropThis_Click(object sender, EventArgs e)
        {
            var c = list[(int)menu.Tag];
            var org = this.Controls[this.Controls.IndexOfKey("org" + menu.Tag.ToString())] as Label;
            SaveClasses();

            Util.JoinUnJoin(c.cinfo, false);
            RemoveMenu();
            Program.family.ShowFamily(Program.FamilyId);
        }
        void JoinThis_Click(object sender, EventArgs e)
        {
            var c = list[(int)menu.Tag];
            var org = this.Controls[this.Controls.IndexOfKey("org" + menu.Tag.ToString())] as Label;
            SaveClasses();

            Util.JoinUnJoin(c.cinfo, true);
            RemoveMenu();
            Program.family.ShowFamily(Program.FamilyId);
        }

        void CancelMenu_Click(object sender, EventArgs e)
        {
            RemoveMenu();
        }
        void Visit_Click(object sender, EventArgs e)
        {
            var c = list[(int)menu.Tag];
            SaveClasses();
            RemoveMenu();
            this.Swap(Program.classes);
            Program.classes.ShowResults(c.cinfo.pid);
        }
        private void SaveClasses()
        {
            classlist = new List<ClassInfo>();
            for (var r = 0; r < list.Count; r++)
            {
                var cc = list[r];
                if (cc.WasChecked)
                {
                    cc.cinfo.nlabels = cc.NumLabels;
                    classlist.Add(cc.cinfo);
                }
            }
        }
        private void ComputeLabels()
        {
            int canprint;
            int willprint;
            var can = list.Where(c => c.CheckedIn && c.NumLabels > 0);
            var will = can.Where(c => c.WasChecked);
            canprint = can.Count();
            willprint = will.Count();
            var show = canprint > willprint;
            PrintAll.Visible = show;
            PrintAll1.Visible = show;
            PrintAll2.Visible = show;
            if (PrintAll.Text.HasValue() || willprint > 0)
                Return.Text = "Print Labels, Return";
            else
                Return.Text = "Return";
        }

        void EditRecord_Click(object sender, EventArgs e)
        {
            var c = list[(int)menu.Tag];

            Program.PeopleId = c.cinfo.pid;
            Program.SetFields(c.last, c.email, c.addr, c.zip, c.home, c.parent, c.emfriend, c.emphone, c.activeother, c.church);
            Program.first.textBox1.Text = c.first;
            Program.goesby.textBox1.Text = c.goesby;
            Program.dob.textBox1.Text = c.dob;
            Program.cellphone.textBox1.Text = c.cell.FmtFone();
            Program.gendermarital.Marital = c.marital;
            Program.gendermarital.Gender = c.gender;
            if (Program.AskChurch)
                Program.gendermarital.ActiveOther.CheckState =
                    c.activeother == bool.TrueString ? CheckState.Checked :
                    c.activeother == bool.FalseString ? CheckState.Unchecked : CheckState.Indeterminate;
            if (Program.AskGrade)
                Program.grade.textBox1.Text = c.grade;
            Program.allergy.textBox1.Text = c.allergies;
            if (Program.AskEmFriend)
            {
                Program.parent.textBox1.Text = c.parent;
                Program.emfriend.textBox1.Text = c.emfriend;
                Program.emphone.textBox1.Text = c.emphone.FmtFone();
            }
            Util.UnLockFamily();
            SaveClasses();

            Program.editing = true;
            RemoveMenu();
            this.Swap(Program.first);
        }
        private void RemoveMenu()
        {
            var nam = this.Controls[this.Controls.IndexOfKey("name" + menu.Tag.ToString())] as Button;
            nam.Enabled = true;
            this.Controls.Remove(menu);
            this.Controls.Remove(mask);
            menu.Dispose();
            mask.Dispose();
        }

        PleaseWait PleaseWaitForm = null;
        private void DoPrinting(object sender, DoWorkEventArgs e)
        {
            if (list == null)
                return;

            var qlist = list.Where(c => c.CheckedIn && c.NumLabels > 0);
            if (!PrintAll.Text.HasValue())
                qlist = qlist.Where(c => c.WasChecked);

            var q = from c in qlist
                    select new LabelInfo
                    {
                        allergies = c.allergies,
                        pid = c.cinfo.pid,
                        mv = c.cinfo.mv,
                        n = c.NumLabels,
                        first = c.first,
                        last = c.last,
                        location = c.location,
                        hour = c.cinfo.hour,
                        org = c.orgname,
                        custody = c.custody,
                        transport = c.transport,
                        requiressecuritylabel = c.RequiresSecurityLabel,
                    };

            using (var ms = new MemoryStream())
            {
                Util.UnLockFamily();
                if (Program.PrintMode == "Print To Server")
                {
                    PrintServerLabels(q);
                    return;
                }
                if (Program.TwoInchLabel)
                    doprint.PrintLabels2(ms, q);
                else
                    doprint.PrintLabels(ms, q);
                doprint.FinishUp(ms);
            }
        }
        private void PrintingCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            PleaseWaitForm.Hide();
            PleaseWaitForm.Dispose();
            PleaseWaitForm = null;
            Program.FamilyId = 0;
            classlist = new List<ClassInfo>();
            PrintAll.Text = string.Empty;
            if (Program.AskLabels)
            {
                var f = new DidItWork();
                var ret = f.ShowDialog();
                f.Hide();
                f.Dispose();
                if (ret == DialogResult.No)
                {
                    Util.ReportPrinterProblem();
                    var fa = new AdminLogin();
                    fa.ShowDialog();
                }
            }
            this.GoHome(string.Empty);
        }
        private void PrintServerLabels(IEnumerable<LabelInfo> q)
        {
            if (list == null)
                return;

            var j = new PrintJob { securitycode = Program.SecurityCode, list = q.ToList() };
            var xs = new XmlSerializer(typeof(PrintJob));
            var sw = new StringWriter();
            xs.Serialize(sw, j);
            Util.UploadPrintJob(sw.ToString());
        }
        private void ClearControls()
        {
            foreach (var c in controls)
            {
                this.Controls.Remove(c);
                c.Dispose();
            }
            controls.Clear();
            doprint.LabelsPrinted = 0;
            sucontrols.Clear();
        }

        private void MagicButton_Click(object sender, EventArgs e)
        {
            Program.TimerStop();
            if (list.Count == 0)
            {
                this.Swap(Program.namesearch);
                return;
            }
            foreach (var c in sucontrols)
            {
                c.Enabled = true;
                if (c.BackColor == SystemColors.Control)
                    c.BackColor = Color.Coral;
            }
        }

        private void pgup_Click(object sender, EventArgs e)
        {
            ShowPage(page - 1);
        }

        private void pgdn_Click(object sender, EventArgs e)
        {
            ShowPage(page + 1);
        }

        private void AddToFamily_Click(object sender, EventArgs e)
        {
            var c = list[(int)menu.Tag];
            Program.SetFields(c.last, c.email, c.addr, c.zip, c.home, c.parent, c.emfriend, c.emphone, c.activeother, c.church);
            Program.editing = false;
            SaveClasses();
            Util.UnLockFamily();
            RemoveMenu();
            this.Swap(Program.first);
        }

        private void CheckUnCheckDoWork(object sender, DoWorkEventArgs e)
        {
            var info = e.Argument as Util.ClassCheckedInfo;
            Util.AttendUnAttend(info);

        }
        private void CheckUncheckCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Cursor.Current = Cursors.Default;
            Program.CursorHide();
        }

        private void Return_Click(object sender, EventArgs e)
        {
            Program.TimerStop();

            if (Return.Text.Contains("Try Again"))
            {
                this.GoHome(string.Empty);
                return;
            }
            PleaseWaitForm = new PleaseWait();
            PleaseWaitForm.Show();

            var bw = new BackgroundWorker();
            bw.DoWork += new DoWorkEventHandler(DoPrinting);
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(PrintingCompleted);
            bw.RunWorkerAsync();
        }
        private void PrintAll_Click(object sender, EventArgs e)
        {
            PrintAll.Text = PrintAll.Text.HasValue() ? String.Empty : STR_CheckMark;
            ComputeLabels();
        }
    }
    public class AttendLabel
    {
        public DateTime? lastpress { get; set; }
        public ClassInfo cinfo { get; set; }
        public string name { get; set; }
        public string first { get; set; }
        public string last { get; set; }
        public string dob { get; set; }
        public string church { get; set; }

        public string goesby { get; set; }
        public string email { get; set; }
        public string addr { get; set; }
        public string zip { get; set; }
        public string home { get; set; }
        public string cell { get; set; }
        public string emfriend { get; set; }
        public string emphone { get; set; }
        public string allergies { get; set; }
        public string grade { get; set; }
        public string activeother { get; set; }
        public string parent { get; set; }
        public bool custody { get; set; }
        public bool transport { get; set; }

        public int gender { get; set; }
        public int marital { get; set; }

        public string orgname { get; set; }
        public string leader { get; set; }
        public string org { get; set; }
        public string location { get; set; }
        public int NumLabels { get; set; }
        public int Row { get; set; }
        public bool CheckedIn { get; set; }
        public bool WasChecked { get; set; }
        public bool HasPicture { get; set; }
        public bool RequiresSecurityLabel { get; set; }
        public double leadtime { get; set; }
    }

    // all of these come from the attribtues on the attendee element
    // attributes have the same name unless noted otherwise
    [Serializable]
    public class LabelInfo 
    {
        public int n { get; set; } // numlabels attribute
        public string location { get; set; } // loc attribute
        public string allergies { get; set; }
        public string org { get; set; } // orgname attribute
        public DateTime? hour { get; set; }
        public int pid { get; set; } // id attribute
        public string mv { get; set; }
        public string first { get; set; }
        public string last { get; set; }
        public bool transport { get; set; }
        public bool custody { get; set; }
        public bool requiressecuritylabel { get; set; }
    }
    [Serializable]
    public class PrintJob
    {
        // securitycode comes from the attribute on the root element (Attendees)
        public string securitycode { get; set; } 
        // the following is a list of each person/class that was checked present
        public List<LabelInfo> list { get; set; }
    }
    [Serializable]
    public class PrintJobs
    {
        public List<PrintJob> jobs { get; set; }
    }
}
