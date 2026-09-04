//////////using System;
//////////using System.Collections.Generic;
//////////using System.Drawing;
//////////using System.Drawing.Drawing2D;
//////////using System.Drawing.Text;
//////////using System.Threading;
//////////using System.Windows.Forms;

//////////namespace UltraModernUI.Controls
//////////{
//////////    #region Enums
//////////    public enum ModernMessageIcon { None, Info, Warning, Error, Success, Question }
//////////    public enum ModernDialogResult { None, OK, Cancel, Yes, No, Abort, Retry, Ignore }
//////////    public enum ToasterType { Info, Success, Warning, Error }
//////////    #endregion

//////////    /// <summary>
//////////    /// A modern, high-fidelity replacement for the standard Windows MessageBox.
//////////    /// </summary>
//////////    public class ModernMessageBox : Form
//////////    {
//////////        private Color _backColor = Color.FromArgb(32, 32, 38);
//////////        private Color _accentColor = Color.FromArgb(0, 120, 212);
//////////        private Color _textColor = Color.FromArgb(240, 240, 240);
//////////        private Color _subTextColor = Color.FromArgb(160, 160, 160);

//////////        private string _title;
//////////        private string _message;
//////////        private ModernMessageIcon _icon;
//////////        private MessageBoxButtons _buttons;
//////////        private List<ModernDialogResult> _buttonResults = new List<ModernDialogResult>();
//////////        private List<Rectangle> _buttonRects = new List<Rectangle>();
//////////        private int _hoveredButton = -1;
//////////        private int _pressedButton = -1;
//////////        private Rectangle _closeRect;
//////////        private bool _hoverClose = false;

//////////        public ModernMessageBox(string message, string title, MessageBoxButtons buttons, ModernMessageIcon icon)
//////////        {
//////////            _title = title;
//////////            _message = message;
//////////            _buttons = buttons;
//////////            _icon = icon;

//////////            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
//////////            FormBorderStyle = FormBorderStyle.None;
//////////            StartPosition = FormStartPosition.CenterParent;
//////////            BackColor = Color.Magenta;
//////////            TransparencyKey = Color.Magenta;
//////////            TopMost = true;
//////////            ShowInTaskbar = false;
//////////            Font = new Font("Segoe UI Variable Display", 9f);
//////////        }

//////////        public static ModernDialogResult Show(string message, string title = "Message", MessageBoxButtons buttons = MessageBoxButtons.OK, ModernMessageIcon icon = ModernMessageIcon.None)
//////////        {
//////////            using (var box = new ModernMessageBox(message, title, buttons, icon))
//////////            {
//////////                box.CalculateSize();
//////////                if (box.ShowDialog() == DialogResult.OK)
//////////                    return box._result;
//////////                return ModernDialogResult.None;
//////////            }
//////////        }

//////////        private ModernDialogResult _result = ModernDialogResult.None;
//////////        private void CalculateSize()
//////////        {
//////////            using (var g = Graphics.FromHwnd(IntPtr.Zero))
//////////            {
//////////                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
//////////                SizeF titleSize = g.MeasureString(_title, new Font(Font, FontStyle.Bold), 500);
//////////                SizeF msgSize = g.MeasureString(_message, Font, 400);

//////////                int width = Math.Max(400, (int)Math.Max(titleSize.Width, msgSize.Width) + 80);
//////////                int height = (int)msgSize.Height + 120;

//////////                if (_icon != ModernMessageIcon.None) width += 60;

//////////                this.Size = new Size(width, height);

//////////                // Setup Buttons
//////////                _buttonResults.Clear();
//////////                switch (_buttons)
//////////                {
//////////                    case MessageBoxButtons.OK: _buttonResults.Add(ModernDialogResult.OK); break;
//////////                    case MessageBoxButtons.OKCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.OK); break;
//////////                    case MessageBoxButtons.YesNo: _buttonResults.Add(ModernDialogResult.No); _buttonResults.Add(ModernDialogResult.Yes); break;
//////////                    case MessageBoxButtons.YesNoCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.No); _buttonResults.Add(ModernDialogResult.Yes); break;
//////////                    case MessageBoxButtons.RetryCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.Retry); break;
//////////                    case MessageBoxButtons.AbortRetryIgnore: _buttonResults.Add(ModernDialogResult.Ignore); _buttonResults.Add(ModernDialogResult.Retry); _buttonResults.Add(ModernDialogResult.Abort); break;
//////////                }

//////////                _buttonRects.Clear();
//////////                int btnWidth = 100;
//////////                int btnHeight = 36;
//////////                int spacing = 12;
//////////                int totalBtnWidth = (_buttonResults.Count * btnWidth) + ((_buttonResults.Count - 1) * spacing);
//////////                int startX = width - totalBtnWidth - 20;

//////////                for (int i = 0; i < _buttonResults.Count; i++)
//////////                {
//////////                    _buttonRects.Add(new Rectangle(startX + (i * (btnWidth + spacing)), height - btnHeight - 20, btnWidth, btnHeight));
//////////                }

//////////                _closeRect = new Rectangle(width - 40, 12, 28, 28);
//////////            }
//////////        }

//////////        protected override void OnPaint(PaintEventArgs e)
//////////        {
//////////            Graphics g = e.Graphics;
//////////            g.SmoothingMode = SmoothingMode.AntiAlias;
//////////            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

//////////            using (var bgPath = GetRoundedPath(ClientRectangle, 8))
//////////            using (var bgBrush = new SolidBrush(_backColor))
//////////            using (var borderPen = new Pen(Color.FromArgb(40, 255, 255, 255), 1))
//////////            {
//////////                g.FillPath(bgBrush, bgPath);
//////////                g.DrawPath(borderPen, bgPath);
//////////            }

//////////            // Draw Icon
//////////            int iconX = 24;
//////////            int iconY = 60;
//////////            if (_icon != ModernMessageIcon.None)
//////////            {
//////////                DrawModernIcon(g, iconX, iconY, _icon);
//////////                iconX += 50;
//////////            }

//////////            // Draw Title
//////////            using (var titleFont = new Font(Font, FontStyle.Bold))
//////////            using (var titleBrush = new SolidBrush(_textColor))
//////////            using (var msgBrush = new SolidBrush(_subTextColor))
//////////            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
//////////            {
//////////                g.DrawString(_title, titleFont, titleBrush, new Rectangle(24, 16, Width - 80, 30), sf);
//////////                g.DrawString(_message, Font, msgBrush, new Rectangle(iconX, 50, Width - iconX - 30, Height - 110), sf);
//////////            }

//////////            // Draw Close Button
//////////            if (_hoverClose)
//////////            {
//////////                using (var closeBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
//////////                using (var closePath = GetRoundedPath(_closeRect, 4))
//////////                    g.FillPath(closeBrush, closePath);
//////////            }
//////////            using (var crossPen = new Pen(_subTextColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
//////////            {
//////////                g.DrawLine(crossPen, _closeRect.Left + 8, _closeRect.Top + 8, _closeRect.Right - 8, _closeRect.Bottom - 8);
//////////                g.DrawLine(crossPen, _closeRect.Right - 8, _closeRect.Top + 8, _closeRect.Left + 8, _closeRect.Bottom - 8);
//////////            }

//////////            // Draw Action Buttons
//////////            for (int i = 0; i < _buttonResults.Count; i++)
//////////            {
//////////                bool isHover = (_hoveredButton == i && _pressedButton == -1);
//////////                bool isPressed = (_pressedButton == i);
//////////                bool isPrimary = (i == _buttonResults.Count - 1);

//////////                Rectangle btnRect = _buttonRects[i];
//////////                Color btnBg = Color.Empty;
//////////                Color btnText = _textColor;

//////////                if (isPrimary)
//////////                {
//////////                    btnBg = _accentColor;
//////////                    if (isHover) btnBg = ControlPaint.Light(_accentColor, 0.1f);
//////////                    else if (isPressed) btnBg = ControlPaint.Dark(_accentColor, 0.1f);
//////////                    btnText = Color.White;
//////////                }
//////////                else
//////////                {
//////////                    btnBg = Color.FromArgb(45, 45, 50);
//////////                    if (isHover) btnBg = Color.FromArgb(55, 55, 60);
//////////                    else if (isPressed) btnBg = Color.FromArgb(35, 35, 40);
//////////                }

//////////                using (var btnPath = GetRoundedPath(btnRect, 6))
//////////                using (var btnBrush = new SolidBrush(btnBg))
//////////                {
//////////                    g.FillPath(btnBrush, btnPath);
//////////                }

//////////                using (var textBrush = new SolidBrush(btnText))
//////////                using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center })
//////////                {
//////////                    g.DrawString(_buttonResults[i].ToString(), Font, textBrush, btnRect, sf);
//////////                }
//////////            }
//////////        }

//////////        private void DrawModernIcon(Graphics g, int x, int y, ModernMessageIcon icon)
//////////        {
//////////            Color iconColor = Color.White;
//////////            using (var pen = new Pen(iconColor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
//////////            using (var brush = new SolidBrush(iconColor))
//////////            {
//////////                if (icon == ModernMessageIcon.Info)
//////////                {
//////////                    iconColor = Color.FromArgb(0, 180, 255);
//////////                    pen.Color = iconColor; brush.Color = iconColor;
//////////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
//////////                    g.FillEllipse(brush, new Rectangle(x + 15, y + 8, 6, 6));
//////////                    g.DrawLine(pen, x + 18, y + 16, x + 18, y + 26);
//////////                }
//////////                else if (icon == ModernMessageIcon.Warning)
//////////                {
//////////                    iconColor = Color.FromArgb(255, 160, 0);
//////////                    pen.Color = iconColor; brush.Color = iconColor;
//////////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
//////////                    g.DrawLine(pen, x + 18, y + 8, x + 18, y + 22);
//////////                    g.FillEllipse(brush, new Rectangle(x + 15, y + 26, 6, 6));
//////////                }
//////////                else if (icon == ModernMessageIcon.Error)
//////////                {
//////////                    iconColor = Color.FromArgb(255, 50, 50);
//////////                    pen.Color = iconColor; brush.Color = iconColor;
//////////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
//////////                    g.DrawLine(pen, x + 10, y + 10, x + 26, y + 26);
//////////                    g.DrawLine(pen, x + 26, y + 10, x + 10, y + 26);
//////////                }
//////////                else if (icon == ModernMessageIcon.Success)
//////////                {
//////////                    iconColor = Color.FromArgb(0, 220, 100);
//////////                    pen.Color = iconColor; brush.Color = iconColor;
//////////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
//////////                    g.DrawLine(pen, x + 10, y + 18, x + 16, y + 24);
//////////                    g.DrawLine(pen, x + 16, y + 24, x + 28, y + 12);
//////////                }
//////////            }
//////////        }

//////////        protected override void OnMouseMove(MouseEventArgs e)
//////////        {
//////////            base.OnMouseMove(e);
//////////            int newHover = -1;
//////////            for (int i = 0; i < _buttonRects.Count; i++) if (_buttonRects[i].Contains(e.Location)) newHover = i;

//////////            bool newHoverClose = _closeRect.Contains(e.Location);

//////////            if (newHover != _hoveredButton || newHoverClose != _hoverClose)
//////////            {
//////////                _hoveredButton = newHover;
//////////                _hoverClose = newHoverClose;
//////////                Cursor = (newHover != -1 || _hoverClose) ? Cursors.Hand : Cursors.Default;
//////////                Invalidate();
//////////            }
//////////        }

//////////        protected override void OnMouseDown(MouseEventArgs e)
//////////        {
//////////            base.OnMouseDown(e);
//////////            _pressedButton = -1;
//////////            for (int i = 0; i < _buttonRects.Count; i++) if (_buttonRects[i].Contains(e.Location)) _pressedButton = i;
//////////            Invalidate();
//////////        }

//////////        protected override void OnMouseUp(MouseEventArgs e)
//////////        {
//////////            base.OnMouseUp(e);
//////////            if (_pressedButton != -1 && _buttonRects[_pressedButton].Contains(e.Location))
//////////            {
//////////                _result = _buttonResults[_pressedButton];
//////////                DialogResult = DialogResult.OK; // Internal form result to close
//////////            }
//////////            else if (_closeRect.Contains(e.Location))
//////////            {
//////////                _result = ModernDialogResult.Cancel;
//////////                DialogResult = DialogResult.Cancel;
//////////            }
//////////            _pressedButton = -1;
//////////            Invalidate();
//////////        }

//////////        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
//////////        {
//////////            var path = new GraphicsPath();
//////////            int r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
//////////            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
//////////            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
//////////            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
//////////            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
//////////            path.CloseFigure();
//////////            return path;
//////////        }
//////////    }

//////////    /// <summary>
//////////    /// A modern, animated toaster notification system.
//////////    /// </summary>
//////////    public static class ModernToaster
//////////    {
//////////        private static List<ToasterForm> _activeToasters = new List<ToasterForm>();

//////////        public static void Show(string title, string message, ToasterType type = ToasterType.Info, int duration = 3000)
//////////        {
//////////            if (Application.OpenForms.Count == 0) return;

//////////            var mainForm = Application.OpenForms[0];
//////////            if (mainForm.InvokeRequired)
//////////            {
//////////                mainForm.BeginInvoke((MethodInvoker)delegate { Show(title, message, type, duration); });
//////////                return;
//////////            }

//////////            var toaster = new ToasterForm(title, message, type, duration);

//////////            // Stack toasters vertically in bottom right
//////////            int yOffset = 20;
//////////            foreach (var t in _activeToasters)
//////////            {
//////////                if (t.Screen == Screen.FromControl(mainForm))
//////////                    yOffset += t.Height + 10;
//////////            }

//////////            toaster.StartPosition = FormStartPosition.Manual;
//////////            var screen = Screen.FromControl(mainForm).WorkingArea;
//////////            toaster.Location = new Point(screen.Right - toaster.Width - 20, screen.Bottom - toaster.Height - yOffset);

//////////            _activeToasters.Add(toaster);
//////////            toaster.FormClosed += (s, e) => { _activeToasters.Remove(toaster); };

//////////            toaster.Show(mainForm);
//////////        }
//////////    }

//////////    public class ToasterForm : Form
//////////    {
//////////        private Color _backColor = Color.FromArgb(32, 32, 38);
//////////        private Color _borderColor = Color.FromArgb(0, 120, 212);
//////////        private Color _textColor = Color.FromArgb(240, 240, 240);
//////////        private Color _subTextColor = Color.FromArgb(160, 160, 160);

//////////        private string _title;
//////////        private string _message;
//////////        private ToasterType _type;
//////////        private int _duration;
//////////        private System.Windows.Forms.Timer _timer;
//////////        private float _progress = 1f;
//////////        private Rectangle _closeRect;
//////////        private bool _hoverClose = false;
//////////        private float _opacity = 0f;
//////////        private int _targetX;

//////////        public ToasterForm(string title, string message, ToasterType type, int duration)
//////////        {
//////////            _title = title;
//////////            _message = message;
//////////            _type = type;
//////////            _duration = duration;

//////////            switch (_type)
//////////            {
//////////                case ToasterType.Info: _borderColor = Color.FromArgb(0, 180, 255); break;
//////////                case ToasterType.Success: _borderColor = Color.FromArgb(0, 220, 100); break;
//////////                case ToasterType.Warning: _borderColor = Color.FromArgb(255, 160, 0); break;
//////////                case ToasterType.Error: _borderColor = Color.FromArgb(255, 50, 50); break;
//////////            }

//////////            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
//////////            FormBorderStyle = FormBorderStyle.None;
//////////            ShowInTaskbar = false;
//////////            TopMost = true;
//////////            BackColor = Color.Magenta;
//////////            TransparencyKey = Color.Magenta;
//////////            Font = new Font("Segoe UI Variable Display", 9f);
//////////            Size = new Size(350, 90);
//////////            Opacity = 0;

//////////            _timer = new System.Windows.Forms.Timer();
//////////            _timer.Interval = 16; // 60fps
//////////            _timer.Tick += Timer_Tick;
//////////            _timer.Start();
//////////        }

//////////        public Screen Screen { get; set; }

//////////        private void Timer_Tick(object sender, EventArgs e)
//////////        {
//////////            if (IsDisposed) return;

//////////            if (_duration > 0)
//////////            {
//////////                _duration -= 16;
//////////                _progress = (float)_duration / 3000f;
//////////                if (_progress < 0) _progress = 0;
//////////            }

//////////            // Slide In Animation
//////////            if (_opacity < 1f)
//////////            {
//////////                _opacity += 0.1f;
//////////                if (_opacity > 1f) _opacity = 1f;
//////////                Opacity = _opacity;
//////////            }

//////////            if (_duration <= 0 && !IsDisposed)
//////////            {
//////////                _opacity -= 0.1f;
//////////                Opacity = _opacity;
//////////                if (_opacity <= 0f)
//////////                {
//////////                    _timer.Stop();
//////////                    this.Close();
//////////                }
//////////            }

//////////            Invalidate();
//////////        }

//////////        protected override void OnPaint(PaintEventArgs e)
//////////        {
//////////            Graphics g = e.Graphics;
//////////            g.SmoothingMode = SmoothingMode.AntiAlias;
//////////            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

//////////            using (var bgPath = GetRoundedPath(ClientRectangle, 8))
//////////            using (var bgBrush = new SolidBrush(_backColor))
//////////            using (var borderPen = new Pen(_borderColor, 2))
//////////            {
//////////                g.FillPath(bgBrush, bgPath);
//////////                g.DrawPath(borderPen, bgPath);
//////////            }

//////////            // Icon
//////////            int iconX = 16, iconY = 20;
//////////            using (var pen = new Pen(_borderColor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
//////////            using (var brush = new SolidBrush(_borderColor))
//////////            {
//////////                if (_type == ToasterType.Info)
//////////                {
//////////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
//////////                    g.FillEllipse(brush, new Rectangle(iconX + 13, iconY + 6, 6, 6));
//////////                    g.DrawLine(pen, iconX + 16, iconY + 14, iconX + 16, iconY + 24);
//////////                }
//////////                else if (_type == ToasterType.Success)
//////////                {
//////////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
//////////                    g.DrawLine(pen, iconX + 8, iconY + 16, iconX + 14, iconY + 22);
//////////                    g.DrawLine(pen, iconX + 14, iconY + 22, iconX + 26, iconY + 10);
//////////                }
//////////                else if (_type == ToasterType.Warning)
//////////                {
//////////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
//////////                    g.DrawLine(pen, iconX + 16, iconY + 6, iconX + 16, iconY + 20);
//////////                    g.FillEllipse(brush, new Rectangle(iconX + 13, iconY + 24, 6, 6));
//////////                }
//////////                else if (_type == ToasterType.Error)
//////////                {
//////////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
//////////                    g.DrawLine(pen, iconX + 9, iconY + 9, iconX + 23, iconY + 23);
//////////                    g.DrawLine(pen, iconX + 23, iconY + 9, iconX + 9, iconY + 23);
//////////                }
//////////            }

//////////            // Text
//////////            using (var titleFont = new Font(Font, FontStyle.Bold))
//////////            using (var titleBrush = new SolidBrush(_textColor))
//////////            using (var msgBrush = new SolidBrush(_subTextColor))
//////////            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
//////////            {
//////////                g.DrawString(_title, titleFont, titleBrush, new Rectangle(60, 18, Width - 90, 25), sf);
//////////                g.DrawString(_message, Font, msgBrush, new Rectangle(60, 45, Width - 90, 40), sf);
//////////            }

//////////            // Close
//////////            _closeRect = new Rectangle(Width - 32, 12, 20, 20);
//////////            if (_hoverClose) { using (var b = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) using (var p = GetRoundedPath(_closeRect, 4)) g.FillPath(b, p); }
//////////            using (var crossPen = new Pen(_subTextColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
//////////            {
//////////                g.DrawLine(crossPen, _closeRect.Left + 5, _closeRect.Top + 5, _closeRect.Right - 5, _closeRect.Bottom - 5);
//////////                g.DrawLine(crossPen, _closeRect.Right - 5, _closeRect.Top + 5, _closeRect.Left + 5, _closeRect.Bottom - 5);
//////////            }

//////////            // Progress Bar
//////////            if (_duration > 0)
//////////            {
//////////                Rectangle progRect = new Rectangle(2, Height - 4, Width - 4, 4);
//////////                int progWidth = (int)((progRect.Width) * _progress);
//////////                using (var progBrush = new SolidBrush(_borderColor))
//////////                {
//////////                    g.FillPath(progBrush, GetRoundedPath(new Rectangle(progRect.X, progRect.Y, progWidth, progRect.Height), 2));
//////////                }
//////////            }
//////////        }

//////////        protected override void OnMouseMove(MouseEventArgs e)
//////////        {
//////////            base.OnMouseMove(e);
//////////            bool newHover = _closeRect.Contains(e.Location);
//////////            if (newHover != _hoverClose)
//////////            {
//////////                _hoverClose = newHover;
//////////                Cursor = _hoverClose ? Cursors.Hand : Cursors.Default;
//////////                Invalidate();
//////////            }
//////////        }

//////////        protected override void OnMouseUp(MouseEventArgs e)
//////////        {
//////////            base.OnMouseUp(e);
//////////            if (_closeRect.Contains(e.Location)) { _duration = 0; _timer.Stop(); this.Close(); }
//////////        }

//////////        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
//////////        {
//////////            var path = new GraphicsPath();
//////////            int r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
//////////            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
//////////            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
//////////            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
//////////            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
//////////            path.CloseFigure();
//////////            return path;
//////////        }
//////////    }
//////////}



















//////////============================== VER 1.1 ================================


