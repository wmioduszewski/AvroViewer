#region

using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using MsCommon.ClickOnce.Extensions;

#endregion

namespace MsCommon.ClickOnce
{
    public class AppForm : Form, IAppender
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(AppForm));

        private IContainer components;
        private ContextMenuStrip lbLogContextMenuStrip;
        private ToolStripMenuItem copyToolStripMenuItem;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem clearToolStripMenuItem;
        private ListBox lbLog;

        public AppForm()
        {
            InitializeComponent();
            ComponentResourceManager resources =
                new ComponentResourceManager(GetType());
            Icon = ((Icon)(resources.GetObject("$this.Icon")));
            ((Hierarchy)LogManager.GetRepository()).Root.AddAppender(this);
            Load += HandleAppFormLoad;
        }

        private void HandleAppFormLoad(object sender, EventArgs e)
        {
            BringToFront();
        }

        #region PerformWork & PerformWorkAsync

        public void PerformWork(Action bgwork, Action fgwork, Action<Exception> customErrorHandler = null)
        {
            this.SetBusy(true);
            new Thread(() =>
            {
                Exception exception = null;
                try
                {
                    if (bgwork != null)
                        bgwork();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                Invoke((Action)(() =>
                {
                    if (exception != null)
                    {
                        if (customErrorHandler != null)
                            customErrorHandler(exception);
                        else
                            new ReportBugForm(exception).ShowDialog(this);
                        this.SetBusy(false);
                        return;
                    }

                    try
                    {
                        if (fgwork != null)
                            fgwork();
                    }
                    catch (Exception ex)
                    {
                        new ReportBugForm(ex).ShowDialog(this);
                    }
                    finally
                    {
                        this.SetBusy(false);
                    }
                }));
            }).Start();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task PerformWorkAsync(Func<Task> bgwork, Func<Task> fgwork,
            Action<Exception> customErrorHandler = null)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            this.SetBusy(true);
            await Task.Factory.StartNew(async () =>
            {
                Exception exception = null;
                try
                {
                    if (bgwork != null)
                        await bgwork();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                Delegate method = (Func<Task>)(async () =>
                {
                    if (exception != null)
                    {
                        if (customErrorHandler != null)
                            customErrorHandler(exception);
                        else
                            new ReportBugForm(exception).ShowDialog(this);
                        this.SetBusy(false);
                        return;
                    }

                    try
                    {
                        if (fgwork != null)
                            await fgwork();
                    }
                    catch (Exception ex)
                    {
                        new ReportBugForm(ex).ShowDialog(this);
                    }
                    finally
                    {
                        this.SetBusy(false);
                    }
                });
                if (InvokeRequired)
                    Invoke(method);
                else
                    method.DynamicInvoke();
            });
        }

        #endregion

        #region Logging

        private string _LastLogMessage;
        private int _LastLogMessageCount;

        protected void SetLoggingListBox(ListBox loggingListBox)
        {
            lbLog = loggingListBox;
            if (lbLog == null)
                return;

            lbLog.DrawMode = DrawMode.OwnerDrawFixed;
            lbLog.DrawItem += HandleDrawLogItem;
            lbLog.ContextMenuStrip = lbLogContextMenuStrip;
        }

        private void AddToLog(LogItem logitem)
        {
            if (lbLog == null)
                return;

            if (lbLog.InvokeRequired)
            {
                lbLog.Invoke((Action<LogItem>)AddToLog, logitem);
                return;
            }

            if (_LastLogMessage != logitem.Message)
            {
                _LastLogMessage = logitem.Message;
                _LastLogMessageCount = 1;
            }
            else
            {
                _LastLogMessageCount++;
            }

            if (_LastLogMessageCount > 1)
            {
                logitem.Message += " (message repeated " + _LastLogMessageCount + " times)";
                lbLog.Items.RemoveAt(0);
            }

            lbLog.Items.Insert(0, logitem);
            while (lbLog.Items.Count > 250)
            {
                lbLog.Items.RemoveAt(lbLog.Items.Count - 1);
            }
        }

        private void HandleDrawLogItem(object sender, DrawItemEventArgs e)
        {
            ListBox lb = (ListBox)sender;
            if (e.Index < 0 || e.Index >= lb.Items.Count)
                return;

            Color backColor = lb.BackColor;
            LogItem li = lb.Items[e.Index] as LogItem;
            if (li != null)
            {
                if (li.Level == Level.Warn)
                    backColor = Color.Yellow;
                if (li.Level == Level.Error)
                    backColor = Color.LightPink;
            }

            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
            {
                backColor = ChangeColorBrightness(backColor, -0.1f);
            }

            Graphics g = e.Graphics;

            string msg = lb.Items[e.Index].ToString();

            int hzSize = (int)g.MeasureString(msg, lb.Font).Width;
            if (lb.HorizontalExtent < hzSize)
                lb.HorizontalExtent = hzSize;

            e.DrawBackground();
            g.FillRectangle(new SolidBrush(backColor), e.Bounds);
            g.DrawString(msg, e.Font, new SolidBrush(Color.Black), new PointF(e.Bounds.X, e.Bounds.Y));
            e.DrawFocusRectangle();
        }

        /// <summary>
        /// http://www.pvladov.com/2012/09/make-color-lighter-or-darker.html
        /// </summary>
        public static Color ChangeColorBrightness(Color color, float correctionFactor)
        {
            float red = color.R;
            float green = color.G;
            float blue = color.B;

            if (correctionFactor < 0)
            {
                correctionFactor = 1 + correctionFactor;
                red *= correctionFactor;
                green *= correctionFactor;
                blue *= correctionFactor;
            }
            else
            {
                red = (255 - red) * correctionFactor + red;
                green = (255 - green) * correctionFactor + green;
                blue = (255 - blue) * correctionFactor + blue;
            }

            return Color.FromArgb(color.A, (int)red, (int)green, (int)blue);
        }

        public void DoAppend(LoggingEvent loggingEvent)
        {
            if (loggingEvent.Level >= Level.Info)
            {
                AddToLog(new LogItem
                {
                    TimeStamp = loggingEvent.TimeStamp, Level = loggingEvent.Level,
                    Message = loggingEvent.RenderedMessage
                });
            }
        }

        public class LogItem
        {
            public DateTime TimeStamp { get; set; }
            public Level Level { get; set; }
            public string Message { get; set; }

            public override string ToString()
            {
                return string.Format("{0}: {1}", TimeStamp.ToString("HH:mm:ss"), Message);
            }

            public string ToStringWithLevel()
            {
                return string.Format("{0} [{2}] {1}", TimeStamp.ToString("HH:mm:ss"), Message, Level);
            }
        }

        private void HandleLogCopyClicked(object sender, EventArgs e)
        {
            LogMethodEntry();

            if (lbLog == null)
                return;

            if (lbLog.SelectedItems.Count == 0)
                return;

            string[] selectedLog = lbLog.SelectedItems.Cast<LogItem>().Select(li => li.ToStringWithLevel()).ToArray();
            Clipboard.SetText(string.Join("\r\n", selectedLog));
        }

        private void HandleLogClearClicked(object sender, EventArgs e)
        {
            if (lbLog == null)
                return;

            lbLog.Items.Clear();
        }

        protected void LogMethodEntry([CallerFilePath] string file = "", [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
        {
            Logger.DebugFormat("=> {0}.{1}:{2}", Path.GetFileNameWithoutExtension(file), member, line);
        }

        #endregion

        private void InitializeComponent()
        {
            components = new Container();
            lbLogContextMenuStrip = new ContextMenuStrip(components);
            copyToolStripMenuItem = new ToolStripMenuItem();
            contextMenuStrip1 = new ContextMenuStrip(components);
            clearToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            lbLogContextMenuStrip.SuspendLayout();
            SuspendLayout();
            // 
            // lbLogContextMenuStrip
            // 
            lbLogContextMenuStrip.Items.AddRange(new ToolStripItem[]
            {
                copyToolStripMenuItem,
                toolStripSeparator1,
                clearToolStripMenuItem
            });
            lbLogContextMenuStrip.Name = "lbLogContextMenuStrip";
            lbLogContextMenuStrip.Size = new Size(153, 76);
            // 
            // copyToolStripMenuItem
            // 
            copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            copyToolStripMenuItem.Size = new Size(152, 22);
            copyToolStripMenuItem.Text = "Copy";
            copyToolStripMenuItem.Click += HandleLogCopyClicked;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(61, 4);
            // 
            // clearToolStripMenuItem
            // 
            clearToolStripMenuItem.Name = "clearToolStripMenuItem";
            clearToolStripMenuItem.Size = new Size(152, 22);
            clearToolStripMenuItem.Text = "Clear";
            clearToolStripMenuItem.Click += HandleLogClearClicked;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(149, 6);
            // 
            // AppForm
            // 
            ClientSize = new Size(284, 261);
            Name = "AppForm";
            lbLogContextMenuStrip.ResumeLayout(false);
            ResumeLayout(false);
        }
    }
}