////////using System;
////////using System.Collections.Generic;
////////using System.Drawing;
////////using System.Drawing.Drawing2D;
////////using System.Drawing.Text;
////////using System.Threading;
////////using System.Windows.Forms;

////////namespace UltraModernUI.Controls
////////{
////////    #region Enums
////////    public enum ModernMessageIcon { None, Info, Warning, Error, Success, Question }
////////    public enum ModernDialogResult { None, OK, Cancel, Yes, No, Abort, Retry, Ignore }
////////    public enum ToasterType { Info, Success, Warning, Error }
////////    #endregion

////////    /// <summary>
////////    /// A modern, high-fidelity replacement for the standard Windows MessageBox.
////////    /// </summary>
////////    public class ModernMessageBox : Form
////////    {
////////        private Color _backColor = Color.FromArgb(32, 32, 38);
////////        private Color _accentColor = Color.FromArgb(0, 120, 212);
////////        private Color _textColor = Color.FromArgb(240, 240, 240);
////////        private Color _subTextColor = Color.FromArgb(160, 160, 160);

////////        private string _title;
////////        private string _message;
////////        private ModernMessageIcon _icon;
////////        private MessageBoxButtons _buttons;
////////        private List<ModernDialogResult> _buttonResults = new List<ModernDialogResult>();
////////        private List<Rectangle> _buttonRects = new List<Rectangle>();
////////        private int _hoveredButton = -1;
////////        private int _pressedButton = -1;
////////        private Rectangle _closeRect;
////////        private bool _hoverClose = false;

////////        public ModernMessageBox(string message, string title, MessageBoxButtons buttons, ModernMessageIcon icon)
////////        {
////////            _title = title;
////////            _message = message;
////////            _buttons = buttons;
////////            _icon = icon;

////////            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
////////            FormBorderStyle = FormBorderStyle.None;
////////            StartPosition = FormStartPosition.CenterParent;
////////            BackColor = Color.Magenta;
////////            TransparencyKey = Color.Magenta;
////////            TopMost = true;
////////            ShowInTaskbar = false;
////////            Font = new Font("Segoe UI Variable Display", 9f);
////////        }

////////        public static ModernDialogResult Show(string message, string title = "Message", MessageBoxButtons buttons = MessageBoxButtons.OK, ModernMessageIcon icon = ModernMessageIcon.None)
////////        {
////////            using (var box = new ModernMessageBox(message, title, buttons, icon))
////////            {
////////                box.CalculateSize();
////////                if (box.ShowDialog() == DialogResult.OK)
////////                    return box._result;
////////                return ModernDialogResult.None;
////////            }
////////        }

////////        private ModernDialogResult _result = ModernDialogResult.None;
////////        private void CalculateSize()
////////        {
////////            using (var g = Graphics.FromHwnd(IntPtr.Zero))
////////            {
////////                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
////////                SizeF titleSize = g.MeasureString(_title, new Font(Font, FontStyle.Bold), 500);
////////                SizeF msgSize = g.MeasureString(_message, Font, 400);

////////                int width = Math.Max(400, (int)Math.Max(titleSize.Width, msgSize.Width) + 80);
////////                int height = (int)msgSize.Height + 120;

////////                if (_icon != ModernMessageIcon.None) width += 60;

////////                this.Size = new Size(width, height);

////////                // Setup Buttons
////////                _buttonResults.Clear();
////////                switch (_buttons)
////////                {
////////                    case MessageBoxButtons.OK: _buttonResults.Add(ModernDialogResult.OK); break;
////////                    case MessageBoxButtons.OKCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.OK); break;
////////                    case MessageBoxButtons.YesNo: _buttonResults.Add(ModernDialogResult.No); _buttonResults.Add(ModernDialogResult.Yes); break;
////////                    case MessageBoxButtons.YesNoCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.No); _buttonResults.Add(ModernDialogResult.Yes); break;
////////                    case MessageBoxButtons.RetryCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.Retry); break;
////////                    case MessageBoxButtons.AbortRetryIgnore: _buttonResults.Add(ModernDialogResult.Ignore); _buttonResults.Add(ModernDialogResult.Retry); _buttonResults.Add(ModernDialogResult.Abort); break;
////////                }

////////                _buttonRects.Clear();
////////                int btnWidth = 100;
////////                int btnHeight = 36;
////////                int spacing = 12;
////////                int totalBtnWidth = (_buttonResults.Count * btnWidth) + ((_buttonResults.Count - 1) * spacing);
////////                int startX = width - totalBtnWidth - 20;

////////                for (int i = 0; i < _buttonResults.Count; i++)
////////                {
////////                    _buttonRects.Add(new Rectangle(startX + (i * (btnWidth + spacing)), height - btnHeight - 20, btnWidth, btnHeight));
////////                }

////////                _closeRect = new Rectangle(width - 40, 12, 28, 28);
////////            }
////////        }

////////        // Allows dragging the borderless form
////////        protected override void WndProc(ref Message m)
////////        {
////////            base.WndProc(ref m);
////////            if (m.Msg == 0x84) // WM_NCHITTEST
////////            {
////////                // Get the mouse position
////////                int x = (int)m.LParam & 0xFFFF;
////////                int y = (int)m.LParam >> 16;
////////                Point pos = PointToClient(new Point(x, y));

////////                // If the mouse is not over a button or the close icon, allow dragging
////////                foreach (var btn in _buttonRects) if (btn.Contains(pos)) return;
////////                if (_closeRect.Contains(pos)) return;

////////                m.Result = (IntPtr)0x2; // HTCAPTION
////////            }
////////        }

////////        protected override void OnPaint(PaintEventArgs e)
////////        {
////////            Graphics g = e.Graphics;
////////            g.SmoothingMode = SmoothingMode.AntiAlias;
////////            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

////////            using (var bgPath = GetRoundedPath(ClientRectangle, 8))
////////            using (var bgBrush = new SolidBrush(_backColor))
////////            using (var borderPen = new Pen(Color.FromArgb(40, 255, 255, 255), 1))
////////            {
////////                g.FillPath(bgBrush, bgPath);
////////                g.DrawPath(borderPen, bgPath);
////////            }

////////            // Draw Icon
////////            int iconX = 24;
////////            int iconY = 60;
////////            if (_icon != ModernMessageIcon.None)
////////            {
////////                DrawModernIcon(g, iconX, iconY, _icon);
////////                iconX += 50;
////////            }

////////            // Draw Title
////////            using (var titleFont = new Font(Font, FontStyle.Bold))
////////            using (var titleBrush = new SolidBrush(_textColor))
////////            using (var msgBrush = new SolidBrush(_subTextColor))
////////            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
////////            {
////////                g.DrawString(_title, titleFont, titleBrush, new Rectangle(24, 16, Width - 80, 30), sf);
////////                g.DrawString(_message, Font, msgBrush, new Rectangle(iconX, 50, Width - iconX - 30, Height - 110), sf);
////////            }

////////            // Draw Close Button
////////            if (_hoverClose)
////////            {
////////                using (var closeBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
////////                using (var closePath = GetRoundedPath(_closeRect, 4))
////////                    g.FillPath(closeBrush, closePath);
////////            }
////////            using (var crossPen = new Pen(_subTextColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
////////            {
////////                g.DrawLine(crossPen, _closeRect.Left + 8, _closeRect.Top + 8, _closeRect.Right - 8, _closeRect.Bottom - 8);
////////                g.DrawLine(crossPen, _closeRect.Right - 8, _closeRect.Top + 8, _closeRect.Left + 8, _closeRect.Bottom - 8);
////////            }

////////            // Draw Action Buttons
////////            for (int i = 0; i < _buttonResults.Count; i++)
////////            {
////////                bool isHover = (_hoveredButton == i && _pressedButton == -1);
////////                bool isPressed = (_pressedButton == i);
////////                bool isPrimary = (i == _buttonResults.Count - 1);

////////                Rectangle btnRect = _buttonRects[i];
////////                Color btnBg = Color.Empty;
////////                Color btnText = _textColor;

////////                if (isPrimary)
////////                {
////////                    btnBg = _accentColor;
////////                    if (isHover) btnBg = ControlPaint.Light(_accentColor, 0.1f);
////////                    else if (isPressed) btnBg = ControlPaint.Dark(_accentColor, 0.1f);
////////                    btnText = Color.White;
////////                }
////////                else
////////                {
////////                    btnBg = Color.FromArgb(45, 45, 50);
////////                    if (isHover) btnBg = Color.FromArgb(55, 55, 60);
////////                    else if (isPressed) btnBg = Color.FromArgb(35, 35, 40);
////////                }

////////                using (var btnPath = GetRoundedPath(btnRect, 6))
////////                using (var btnBrush = new SolidBrush(btnBg))
////////                {
////////                    g.FillPath(btnBrush, btnPath);
////////                }

////////                using (var textBrush = new SolidBrush(btnText))
////////                using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center })
////////                {
////////                    g.DrawString(_buttonResults[i].ToString(), Font, textBrush, btnRect, sf);
////////                }
////////            }
////////        }

////////        private void DrawModernIcon(Graphics g, int x, int y, ModernMessageIcon icon)
////////        {
////////            Color iconColor = Color.White;
////////            using (var pen = new Pen(iconColor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
////////            using (var brush = new SolidBrush(iconColor))
////////            {
////////                if (icon == ModernMessageIcon.Info)
////////                {
////////                    iconColor = Color.FromArgb(0, 180, 255);
////////                    pen.Color = iconColor; brush.Color = iconColor;
////////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
////////                    g.FillEllipse(brush, new Rectangle(x + 15, y + 8, 6, 6));
////////                    g.DrawLine(pen, x + 18, y + 16, x + 18, y + 26);
////////                }
////////                else if (icon == ModernMessageIcon.Warning)
////////                {
////////                    iconColor = Color.FromArgb(255, 160, 0);
////////                    pen.Color = iconColor; brush.Color = iconColor;
////////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
////////                    g.DrawLine(pen, x + 18, y + 8, x + 18, y + 22);
////////                    g.FillEllipse(brush, new Rectangle(x + 15, y + 26, 6, 6));
////////                }
////////                else if (icon == ModernMessageIcon.Error)
////////                {
////////                    iconColor = Color.FromArgb(255, 50, 50);
////////                    pen.Color = iconColor; brush.Color = iconColor;
////////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
////////                    g.DrawLine(pen, x + 10, y + 10, x + 26, y + 26);
////////                    g.DrawLine(pen, x + 26, y + 10, x + 10, y + 26);
////////                }
////////                else if (icon == ModernMessageIcon.Success)
////////                {
////////                    iconColor = Color.FromArgb(0, 220, 100);
////////                    pen.Color = iconColor; brush.Color = iconColor;
////////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
////////                    g.DrawLine(pen, x + 10, y + 18, x + 16, y + 24);
////////                    g.DrawLine(pen, x + 16, y + 24, x + 28, y + 12);
////////                }
////////            }
////////        }

////////        protected override void OnMouseMove(MouseEventArgs e)
////////        {
////////            base.OnMouseMove(e);
////////            int newHover = -1;
////////            for (int i = 0; i < _buttonRects.Count; i++) if (_buttonRects[i].Contains(e.Location)) newHover = i;

////////            bool newHoverClose = _closeRect.Contains(e.Location);

////////            if (newHover != _hoveredButton || newHoverClose != _hoverClose)
////////            {
////////                _hoveredButton = newHover;
////////                _hoverClose = newHoverClose;
////////                Cursor = (newHover != -1 || _hoverClose) ? Cursors.Hand : Cursors.Default;
////////                Invalidate();
////////            }
////////        }

////////        protected override void OnMouseDown(MouseEventArgs e)
////////        {
////////            base.OnMouseDown(e);
////////            _pressedButton = -1;
////////            for (int i = 0; i < _buttonRects.Count; i++) if (_buttonRects[i].Contains(e.Location)) _pressedButton = i;
////////            Invalidate();
////////        }

////////        protected override void OnMouseUp(MouseEventArgs e)
////////        {
////////            base.OnMouseUp(e);
////////            if (_pressedButton != -1 && _buttonRects[_pressedButton].Contains(e.Location))
////////            {
////////                _result = _buttonResults[_pressedButton];
////////                DialogResult = DialogResult.OK;
////////            }
////////            else if (_closeRect.Contains(e.Location))
////////            {
////////                _result = ModernDialogResult.Cancel;
////////                DialogResult = DialogResult.Cancel;
////////            }
////////            _pressedButton = -1;
////////            Invalidate();
////////        }

////////        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
////////        {
////////            var path = new GraphicsPath();
////////            if (rect.Width <= 0 || rect.Height <= 0) return path; // CRASH FIX
////////            int r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
////////            if (r <= 0) { path.AddRectangle(rect); return path; }
////////            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
////////            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
////////            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
////////            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
////////            path.CloseFigure();
////////            return path;
////////        }
////////    }

////////    /// <summary>
////////    /// A modern, animated toaster notification system.
////////    /// </summary>
////////    public static class ModernToaster
////////    {
////////        private static List<ToasterForm> _activeToasters = new List<ToasterForm>();

////////        public static void Show(string title, string message, ToasterType type = ToasterType.Info, int duration = 3000)
////////        {
////////            if (Application.OpenForms.Count == 0) return;

////////            var mainForm = Application.OpenForms[0];
////////            if (mainForm.InvokeRequired)
////////            {
////////                mainForm.BeginInvoke((MethodInvoker)delegate { Show(title, message, type, duration); });
////////                return;
////////            }

////////            var toaster = new ToasterForm(title, message, type, duration);

////////            int yOffset = 20;
////////            foreach (var t in _activeToasters)
////////            {
////////                if (t.Screen == Screen.FromControl(mainForm))
////////                    yOffset += t.Height + 10;
////////            }

////////            toaster.StartPosition = FormStartPosition.Manual;
////////            var screen = Screen.FromControl(mainForm).WorkingArea;
////////            toaster.Location = new Point(screen.Right - toaster.Width - 20, screen.Bottom - toaster.Height - yOffset);

////////            _activeToasters.Add(toaster);
////////            toaster.FormClosed += (s, e) => { _activeToasters.Remove(toaster); };

////////            toaster.Show(mainForm);
////////        }
////////    }

////////    public class ToasterForm : Form
////////    {
////////        private Color _backColor = Color.FromArgb(32, 32, 38);
////////        private Color _borderColor = Color.FromArgb(0, 120, 212);
////////        private Color _textColor = Color.FromArgb(240, 240, 240);
////////        private Color _subTextColor = Color.FromArgb(160, 160, 160);

////////        private string _title;
////////        private string _message;
////////        private ToasterType _type;
////////        private int _duration;
////////        private System.Windows.Forms.Timer _timer;
////////        private float _progress = 1f;
////////        private Rectangle _closeRect;
////////        private bool _hoverClose = false;
////////        private float _opacity = 0f;

////////        public ToasterForm(string title, string message, ToasterType type, int duration)
////////        {
////////            _title = title;
////////            _message = message;
////////            _type = type;
////////            _duration = duration;

////////            switch (_type)
////////            {
////////                case ToasterType.Info: _borderColor = Color.FromArgb(0, 180, 255); break;
////////                case ToasterType.Success: _borderColor = Color.FromArgb(0, 220, 100); break;
////////                case ToasterType.Warning: _borderColor = Color.FromArgb(255, 160, 0); break;
////////                case ToasterType.Error: _borderColor = Color.FromArgb(255, 50, 50); break;
////////            }

////////            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
////////            FormBorderStyle = FormBorderStyle.None;
////////            ShowInTaskbar = false;
////////            TopMost = true;
////////            BackColor = Color.Magenta;
////////            TransparencyKey = Color.Magenta;
////////            Font = new Font("Segoe UI Variable Display", 9f);
////////            Size = new Size(350, 90);
////////            Opacity = 0;

////////            _timer = new System.Windows.Forms.Timer();
////////            _timer.Interval = 16;
////////            _timer.Tick += Timer_Tick;
////////            _timer.Start();
////////        }

////////        public Screen Screen { get; set; }

////////        private void Timer_Tick(object sender, EventArgs e)
////////        {
////////            if (IsDisposed) return;

////////            if (_duration > 0)
////////            {
////////                _duration -= 16;
////////                _progress = (float)_duration / 3000f;
////////                if (_progress < 0) _progress = 0;
////////            }

////////            if (_opacity < 1f)
////////            {
////////                _opacity += 0.1f;
////////                if (_opacity > 1f) _opacity = 1f;
////////                Opacity = _opacity;
////////            }

////////            if (_duration <= 0 && !IsDisposed)
////////            {
////////                _opacity -= 0.1f;
////////                Opacity = _opacity;
////////                if (_opacity <= 0f)
////////                {
////////                    _timer.Stop();
////////                    this.Close();
////////                }
////////            }

////////            Invalidate();
////////        }

////////        protected override void OnPaint(PaintEventArgs e)
////////        {
////////            Graphics g = e.Graphics;
////////            g.SmoothingMode = SmoothingMode.AntiAlias;
////////            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

////////            using (var bgPath = GetRoundedPath(ClientRectangle, 8))
////////            using (var bgBrush = new SolidBrush(_backColor))
////////            using (var borderPen = new Pen(_borderColor, 2))
////////            {
////////                g.FillPath(bgBrush, bgPath);
////////                g.DrawPath(borderPen, bgPath);
////////            }

////////            int iconX = 16, iconY = 20;
////////            using (var pen = new Pen(_borderColor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
////////            using (var brush = new SolidBrush(_borderColor))
////////            {
////////                if (_type == ToasterType.Info)
////////                {
////////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
////////                    g.FillEllipse(brush, new Rectangle(iconX + 13, iconY + 6, 6, 6));
////////                    g.DrawLine(pen, iconX + 16, iconY + 14, iconX + 16, iconY + 24);
////////                }
////////                else if (_type == ToasterType.Success)
////////                {
////////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
////////                    g.DrawLine(pen, iconX + 8, iconY + 16, iconX + 14, iconY + 22);
////////                    g.DrawLine(pen, iconX + 14, iconY + 22, iconX + 26, iconY + 10);
////////                }
////////                else if (_type == ToasterType.Warning)
////////                {
////////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
////////                    g.DrawLine(pen, iconX + 16, iconY + 6, iconX + 16, iconY + 20);
////////                    g.FillEllipse(brush, new Rectangle(iconX + 13, iconY + 24, 6, 6));
////////                }
////////                else if (_type == ToasterType.Error)
////////                {
////////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
////////                    g.DrawLine(pen, iconX + 9, iconY + 9, iconX + 23, iconY + 23);
////////                    g.DrawLine(pen, iconX + 23, iconY + 9, iconX + 9, iconY + 23);
////////                }
////////            }

////////            using (var titleFont = new Font(Font, FontStyle.Bold))
////////            using (var titleBrush = new SolidBrush(_textColor))
////////            using (var msgBrush = new SolidBrush(_subTextColor))
////////            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
////////            {
////////                g.DrawString(_title, titleFont, titleBrush, new Rectangle(60, 18, Width - 90, 25), sf);
////////                g.DrawString(_message, Font, msgBrush, new Rectangle(60, 45, Width - 90, 40), sf);
////////            }

////////            _closeRect = new Rectangle(Width - 32, 12, 20, 20);
////////            if (_hoverClose) { using (var b = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) using (var p = GetRoundedPath(_closeRect, 4)) g.FillPath(b, p); }
////////            using (var crossPen = new Pen(_subTextColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
////////            {
////////                g.DrawLine(crossPen, _closeRect.Left + 5, _closeRect.Top + 5, _closeRect.Right - 5, _closeRect.Bottom - 5);
////////                g.DrawLine(crossPen, _closeRect.Right - 5, _closeRect.Top + 5, _closeRect.Left + 5, _closeRect.Bottom - 5);
////////            }

////////            if (_duration > 0)
////////            {
////////                Rectangle progRect = new Rectangle(2, Height - 4, Width - 4, 4);
////////                int progWidth = Math.Max(0, (int)(progRect.Width * _progress)); // CRASH FIX
////////                using (var progBrush = new SolidBrush(_borderColor))
////////                {
////////                    g.FillPath(progBrush, GetRoundedPath(new Rectangle(progRect.X, progRect.Y, progWidth, progRect.Height), 2));
////////                }
////////            }
////////        }

////////        protected override void OnMouseMove(MouseEventArgs e)
////////        {
////////            base.OnMouseMove(e);
////////            bool newHover = _closeRect.Contains(e.Location);
////////            if (newHover != _hoverClose)
////////            {
////////                _hoverClose = newHover;
////////                Cursor = _hoverClose ? Cursors.Hand : Cursors.Default;
////////                Invalidate();
////////            }
////////        }

////////        protected override void OnMouseUp(MouseEventArgs e)
////////        {
////////            base.OnMouseUp(e);
////////            if (_closeRect.Contains(e.Location)) { _duration = 0; _timer.Stop(); this.Close(); }
////////        }

////////        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
////////        {
////////            var path = new GraphicsPath();
////////            if (rect.Width <= 0 || rect.Height <= 0) return path; // CRASH FIX
////////            int r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
////////            if (r <= 0) { path.AddRectangle(rect); return path; }
////////            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
////////            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
////////            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
////////            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
////////            path.CloseFigure();
////////            return path;
////////        }
////////    }
////////}















////////=============================== VER 1.3 =====================

////////using System;
////////using System.Collections.Generic;
////////using System.Drawing;
////////using System.Drawing.Drawing2D;
////////using System.Drawing.Text;
////////using System.Runtime.InteropServices;
////////using System.Windows.Forms;

////////namespace UltraModernUI.Controls
////////{
////////    #region Enums & Config
////////    public enum ModernMessageIcon { None, Info, Warning, Error, Success, Question }
////////    public enum ModernDialogResult { None, OK, Cancel, Yes, No, Abort, Retry, Ignore }
////////    public enum ToasterType { Info, Success, Warning, Error }

////////    /// <summary>
////////    /// Global configuration for ModernMessageBox and ModernToaster.
////////    /// Modify this to match your app's theme!
////////    /// </summary>
////////    public static class ModernNotificationConfig
////////    {
////////        public static Color BackColor { get; set; } = Color.FromArgb(32, 32, 38);
////////        public static Color AccentColor { get; set; } = Color.FromArgb(0, 120, 212);
////////        public static Color TextColor { get; set; } = Color.FromArgb(240, 240, 240);
////////        public static Color SubTextColor { get; set; } = Color.FromArgb(160, 160, 160);

////////        // Set to true if you want the hard 1px border back
////////        public static bool ShowBorder { get; set; } = false;
////////        public static Color BorderColor { get; set; } = Color.FromArgb(45, 45, 50);
////////    }
////////    #endregion

////////    /// <summary>
////////    /// A modern, high-fidelity replacement for the standard Windows MessageBox.
////////    /// </summary>
////////    public class ModernMessageBox : Form
////////    {
////////        private Color _backColor = ModernNotificationConfig.BackColor;
////////        private Color _accentColor = ModernNotificationConfig.AccentColor;
////////        private Color _textColor = ModernNotificationConfig.TextColor;
////////        private Color _subTextColor = ModernNotificationConfig.SubTextColor;
////////        private Color _borderColor = ModernNotificationConfig.BorderColor;

////////        private string _title;
////////        private string _message;
////////        private ModernMessageIcon _icon;
////////        private MessageBoxButtons _buttons;
////////        private List<ModernDialogResult> _buttonResults = new List<ModernDialogResult>();
////////        private List<Rectangle> _buttonRects = new List<Rectangle>();
////////        private int _hoveredButton = -1;
////////        private int _pressedButton = -1;
////////        private Rectangle _closeRect;
////////        private bool _hoverClose = false;

////////        public ModernMessageBox(string message, string title, MessageBoxButtons buttons, ModernMessageIcon icon)
////////        {
////////            _title = title;
////////            _message = message;
////////            _buttons = buttons;
////////            _icon = icon;

////////            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
////////            FormBorderStyle = FormBorderStyle.None;
////////            StartPosition = FormStartPosition.CenterParent;
////////            TopMost = true;
////////            ShowInTaskbar = false;
////////            Font = new Font("Segoe UI Variable Display", 9f);

////////            // Enable native Windows drop shadow (removes need for ugly drawn borders)
////////            this.HandleCreated += (s, e) => {
////////                int val = 2;
////////                DwmSetWindowAttribute(this.Handle, 2, ref val, sizeof(int));
////////            };
////////        }

////////        public static ModernDialogResult Show(string message, string title = "Message", MessageBoxButtons buttons = MessageBoxButtons.OK, ModernMessageIcon icon = ModernMessageIcon.None)
////////        {
////////            using (var box = new ModernMessageBox(message, title, buttons, icon))
////////            {
////////                box.CalculateSize();
////////                if (box.ShowDialog() == DialogResult.OK)
////////                    return box._result;
////////                return ModernDialogResult.None;
////////            }
////////        }

////////        private ModernDialogResult _result = ModernDialogResult.None;
////////        private void CalculateSize()
////////        {
////////            using (var g = Graphics.FromHwnd(IntPtr.Zero))
////////            {
////////                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
////////                SizeF titleSize = g.MeasureString(_title, new Font(Font, FontStyle.Bold), 500);
////////                SizeF msgSize = g.MeasureString(_message, Font, 400);

////////                int width = Math.Max(400, (int)Math.Max(titleSize.Width, msgSize.Width) + 80);
////////                int height = (int)msgSize.Height + 120;

////////                if (_icon != ModernMessageIcon.None) width += 60;

////////                this.Size = new Size(width, height);

////////                // Setup Buttons
////////                _buttonResults.Clear();
////////                switch (_buttons)
////////                {
////////                    case MessageBoxButtons.OK: _buttonResults.Add(ModernDialogResult.OK); break;
////////                    case MessageBoxButtons.OKCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.OK); break;
////////                    case MessageBoxButtons.YesNo: _buttonResults.Add(ModernDialogResult.No); _buttonResults.Add(ModernDialogResult.Yes); break;
////////                    case MessageBoxButtons.YesNoCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.No); _buttonResults.Add(ModernDialogResult.Yes); break;
////////                    case MessageBoxButtons.RetryCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.Retry); break;
////////                    case MessageBoxButtons.AbortRetryIgnore: _buttonResults.Add(ModernDialogResult.Ignore); _buttonResults.Add(ModernDialogResult.Retry); _buttonResults.Add(ModernDialogResult.Abort); break;
////////                }

////////                _buttonRects.Clear();
////////                int btnWidth = 100;
////////                int btnHeight = 36;
////////                int spacing = 12;
////////                int totalBtnWidth = (_buttonResults.Count * btnWidth) + ((_buttonResults.Count - 1) * spacing);
////////                int startX = width - totalBtnWidth - 20;

////////                for (int i = 0; i < _buttonResults.Count; i++)
////////                {
////////                    _buttonRects.Add(new Rectangle(startX + (i * (btnWidth + spacing)), height - btnHeight - 20, btnWidth, btnHeight));
////////                }

////////                _closeRect = new Rectangle(width - 40, 12, 28, 28);
////////            }

////////            // Apply rounded region to hide hard square edges (crisp anti-aliasing)
////////            this.Region = new Region(GetRoundedPath(ClientRectangle, 8));
////////        }

////////        protected override void OnResize(EventArgs e)
////////        {
////////            base.OnResize(e);
////////            if (this.Handle != IntPtr.Zero)
////////                this.Region = new Region(GetRoundedPath(ClientRectangle, 8));
////////        }

////////        // Allows dragging the borderless form
////////        protected override void WndProc(ref Message m)
////////        {
////////            base.WndProc(ref m);
////////            if (m.Msg == 0x84) // WM_NCHITTEST
////////            {
////////                int x = (int)m.LParam & 0xFFFF;
////////                int y = (int)m.LParam >> 16;
////////                Point pos = PointToClient(new Point(x, y));

////////                foreach (var btn in _buttonRects) if (btn.Contains(pos)) return;
////////                if (_closeRect.Contains(pos)) return;

////////                m.Result = (IntPtr)0x2; // HTCAPTION
////////            }
////////        }

////////        protected override void OnPaint(PaintEventArgs e)
////////        {
////////            Graphics g = e.Graphics;
////////            g.SmoothingMode = SmoothingMode.AntiAlias;
////////            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

////////            using (var bgBrush = new SolidBrush(_backColor))
////////            using (var bgPath = GetRoundedPath(ClientRectangle, 8))
////////            {
////////                g.FillPath(bgBrush, bgPath);
////////            }

////////            if (ModernNotificationConfig.ShowBorder)
////////            {
////////                using (var borderPen = new Pen(_borderColor, 1))
////////                using (var bgPath = GetRoundedPath(ClientRectangle, 8))
////////                {
////////                    g.DrawPath(borderPen, bgPath);
////////                }
////////            }

////////            int iconX = 24;
////////            int iconY = 60;
////////            if (_icon != ModernMessageIcon.None)
////////            {
////////                DrawModernIcon(g, iconX, iconY, _icon);
////////                iconX += 50;
////////            }

////////            using (var titleFont = new Font(Font, FontStyle.Bold))
////////            using (var titleBrush = new SolidBrush(_textColor))
////////            using (var msgBrush = new SolidBrush(_subTextColor))
////////            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
////////            {
////////                g.DrawString(_title, titleFont, titleBrush, new Rectangle(24, 16, Width - 80, 30), sf);
////////                g.DrawString(_message, Font, msgBrush, new Rectangle(iconX, 50, Width - iconX - 30, Height - 110), sf);
////////            }

////////            if (_hoverClose)
////////            {
////////                using (var closeBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
////////                using (var closePath = GetRoundedPath(_closeRect, 4))
////////                    g.FillPath(closeBrush, closePath);
////////            }
////////            using (var crossPen = new Pen(_subTextColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
////////            {
////////                g.DrawLine(crossPen, _closeRect.Left + 8, _closeRect.Top + 8, _closeRect.Right - 8, _closeRect.Bottom - 8);
////////                g.DrawLine(crossPen, _closeRect.Right - 8, _closeRect.Top + 8, _closeRect.Left + 8, _closeRect.Bottom - 8);
////////            }

////////            for (int i = 0; i < _buttonResults.Count; i++)
////////            {
////////                bool isHover = (_hoveredButton == i && _pressedButton == -1);
////////                bool isPressed = (_pressedButton == i);
////////                bool isPrimary = (i == _buttonResults.Count - 1);

////////                Rectangle btnRect = _buttonRects[i];
////////                Color btnBg = Color.Empty;
////////                Color btnText = _textColor;

////////                if (isPrimary)
////////                {
////////                    btnBg = _accentColor;
////////                    if (isHover) btnBg = ControlPaint.Light(_accentColor, 0.1f);
////////                    else if (isPressed) btnBg = ControlPaint.Dark(_accentColor, 0.1f);
////////                    btnText = Color.White;
////////                }
////////                else
////////                {
////////                    btnBg = Color.FromArgb(45, 45, 50);
////////                    if (isHover) btnBg = Color.FromArgb(55, 55, 60);
////////                    else if (isPressed) btnBg = Color.FromArgb(35, 35, 40);
////////                }

////////                using (var btnPath = GetRoundedPath(btnRect, 6))
////////                using (var btnBrush = new SolidBrush(btnBg))
////////                {
////////                    g.FillPath(btnBrush, btnPath);
////////                }

////////                using (var textBrush = new SolidBrush(btnText))
////////                using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center })
////////                {
////////                    g.DrawString(_buttonResults[i].ToString(), Font, textBrush, btnRect, sf);
////////                }
////////            }
////////        }

////////        private void DrawModernIcon(Graphics g, int x, int y, ModernMessageIcon icon)
////////        {
////////            Color iconColor = Color.White;
////////            using (var pen = new Pen(iconColor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
////////            using (var brush = new SolidBrush(iconColor))
////////            {
////////                if (icon == ModernMessageIcon.Info)
////////                {
////////                    iconColor = Color.FromArgb(0, 180, 255);
////////                    pen.Color = iconColor; brush.Color = iconColor;
////////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
////////                    g.FillEllipse(brush, new Rectangle(x + 15, y + 8, 6, 6));
////////                    g.DrawLine(pen, x + 18, y + 16, x + 18, y + 26);
////////                }
////////                else if (icon == ModernMessageIcon.Warning)
////////                {
////////                    iconColor = Color.FromArgb(255, 160, 0);
////////                    pen.Color = iconColor; brush.Color = iconColor;
////////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
////////                    g.DrawLine(pen, x + 18, y + 8, x + 18, y + 22);
////////                    g.FillEllipse(brush, new Rectangle(x + 15, y + 26, 6, 6));
////////                }
////////                else if (icon == ModernMessageIcon.Error)
////////                {
////////                    iconColor = Color.FromArgb(255, 50, 50);
////////                    pen.Color = iconColor; brush.Color = iconColor;
////////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
////////                    g.DrawLine(pen, x + 10, y + 10, x + 26, y + 26);
////////                    g.DrawLine(pen, x + 26, y + 10, x + 10, y + 26);
////////                }
////////                else if (icon == ModernMessageIcon.Success)
////////                {
////////                    iconColor = Color.FromArgb(0, 220, 100);
////////                    pen.Color = iconColor; brush.Color = iconColor;
////////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
////////                    g.DrawLine(pen, x + 10, y + 18, x + 16, y + 24);
////////                    g.DrawLine(pen, x + 16, y + 24, x + 28, y + 12);
////////                }
////////            }
////////        }

////////        protected override void OnMouseMove(MouseEventArgs e)
////////        {
////////            base.OnMouseMove(e);
////////            int newHover = -1;
////////            for (int i = 0; i < _buttonRects.Count; i++) if (_buttonRects[i].Contains(e.Location)) newHover = i;

////////            bool newHoverClose = _closeRect.Contains(e.Location);

////////            if (newHover != _hoveredButton || newHoverClose != _hoverClose)
////////            {
////////                _hoveredButton = newHover;
////////                _hoverClose = newHoverClose;
////////                Cursor = (newHover != -1 || _hoverClose) ? Cursors.Hand : Cursors.Default;
////////                Invalidate();
////////            }
////////        }

////////        protected override void OnMouseDown(MouseEventArgs e)
////////        {
////////            base.OnMouseDown(e);
////////            _pressedButton = -1;
////////            for (int i = 0; i < _buttonRects.Count; i++) if (_buttonRects[i].Contains(e.Location)) _pressedButton = i;
////////            Invalidate();
////////        }

////////        protected override void OnMouseUp(MouseEventArgs e)
////////        {
////////            base.OnMouseUp(e);
////////            if (_pressedButton != -1 && _buttonRects[_pressedButton].Contains(e.Location))
////////            {
////////                _result = _buttonResults[_pressedButton];
////////                DialogResult = DialogResult.OK;
////////            }
////////            else if (_closeRect.Contains(e.Location))
////////            {
////////                _result = ModernDialogResult.Cancel;
////////                DialogResult = DialogResult.Cancel;
////////            }
////////            _pressedButton = -1;
////////            Invalidate();
////////        }

////////        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
////////        {
////////            var path = new GraphicsPath();
////////            if (rect.Width <= 0 || rect.Height <= 0) return path;
////////            int r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
////////            if (r <= 0) { path.AddRectangle(rect); return path; }
////////            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
////////            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
////////            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
////////            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
////////            path.CloseFigure();
////////            return path;
////////        }

////////        [DllImport("dwmapi.dll")]
////////        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
////////    }

////////    /// <summary>
////////    /// A modern, animated toaster notification system.
////////    /// </summary>
////////    public static class ModernToaster
////////    {
////////        private static List<ToasterForm> _activeToasters = new List<ToasterForm>();

////////        public static void Show(string title, string message, ToasterType type = ToasterType.Info, int duration = 3000)
////////        {
////////            if (Application.OpenForms.Count == 0) return;

////////            var mainForm = Application.OpenForms[0];
////////            if (mainForm.InvokeRequired)
////////            {
////////                mainForm.BeginInvoke((MethodInvoker)delegate { Show(title, message, type, duration); });
////////                return;
////////            }

////////            var toaster = new ToasterForm(title, message, type, duration);

////////            int yOffset = 20;
////////            foreach (var t in _activeToasters)
////////            {
////////                if (t.Screen == Screen.FromControl(mainForm))
////////                    yOffset += t.Height + 10;
////////            }

////////            toaster.StartPosition = FormStartPosition.Manual;
////////            var screen = Screen.FromControl(mainForm).WorkingArea;
////////            toaster.Location = new Point(screen.Right - toaster.Width - 20, screen.Bottom - toaster.Height - yOffset);

////////            _activeToasters.Add(toaster);
////////            toaster.FormClosed += (s, e) => { _activeToasters.Remove(toaster); };

////////            toaster.Show(mainForm);
////////        }
////////    }

////////    public class ToasterForm : Form
////////    {
////////        private Color _backColor = ModernNotificationConfig.BackColor;
////////        private Color _borderColor = ModernNotificationConfig.AccentColor;
////////        private Color _textColor = ModernNotificationConfig.TextColor;
////////        private Color _subTextColor = ModernNotificationConfig.SubTextColor;

////////        private string _title;
////////        private string _message;
////////        private ToasterType _type;
////////        private int _duration;
////////        private System.Windows.Forms.Timer _timer;
////////        private float _progress = 1f;
////////        private Rectangle _closeRect;
////////        private bool _hoverClose = false;
////////        private float _opacity = 0f;

////////        public ToasterForm(string title, string message, ToasterType type, int duration)
////////        {
////////            _title = title;
////////            _message = message;
////////            _type = type;
////////            _duration = duration;

////////            switch (_type)
////////            {
////////                case ToasterType.Info: _borderColor = Color.FromArgb(0, 180, 255); break;
////////                case ToasterType.Success: _borderColor = Color.FromArgb(0, 220, 100); break;
////////                case ToasterType.Warning: _borderColor = Color.FromArgb(255, 160, 0); break;
////////                case ToasterType.Error: _borderColor = Color.FromArgb(255, 50, 50); break;
////////            }

////////            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
////////            FormBorderStyle = FormBorderStyle.None;
////////            ShowInTaskbar = false;
////////            TopMost = true;
////////            Font = new Font("Segoe UI Variable Display", 9f);
////////            Size = new Size(350, 90);
////////            Opacity = 0;

////////            _timer = new System.Windows.Forms.Timer();
////////            _timer.Interval = 16;
////////            _timer.Tick += Timer_Tick;
////////            _timer.Start();

////////            this.HandleCreated += (s, e) => {
////////                int val = 2;
////////                DwmSetWindowAttribute(this.Handle, 2, ref val, sizeof(int));
////////            };
////////        }

////////        public Screen Screen { get; set; }

////////        private void Timer_Tick(object sender, EventArgs e)
////////        {
////////            if (IsDisposed) return;

////////            if (_duration > 0)
////////            {
////////                _duration -= 16;
////////                _progress = (float)_duration / 3000f;
////////                if (_progress < 0) _progress = 0;
////////            }

////////            if (_opacity < 1f)
////////            {
////////                _opacity += 0.1f;
////////                if (_opacity > 1f) _opacity = 1f;
////////                Opacity = _opacity;
////////            }

////////            if (_duration <= 0 && !IsDisposed)
////////            {
////////                _opacity -= 0.1f;
////////                Opacity = _opacity;
////////                if (_opacity <= 0f)
////////                {
////////                    _timer.Stop();
////////                    this.Close();
////////                }
////////            }

////////            Invalidate();
////////        }

////////        protected override void OnPaint(PaintEventArgs e)
////////        {
////////            Graphics g = e.Graphics;
////////            g.SmoothingMode = SmoothingMode.AntiAlias;
////////            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

////////            using (var bgPath = GetRoundedPath(ClientRectangle, 8))
////////            using (var bgBrush = new SolidBrush(_backColor))
////////            {
////////                g.FillPath(bgBrush, bgPath);
////////            }

////////            // Only draw the left accent bar, no full ugly border
////////            using (var sideBrush = new SolidBrush(_borderColor))
////////            {
////////                g.FillPath(sideBrush, GetRoundedPath(new Rectangle(0, 0, 6, Height), 3));
////////            }

////////            int iconX = 20, iconY = 20;
////////            using (var pen = new Pen(_borderColor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
////////            using (var brush = new SolidBrush(_borderColor))
////////            {
////////                if (_type == ToasterType.Info)
////////                {
////////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
////////                    g.FillEllipse(brush, new Rectangle(iconX + 13, iconY + 6, 6, 6));
////////                    g.DrawLine(pen, iconX + 16, iconY + 14, iconX + 16, iconY + 24);
////////                }
////////                else if (_type == ToasterType.Success)
////////                {
////////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
////////                    g.DrawLine(pen, iconX + 8, iconY + 16, iconX + 14, iconY + 22);
////////                    g.DrawLine(pen, iconX + 14, iconY + 22, iconX + 26, iconY + 10);
////////                }
////////                else if (_type == ToasterType.Warning)
////////                {
////////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
////////                    g.DrawLine(pen, iconX + 16, iconY + 6, iconX + 16, iconY + 20);
////////                    g.FillEllipse(brush, new Rectangle(iconX + 13, iconY + 24, 6, 6));
////////                }
////////                else if (_type == ToasterType.Error)
////////                {
////////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
////////                    g.DrawLine(pen, iconX + 9, iconY + 9, iconX + 23, iconY + 23);
////////                    g.DrawLine(pen, iconX + 23, iconY + 9, iconX + 9, iconY + 23);
////////                }
////////            }

////////            using (var titleFont = new Font(Font, FontStyle.Bold))
////////            using (var titleBrush = new SolidBrush(_textColor))
////////            using (var msgBrush = new SolidBrush(_subTextColor))
////////            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
////////            {
////////                g.DrawString(_title, titleFont, titleBrush, new Rectangle(64, 18, Width - 100, 25), sf);
////////                g.DrawString(_message, Font, msgBrush, new Rectangle(64, 45, Width - 100, 40), sf);
////////            }

////////            _closeRect = new Rectangle(Width - 32, 12, 20, 20);
////////            if (_hoverClose) { using (var b = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) using (var p = GetRoundedPath(_closeRect, 4)) g.FillPath(b, p); }
////////            using (var crossPen = new Pen(_subTextColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
////////            {
////////                g.DrawLine(crossPen, _closeRect.Left + 5, _closeRect.Top + 5, _closeRect.Right - 5, _closeRect.Bottom - 5);
////////                g.DrawLine(crossPen, _closeRect.Right - 5, _closeRect.Top + 5, _closeRect.Left + 5, _closeRect.Bottom - 5);
////////            }

////////            if (_duration > 0)
////////            {
////////                Rectangle progRect = new Rectangle(2, Height - 4, Width - 4, 4);
////////                int progWidth = Math.Max(0, (int)(progRect.Width * _progress));
////////                using (var progBrush = new SolidBrush(_borderColor))
////////                {
////////                    g.FillPath(progBrush, GetRoundedPath(new Rectangle(progRect.X, progRect.Y, progWidth, progRect.Height), 2));
////////                }
////////            }
////////        }

////////        protected override void OnMouseMove(MouseEventArgs e)
////////        {
////////            base.OnMouseMove(e);
////////            bool newHover = _closeRect.Contains(e.Location);
////////            if (newHover != _hoverClose)
////////            {
////////                _hoverClose = newHover;
////////                Cursor = _hoverClose ? Cursors.Hand : Cursors.Default;
////////                Invalidate();
////////            }
////////        }

////////        protected override void OnMouseUp(MouseEventArgs e)
////////        {
////////            base.OnMouseUp(e);
////////            if (_closeRect.Contains(e.Location)) { _duration = 0; _timer.Stop(); this.Close(); }
////////        }

////////        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
////////        {
////////            var path = new GraphicsPath();
////////            if (rect.Width <= 0 || rect.Height <= 0) return path;
////////            int r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
////////            if (r <= 0) { path.AddRectangle(rect); return path; }
////////            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
////////            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
////////            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
////////            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
////////            path.CloseFigure();
////////            return path;
////////        }

////////        [DllImport("dwmapi.dll")]
////////        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
////////    }
////////}



















////////================================ VEr 1.5 =============

//////using System;
//////using System.Collections.Generic;
//////using System.Drawing;
//////using System.Drawing.Drawing2D;
//////using System.Drawing.Text;
//////using System.Runtime.InteropServices;
//////using System.Windows.Forms;

//////namespace UltraModernUI.Controls
//////{
//////    #region Enums & Config
//////    public enum ModernMessageIcon { None, Info, Warning, Error, Success, Question }
//////    public enum ModernDialogResult { None, OK, Cancel, Yes, No, Abort, Retry, Ignore }
//////    public enum ToasterType { Info, Success, Warning, Error }

//////    // Shared Theme Enums (Matching the DataGridView)
//////    public enum NotificationThemeMode { Light, Dark }
//////    public enum NotificationDesignStyle { DashboardPremium, FluentGlass, MaterialFlat, SoftNeumorphic, CyberpunkIndustrial }

//////    /// <summary>
//////    /// Global configuration for ModernMessageBox and ModernToaster.
//////    /// Modify this to match your app's theme!
//////    /// </summary>
//////    public static class ModernNotificationConfig
//////    {
//////        public static NotificationThemeMode Theme { get; set; } = NotificationThemeMode.Dark;
//////        public static NotificationDesignStyle Style { get; set; } = NotificationDesignStyle.DashboardPremium;
//////    }
//////    #endregion

//////    /// <summary>
//////    /// A modern, high-fidelity replacement for the standard Windows MessageBox.
//////    /// </summary>
//////    public class ModernMessageBox : Form
//////    {
//////        private Color _backColor;
//////        private Color _accentColor;
//////        private Color _textColor;
//////        private Color _subTextColor;
//////        private int _cornerRadius = 8;

//////        private string _title;
//////        private string _message;
//////        private ModernMessageIcon _icon;
//////        private MessageBoxButtons _buttons;
//////        private List<ModernDialogResult> _buttonResults = new List<ModernDialogResult>();
//////        private List<Rectangle> _buttonRects = new List<Rectangle>();
//////        private int _hoveredButton = -1;
//////        private int _pressedButton = -1;
//////        private Rectangle _closeRect;
//////        private bool _hoverClose = false;

//////        public ModernMessageBox(string message, string title, MessageBoxButtons buttons, ModernMessageIcon icon)
//////        {
//////            _title = title;
//////            _message = message;
//////            _buttons = buttons;
//////            _icon = icon;

//////            ApplyTheme();

//////            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
//////            FormBorderStyle = FormBorderStyle.None;
//////            StartPosition = FormStartPosition.CenterParent;
//////            BackColor = Color.Magenta; // Needed for anti-aliased rounded edges
//////            TransparencyKey = Color.Magenta;
//////            TopMost = true;
//////            ShowInTaskbar = false;
//////            Font = new Font("Segoe UI Variable Display", 9f);

//////            // Enable native Windows drop shadow (removes need for ugly drawn borders)
//////            this.HandleCreated += (s, e) =>
//////            {
//////                int val = 2;
//////                DwmSetWindowAttribute(this.Handle, 2, ref val, sizeof(int));
//////            };
//////        }

//////        private void ApplyTheme()
//////        {
//////            bool isDark = ModernNotificationConfig.Theme == NotificationThemeMode.Dark;
//////            switch (ModernNotificationConfig.Style)
//////            {
//////                case NotificationDesignStyle.DashboardPremium:
//////                    _backColor = isDark ? Color.FromArgb(32, 32, 38) : Color.FromArgb(248, 250, 252);
//////                    _accentColor = isDark ? Color.FromArgb(0, 120, 212) : Color.FromArgb(0, 100, 180);
//////                    _textColor = isDark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(30, 30, 30);
//////                    _subTextColor = isDark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(100, 110, 120);
//////                    _cornerRadius = 8;
//////                    break;
//////                case NotificationDesignStyle.FluentGlass:
//////                    _backColor = isDark ? Color.FromArgb(40, 40, 48) : Color.FromArgb(243, 244, 246);
//////                    _accentColor = Color.FromArgb(0, 180, 255);
//////                    _textColor = isDark ? Color.White : Color.Black;
//////                    _subTextColor = isDark ? Color.LightGray : Color.DarkGray;
//////                    _cornerRadius = 10;
//////                    break;
//////                case NotificationDesignStyle.MaterialFlat:
//////                    _backColor = isDark ? Color.FromArgb(18, 18, 18) : Color.White;
//////                    _accentColor = Color.FromArgb(98, 0, 238);
//////                    _textColor = isDark ? Color.White : Color.Black;
//////                    _subTextColor = isDark ? Color.FromArgb(180, 180, 180) : Color.FromArgb(70, 70, 70);
//////                    _cornerRadius = 2; // Material is sharper
//////                    break;
//////                case NotificationDesignStyle.SoftNeumorphic:
//////                    _backColor = isDark ? Color.FromArgb(32, 38, 48) : Color.FromArgb(224, 229, 236);
//////                    _accentColor = Color.FromArgb(255, 87, 51);
//////                    _textColor = isDark ? Color.FromArgb(220, 230, 240) : Color.FromArgb(50, 60, 70);
//////                    _subTextColor = isDark ? Color.FromArgb(160, 170, 180) : Color.FromArgb(100, 110, 120);
//////                    _cornerRadius = 12;
//////                    break;
//////                case NotificationDesignStyle.CyberpunkIndustrial:
//////                    _backColor = Color.FromArgb(12, 12, 14);
//////                    _accentColor = Color.FromArgb(0, 255, 204);
//////                    _textColor = Color.FromArgb(230, 240, 255);
//////                    _subTextColor = Color.FromArgb(100, 120, 140);
//////                    _cornerRadius = 0; // Sharp edges
//////                    break;
//////            }
//////        }

//////        public static ModernDialogResult Show(string message, string title = "Message", MessageBoxButtons buttons = MessageBoxButtons.OK, ModernMessageIcon icon = ModernMessageIcon.None)
//////        {
//////            using (var box = new ModernMessageBox(message, title, buttons, icon))
//////            {
//////                box.CalculateSize();
//////                if (box.ShowDialog() == DialogResult.OK)
//////                    return box._result;
//////                return ModernDialogResult.None;
//////            }
//////        }

//////        private ModernDialogResult _result = ModernDialogResult.None;
//////        private void CalculateSize()
//////        {
//////            using (var g = Graphics.FromHwnd(IntPtr.Zero))
//////            {
//////                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
//////                SizeF titleSize = g.MeasureString(_title, new Font(Font, FontStyle.Bold), 500);
//////                SizeF msgSize = g.MeasureString(_message, Font, 400);

//////                int width = Math.Max(400, (int)Math.Max(titleSize.Width, msgSize.Width) + 80);
//////                int height = (int)msgSize.Height + 120;

//////                if (_icon != ModernMessageIcon.None) width += 60;

//////                this.Size = new Size(width, height);

//////                // Setup Buttons
//////                _buttonResults.Clear();
//////                switch (_buttons)
//////                {
//////                    case MessageBoxButtons.OK: _buttonResults.Add(ModernDialogResult.OK); break;
//////                    case MessageBoxButtons.OKCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.OK); break;
//////                    case MessageBoxButtons.YesNo: _buttonResults.Add(ModernDialogResult.No); _buttonResults.Add(ModernDialogResult.Yes); break;
//////                    case MessageBoxButtons.YesNoCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.No); _buttonResults.Add(ModernDialogResult.Yes); break;
//////                    case MessageBoxButtons.RetryCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.Retry); break;
//////                    case MessageBoxButtons.AbortRetryIgnore: _buttonResults.Add(ModernDialogResult.Ignore); _buttonResults.Add(ModernDialogResult.Retry); _buttonResults.Add(ModernDialogResult.Abort); break;
//////                }

//////                _buttonRects.Clear();
//////                int btnWidth = 100;
//////                int btnHeight = 36;
//////                int spacing = 12;
//////                int totalBtnWidth = (_buttonResults.Count * btnWidth) + ((_buttonResults.Count - 1) * spacing);
//////                int startX = width - totalBtnWidth - 20;

//////                for (int i = 0; i < _buttonResults.Count; i++)
//////                {
//////                    _buttonRects.Add(new Rectangle(startX + (i * (btnWidth + spacing)), height - btnHeight - 20, btnWidth, btnHeight));
//////                }

//////                _closeRect = new Rectangle(width - 40, 12, 28, 28);
//////            }
//////        }

//////        // Allows dragging the borderless form
//////        protected override void WndProc(ref Message m)
//////        {
//////            base.WndProc(ref m);
//////            if (m.Msg == 0x84) // WM_NCHITTEST
//////            {
//////                int x = (int)m.LParam & 0xFFFF;
//////                int y = (int)m.LParam >> 16;
//////                Point pos = PointToClient(new Point(x, y));

//////                foreach (var btn in _buttonRects) if (btn.Contains(pos)) return;
//////                if (_closeRect.Contains(pos)) return;

//////                m.Result = (IntPtr)0x2; // HTCAPTION
//////            }
//////        }

//////        protected override void OnPaint(PaintEventArgs e)
//////        {
//////            Graphics g = e.Graphics;
//////            g.SmoothingMode = SmoothingMode.AntiAlias;
//////            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

//////            // Draw Background
//////            using (var bgPath = GetRoundedPath(ClientRectangle, _cornerRadius))
//////            using (var bgBrush = new SolidBrush(_backColor))
//////            {
//////                g.FillPath(bgBrush, bgPath);
//////            }

//////            // Cyberpunk style gets a neon border
//////            if (ModernNotificationConfig.Style == NotificationDesignStyle.CyberpunkIndustrial)
//////            {
//////                using (var glowPen = new Pen(Color.FromArgb(100, _accentColor), 4))
//////                using (var corePen = new Pen(_accentColor, 1))
//////                using (var bgPath = GetRoundedPath(ClientRectangle, _cornerRadius))
//////                {
//////                    g.DrawPath(glowPen, bgPath);
//////                    g.DrawPath(corePen, bgPath);
//////                }
//////            }

//////            // Draw Icon
//////            int iconX = 24;
//////            int iconY = 60;
//////            if (_icon != ModernMessageIcon.None)
//////            {
//////                DrawModernIcon(g, iconX, iconY, _icon);
//////                iconX += 50;
//////            }

//////            // Draw Title
//////            using (var titleFont = new Font(Font, FontStyle.Bold))
//////            using (var titleBrush = new SolidBrush(_textColor))
//////            using (var msgBrush = new SolidBrush(_subTextColor))
//////            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
//////            {
//////                g.DrawString(_title, titleFont, titleBrush, new Rectangle(24, 16, Width - 80, 30), sf);
//////                g.DrawString(_message, Font, msgBrush, new Rectangle(iconX, 50, Width - iconX - 30, Height - 110), sf);
//////            }

//////            // Draw Close Button
//////            if (_hoverClose)
//////            {
//////                using (var closeBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
//////                using (var closePath = GetRoundedPath(_closeRect, 4))
//////                    g.FillPath(closeBrush, closePath);
//////            }
//////            using (var crossPen = new Pen(_subTextColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
//////            {
//////                g.DrawLine(crossPen, _closeRect.Left + 8, _closeRect.Top + 8, _closeRect.Right - 8, _closeRect.Bottom - 8);
//////                g.DrawLine(crossPen, _closeRect.Right - 8, _closeRect.Top + 8, _closeRect.Left + 8, _closeRect.Bottom - 8);
//////            }

//////            // Draw Action Buttons
//////            for (int i = 0; i < _buttonResults.Count; i++)
//////            {
//////                bool isHover = (_hoveredButton == i && _pressedButton == -1);
//////                bool isPressed = (_pressedButton == i);
//////                bool isPrimary = (i == _buttonResults.Count - 1);

//////                Rectangle btnRect = _buttonRects[i];
//////                Color btnBg = Color.Empty;
//////                Color btnText = _textColor;

//////                if (isPrimary)
//////                {
//////                    btnBg = _accentColor;
//////                    if (isHover) btnBg = ControlPaint.Light(_accentColor, 0.1f);
//////                    else if (isPressed) btnBg = ControlPaint.Dark(_accentColor, 0.1f);
//////                    btnText = Color.White;
//////                }
//////                else
//////                {
//////                    btnBg = ControlPaint.Light(_backColor, 0.05f);
//////                    if (isHover) btnBg = ControlPaint.Light(_backColor, 0.1f);
//////                    else if (isPressed) btnBg = ControlPaint.Dark(_backColor, 0.05f);
//////                }

//////                using (var btnPath = GetRoundedPath(btnRect, _cornerRadius == 0 ? 0 : 6))
//////                using (var btnBrush = new SolidBrush(btnBg))
//////                {
//////                    g.FillPath(btnBrush, btnPath);
//////                }

//////                using (var textBrush = new SolidBrush(btnText))
//////                using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center })
//////                {
//////                    g.DrawString(_buttonResults[i].ToString(), Font, textBrush, btnRect, sf);
//////                }
//////            }
//////        }

//////        private void DrawModernIcon(Graphics g, int x, int y, ModernMessageIcon icon)
//////        {
//////            Color iconColor = Color.White;
//////            using (var pen = new Pen(iconColor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
//////            using (var brush = new SolidBrush(iconColor))
//////            {
//////                if (icon == ModernMessageIcon.Info)
//////                {
//////                    iconColor = Color.FromArgb(0, 180, 255);
//////                    pen.Color = iconColor; brush.Color = iconColor;
//////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
//////                    g.FillEllipse(brush, new Rectangle(x + 15, y + 8, 6, 6));
//////                    g.DrawLine(pen, x + 18, y + 16, x + 18, y + 26);
//////                }
//////                else if (icon == ModernMessageIcon.Warning)
//////                {
//////                    iconColor = Color.FromArgb(255, 160, 0);
//////                    pen.Color = iconColor; brush.Color = iconColor;
//////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
//////                    g.DrawLine(pen, x + 18, y + 8, x + 18, y + 22);
//////                    g.FillEllipse(brush, new Rectangle(x + 15, y + 26, 6, 6));
//////                }
//////                else if (icon == ModernMessageIcon.Error)
//////                {
//////                    iconColor = Color.FromArgb(255, 50, 50);
//////                    pen.Color = iconColor; brush.Color = iconColor;
//////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
//////                    g.DrawLine(pen, x + 10, y + 10, x + 26, y + 26);
//////                    g.DrawLine(pen, x + 26, y + 10, x + 10, y + 26);
//////                }
//////                else if (icon == ModernMessageIcon.Success)
//////                {
//////                    iconColor = Color.FromArgb(0, 220, 100);
//////                    pen.Color = iconColor; brush.Color = iconColor;
//////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
//////                    g.DrawLine(pen, x + 10, y + 18, x + 16, y + 24);
//////                    g.DrawLine(pen, x + 16, y + 24, x + 28, y + 12);
//////                }
//////            }
//////        }

//////        protected override void OnMouseMove(MouseEventArgs e)
//////        {
//////            base.OnMouseMove(e);
//////            int newHover = -1;
//////            for (int i = 0; i < _buttonRects.Count; i++) if (_buttonRects[i].Contains(e.Location)) newHover = i;

//////            bool newHoverClose = _closeRect.Contains(e.Location);

//////            if (newHover != _hoveredButton || newHoverClose != _hoverClose)
//////            {
//////                _hoveredButton = newHover;
//////                _hoverClose = newHoverClose;
//////                Cursor = (newHover != -1 || _hoverClose) ? Cursors.Hand : Cursors.Default;
//////                Invalidate();
//////            }
//////        }

//////        protected override void OnMouseDown(MouseEventArgs e)
//////        {
//////            base.OnMouseDown(e);
//////            _pressedButton = -1;
//////            for (int i = 0; i < _buttonRects.Count; i++) if (_buttonRects[i].Contains(e.Location)) _pressedButton = i;
//////            Invalidate();
//////        }

//////        protected override void OnMouseUp(MouseEventArgs e)
//////        {
//////            base.OnMouseUp(e);
//////            if (_pressedButton != -1 && _buttonRects[_pressedButton].Contains(e.Location))
//////            {
//////                _result = _buttonResults[_pressedButton];
//////                DialogResult = DialogResult.OK;
//////            }
//////            else if (_closeRect.Contains(e.Location))
//////            {
//////                _result = ModernDialogResult.Cancel;
//////                DialogResult = DialogResult.Cancel;
//////            }
//////            _pressedButton = -1;
//////            Invalidate();
//////        }

//////        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
//////        {
//////            var path = new GraphicsPath();
//////            if (rect.Width <= 0 || rect.Height <= 0) return path; // CRASH FIX
//////            int r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
//////            if (r <= 0) { path.AddRectangle(rect); return path; }
//////            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
//////            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
//////            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
//////            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
//////            path.CloseFigure();
//////            return path;
//////        }

//////        [DllImport("dwmapi.dll")]
//////        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
//////    }

//////    /// <summary>
//////    /// A modern, animated toaster notification system.
//////    /// </summary>
//////    public static class ModernToaster
//////    {
//////        private static List<ToasterForm> _activeToasters = new List<ToasterForm>();

//////        public static void Show(string title, string message, ToasterType type = ToasterType.Info, int duration = 3000)
//////        {
//////            if (Application.OpenForms.Count == 0) return;

//////            var mainForm = Application.OpenForms[0];
//////            if (mainForm.InvokeRequired)
//////            {
//////                mainForm.BeginInvoke((MethodInvoker)delegate { Show(title, message, type, duration); });
//////                return;
//////            }

//////            var toaster = new ToasterForm(title, message, type, duration);

//////            int yOffset = 20;
//////            foreach (var t in _activeToasters)
//////            {
//////                if (t.Screen == Screen.FromControl(mainForm))
//////                    yOffset += t.Height + 10;
//////            }

//////            toaster.StartPosition = FormStartPosition.Manual;
//////            var screen = Screen.FromControl(mainForm).WorkingArea;
//////            toaster.Location = new Point(screen.Right - toaster.Width - 20, screen.Bottom - toaster.Height - yOffset);

//////            _activeToasters.Add(toaster);
//////            toaster.FormClosed += (s, e) => { _activeToasters.Remove(toaster); };

//////            toaster.Show(mainForm);
//////        }
//////    }

//////    public class ToasterForm : Form
//////    {
//////        private Color _backColor;
//////        private Color _borderColor;
//////        private Color _textColor;
//////        private Color _subTextColor;
//////        private int _cornerRadius = 8;

//////        private string _title;
//////        private string _message;
//////        private ToasterType _type;
//////        private int _duration;
//////        private System.Windows.Forms.Timer _timer;
//////        private float _progress = 1f;
//////        private Rectangle _closeRect;
//////        private bool _hoverClose = false;
//////        private float _opacity = 0f;

//////        public ToasterForm(string title, string message, ToasterType type, int duration)
//////        {
//////            _title = title;
//////            _message = message;
//////            _type = type;
//////            _duration = duration;

//////            ApplyTheme();

//////            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
//////            FormBorderStyle = FormBorderStyle.None;
//////            ShowInTaskbar = false;
//////            TopMost = true;
//////            BackColor = Color.Magenta;
//////            TransparencyKey = Color.Magenta;
//////            Font = new Font("Segoe UI Variable Display", 9f);
//////            Size = new Size(350, 90);
//////            Opacity = 0;

//////            _timer = new System.Windows.Forms.Timer();
//////            _timer.Interval = 16;
//////            _timer.Tick += Timer_Tick;
//////            _timer.Start();

//////            this.HandleCreated += (s, e) =>
//////            {
//////                int val = 2;
//////                DwmSetWindowAttribute(this.Handle, 2, ref val, sizeof(int));
//////            };
//////        }

//////        private void ApplyTheme()
//////        {
//////            bool isDark = ModernNotificationConfig.Theme == NotificationThemeMode.Dark;
//////            switch (ModernNotificationConfig.Style)
//////            {
//////                case NotificationDesignStyle.DashboardPremium:
//////                    _backColor = isDark ? Color.FromArgb(32, 32, 38) : Color.FromArgb(248, 250, 252);
//////                    _textColor = isDark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(30, 30, 30);
//////                    _subTextColor = isDark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(100, 110, 120);
//////                    _cornerRadius = 8;
//////                    break;
//////                case NotificationDesignStyle.FluentGlass:
//////                    _backColor = isDark ? Color.FromArgb(40, 40, 48) : Color.FromArgb(243, 244, 246);
//////                    _textColor = isDark ? Color.White : Color.Black;
//////                    _subTextColor = isDark ? Color.LightGray : Color.DarkGray;
//////                    _cornerRadius = 10;
//////                    break;
//////                case NotificationDesignStyle.MaterialFlat:
//////                    _backColor = isDark ? Color.FromArgb(18, 18, 18) : Color.White;
//////                    _textColor = isDark ? Color.White : Color.Black;
//////                    _subTextColor = isDark ? Color.FromArgb(180, 180, 180) : Color.FromArgb(70, 70, 70);
//////                    _cornerRadius = 2;
//////                    break;
//////                case NotificationDesignStyle.SoftNeumorphic:
//////                    _backColor = isDark ? Color.FromArgb(32, 38, 48) : Color.FromArgb(224, 229, 236);
//////                    _textColor = isDark ? Color.FromArgb(220, 230, 240) : Color.FromArgb(50, 60, 70);
//////                    _subTextColor = isDark ? Color.FromArgb(160, 170, 180) : Color.FromArgb(100, 110, 120);
//////                    _cornerRadius = 12;
//////                    break;
//////                case NotificationDesignStyle.CyberpunkIndustrial:
//////                    _backColor = Color.FromArgb(12, 12, 14);
//////                    _textColor = Color.FromArgb(230, 240, 255);
//////                    _subTextColor = Color.FromArgb(100, 120, 140);
//////                    _cornerRadius = 0;
//////                    break;
//////            }

//////            // Set Accent/Border color based on Toaster Type
//////            switch (_type)
//////            {
//////                case ToasterType.Info: _borderColor = Color.FromArgb(0, 180, 255); break;
//////                case ToasterType.Success: _borderColor = Color.FromArgb(0, 220, 100); break;
//////                case ToasterType.Warning: _borderColor = Color.FromArgb(255, 160, 0); break;
//////                case ToasterType.Error: _borderColor = Color.FromArgb(255, 50, 50); break;
//////            }
//////        }

//////        public Screen Screen { get; set; }

//////        private void Timer_Tick(object sender, EventArgs e)
//////        {
//////            if (IsDisposed) return;

//////            if (_duration > 0)
//////            {
//////                _duration -= 16;
//////                _progress = (float)_duration / 3000f;
//////                if (_progress < 0) _progress = 0;
//////            }

//////            if (_opacity < 1f)
//////            {
//////                _opacity += 0.1f;
//////                if (_opacity > 1f) _opacity = 1f;
//////                Opacity = _opacity;
//////            }

//////            if (_duration <= 0 && !IsDisposed)
//////            {
//////                _opacity -= 0.1f;
//////                Opacity = _opacity;
//////                if (_opacity <= 0f)
//////                {
//////                    _timer.Stop();
//////                    this.Close();
//////                }
//////            }

//////            Invalidate();
//////        }

//////        protected override void OnPaint(PaintEventArgs e)
//////        {
//////            Graphics g = e.Graphics;
//////            g.SmoothingMode = SmoothingMode.AntiAlias;
//////            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

//////            // Draw Background
//////            using (var bgPath = GetRoundedPath(ClientRectangle, _cornerRadius))
//////            using (var bgBrush = new SolidBrush(_backColor))
//////            {
//////                g.FillPath(bgBrush, bgPath);
//////            }

//////            // Cyberpunk style gets a neon border
//////            if (ModernNotificationConfig.Style == NotificationDesignStyle.CyberpunkIndustrial)
//////            {
//////                using (var glowPen = new Pen(Color.FromArgb(100, _borderColor), 4))
//////                using (var corePen = new Pen(_borderColor, 1))
//////                using (var bgPath = GetRoundedPath(ClientRectangle, _cornerRadius))
//////                {
//////                    g.DrawPath(glowPen, bgPath);
//////                    g.DrawPath(corePen, bgPath);
//////                }
//////            }
//////            else
//////            {
//////                // Draw a clean, colored left accent bar (replaces the ugly full border)
//////                using (var sideBrush = new SolidBrush(_borderColor))
//////                {
//////                    g.FillPath(sideBrush, GetRoundedPath(new Rectangle(0, 0, 6, Height), 3));
//////                }
//////            }

//////            int iconX = 20, iconY = 20;
//////            using (var pen = new Pen(_borderColor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
//////            using (var brush = new SolidBrush(_borderColor))
//////            {
//////                if (_type == ToasterType.Info)
//////                {
//////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
//////                    g.FillEllipse(brush, new Rectangle(iconX + 13, iconY + 6, 6, 6));
//////                    g.DrawLine(pen, iconX + 16, iconY + 14, iconX + 16, iconY + 24);
//////                }
//////                else if (_type == ToasterType.Success)
//////                {
//////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
//////                    g.DrawLine(pen, iconX + 8, iconY + 16, iconX + 14, iconY + 22);
//////                    g.DrawLine(pen, iconX + 14, iconY + 22, iconX + 26, iconY + 10);
//////                }
//////                else if (_type == ToasterType.Warning)
//////                {
//////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
//////                    g.DrawLine(pen, iconX + 16, iconY + 6, iconX + 16, iconY + 20);
//////                    g.FillEllipse(brush, new Rectangle(iconX + 13, iconY + 24, 6, 6));
//////                }
//////                else if (_type == ToasterType.Error)
//////                {
//////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
//////                    g.DrawLine(pen, iconX + 9, iconY + 9, iconX + 23, iconY + 23);
//////                    g.DrawLine(pen, iconX + 23, iconY + 9, iconX + 9, iconY + 23);
//////                }
//////            }

//////            using (var titleFont = new Font(Font, FontStyle.Bold))
//////            using (var titleBrush = new SolidBrush(_textColor))
//////            using (var msgBrush = new SolidBrush(_subTextColor))
//////            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
//////            {
//////                g.DrawString(_title, titleFont, titleBrush, new Rectangle(64, 18, Width - 100, 25), sf);
//////                g.DrawString(_message, Font, msgBrush, new Rectangle(64, 45, Width - 100, 40), sf);
//////            }

//////            _closeRect = new Rectangle(Width - 32, 12, 20, 20);
//////            if (_hoverClose) { using (var b = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) using (var p = GetRoundedPath(_closeRect, 4)) g.FillPath(b, p); }
//////            using (var crossPen = new Pen(_subTextColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
//////            {
//////                g.DrawLine(crossPen, _closeRect.Left + 5, _closeRect.Top + 5, _closeRect.Right - 5, _closeRect.Bottom - 5);
//////                g.DrawLine(crossPen, _closeRect.Right - 5, _closeRect.Top + 5, _closeRect.Left + 5, _closeRect.Bottom - 5);
//////            }

//////            if (_duration > 0)
//////            {
//////                Rectangle progRect = new Rectangle(2, Height - 4, Width - 4, 4);
//////                int progWidth = Math.Max(0, (int)(progRect.Width * _progress)); // CRASH FIX
//////                using (var progBrush = new SolidBrush(_borderColor))
//////                {
//////                    g.FillPath(progBrush, GetRoundedPath(new Rectangle(progRect.X, progRect.Y, progWidth, progRect.Height), 2));
//////                }
//////            }
//////        }

//////        protected override void OnMouseMove(MouseEventArgs e)
//////        {
//////            base.OnMouseMove(e);
//////            bool newHover = _closeRect.Contains(e.Location);
//////            if (newHover != _hoverClose)
//////            {
//////                _hoverClose = newHover;
//////                Cursor = _hoverClose ? Cursors.Hand : Cursors.Default;
//////                Invalidate();
//////            }
//////        }

//////        protected override void OnMouseUp(MouseEventArgs e)
//////        {
//////            base.OnMouseUp(e);
//////            if (_closeRect.Contains(e.Location)) { _duration = 0; _timer.Stop(); this.Close(); }
//////        }

//////        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
//////        {
//////            var path = new GraphicsPath();
//////            if (rect.Width <= 0 || rect.Height <= 0) return path; // CRASH FIX
//////            int r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
//////            if (r <= 0) { path.AddRectangle(rect); return path; }
//////            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
//////            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
//////            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
//////            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
//////            path.CloseFigure();
//////            return path;
//////        }

//////        [DllImport("dwmapi.dll")]
//////        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
//////    }
//////}























//////============================== VER 1.6 ======================


////using System;
////using System.Collections.Generic;
////using System.Drawing;
////using System.Drawing.Drawing2D;
////using System.Drawing.Text;
////using System.Runtime.InteropServices;
////using System.Windows.Forms;

////namespace UltraModernUI.Controls
////{
////    #region Enums & Config
////    public enum ModernMessageIcon { None, Info, Warning, Error, Success, Question }
////    public enum ModernDialogResult { None, OK, Cancel, Yes, No, Abort, Retry, Ignore }
////    public enum ToasterType { Info, Success, Warning, Error }

////    // Shared Theme Enums
////    public enum NotificationThemeMode { Light, Dark }
////    public enum NotificationDesignStyle { DashboardPremium, FluentGlass, MaterialFlat, SoftNeumorphic, CyberpunkIndustrial }

////    /// <summary>
////    /// Global configuration for ModernMessageBox and ModernToaster.
////    /// </summary>
////    public static class ModernNotificationConfig
////    {
////        public static NotificationThemeMode Theme { get; set; } = NotificationThemeMode.Dark;
////        public static NotificationDesignStyle Style { get; set; } = NotificationDesignStyle.DashboardPremium;
////    }
////    #endregion

////    /// <summary>
////    /// A modern, high-fidelity replacement for the standard Windows MessageBox.
////    /// </summary>
////    public class ModernMessageBox : Form
////    {
////        private Color _backColor;
////        private Color _accentColor;
////        private Color _textColor;
////        private Color _subTextColor;
////        private int _cornerRadius = 8;

////        private string _title;
////        private string _message;
////        private ModernMessageIcon _icon;
////        private MessageBoxButtons _buttons;
////        private List<ModernDialogResult> _buttonResults = new List<ModernDialogResult>();
////        private List<Rectangle> _buttonRects = new List<Rectangle>();
////        private int _hoveredButton = -1;
////        private int _pressedButton = -1;
////        private Rectangle _closeRect;
////        private bool _hoverClose = false;

////        public ModernMessageBox(string message, string title, MessageBoxButtons buttons, ModernMessageIcon icon)
////        {
////            _title = title;
////            _message = message;
////            _buttons = buttons;
////            _icon = icon;

////            ApplyTheme();

////            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
////            FormBorderStyle = FormBorderStyle.None;
////            StartPosition = FormStartPosition.CenterParent;
////            BackColor = Color.Magenta;
////            TransparencyKey = Color.Magenta;
////            TopMost = true;
////            ShowInTaskbar = false;
////            Font = new Font("Segoe UI Variable Display", 9f);

////            this.HandleCreated += (s, e) => {
////                int val = 2;
////                DwmSetWindowAttribute(this.Handle, 2, ref val, sizeof(int));
////            };
////        }

////        protected override CreateParams CreateParams
////        {
////            get
////            {
////                var cp = base.CreateParams;
////                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED (Fixes flicker)
////                return cp;
////            }
////        }

////        private void ApplyTheme()
////        {
////            bool isDark = ModernNotificationConfig.Theme == NotificationThemeMode.Dark;
////            switch (ModernNotificationConfig.Style)
////            {
////                case NotificationDesignStyle.DashboardPremium:
////                    _backColor = isDark ? Color.FromArgb(32, 32, 38) : Color.FromArgb(248, 250, 252);
////                    _accentColor = isDark ? Color.FromArgb(0, 120, 212) : Color.FromArgb(0, 100, 180);
////                    _textColor = isDark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(30, 30, 30);
////                    _subTextColor = isDark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(100, 110, 120);
////                    _cornerRadius = 8;
////                    break;
////                case NotificationDesignStyle.FluentGlass:
////                    _backColor = isDark ? Color.FromArgb(40, 40, 48) : Color.FromArgb(243, 244, 246);
////                    _accentColor = Color.FromArgb(0, 180, 255);
////                    _textColor = isDark ? Color.White : Color.Black;
////                    _subTextColor = isDark ? Color.LightGray : Color.DarkGray;
////                    _cornerRadius = 10;
////                    break;
////                case NotificationDesignStyle.MaterialFlat:
////                    _backColor = isDark ? Color.FromArgb(18, 18, 18) : Color.White;
////                    _accentColor = Color.FromArgb(98, 0, 238);
////                    _textColor = isDark ? Color.White : Color.Black;
////                    _subTextColor = isDark ? Color.FromArgb(180, 180, 180) : Color.FromArgb(70, 70, 70);
////                    _cornerRadius = 2;
////                    break;
////                case NotificationDesignStyle.SoftNeumorphic:
////                    _backColor = isDark ? Color.FromArgb(32, 38, 48) : Color.FromArgb(224, 229, 236);
////                    _accentColor = Color.FromArgb(255, 87, 51);
////                    _textColor = isDark ? Color.FromArgb(220, 230, 240) : Color.FromArgb(50, 60, 70);
////                    _subTextColor = isDark ? Color.FromArgb(160, 170, 180) : Color.FromArgb(100, 110, 120);
////                    _cornerRadius = 12;
////                    break;
////                case NotificationDesignStyle.CyberpunkIndustrial:
////                    _backColor = Color.FromArgb(12, 12, 14);
////                    _accentColor = Color.FromArgb(0, 255, 204);
////                    _textColor = Color.FromArgb(230, 240, 255);
////                    _subTextColor = Color.FromArgb(100, 120, 140);
////                    _cornerRadius = 0;
////                    break;
////            }
////        }

////        public static ModernDialogResult Show(string message, string title = "Message", MessageBoxButtons buttons = MessageBoxButtons.OK, ModernMessageIcon icon = ModernMessageIcon.None)
////        {
////            using (var box = new ModernMessageBox(message, title, buttons, icon))
////            {
////                box.CalculateSize();
////                if (box.ShowDialog() == DialogResult.OK)
////                    return box._result;
////                return ModernDialogResult.None;
////            }
////        }

////        private ModernDialogResult _result = ModernDialogResult.None;
////        private void CalculateSize()
////        {
////            using (var g = Graphics.FromHwnd(IntPtr.Zero))
////            {
////                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
////                SizeF titleSize = g.MeasureString(_title, new Font(Font, FontStyle.Bold), 500);
////                SizeF msgSize = g.MeasureString(_message, Font, 400);

////                int width = Math.Max(400, (int)Math.Max(titleSize.Width, msgSize.Width) + 80);
////                int height = (int)msgSize.Height + 120;

////                if (_icon != ModernMessageIcon.None) width += 60;

////                this.Size = new Size(width, height);

////                _buttonResults.Clear();
////                switch (_buttons)
////                {
////                    case MessageBoxButtons.OK: _buttonResults.Add(ModernDialogResult.OK); break;
////                    case MessageBoxButtons.OKCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.OK); break;
////                    case MessageBoxButtons.YesNo: _buttonResults.Add(ModernDialogResult.No); _buttonResults.Add(ModernDialogResult.Yes); break;
////                    case MessageBoxButtons.YesNoCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.No); _buttonResults.Add(ModernDialogResult.Yes); break;
////                    case MessageBoxButtons.RetryCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.Retry); break;
////                    case MessageBoxButtons.AbortRetryIgnore: _buttonResults.Add(ModernDialogResult.Ignore); _buttonResults.Add(ModernDialogResult.Retry); _buttonResults.Add(ModernDialogResult.Abort); break;
////                }

////                _buttonRects.Clear();
////                int btnWidth = 100;
////                int btnHeight = 36;
////                int spacing = 12;
////                int totalBtnWidth = (_buttonResults.Count * btnWidth) + ((_buttonResults.Count - 1) * spacing);
////                int startX = width - totalBtnWidth - 20;

////                for (int i = 0; i < _buttonResults.Count; i++)
////                {
////                    _buttonRects.Add(new Rectangle(startX + (i * (btnWidth + spacing)), height - btnHeight - 20, btnWidth, btnHeight));
////                }

////                _closeRect = new Rectangle(width - 40, 12, 28, 28);
////            }
////        }

////        protected override void WndProc(ref Message m)
////        {
////            base.WndProc(ref m);
////            if (m.Msg == 0x84) // WM_NCHITTEST
////            {
////                int x = (int)m.LParam & 0xFFFF;
////                int y = (int)m.LParam >> 16;
////                Point pos = PointToClient(new Point(x, y));

////                foreach (var btn in _buttonRects) if (btn.Contains(pos)) return;
////                if (_closeRect.Contains(pos)) return;

////                m.Result = (IntPtr)0x2; // HTCAPTION
////            }
////        }

////        protected override void OnPaint(PaintEventArgs e)
////        {
////            Graphics g = e.Graphics;
////            g.SmoothingMode = SmoothingMode.AntiAlias;
////            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

////            using (var bgPath = GetRoundedPath(ClientRectangle, _cornerRadius))
////            using (var bgBrush = new SolidBrush(_backColor))
////            {
////                g.FillPath(bgBrush, bgPath);
////            }

////            if (ModernNotificationConfig.Style == NotificationDesignStyle.CyberpunkIndustrial)
////            {
////                using (var glowPen = new Pen(Color.FromArgb(100, _accentColor), 4))
////                using (var corePen = new Pen(_accentColor, 1))
////                using (var bgPath = GetRoundedPath(ClientRectangle, _cornerRadius))
////                {
////                    g.DrawPath(glowPen, bgPath);
////                    g.DrawPath(corePen, bgPath);
////                }
////            }

////            int iconX = 24;
////            int iconY = 60;
////            if (_icon != ModernMessageIcon.None)
////            {
////                DrawModernIcon(g, iconX, iconY, _icon);
////                iconX += 50;
////            }

////            using (var titleFont = new Font(Font, FontStyle.Bold))
////            using (var titleBrush = new SolidBrush(_textColor))
////            using (var msgBrush = new SolidBrush(_subTextColor))
////            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
////            {
////                g.DrawString(_title, titleFont, titleBrush, new Rectangle(24, 16, Width - 80, 30), sf);
////                g.DrawString(_message, Font, msgBrush, new Rectangle(iconX, 50, Width - iconX - 30, Height - 110), sf);
////            }

////            if (_hoverClose)
////            {
////                using (var closeBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
////                using (var closePath = GetRoundedPath(_closeRect, 4))
////                    g.FillPath(closeBrush, closePath);
////            }
////            using (var crossPen = new Pen(_subTextColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
////            {
////                g.DrawLine(crossPen, _closeRect.Left + 8, _closeRect.Top + 8, _closeRect.Right - 8, _closeRect.Bottom - 8);
////                g.DrawLine(crossPen, _closeRect.Right - 8, _closeRect.Top + 8, _closeRect.Left + 8, _closeRect.Bottom - 8);
////            }

////            for (int i = 0; i < _buttonResults.Count; i++)
////            {
////                bool isHover = (_hoveredButton == i && _pressedButton == -1);
////                bool isPressed = (_pressedButton == i);
////                bool isPrimary = (i == _buttonResults.Count - 1);

////                Rectangle btnRect = _buttonRects[i];
////                Color btnBg = Color.Empty;
////                Color btnText = _textColor;

////                if (isPrimary)
////                {
////                    btnBg = _accentColor;
////                    if (isHover) btnBg = ControlPaint.Light(_accentColor, 0.1f);
////                    else if (isPressed) btnBg = ControlPaint.Dark(_accentColor, 0.1f);
////                    btnText = Color.White;
////                }
////                else
////                {
////                    btnBg = ControlPaint.Light(_backColor, 0.05f);
////                    if (isHover) btnBg = ControlPaint.Light(_backColor, 0.1f);
////                    else if (isPressed) btnBg = ControlPaint.Dark(_backColor, 0.05f);
////                }

////                using (var btnPath = GetRoundedPath(btnRect, _cornerRadius == 0 ? 0 : 6))
////                using (var btnBrush = new SolidBrush(btnBg))
////                {
////                    g.FillPath(btnBrush, btnPath);
////                }

////                using (var textBrush = new SolidBrush(btnText))
////                using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center })
////                {
////                    g.DrawString(_buttonResults[i].ToString(), Font, textBrush, btnRect, sf);
////                }
////            }
////        }

////        private void DrawModernIcon(Graphics g, int x, int y, ModernMessageIcon icon)
////        {
////            Color iconColor = Color.White;
////            using (var pen = new Pen(iconColor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
////            using (var brush = new SolidBrush(iconColor))
////            {
////                if (icon == ModernMessageIcon.Info)
////                {
////                    iconColor = Color.FromArgb(0, 180, 255);
////                    pen.Color = iconColor; brush.Color = iconColor;
////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
////                    g.FillEllipse(brush, new Rectangle(x + 15, y + 8, 6, 6));
////                    g.DrawLine(pen, x + 18, y + 16, x + 18, y + 26);
////                }
////                else if (icon == ModernMessageIcon.Warning)
////                {
////                    iconColor = Color.FromArgb(255, 160, 0);
////                    pen.Color = iconColor; brush.Color = iconColor;
////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
////                    g.DrawLine(pen, x + 18, y + 8, x + 18, y + 22);
////                    g.FillEllipse(brush, new Rectangle(x + 15, y + 26, 6, 6));
////                }
////                else if (icon == ModernMessageIcon.Error)
////                {
////                    iconColor = Color.FromArgb(255, 50, 50);
////                    pen.Color = iconColor; brush.Color = iconColor;
////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
////                    g.DrawLine(pen, x + 10, y + 10, x + 26, y + 26);
////                    g.DrawLine(pen, x + 26, y + 10, x + 10, y + 26);
////                }
////                else if (icon == ModernMessageIcon.Success)
////                {
////                    iconColor = Color.FromArgb(0, 220, 100);
////                    pen.Color = iconColor; brush.Color = iconColor;
////                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
////                    g.DrawLine(pen, x + 10, y + 18, x + 16, y + 24);
////                    g.DrawLine(pen, x + 16, y + 24, x + 28, y + 12);
////                }
////            }
////        }

////        protected override void OnMouseMove(MouseEventArgs e)
////        {
////            base.OnMouseMove(e);
////            int newHover = -1;
////            for (int i = 0; i < _buttonRects.Count; i++) if (_buttonRects[i].Contains(e.Location)) newHover = i;

////            bool newHoverClose = _closeRect.Contains(e.Location);

////            if (newHover != _hoveredButton || newHoverClose != _hoverClose)
////            {
////                _hoveredButton = newHover;
////                _hoverClose = newHoverClose;
////                Cursor = (newHover != -1 || _hoverClose) ? Cursors.Hand : Cursors.Default;
////                Invalidate();
////            }
////        }

////        protected override void OnMouseDown(MouseEventArgs e)
////        {
////            base.OnMouseDown(e);
////            _pressedButton = -1;
////            for (int i = 0; i < _buttonRects.Count; i++) if (_buttonRects[i].Contains(e.Location)) _pressedButton = i;
////            Invalidate();
////        }

////        protected override void OnMouseUp(MouseEventArgs e)
////        {
////            base.OnMouseUp(e);
////            if (_pressedButton != -1 && _buttonRects[_pressedButton].Contains(e.Location))
////            {
////                _result = _buttonResults[_pressedButton];
////                DialogResult = DialogResult.OK;
////            }
////            else if (_closeRect.Contains(e.Location))
////            {
////                _result = ModernDialogResult.Cancel;
////                DialogResult = DialogResult.Cancel;
////            }
////            _pressedButton = -1;
////            Invalidate();
////        }

////        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
////        {
////            var path = new GraphicsPath();
////            if (rect.Width <= 0 || rect.Height <= 0) return path;
////            int r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
////            if (r <= 0) { path.AddRectangle(rect); return path; }
////            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
////            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
////            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
////            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
////            path.CloseFigure();
////            return path;
////        }

////        [DllImport("dwmapi.dll")]
////        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
////    }

////    /// <summary>
////    /// A modern, animated toaster notification system.
////    /// </summary>
////    public static class ModernToaster
////    {
////        private static List<ToasterForm> _activeToasters = new List<ToasterForm>();

////        /// <summary>
////        /// Shows a toaster notification.
////        /// </summary>
////        /// <param name="title">The title of the notification.</param>
////        /// <param name="message">The message body.</param>
////        /// <param name="type">The icon and accent color type.</param>
////        /// <param name="duration">Auto-close time in milliseconds. Set to 0 to make it sticky (never auto-close).</param>
////        public static void Show(string title, string message, ToasterType type = ToasterType.Info, int duration = 3000)
////        {
////            if (Application.OpenForms.Count == 0) return;

////            var mainForm = Application.OpenForms[0];
////            if (mainForm.InvokeRequired)
////            {
////                mainForm.BeginInvoke((MethodInvoker)delegate { Show(title, message, type, duration); });
////                return;
////            }

////            var toaster = new ToasterForm(title, message, type, duration);

////            int yOffset = 20;
////            foreach (var t in _activeToasters)
////            {
////                if (t.Screen == Screen.FromControl(mainForm))
////                    yOffset += t.Height + 10;
////            }

////            toaster.StartPosition = FormStartPosition.Manual;
////            var screen = Screen.FromControl(mainForm).WorkingArea;
////            toaster.Location = new Point(screen.Right - toaster.Width - 20, screen.Bottom - toaster.Height - yOffset);

////            _activeToasters.Add(toaster);
////            toaster.FormClosed += (s, e) => { _activeToasters.Remove(toaster); };

////            toaster.Show(mainForm);
////        }
////    }

////    public class ToasterForm : Form
////    {
////        private Color _backColor;
////        private Color _borderColor;
////        private Color _textColor;
////        private Color _subTextColor;
////        private int _cornerRadius = 8;

////        private string _title;
////        private string _message;
////        private ToasterType _type;
////        private int _duration;
////        private int _initialDuration; // Saved to calculate progress bar accurately
////        private System.Windows.Forms.Timer _timer;
////        private float _progress = 1f;
////        private Rectangle _closeRect;
////        private bool _hoverClose = false;
////        private float _opacity = 0f;

////        public ToasterForm(string title, string message, ToasterType type, int duration)
////        {
////            _title = title;
////            _message = message;
////            _type = type;
////            _duration = duration;
////            _initialDuration = duration; // Save initial value

////            ApplyTheme();

////            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
////            FormBorderStyle = FormBorderStyle.None;
////            ShowInTaskbar = false;
////            TopMost = true;
////            Font = new Font("Segoe UI Variable Display", 9f);
////            Size = new Size(350, 90);
////            Opacity = 0;

////            // Removed TransparencyKey to fix the severe flickering issue.
////            // We use a mathematical Region instead for crisp rounded corners.
////            this.BackColor = _backColor;

////            _timer = new System.Windows.Forms.Timer();
////            _timer.Interval = 16;
////            _timer.Tick += Timer_Tick;
////            _timer.Start();

////            this.HandleCreated += (s, e) => {
////                int val = 2;
////                DwmSetWindowAttribute(this.Handle, 2, ref val, sizeof(int));
////            };
////        }

////        protected override CreateParams CreateParams
////        {
////            get
////            {
////                var cp = base.CreateParams;
////                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED (Fixes flicker)
////                return cp;
////            }
////        }

////        protected override void OnResize(EventArgs e)
////        {
////            base.OnResize(e);
////            if (this.Handle != IntPtr.Zero && Width > 0 && Height > 0)
////            {
////                // Apply Region to clip the square form into a rounded shape
////                this.Region = new Region(GetRoundedPath(ClientRectangle, _cornerRadius));
////            }
////        }

////        private void ApplyTheme()
////        {
////            bool isDark = ModernNotificationConfig.Theme == NotificationThemeMode.Dark;
////            switch (ModernNotificationConfig.Style)
////            {
////                case NotificationDesignStyle.DashboardPremium:
////                    _backColor = isDark ? Color.FromArgb(32, 32, 38) : Color.FromArgb(248, 250, 252);
////                    _textColor = isDark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(30, 30, 30);
////                    _subTextColor = isDark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(100, 110, 120);
////                    _cornerRadius = 8;
////                    break;
////                case NotificationDesignStyle.FluentGlass:
////                    _backColor = isDark ? Color.FromArgb(40, 40, 48) : Color.FromArgb(243, 244, 246);
////                    _textColor = isDark ? Color.White : Color.Black;
////                    _subTextColor = isDark ? Color.LightGray : Color.DarkGray;
////                    _cornerRadius = 10;
////                    break;
////                case NotificationDesignStyle.MaterialFlat:
////                    _backColor = isDark ? Color.FromArgb(18, 18, 18) : Color.White;
////                    _textColor = isDark ? Color.White : Color.Black;
////                    _subTextColor = isDark ? Color.FromArgb(180, 180, 180) : Color.FromArgb(70, 70, 70);
////                    _cornerRadius = 2;
////                    break;
////                case NotificationDesignStyle.SoftNeumorphic:
////                    _backColor = isDark ? Color.FromArgb(32, 38, 48) : Color.FromArgb(224, 229, 236);
////                    _textColor = isDark ? Color.FromArgb(220, 230, 240) : Color.FromArgb(50, 60, 70);
////                    _subTextColor = isDark ? Color.FromArgb(160, 170, 180) : Color.FromArgb(100, 110, 120);
////                    _cornerRadius = 12;
////                    break;
////                case NotificationDesignStyle.CyberpunkIndustrial:
////                    _backColor = Color.FromArgb(12, 12, 14);
////                    _textColor = Color.FromArgb(230, 240, 255);
////                    _subTextColor = Color.FromArgb(100, 120, 140);
////                    _cornerRadius = 0;
////                    break;
////            }

////            switch (_type)
////            {
////                case ToasterType.Info: _borderColor = Color.FromArgb(0, 180, 255); break;
////                case ToasterType.Success: _borderColor = Color.FromArgb(0, 220, 100); break;
////                case ToasterType.Warning: _borderColor = Color.FromArgb(255, 160, 0); break;
////                case ToasterType.Error: _borderColor = Color.FromArgb(255, 50, 50); break;
////            }
////        }

////        public Screen Screen { get; set; }

////        private void Timer_Tick(object sender, EventArgs e)
////        {
////            if (IsDisposed) return;

////            bool needsRedraw = false;

////            // Fade In
////            if (_opacity < 1f)
////            {
////                _opacity += 0.1f;
////                if (_opacity > 1f) _opacity = 1f;
////                Opacity = _opacity;
////                needsRedraw = true;
////            }

////            // Countdown & Progress Bar
////            if (_initialDuration > 0)
////            {
////                if (_duration > 0)
////                {
////                    _duration -= 16;
////                    // Calculate progress based on INITIAL duration, not hardcoded 3000
////                    _progress = (float)_duration / _initialDuration;
////                    if (_progress < 0) _progress = 0;
////                    needsRedraw = true;
////                }
////                else if (!IsDisposed)
////                {
////                    // Fade Out
////                    _opacity -= 0.1f;
////                    Opacity = _opacity;
////                    if (_opacity <= 0f)
////                    {
////                        _timer.Stop();
////                        this.Close();
////                        return;
////                    }
////                    needsRedraw = true;
////                }
////            }
////            else if (_opacity >= 1f)
////            {
////                // If duration is 0 (Sticky), stop the timer to save CPU once faded in
////                _timer.Stop();
////            }

////            if (needsRedraw) Invalidate();
////        }

////        protected override void OnPaint(PaintEventArgs e)
////        {
////            Graphics g = e.Graphics;
////            g.SmoothingMode = SmoothingMode.AntiAlias;
////            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

////            // Draw Background
////            using (var bgBrush = new SolidBrush(_backColor))
////            {
////                g.FillRectangle(bgBrush, ClientRectangle);
////            }

////            // Cyberpunk style gets a neon border
////            if (ModernNotificationConfig.Style == NotificationDesignStyle.CyberpunkIndustrial)
////            {
////                using (var glowPen = new Pen(Color.FromArgb(100, _borderColor), 4))
////                using (var corePen = new Pen(_borderColor, 1))
////                {
////                    g.DrawRectangle(glowPen, new Rectangle(0, 0, Width - 1, Height - 1));
////                    g.DrawRectangle(corePen, new Rectangle(0, 0, Width - 1, Height - 1));
////                }
////            }
////            else
////            {
////                // Draw a clean, colored left accent bar
////                using (var sideBrush = new SolidBrush(_borderColor))
////                {
////                    g.FillRectangle(sideBrush, new Rectangle(0, 0, 6, Height));
////                }
////            }

////            int iconX = 20, iconY = 20;
////            using (var pen = new Pen(_borderColor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
////            using (var brush = new SolidBrush(_borderColor))
////            {
////                if (_type == ToasterType.Info)
////                {
////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
////                    g.FillEllipse(brush, new Rectangle(iconX + 13, iconY + 6, 6, 6));
////                    g.DrawLine(pen, iconX + 16, iconY + 14, iconX + 16, iconY + 24);
////                }
////                else if (_type == ToasterType.Success)
////                {
////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
////                    g.DrawLine(pen, iconX + 8, iconY + 16, iconX + 14, iconY + 22);
////                    g.DrawLine(pen, iconX + 14, iconY + 22, iconX + 26, iconY + 10);
////                }
////                else if (_type == ToasterType.Warning)
////                {
////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
////                    g.DrawLine(pen, iconX + 16, iconY + 6, iconX + 16, iconY + 20);
////                    g.FillEllipse(brush, new Rectangle(iconX + 13, iconY + 24, 6, 6));
////                }
////                else if (_type == ToasterType.Error)
////                {
////                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
////                    g.DrawLine(pen, iconX + 9, iconY + 9, iconX + 23, iconY + 23);
////                    g.DrawLine(pen, iconX + 23, iconY + 9, iconX + 9, iconY + 23);
////                }
////            }

////            using (var titleFont = new Font(Font, FontStyle.Bold))
////            using (var titleBrush = new SolidBrush(_textColor))
////            using (var msgBrush = new SolidBrush(_subTextColor))
////            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
////            {
////                g.DrawString(_title, titleFont, titleBrush, new Rectangle(64, 18, Width - 100, 25), sf);
////                g.DrawString(_message, Font, msgBrush, new Rectangle(64, 45, Width - 100, 40), sf);
////            }

////            _closeRect = new Rectangle(Width - 32, 12, 20, 20);
////            if (_hoverClose) { using (var b = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) g.FillRectangle(b, _closeRect); }
////            using (var crossPen = new Pen(_subTextColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
////            {
////                g.DrawLine(crossPen, _closeRect.Left + 5, _closeRect.Top + 5, _closeRect.Right - 5, _closeRect.Bottom - 5);
////                g.DrawLine(crossPen, _closeRect.Right - 5, _closeRect.Top + 5, _closeRect.Left + 5, _closeRect.Bottom - 5);
////            }

////            // Only draw progress bar if a duration is set (> 0)
////            if (_initialDuration > 0)
////            {
////                Rectangle progRect = new Rectangle(2, Height - 4, Width - 4, 4);
////                int progWidth = Math.Max(0, (int)(progRect.Width * _progress));
////                using (var progBrush = new SolidBrush(_borderColor))
////                {
////                    g.FillRectangle(progBrush, new Rectangle(progRect.X, progRect.Y, progWidth, progRect.Height));
////                }
////            }
////        }

////        protected override void OnMouseMove(MouseEventArgs e)
////        {
////            base.OnMouseMove(e);
////            bool newHover = _closeRect.Contains(e.Location);
////            if (newHover != _hoverClose)
////            {
////                _hoverClose = newHover;
////                Cursor = _hoverClose ? Cursors.Hand : Cursors.Default;
////                Invalidate();
////            }
////        }

////        protected override void OnMouseUp(MouseEventArgs e)
////        {
////            base.OnMouseUp(e);
////            // If user clicks close, force close even if duration is 0 (sticky)
////            if (_closeRect.Contains(e.Location))
////            {
////                _duration = 0;
////                _initialDuration = 1; // Trick timer into fading out
////                _timer.Start();
////            }
////        }

////        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
////        {
////            var path = new GraphicsPath();
////            if (rect.Width <= 0 || rect.Height <= 0) return path;
////            int r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
////            if (r <= 0) { path.AddRectangle(rect); return path; }
////            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
////            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
////            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
////            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
////            path.CloseFigure();
////            return path;
////        }

////        [DllImport("dwmapi.dll")]
////        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
////    }
////}













////======= VER 1.7 ========


//using System;
//using System.Collections.Generic;
//using System.Drawing;
//using System.Drawing.Drawing2D;
//using System.Drawing.Text;
//using System.Runtime.InteropServices;
//using System.Windows.Forms;

//namespace UltraModernUI.Controls
//{
//    #region Enums & Config
//    public enum ModernMessageIcon { None, Info, Warning, Error, Success, Question }
//    public enum ModernDialogResult { None, OK, Cancel, Yes, No, Abort, Retry, Ignore }
//    public enum ToasterType { Info, Success, Warning, Error }

//    public enum NotificationThemeMode { Light, Dark }
//    public enum NotificationDesignStyle { DashboardPremium, FluentGlass, MaterialFlat, SoftNeumorphic, CyberpunkIndustrial }

//    public static class ModernNotificationConfig
//    {
//        public static NotificationThemeMode Theme { get; set; } = NotificationThemeMode.Dark;
//        public static NotificationDesignStyle Style { get; set; } = NotificationDesignStyle.DashboardPremium;
//    }
//    #endregion

//    public class ModernMessageBox : Form
//    {
//        private Color _backColor;
//        private Color _accentColor;
//        private Color _textColor;
//        private Color _subTextColor;
//        private int _cornerRadius = 8;

//        private string _title;
//        private string _message;
//        private ModernMessageIcon _icon;
//        private MessageBoxButtons _buttons;
//        private List<ModernDialogResult> _buttonResults = new List<ModernDialogResult>();
//        private List<Rectangle> _buttonRects = new List<Rectangle>();
//        private int _hoveredButton = -1;
//        private int _pressedButton = -1;
//        private Rectangle _closeRect;
//        private bool _hoverClose = false;

//        public ModernMessageBox(string message, string title, MessageBoxButtons buttons, ModernMessageIcon icon)
//        {
//            _title = title;
//            _message = message;
//            _buttons = buttons;
//            _icon = icon;

//            ApplyTheme();

//            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
//            FormBorderStyle = FormBorderStyle.None;
//            StartPosition = FormStartPosition.CenterParent;
//            BackColor = Color.Magenta;
//            TransparencyKey = Color.Magenta;
//            TopMost = true;
//            ShowInTaskbar = false;
//            Font = new Font("Segoe UI Variable Display", 9f);

//            this.HandleCreated += (s, e) => {
//                int val = 2;
//                DwmSetWindowAttribute(this.Handle, 2, ref val, sizeof(int));
//            };
//        }

//        private void ApplyTheme()
//        {
//            bool isDark = ModernNotificationConfig.Theme == NotificationThemeMode.Dark;
//            switch (ModernNotificationConfig.Style)
//            {
//                case NotificationDesignStyle.DashboardPremium:
//                    _backColor = isDark ? Color.FromArgb(32, 32, 38) : Color.FromArgb(248, 250, 252);
//                    _accentColor = isDark ? Color.FromArgb(0, 120, 212) : Color.FromArgb(0, 100, 180);
//                    _textColor = isDark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(30, 30, 30);
//                    _subTextColor = isDark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(100, 110, 120);
//                    _cornerRadius = 8;
//                    break;
//                case NotificationDesignStyle.FluentGlass:
//                    _backColor = isDark ? Color.FromArgb(40, 40, 48) : Color.FromArgb(243, 244, 246);
//                    _accentColor = Color.FromArgb(0, 180, 255);
//                    _textColor = isDark ? Color.White : Color.Black;
//                    _subTextColor = isDark ? Color.LightGray : Color.DarkGray;
//                    _cornerRadius = 10;
//                    break;
//                case NotificationDesignStyle.MaterialFlat:
//                    _backColor = isDark ? Color.FromArgb(18, 18, 18) : Color.White;
//                    _accentColor = Color.FromArgb(98, 0, 238);
//                    _textColor = isDark ? Color.White : Color.Black;
//                    _subTextColor = isDark ? Color.FromArgb(180, 180, 180) : Color.FromArgb(70, 70, 70);
//                    _cornerRadius = 2;
//                    break;
//                case NotificationDesignStyle.SoftNeumorphic:
//                    _backColor = isDark ? Color.FromArgb(32, 38, 48) : Color.FromArgb(224, 229, 236);
//                    _accentColor = Color.FromArgb(255, 87, 51);
//                    _textColor = isDark ? Color.FromArgb(220, 230, 240) : Color.FromArgb(50, 60, 70);
//                    _subTextColor = isDark ? Color.FromArgb(160, 170, 180) : Color.FromArgb(100, 110, 120);
//                    _cornerRadius = 12;
//                    break;
//                case NotificationDesignStyle.CyberpunkIndustrial:
//                    _backColor = Color.FromArgb(12, 12, 14);
//                    _accentColor = Color.FromArgb(0, 255, 204);
//                    _textColor = Color.FromArgb(230, 240, 255);
//                    _subTextColor = Color.FromArgb(100, 120, 140);
//                    _cornerRadius = 0;
//                    break;
//            }
//        }

//        public static ModernDialogResult Show(string message, string title = "Message", MessageBoxButtons buttons = MessageBoxButtons.OK, ModernMessageIcon icon = ModernMessageIcon.None)
//        {
//            using (var box = new ModernMessageBox(message, title, buttons, icon))
//            {
//                box.CalculateSize();
//                if (box.ShowDialog() == DialogResult.OK)
//                    return box._result;
//                return ModernDialogResult.None;
//            }
//        }

//        private ModernDialogResult _result = ModernDialogResult.None;
//        private void CalculateSize()
//        {
//            using (var g = Graphics.FromHwnd(IntPtr.Zero))
//            {
//                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
//                SizeF titleSize = g.MeasureString(_title, new Font(Font, FontStyle.Bold), 500);
//                SizeF msgSize = g.MeasureString(_message, Font, 400);

//                int width = Math.Max(400, (int)Math.Max(titleSize.Width, msgSize.Width) + 80);
//                int height = (int)msgSize.Height + 120;

//                if (_icon != ModernMessageIcon.None) width += 60;

//                this.Size = new Size(width, height);

//                _buttonResults.Clear();
//                switch (_buttons)
//                {
//                    case MessageBoxButtons.OK: _buttonResults.Add(ModernDialogResult.OK); break;
//                    case MessageBoxButtons.OKCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.OK); break;
//                    case MessageBoxButtons.YesNo: _buttonResults.Add(ModernDialogResult.No); _buttonResults.Add(ModernDialogResult.Yes); break;
//                    case MessageBoxButtons.YesNoCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.No); _buttonResults.Add(ModernDialogResult.Yes); break;
//                    case MessageBoxButtons.RetryCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.Retry); break;
//                    case MessageBoxButtons.AbortRetryIgnore: _buttonResults.Add(ModernDialogResult.Ignore); _buttonResults.Add(ModernDialogResult.Retry); _buttonResults.Add(ModernDialogResult.Abort); break;
//                }

//                _buttonRects.Clear();
//                int btnWidth = 100;
//                int btnHeight = 36;
//                int spacing = 12;
//                int totalBtnWidth = (_buttonResults.Count * btnWidth) + ((_buttonResults.Count - 1) * spacing);
//                int startX = width - totalBtnWidth - 20;

//                for (int i = 0; i < _buttonResults.Count; i++)
//                {
//                    _buttonRects.Add(new Rectangle(startX + (i * (btnWidth + spacing)), height - btnHeight - 20, btnWidth, btnHeight));
//                }

//                _closeRect = new Rectangle(width - 40, 12, 28, 28);
//            }
//        }

//        protected override void WndProc(ref Message m)
//        {
//            base.WndProc(ref m);
//            if (m.Msg == 0x84) // WM_NCHITTEST
//            {
//                int x = (int)m.LParam & 0xFFFF;
//                int y = (int)m.LParam >> 16;
//                Point pos = PointToClient(new Point(x, y));

//                foreach (var btn in _buttonRects) if (btn.Contains(pos)) return;
//                if (_closeRect.Contains(pos)) return;

//                m.Result = (IntPtr)0x2; // HTCAPTION
//            }
//        }

//        protected override void OnPaint(PaintEventArgs e)
//        {
//            Graphics g = e.Graphics;
//            g.SmoothingMode = SmoothingMode.AntiAlias;
//            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

//            using (var bgPath = GetRoundedPath(ClientRectangle, _cornerRadius))
//            using (var bgBrush = new SolidBrush(_backColor))
//            {
//                g.FillPath(bgBrush, bgPath);
//            }

//            if (ModernNotificationConfig.Style == NotificationDesignStyle.CyberpunkIndustrial)
//            {
//                using (var glowPen = new Pen(Color.FromArgb(100, _accentColor), 4))
//                using (var corePen = new Pen(_accentColor, 1))
//                using (var bgPath = GetRoundedPath(ClientRectangle, _cornerRadius))
//                {
//                    g.DrawPath(glowPen, bgPath);
//                    g.DrawPath(corePen, bgPath);
//                }
//            }

//            int iconX = 24;
//            int iconY = 60;
//            if (_icon != ModernMessageIcon.None)
//            {
//                DrawModernIcon(g, iconX, iconY, _icon);
//                iconX += 50;
//            }

//            using (var titleFont = new Font(Font, FontStyle.Bold))
//            using (var titleBrush = new SolidBrush(_textColor))
//            using (var msgBrush = new SolidBrush(_subTextColor))
//            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
//            {
//                g.DrawString(_title, titleFont, titleBrush, new Rectangle(24, 16, Width - 80, 30), sf);
//                g.DrawString(_message, Font, msgBrush, new Rectangle(iconX, 50, Width - iconX - 30, Height - 110), sf);
//            }

//            if (_hoverClose)
//            {
//                using (var closeBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
//                using (var closePath = GetRoundedPath(_closeRect, 4))
//                    g.FillPath(closeBrush, closePath);
//            }
//            using (var crossPen = new Pen(_subTextColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
//            {
//                g.DrawLine(crossPen, _closeRect.Left + 8, _closeRect.Top + 8, _closeRect.Right - 8, _closeRect.Bottom - 8);
//                g.DrawLine(crossPen, _closeRect.Right - 8, _closeRect.Top + 8, _closeRect.Left + 8, _closeRect.Bottom - 8);
//            }

//            for (int i = 0; i < _buttonResults.Count; i++)
//            {
//                bool isHover = (_hoveredButton == i && _pressedButton == -1);
//                bool isPressed = (_pressedButton == i);
//                bool isPrimary = (i == _buttonResults.Count - 1);

//                Rectangle btnRect = _buttonRects[i];
//                Color btnBg = Color.Empty;
//                Color btnText = _textColor;

//                if (isPrimary)
//                {
//                    btnBg = _accentColor;
//                    if (isHover) btnBg = ControlPaint.Light(_accentColor, 0.1f);
//                    else if (isPressed) btnBg = ControlPaint.Dark(_accentColor, 0.1f);
//                    btnText = Color.White;
//                }
//                else
//                {
//                    btnBg = ControlPaint.Light(_backColor, 0.05f);
//                    if (isHover) btnBg = ControlPaint.Light(_backColor, 0.1f);
//                    else if (isPressed) btnBg = ControlPaint.Dark(_backColor, 0.05f);
//                }

//                using (var btnPath = GetRoundedPath(btnRect, _cornerRadius == 0 ? 0 : 6))
//                using (var btnBrush = new SolidBrush(btnBg))
//                {
//                    g.FillPath(btnBrush, btnPath);
//                }

//                using (var textBrush = new SolidBrush(btnText))
//                using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center })
//                {
//                    g.DrawString(_buttonResults[i].ToString(), Font, textBrush, btnRect, sf);
//                }
//            }
//        }

//        private void DrawModernIcon(Graphics g, int x, int y, ModernMessageIcon icon)
//        {
//            Color iconColor = Color.White;
//            using (var pen = new Pen(iconColor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
//            using (var brush = new SolidBrush(iconColor))
//            {
//                if (icon == ModernMessageIcon.Info)
//                {
//                    iconColor = Color.FromArgb(0, 180, 255);
//                    pen.Color = iconColor; brush.Color = iconColor;
//                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
//                    g.FillEllipse(brush, new Rectangle(x + 15, y + 8, 6, 6));
//                    g.DrawLine(pen, x + 18, y + 16, x + 18, y + 26);
//                }
//                else if (icon == ModernMessageIcon.Warning)
//                {
//                    iconColor = Color.FromArgb(255, 160, 0);
//                    pen.Color = iconColor; brush.Color = iconColor;
//                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
//                    g.DrawLine(pen, x + 18, y + 8, x + 18, y + 22);
//                    g.FillEllipse(brush, new Rectangle(x + 15, y + 26, 6, 6));
//                }
//                else if (icon == ModernMessageIcon.Error)
//                {
//                    iconColor = Color.FromArgb(255, 50, 50);
//                    pen.Color = iconColor; brush.Color = iconColor;
//                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
//                    g.DrawLine(pen, x + 10, y + 10, x + 26, y + 26);
//                    g.DrawLine(pen, x + 26, y + 10, x + 10, y + 26);
//                }
//                else if (icon == ModernMessageIcon.Success)
//                {
//                    iconColor = Color.FromArgb(0, 220, 100);
//                    pen.Color = iconColor; brush.Color = iconColor;
//                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
//                    g.DrawLine(pen, x + 10, y + 18, x + 16, y + 24);
//                    g.DrawLine(pen, x + 16, y + 24, x + 28, y + 12);
//                }
//            }
//        }

//        protected override void OnMouseMove(MouseEventArgs e)
//        {
//            base.OnMouseMove(e);
//            int newHover = -1;
//            for (int i = 0; i < _buttonRects.Count; i++) if (_buttonRects[i].Contains(e.Location)) newHover = i;

//            bool newHoverClose = _closeRect.Contains(e.Location);

//            if (newHover != _hoveredButton || newHoverClose != _hoverClose)
//            {
//                _hoveredButton = newHover;
//                _hoverClose = newHoverClose;
//                Cursor = (newHover != -1 || _hoverClose) ? Cursors.Hand : Cursors.Default;
//                Invalidate();
//            }
//        }

//        protected override void OnMouseDown(MouseEventArgs e)
//        {
//            base.OnMouseDown(e);
//            _pressedButton = -1;
//            for (int i = 0; i < _buttonRects.Count; i++) if (_buttonRects[i].Contains(e.Location)) _pressedButton = i;
//            Invalidate();
//        }

//        protected override void OnMouseUp(MouseEventArgs e)
//        {
//            base.OnMouseUp(e);
//            if (_pressedButton != -1 && _buttonRects[_pressedButton].Contains(e.Location))
//            {
//                _result = _buttonResults[_pressedButton];
//                DialogResult = DialogResult.OK;
//            }
//            else if (_closeRect.Contains(e.Location))
//            {
//                _result = ModernDialogResult.Cancel;
//                DialogResult = DialogResult.Cancel;
//            }
//            _pressedButton = -1;
//            Invalidate();
//        }

//        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
//        {
//            var path = new GraphicsPath();
//            if (rect.Width <= 0 || rect.Height <= 0) return path;
//            int r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
//            if (r <= 0) { path.AddRectangle(rect); return path; }
//            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
//            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
//            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
//            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
//            path.CloseFigure();
//            return path;
//        }

//        [DllImport("dwmapi.dll")]
//        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
//    }

//    public static class ModernToaster
//    {
//        private static List<ToasterForm> _activeToasters = new List<ToasterForm>();

//        public static void Show(string title, string message, ToasterType type = ToasterType.Info, int duration = 3000)
//        {
//            if (Application.OpenForms.Count == 0) return;

//            var mainForm = Application.OpenForms[0];
//            if (mainForm.InvokeRequired)
//            {
//                mainForm.BeginInvoke((MethodInvoker)delegate { Show(title, message, type, duration); });
//                return;
//            }

//            var toaster = new ToasterForm(title, message, type, duration);

//            int yOffset = 20;
//            foreach (var t in _activeToasters)
//            {
//                if (t.Screen == Screen.FromControl(mainForm))
//                    yOffset += t.Height + 10;
//            }

//            toaster.StartPosition = FormStartPosition.Manual;
//            var screen = Screen.FromControl(mainForm).WorkingArea;
//            toaster.Location = new Point(screen.Right - toaster.Width - 20, screen.Bottom - toaster.Height - yOffset);

//            _activeToasters.Add(toaster);
//            toaster.FormClosed += (s, e) => { _activeToasters.Remove(toaster); };

//            toaster.Show(mainForm);
//        }
//    }

//    public class ToasterForm : Form
//    {
//        private Color _backColor;
//        private Color _borderColor;
//        private Color _textColor;
//        private Color _subTextColor;
//        private int _cornerRadius = 8;

//        private string _title;
//        private string _message;
//        private ToasterType _type;
//        private int _duration;
//        private int _initialDuration;
//        private System.Windows.Forms.Timer _timer;
//        private float _progress = 1f;
//        private Rectangle _closeRect;
//        private bool _hoverClose = false;
//        private float _opacity = 0f;

//        // State machine to prevent timer bugs
//        private int _state = 0; // 0 = Fade In, 1 = Waiting/Counting, 2 = Fade Out

//        public ToasterForm(string title, string message, ToasterType type, int duration)
//        {
//            _title = title;
//            _message = message;
//            _type = type;
//            _duration = duration;
//            _initialDuration = duration;

//            ApplyTheme();

//            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
//            FormBorderStyle = FormBorderStyle.None;
//            ShowInTaskbar = false;
//            TopMost = true;
//            Font = new Font("Segoe UI Variable Display", 9f);
//            Size = new Size(350, 90);
//            Opacity = 0;
//            BackColor = _backColor;

//            _timer = new System.Windows.Forms.Timer();
//            _timer.Interval = 16;
//            _timer.Tick += Timer_Tick;
//            _timer.Start();

//            this.HandleCreated += (s, e) => {
//                int val = 2;
//                DwmSetWindowAttribute(this.Handle, 2, ref val, sizeof(int));
//            };
//        }

//        protected override void OnResize(EventArgs e)
//        {
//            base.OnResize(e);
//            if (Width > 0 && Height > 0)
//            {
//                // CRITICAL FIX: Dispose the GraphicsPath to prevent severe GDI+ memory leaks!
//                using (var path = GetRoundedPath(ClientRectangle, _cornerRadius))
//                {
//                    this.Region = new Region(path);
//                }
//            }
//        }

//        private void ApplyTheme()
//        {
//            bool isDark = ModernNotificationConfig.Theme == NotificationThemeMode.Dark;
//            switch (ModernNotificationConfig.Style)
//            {
//                case NotificationDesignStyle.DashboardPremium:
//                    _backColor = isDark ? Color.FromArgb(32, 32, 38) : Color.FromArgb(248, 250, 252);
//                    _textColor = isDark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(30, 30, 30);
//                    _subTextColor = isDark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(100, 110, 120);
//                    _cornerRadius = 8;
//                    break;
//                case NotificationDesignStyle.FluentGlass:
//                    _backColor = isDark ? Color.FromArgb(40, 40, 48) : Color.FromArgb(243, 244, 246);
//                    _textColor = isDark ? Color.White : Color.Black;
//                    _subTextColor = isDark ? Color.LightGray : Color.DarkGray;
//                    _cornerRadius = 10;
//                    break;
//                case NotificationDesignStyle.MaterialFlat:
//                    _backColor = isDark ? Color.FromArgb(18, 18, 18) : Color.White;
//                    _textColor = isDark ? Color.White : Color.Black;
//                    _subTextColor = isDark ? Color.FromArgb(180, 180, 180) : Color.FromArgb(70, 70, 70);
//                    _cornerRadius = 2;
//                    break;
//                case NotificationDesignStyle.SoftNeumorphic:
//                    _backColor = isDark ? Color.FromArgb(32, 38, 48) : Color.FromArgb(224, 229, 236);
//                    _textColor = isDark ? Color.FromArgb(220, 230, 240) : Color.FromArgb(50, 60, 70);
//                    _subTextColor = isDark ? Color.FromArgb(160, 170, 180) : Color.FromArgb(100, 110, 120);
//                    _cornerRadius = 12;
//                    break;
//                case NotificationDesignStyle.CyberpunkIndustrial:
//                    _backColor = Color.FromArgb(12, 12, 14);
//                    _textColor = Color.FromArgb(230, 240, 255);
//                    _subTextColor = Color.FromArgb(100, 120, 140);
//                    _cornerRadius = 0;
//                    break;
//            }

//            switch (_type)
//            {
//                case ToasterType.Info: _borderColor = Color.FromArgb(0, 180, 255); break;
//                case ToasterType.Success: _borderColor = Color.FromArgb(0, 220, 100); break;
//                case ToasterType.Warning: _borderColor = Color.FromArgb(255, 160, 0); break;
//                case ToasterType.Error: _borderColor = Color.FromArgb(255, 50, 50); break;
//            }
//        }

//        public Screen Screen { get; set; }

//        private void Timer_Tick(object sender, EventArgs e)
//        {
//            if (IsDisposed) return;

//            // STATE 0: Fade In
//            if (_state == 0)
//            {
//                _opacity += 0.1f;
//                if (_opacity >= 1f)
//                {
//                    _opacity = 1f;
//                    _state = 1; // Move to Counting state
//                }
//                Opacity = _opacity;
//                Invalidate();
//            }
//            // STATE 1: Counting down (Waiting)
//            else if (_state == 1)
//            {
//                if (_initialDuration > 0)
//                {
//                    _duration -= 16;
//                    _progress = (float)_duration / _initialDuration;
//                    if (_progress < 0) _progress = 0;

//                    if (_duration <= 0)
//                    {
//                        _state = 2; // Move to Fade Out state
//                    }
//                    Invalidate();
//                }
//                else
//                {
//                    // If duration is 0 (Sticky), stop timer to save CPU
//                    _timer.Stop();
//                }
//            }
//            // STATE 2: Fade Out
//            else if (_state == 2)
//            {
//                _opacity -= 0.1f;
//                if (_opacity <= 0f)
//                {
//                    _timer.Stop();
//                    this.Close();
//                    return;
//                }
//                Opacity = _opacity;
//                Invalidate();
//            }
//        }

//        protected override void OnPaint(PaintEventArgs e)
//        {
//            Graphics g = e.Graphics;
//            g.SmoothingMode = SmoothingMode.AntiAlias;
//            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

//            using (var bgBrush = new SolidBrush(_backColor))
//            {
//                g.FillRectangle(bgBrush, ClientRectangle);
//            }

//            if (ModernNotificationConfig.Style == NotificationDesignStyle.CyberpunkIndustrial)
//            {
//                using (var glowPen = new Pen(Color.FromArgb(100, _borderColor), 4))
//                using (var corePen = new Pen(_borderColor, 1))
//                {
//                    g.DrawRectangle(glowPen, new Rectangle(0, 0, Width - 1, Height - 1));
//                    g.DrawRectangle(corePen, new Rectangle(0, 0, Width - 1, Height - 1));
//                }
//            }
//            else
//            {
//                using (var sideBrush = new SolidBrush(_borderColor))
//                {
//                    g.FillRectangle(sideBrush, new Rectangle(0, 0, 6, Height));
//                }
//            }

//            int iconX = 20, iconY = 20;
//            using (var pen = new Pen(_borderColor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
//            using (var brush = new SolidBrush(_borderColor))
//            {
//                if (_type == ToasterType.Info)
//                {
//                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
//                    g.FillEllipse(brush, new Rectangle(iconX + 13, iconY + 6, 6, 6));
//                    g.DrawLine(pen, iconX + 16, iconY + 14, iconX + 16, iconY + 24);
//                }
//                else if (_type == ToasterType.Success)
//                {
//                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
//                    g.DrawLine(pen, iconX + 8, iconY + 16, iconX + 14, iconY + 22);
//                    g.DrawLine(pen, iconX + 14, iconY + 22, iconX + 26, iconY + 10);
//                }
//                else if (_type == ToasterType.Warning)
//                {
//                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
//                    g.DrawLine(pen, iconX + 16, iconY + 6, iconX + 16, iconY + 20);
//                    g.FillEllipse(brush, new Rectangle(iconX + 13, iconY + 24, 6, 6));
//                }
//                else if (_type == ToasterType.Error)
//                {
//                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
//                    g.DrawLine(pen, iconX + 9, iconY + 9, iconX + 23, iconY + 23);
//                    g.DrawLine(pen, iconX + 23, iconY + 9, iconX + 9, iconY + 23);
//                }
//            }

//            using (var titleFont = new Font(Font, FontStyle.Bold))
//            using (var titleBrush = new SolidBrush(_textColor))
//            using (var msgBrush = new SolidBrush(_subTextColor))
//            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
//            {
//                g.DrawString(_title, titleFont, titleBrush, new Rectangle(64, 18, Width - 100, 25), sf);
//                g.DrawString(_message, Font, msgBrush, new Rectangle(64, 45, Width - 100, 40), sf);
//            }

//            _closeRect = new Rectangle(Width - 32, 12, 20, 20);
//            if (_hoverClose) { using (var b = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) g.FillRectangle(b, _closeRect); }
//            using (var crossPen = new Pen(_subTextColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
//            {
//                g.DrawLine(crossPen, _closeRect.Left + 5, _closeRect.Top + 5, _closeRect.Right - 5, _closeRect.Bottom - 5);
//                g.DrawLine(crossPen, _closeRect.Right - 5, _closeRect.Top + 5, _closeRect.Left + 5, _closeRect.Bottom - 5);
//            }

//            if (_initialDuration > 0 && _state == 1)
//            {
//                Rectangle progRect = new Rectangle(2, Height - 4, Width - 4, 4);
//                int progWidth = Math.Max(0, (int)(progRect.Width * _progress));
//                using (var progBrush = new SolidBrush(_borderColor))
//                {
//                    g.FillRectangle(progBrush, new Rectangle(progRect.X, progRect.Y, progWidth, progRect.Height));
//                }
//            }
//        }

//        protected override void OnMouseMove(MouseEventArgs e)
//        {
//            base.OnMouseMove(e);
//            bool newHover = _closeRect.Contains(e.Location);
//            if (newHover != _hoverClose)
//            {
//                _hoverClose = newHover;
//                Cursor = _hoverClose ? Cursors.Hand : Cursors.Default;
//                Invalidate();
//            }
//        }

//        protected override void OnMouseUp(MouseEventArgs e)
//        {
//            base.OnMouseUp(e);
//            if (_closeRect.Contains(e.Location))
//            {
//                _state = 2; // Force transition to Fade Out state
//                _timer.Start(); // Ensure timer is running
//            }
//        }

//        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
//        {
//            var path = new GraphicsPath();
//            if (rect.Width <= 0 || rect.Height <= 0) return path;
//            int r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
//            if (r <= 0) { path.AddRectangle(rect); return path; }
//            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
//            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
//            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
//            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
//            path.CloseFigure();
//            return path;
//        }

//        [DllImport("dwmapi.dll")]
//        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
//    }
//}








//VER 1.8 ==================



using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UltraModernUI.Controls
{
    #region Enums & Config
    public enum ModernMessageIcon { None, Info, Warning, Error, Success, Question }
    public enum ModernDialogResult { None, OK, Cancel, Yes, No, Abort, Retry, Ignore }
    public enum ToasterType { Info, Success, Warning, Error }

    public enum NotificationThemeMode { Light, Dark }
    public enum NotificationDesignStyle { DashboardPremium, FluentGlass, MaterialFlat, SoftNeumorphic, CyberpunkIndustrial }

    public static class ModernNotificationConfig
    {
        public static NotificationThemeMode Theme { get; set; } = NotificationThemeMode.Dark;
        public static NotificationDesignStyle Style { get; set; } = NotificationDesignStyle.DashboardPremium;
    }
    #endregion

    /// <summary>
    /// Base class that uses UpdateLayeredWindow for pixel-perfect HD alpha rendering and smooth fading.
    /// </summary>
    public abstract class LayeredForm : Form
    {
        protected float _opacity = 1f;

        protected LayeredForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED - Required for UpdateLayeredWindow
                return cp;
            }
        }

        protected void RenderForm()
        {
            if (Width <= 0 || Height <= 0 || !IsHandleCreated) return;

            using (Bitmap bmp = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent); // Clear background to transparent

                    OnDraw(g);
                }

                IntPtr hdcScreen = GetDC(IntPtr.Zero);
                IntPtr hdcSrc = CreateCompatibleDC(hdcScreen);
                IntPtr hBitmap = bmp.GetHbitmap();
                IntPtr hOldBitmap = SelectObject(hdcSrc, hBitmap);

                BLENDFUNCTION blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    SourceConstantAlpha = (byte)(255 * _opacity),
                    AlphaFormat = AC_SRC_ALPHA
                };

                Point ptDst = new Point(Left, Top);
                Size sz = new Size(bmp.Width, bmp.Height);
                Point ptSrc = new Point(0, 0);

                UpdateLayeredWindow(this.Handle, hdcScreen, ref ptDst, ref sz, hdcSrc, ref ptSrc, 0, ref blend, ULW_ALPHA);

                SelectObject(hdcSrc, hOldBitmap);
                DeleteObject(hBitmap);
                ReleaseDC(IntPtr.Zero, hdcScreen);
                DeleteDC(hdcSrc);
            }
        }

        protected abstract void OnDraw(Graphics g);

        protected override void OnPaint(PaintEventArgs e) { RenderForm(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); if (IsHandleCreated) RenderForm(); }

        #region Win32 API
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        struct BLENDFUNCTION { public byte BlendOp; public byte BlendFlags; public byte SourceConstantAlpha; public byte AlphaFormat; }

        const byte AC_SRC_OVER = 0;
        const byte AC_SRC_ALPHA = 1;
        const uint ULW_ALPHA = 2;
        #endregion
    }

    public class ModernMessageBox : LayeredForm
    {
        private Color _backColor;
        private Color _accentColor;
        private Color _textColor;
        private Color _subTextColor;
        private int _cornerRadius = 8;

        private string _title;
        private string _message;
        private ModernMessageIcon _icon;
        private MessageBoxButtons _buttons;
        private List<ModernDialogResult> _buttonResults = new List<ModernDialogResult>();
        private List<Rectangle> _buttonRects = new List<Rectangle>();
        private int _hoveredButton = -1;
        private int _pressedButton = -1;
        private Rectangle _closeRect;
        private bool _hoverClose = false;

        public ModernMessageBox(string message, string title, MessageBoxButtons buttons, ModernMessageIcon icon)
        {
            _title = title;
            _message = message;
            _buttons = buttons;
            _icon = icon;

            ApplyTheme();
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI Variable Display", 9f);
        }

        private void ApplyTheme()
        {
            bool isDark = ModernNotificationConfig.Theme == NotificationThemeMode.Dark;
            switch (ModernNotificationConfig.Style)
            {
                case NotificationDesignStyle.DashboardPremium:
                    _backColor = isDark ? Color.FromArgb(32, 32, 38) : Color.FromArgb(248, 250, 252);
                    _accentColor = isDark ? Color.FromArgb(0, 120, 212) : Color.FromArgb(0, 100, 180);
                    _textColor = isDark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(30, 30, 30);
                    _subTextColor = isDark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(100, 110, 120);
                    _cornerRadius = 8;
                    break;
                case NotificationDesignStyle.FluentGlass:
                    _backColor = isDark ? Color.FromArgb(40, 40, 48) : Color.FromArgb(243, 244, 246);
                    _accentColor = Color.FromArgb(0, 180, 255);
                    _textColor = isDark ? Color.White : Color.Black;
                    _subTextColor = isDark ? Color.LightGray : Color.DarkGray;
                    _cornerRadius = 10;
                    break;
                case NotificationDesignStyle.MaterialFlat:
                    _backColor = isDark ? Color.FromArgb(18, 18, 18) : Color.White;
                    _accentColor = Color.FromArgb(98, 0, 238);
                    _textColor = isDark ? Color.White : Color.Black;
                    _subTextColor = isDark ? Color.FromArgb(180, 180, 180) : Color.FromArgb(70, 70, 70);
                    _cornerRadius = 2;
                    break;
                case NotificationDesignStyle.SoftNeumorphic:
                    _backColor = isDark ? Color.FromArgb(32, 38, 48) : Color.FromArgb(224, 229, 236);
                    _accentColor = Color.FromArgb(255, 87, 51);
                    _textColor = isDark ? Color.FromArgb(220, 230, 240) : Color.FromArgb(50, 60, 70);
                    _subTextColor = isDark ? Color.FromArgb(160, 170, 180) : Color.FromArgb(100, 110, 120);
                    _cornerRadius = 12;
                    break;
                case NotificationDesignStyle.CyberpunkIndustrial:
                    _backColor = Color.FromArgb(12, 12, 14);
                    _accentColor = Color.FromArgb(0, 255, 204);
                    _textColor = Color.FromArgb(230, 240, 255);
                    _subTextColor = Color.FromArgb(100, 120, 140);
                    _cornerRadius = 0;
                    break;
            }
        }

        public static ModernDialogResult Show(string message, string title = "Message", MessageBoxButtons buttons = MessageBoxButtons.OK, ModernMessageIcon icon = ModernMessageIcon.None)
        {
            using (var box = new ModernMessageBox(message, title, buttons, icon))
            {
                box.CalculateSize();
                if (box.ShowDialog() == DialogResult.OK)
                    return box._result;
                return ModernDialogResult.None;
            }
        }

        private ModernDialogResult _result = ModernDialogResult.None;
        private void CalculateSize()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
            {
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                SizeF titleSize = g.MeasureString(_title, new Font(Font, FontStyle.Bold), 500);
                SizeF msgSize = g.MeasureString(_message, Font, 400);

                int width = Math.Max(400, (int)Math.Max(titleSize.Width, msgSize.Width) + 80);
                int height = (int)msgSize.Height + 120;

                if (_icon != ModernMessageIcon.None) width += 60;
                this.Size = new Size(width, height);

                _buttonResults.Clear();
                switch (_buttons)
                {
                    case MessageBoxButtons.OK: _buttonResults.Add(ModernDialogResult.OK); break;
                    case MessageBoxButtons.OKCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.OK); break;
                    case MessageBoxButtons.YesNo: _buttonResults.Add(ModernDialogResult.No); _buttonResults.Add(ModernDialogResult.Yes); break;
                    case MessageBoxButtons.YesNoCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.No); _buttonResults.Add(ModernDialogResult.Yes); break;
                    case MessageBoxButtons.RetryCancel: _buttonResults.Add(ModernDialogResult.Cancel); _buttonResults.Add(ModernDialogResult.Retry); break;
                    case MessageBoxButtons.AbortRetryIgnore: _buttonResults.Add(ModernDialogResult.Ignore); _buttonResults.Add(ModernDialogResult.Retry); _buttonResults.Add(ModernDialogResult.Abort); break;
                }

                _buttonRects.Clear();
                int btnWidth = 100, btnHeight = 36, spacing = 12;
                int totalBtnWidth = (_buttonResults.Count * btnWidth) + ((_buttonResults.Count - 1) * spacing);
                int startX = width - totalBtnWidth - 20;

                for (int i = 0; i < _buttonResults.Count; i++)
                    _buttonRects.Add(new Rectangle(startX + (i * (btnWidth + spacing)), height - btnHeight - 20, btnWidth, btnHeight));

                _closeRect = new Rectangle(width - 40, 12, 28, 28);
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x84) // WM_NCHITTEST
            {
                int x = (int)m.LParam & 0xFFFF;
                int y = (int)m.LParam >> 16;
                Point pos = PointToClient(new Point(x, y));

                foreach (var btn in _buttonRects) if (btn.Contains(pos)) return;
                if (_closeRect.Contains(pos)) return;

                m.Result = (IntPtr)0x2; // HTCAPTION (Allow dragging)
            }
        }

        protected override void OnDraw(Graphics g)
        {
            using (var bgPath = GetRoundedPath(ClientRectangle, _cornerRadius))
            using (var bgBrush = new SolidBrush(_backColor))
                g.FillPath(bgBrush, bgPath);

            if (ModernNotificationConfig.Style == NotificationDesignStyle.CyberpunkIndustrial)
            {
                using (var glowPen = new Pen(Color.FromArgb(100, _accentColor), 4))
                using (var corePen = new Pen(_accentColor, 1))
                using (var bgPath = GetRoundedPath(ClientRectangle, _cornerRadius))
                {
                    g.DrawPath(glowPen, bgPath);
                    g.DrawPath(corePen, bgPath);
                }
            }

            int iconX = 24, iconY = 60;
            if (_icon != ModernMessageIcon.None)
            {
                DrawModernIcon(g, iconX, iconY, _icon);
                iconX += 50;
            }

            using (var titleFont = new Font(Font, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(_textColor))
            using (var msgBrush = new SolidBrush(_subTextColor))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(_title, titleFont, titleBrush, new Rectangle(24, 16, Width - 80, 30), sf);
                g.DrawString(_message, Font, msgBrush, new Rectangle(iconX, 50, Width - iconX - 30, Height - 110), sf);
            }

            if (_hoverClose)
            {
                using (var closeBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                using (var closePath = GetRoundedPath(_closeRect, 4))
                    g.FillPath(closeBrush, closePath);
            }
            using (var crossPen = new Pen(_subTextColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(crossPen, _closeRect.Left + 8, _closeRect.Top + 8, _closeRect.Right - 8, _closeRect.Bottom - 8);
                g.DrawLine(crossPen, _closeRect.Right - 8, _closeRect.Top + 8, _closeRect.Left + 8, _closeRect.Bottom - 8);
            }

            for (int i = 0; i < _buttonResults.Count; i++)
            {
                bool isHover = (_hoveredButton == i && _pressedButton == -1);
                bool isPressed = (_pressedButton == i);
                bool isPrimary = (i == _buttonResults.Count - 1);

                Rectangle btnRect = _buttonRects[i];
                Color btnBg = Color.Empty;
                Color btnText = _textColor;

                if (isPrimary)
                {
                    btnBg = _accentColor;
                    if (isHover) btnBg = ControlPaint.Light(_accentColor, 0.1f);
                    else if (isPressed) btnBg = ControlPaint.Dark(_accentColor, 0.1f);
                    btnText = Color.White;
                }
                else
                {
                    btnBg = ControlPaint.Light(_backColor, 0.05f);
                    if (isHover) btnBg = ControlPaint.Light(_backColor, 0.1f);
                    else if (isPressed) btnBg = ControlPaint.Dark(_backColor, 0.05f);
                }

                using (var btnPath = GetRoundedPath(btnRect, _cornerRadius == 0 ? 0 : 6))
                using (var btnBrush = new SolidBrush(btnBg))
                    g.FillPath(btnBrush, btnPath);

                using (var textBrush = new SolidBrush(btnText))
                using (var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center })
                    g.DrawString(_buttonResults[i].ToString(), Font, textBrush, btnRect, sf);
            }
        }

        private void DrawModernIcon(Graphics g, int x, int y, ModernMessageIcon icon)
        {
            using (var pen = new Pen(Color.White, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var brush = new SolidBrush(Color.White))
            {
                if (icon == ModernMessageIcon.Info)
                {
                    pen.Color = Color.FromArgb(0, 180, 255); brush.Color = pen.Color;
                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
                    g.FillEllipse(brush, new Rectangle(x + 15, y + 8, 6, 6));
                    g.DrawLine(pen, x + 18, y + 16, x + 18, y + 26);
                }
                else if (icon == ModernMessageIcon.Warning)
                {
                    pen.Color = Color.FromArgb(255, 160, 0); brush.Color = pen.Color;
                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
                    g.DrawLine(pen, x + 18, y + 8, x + 18, y + 22);
                    g.FillEllipse(brush, new Rectangle(x + 15, y + 26, 6, 6));
                }
                else if (icon == ModernMessageIcon.Error)
                {
                    pen.Color = Color.FromArgb(255, 50, 50); brush.Color = pen.Color;
                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
                    g.DrawLine(pen, x + 10, y + 10, x + 26, y + 26);
                    g.DrawLine(pen, x + 26, y + 10, x + 10, y + 26);
                }
                else if (icon == ModernMessageIcon.Success)
                {
                    pen.Color = Color.FromArgb(0, 220, 100); brush.Color = pen.Color;
                    g.DrawEllipse(pen, new Rectangle(x, y, 36, 36));
                    g.DrawLine(pen, x + 10, y + 18, x + 16, y + 24);
                    g.DrawLine(pen, x + 16, y + 24, x + 28, y + 12);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int newHover = -1;
            for (int i = 0; i < _buttonRects.Count; i++) if (_buttonRects[i].Contains(e.Location)) newHover = i;
            bool newHoverClose = _closeRect.Contains(e.Location);

            if (newHover != _hoveredButton || newHoverClose != _hoverClose)
            {
                _hoveredButton = newHover;
                _hoverClose = newHoverClose;
                Cursor = (newHover != -1 || _hoverClose) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _pressedButton = -1;
            for (int i = 0; i < _buttonRects.Count; i++) if (_buttonRects[i].Contains(e.Location)) _pressedButton = i;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_pressedButton != -1 && _buttonRects[_pressedButton].Contains(e.Location))
            {
                _result = _buttonResults[_pressedButton];
                DialogResult = DialogResult.OK;
            }
            else if (_closeRect.Contains(e.Location))
            {
                _result = ModernDialogResult.Cancel;
                DialogResult = DialogResult.Cancel;
            }
            _pressedButton = -1;
            Invalidate();
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (rect.Width <= 0 || rect.Height <= 0) return path;
            int r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
            if (r <= 0) { path.AddRectangle(rect); return path; }
            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public static class ModernToaster
    {
        private static List<ToasterForm> _activeToasters = new List<ToasterForm>();

        public static void Show(string title, string message, ToasterType type = ToasterType.Info, int duration = 3000)
        {
            if (Application.OpenForms.Count == 0) return;

            var mainForm = Application.OpenForms[0];
            if (mainForm.InvokeRequired)
            {
                mainForm.BeginInvoke((MethodInvoker)delegate { Show(title, message, type, duration); });
                return;
            }

            var toaster = new ToasterForm(title, message, type, duration);

            int yOffset = 20;
            foreach (var t in _activeToasters)
            {
                if (t.Screen == Screen.FromControl(mainForm))
                    yOffset += t.Height + 10;
            }

            var screen = Screen.FromControl(mainForm).WorkingArea;
            toaster.Location = new Point(screen.Right - toaster.Width - 20, screen.Bottom - toaster.Height - yOffset);

            _activeToasters.Add(toaster);
            toaster.FormClosed += (s, e) => { _activeToasters.Remove(toaster); };

            toaster.Show(mainForm);
        }
    }

    public class ToasterForm : LayeredForm
    {
        private Color _backColor;
        private Color _borderColor;
        private Color _textColor;
        private Color _subTextColor;
        private int _cornerRadius = 8;

        private string _title;
        private string _message;
        private ToasterType _type;
        private int _duration;
        private int _initialDuration;
        private System.Windows.Forms.Timer _timer;
        private float _progress = 1f;
        private Rectangle _closeRect;
        private bool _hoverClose = false;

        private int _state = 0; // 0 = Fade In, 1 = Waiting/Counting, 2 = Fade Out

        public ToasterForm(string title, string message, ToasterType type, int duration)
        {
            _title = title;
            _message = message;
            _type = type;
            _duration = duration;
            _initialDuration = duration;
            _opacity = 0; // Start transparent

            ApplyTheme();
            Font = new Font("Segoe UI Variable Display", 9f);
            Size = new Size(350, 90);

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 16;
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        public Screen Screen { get; set; }

        private void ApplyTheme()
        {
            bool isDark = ModernNotificationConfig.Theme == NotificationThemeMode.Dark;
            switch (ModernNotificationConfig.Style)
            {
                case NotificationDesignStyle.DashboardPremium:
                    _backColor = isDark ? Color.FromArgb(32, 32, 38) : Color.FromArgb(248, 250, 252);
                    _textColor = isDark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(30, 30, 30);
                    _subTextColor = isDark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(100, 110, 120);
                    _cornerRadius = 8;
                    break;
                case NotificationDesignStyle.FluentGlass:
                    _backColor = isDark ? Color.FromArgb(40, 40, 48) : Color.FromArgb(243, 244, 246);
                    _textColor = isDark ? Color.White : Color.Black;
                    _subTextColor = isDark ? Color.LightGray : Color.DarkGray;
                    _cornerRadius = 10;
                    break;
                case NotificationDesignStyle.MaterialFlat:
                    _backColor = isDark ? Color.FromArgb(18, 18, 18) : Color.White;
                    _textColor = isDark ? Color.White : Color.Black;
                    _subTextColor = isDark ? Color.FromArgb(180, 180, 180) : Color.FromArgb(70, 70, 70);
                    _cornerRadius = 2;
                    break;
                case NotificationDesignStyle.SoftNeumorphic:
                    _backColor = isDark ? Color.FromArgb(32, 38, 48) : Color.FromArgb(224, 229, 236);
                    _textColor = isDark ? Color.FromArgb(220, 230, 240) : Color.FromArgb(50, 60, 70);
                    _subTextColor = isDark ? Color.FromArgb(160, 170, 180) : Color.FromArgb(100, 110, 120);
                    _cornerRadius = 12;
                    break;
                case NotificationDesignStyle.CyberpunkIndustrial:
                    _backColor = Color.FromArgb(12, 12, 14);
                    _textColor = Color.FromArgb(230, 240, 255);
                    _subTextColor = Color.FromArgb(100, 120, 140);
                    _cornerRadius = 0;
                    break;
            }

            switch (_type)
            {
                case ToasterType.Info: _borderColor = Color.FromArgb(0, 180, 255); break;
                case ToasterType.Success: _borderColor = Color.FromArgb(0, 220, 100); break;
                case ToasterType.Warning: _borderColor = Color.FromArgb(255, 160, 0); break;
                case ToasterType.Error: _borderColor = Color.FromArgb(255, 50, 50); break;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (IsDisposed) return;

            bool needsRedraw = false;

            if (_state == 0)
            {
                _opacity += 0.1f;
                if (_opacity >= 1f) { _opacity = 1f; _state = 1; }
                needsRedraw = true;
            }
            else if (_state == 1)
            {
                if (_initialDuration > 0)
                {
                    _duration -= 16;
                    _progress = (float)_duration / _initialDuration;
                    if (_progress < 0) _progress = 0;

                    if (_duration <= 0)
                    {
                        _state = 2;
                    }
                    needsRedraw = true;
                }
                else
                {
                    _timer.Stop();
                }
            }
            else if (_state == 2)
            {
                _opacity -= 0.1f;
                if (_opacity <= 0f)
                {
                    _timer.Stop();
                    this.Close();
                    return;
                }
                needsRedraw = true;
            }

            if (needsRedraw) Invalidate(); // Triggers OnPaint -> RenderForm
        }

        protected override void OnDraw(Graphics g)
        {
            using (var bgPath = GetRoundedPath(ClientRectangle, _cornerRadius))
            using (var bgBrush = new SolidBrush(_backColor))
                g.FillPath(bgBrush, bgPath);

            if (ModernNotificationConfig.Style == NotificationDesignStyle.CyberpunkIndustrial)
            {
                using (var glowPen = new Pen(Color.FromArgb(100, _borderColor), 4))
                using (var corePen = new Pen(_borderColor, 1))
                {
                    g.DrawPath(glowPen, GetRoundedPath(ClientRectangle, _cornerRadius));
                    g.DrawPath(corePen, GetRoundedPath(ClientRectangle, _cornerRadius));
                }
            }
            else
            {
                using (var sidePath = GetRoundedPath(new Rectangle(0, 0, 6, Height), 3))
                using (var sideBrush = new SolidBrush(_borderColor))
                    g.FillPath(sideBrush, sidePath);
            }

            int iconX = 20, iconY = 20;
            using (var pen = new Pen(_borderColor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var brush = new SolidBrush(_borderColor))
            {
                if (_type == ToasterType.Info)
                {
                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
                    g.FillEllipse(brush, new Rectangle(iconX + 13, iconY + 6, 6, 6));
                    g.DrawLine(pen, iconX + 16, iconY + 14, iconX + 16, iconY + 24);
                }
                else if (_type == ToasterType.Success)
                {
                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
                    g.DrawLine(pen, iconX + 8, iconY + 16, iconX + 14, iconY + 22);
                    g.DrawLine(pen, iconX + 14, iconY + 22, iconX + 26, iconY + 10);
                }
                else if (_type == ToasterType.Warning)
                {
                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
                    g.DrawLine(pen, iconX + 16, iconY + 6, iconX + 16, iconY + 20);
                    g.FillEllipse(brush, new Rectangle(iconX + 13, iconY + 24, 6, 6));
                }
                else if (_type == ToasterType.Error)
                {
                    g.DrawEllipse(pen, new Rectangle(iconX, iconY, 32, 32));
                    g.DrawLine(pen, iconX + 9, iconY + 9, iconX + 23, iconY + 23);
                    g.DrawLine(pen, iconX + 23, iconY + 9, iconX + 9, iconY + 23);
                }
            }

            using (var titleFont = new Font(Font, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(_textColor))
            using (var msgBrush = new SolidBrush(_subTextColor))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(_title, titleFont, titleBrush, new Rectangle(64, 18, Width - 100, 25), sf);
                g.DrawString(_message, Font, msgBrush, new Rectangle(64, 45, Width - 100, 40), sf);
            }

            _closeRect = new Rectangle(Width - 32, 12, 20, 20);
            if (_hoverClose)
            {
                using (var b = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                using (var p = GetRoundedPath(_closeRect, 4))
                    g.FillPath(b, p);
            }
            using (var crossPen = new Pen(_subTextColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(crossPen, _closeRect.Left + 5, _closeRect.Top + 5, _closeRect.Right - 5, _closeRect.Bottom - 5);
                g.DrawLine(crossPen, _closeRect.Right - 5, _closeRect.Top + 5, _closeRect.Left + 5, _closeRect.Bottom - 5);
            }

            if (_initialDuration > 0 && _state == 1)
            {
                Rectangle progRect = new Rectangle(2, Height - 4, Width - 4, 4);
                int progWidth = Math.Max(0, (int)(progRect.Width * _progress));
                using (var progBrush = new SolidBrush(_borderColor))
                {
                    g.FillRectangle(progBrush, new Rectangle(progRect.X, progRect.Y, progWidth, progRect.Height));
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool newHover = _closeRect.Contains(e.Location);
            if (newHover != _hoverClose)
            {
                _hoverClose = newHover;
                Cursor = _hoverClose ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_closeRect.Contains(e.Location))
            {
                _state = 2;
                _timer.Start();
            }
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (rect.Width <= 0 || rect.Height <= 0) return path;
            int r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
            if (r <= 0) { path.AddRectangle(rect); return path; }
            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}