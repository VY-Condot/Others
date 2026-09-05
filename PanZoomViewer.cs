////// ============================================================================
//////  PanZoomViewer.cs — Reference-quality pan/zoom image surface
//////  ---------------------------------------------------------------------------
//////  • 5 integrated design systems (ViewerStyle) × 2 themes (ThemeMode)
//////  • Zero-flicker, fully buffered custom paint pipeline (base.OnPaint NOT called)
//////  • Strict instant GDI+ disposal — every Pen/Brush/Path created in OnPaint
//////    lives and dies inside a using block. Fonts are cached and disposed once.
//////  • Vector-precise geometry, fractional padding, per-style corner radii
//////  • Full control-state engine: Normal / Hover / Pressed / Focused / Disabled
//////  • Live Designer support — every aesthetic property invalidates on set
//////  Target: .NET Framework 4.7+ / .NET 6+ WinForms, C# 7.3+. No dependencies.
////// ============================================================================
////using System;
////using System.Collections.Generic;
////using System.ComponentModel;
////using System.Diagnostics;
////using System.Drawing;
////using System.Drawing.Drawing2D;
////using System.Drawing.Text;
////using System.Windows.Forms;

////namespace CsplCameraOcr.UserControls
////{
////    // ========================================================================
////    //  PUBLIC ENUMS
////    // ========================================================================

////    /// <summary>Surface theme.</summary>
////    public enum ThemeMode
////    {
////        Light,
////        Dark
////    }

////    /// <summary>Integrated design systems.</summary>
////    public enum ViewerStyle
////    {
////        /// <summary>Clean face, crisp vector ticks, threshold accent strips, minimal readouts.</summary>
////        DashboardPremium = 1,
////        /// <summary>Frosted acrylic layers, light-reflecting borders, neon interaction states.</summary>
////        FluentGlass = 2,
////        /// <summary>Flat bold color blocks, generous padding, zero gradients.</summary>
////        MaterialFlat = 3,
////        /// <summary>Dual ambient-light shadows, embossed center depth, minimal contrast.</summary>
////        SoftNeumorphic = 4,
////        /// <summary>Deep onyx, glowing neon geometry, tech corners, digital type.</summary>
////        Cyberpunk = 5
////    }

////    // ========================================================================
////    //  PALETTE SNAPSHOT (immutable to consumers — read via GetPalette())
////    // ========================================================================

////    /// <summary>Resolved color system for the current ThemeMode + ViewerStyle.</summary>
////    public sealed class ViewerPalette
////    {
////        public Color Canvas { get; internal set; }
////        public Color BackdropGlow { get; internal set; }
////        public Color ViewportFace { get; internal set; }
////        public Color ViewportWell { get; internal set; }
////        public Color Border { get; internal set; }
////        public Color BorderHighlight { get; internal set; }
////        public Color TextPrimary { get; internal set; }
////        public Color TextSecondary { get; internal set; }
////        public Color TextDim { get; internal set; }
////        public Color Accent { get; internal set; }
////        public Color AccentAlt { get; internal set; }
////        public Color ZoneGood { get; internal set; }
////        public Color ZoneWarn { get; internal set; }
////        public Color ZoneHot { get; internal set; }
////        public Color Grid { get; internal set; }
////        public Color Tick { get; internal set; }
////        public Color HudBack { get; internal set; }
////        public Color HudBorder { get; internal set; }
////        public Color HudFore { get; internal set; }
////        public Color HudForeDim { get; internal set; }
////        public Color Shadow { get; internal set; }
////        public Color ShadowLight { get; internal set; }
////        public Color ShadowDark { get; internal set; }
////        public Color HoverOverlay { get; internal set; }
////        public Color FocusRing { get; internal set; }
////    }

////    /// <summary>Context-rich overlay paint event (modern replacement for OnCustomPaint).</summary>
////    public sealed class OverlayPaintEventArgs : EventArgs
////    {
////        internal OverlayPaintEventArgs(Graphics graphics, RectangleF imageBounds,
////                                       float zoom, PointF pan, ViewerPalette palette)
////        {
////            Graphics = graphics;
////            ImageBounds = imageBounds;
////            Zoom = zoom;
////            Pan = pan;
////            Palette = palette;
////        }

////        public Graphics Graphics { get; private set; }
////        /// <summary>Current image bounds in control-space coordinates (draw ROI frames here).</summary>
////        public RectangleF ImageBounds { get; private set; }
////        public float Zoom { get; private set; }
////        public PointF Pan { get; private set; }
////        public ViewerPalette Palette { get; private set; }
////    }

////    // ========================================================================
////    //  CONTROL
////    // ========================================================================

////    [ToolboxItem(true)]
////    [Description("Reference-quality pan/zoom image surface with five integrated design styles.")]
////    public partial class PanZoomViewer : Control
////    {
////        // ====================================================================
////        //  CORE DATA
////        // ====================================================================
////        private Bitmap _image;
////        private float _zoom = 1f;
////        private PointF _pan = PointF.Empty;
////        private bool _needsAutoFit = true;

////        // Interaction state
////        private bool _hover;
////        private bool _pressed;
////        private bool _panning;
////        private Point _lastMouse;
////        private Point _hoverPos;

////        // Design system
////        private ThemeMode _themeMode = ThemeMode.Dark;
////        private ViewerStyle _controlStyle = ViewerStyle.DashboardPremium;
////        private ViewerPalette _palette;

////        private bool _showHud = true;
////        private bool _showScanlines = true;
////        private bool _showCrosshair = true;
////        private InterpolationMode _interpolation = InterpolationMode.HighQualityBicubic;
////        private float _minZoom = 0.05f;
////        private float _maxZoom = 100f;
////        private float _dpiScale = 1f;

////        // Font engine (cached for performance, disposed exactly once in Dispose)
////        private readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();
////        private static string _uiFamily;
////        private static string _monoFamily;
////        private static readonly string[] UiFontCandidates =
////            { "Segoe UI Variable Display", "Segoe UI Workspace", "Segoe UI", "Arial" };
////        private static readonly string[] MonoFontCandidates =
////            { "Cascadia Code", "Cascadia Mono", "JetBrains Mono", "Consolas", "Courier New" };

////        private struct ViewerLayout
////        {
////            public RectangleF View;
////            public RectangleF Content;
////            public float ViewRadius;
////            public float ContentRadius;
////            public float Dpi;
////        }

////        private enum ChipAnchor { TopLeft, BottomLeft, BottomRight }

////        // ====================================================================
////        //  CONSTRUCTION — flicker-free rendering flags
////        // ====================================================================
////        public PanZoomViewer()
////        {
////            SetStyle(
////                ControlStyles.UserPaint |
////                ControlStyles.AllPaintingInWmPaint |
////                ControlStyles.DoubleBuffer |
////                ControlStyles.OptimizedDoubleBuffer |
////                ControlStyles.ResizeRedraw |
////                ControlStyles.Selectable, true);
////            UpdateStyles();

////            TabStop = true;
////            AllowDrop = true;
////            Cursor = Cursors.Cross;

////            _palette = BuildPalette(_themeMode, _controlStyle);
////        }

////        // ====================================================================
////        //  PUBLIC PROPERTIES — every setter invalidates for live Designer edits
////        // ====================================================================
////        [Category("Appearance")]
////        [Description("Light or Dark surface theme.")]
////        [DefaultValue(ThemeMode.Dark)]
////        public ThemeMode ThemeMode
////        {
////            get { return _themeMode; }
////            set
////            {
////                if (_themeMode == value) return;
////                _themeMode = value;
////                _palette = BuildPalette(_themeMode, _controlStyle);
////                Invalidate();
////            }
////        }

////        [Category("Appearance")]
////        [Description("Visual design system (Dashboard / Glass / Material / Neumorphic / Cyber).")]
////        [DefaultValue(ViewerStyle.DashboardPremium)]
////        public ViewerStyle ControlStyle
////        {
////            get { return _controlStyle; }
////            set
////            {
////                if (_controlStyle == value) return;
////                _controlStyle = value;
////                _palette = BuildPalette(_themeMode, _controlStyle);
////                Invalidate();
////            }
////        }

////        [Category("Appearance")]
////        [Description("Shows the minimal readout chips (source, zoom, probe).")]
////        [DefaultValue(true)]
////        public bool ShowHud
////        {
////            get { return _showHud; }
////            set { if (_showHud == value) return; _showHud = value; Invalidate(); }
////        }

////        [Category("Appearance")]
////        [Description("Cyberpunk style: subtle CRT scanline film over the viewport.")]
////        [DefaultValue(true)]
////        public bool ShowScanlines
////        {
////            get { return _showScanlines; }
////            set { if (_showScanlines == value) return; _showScanlines = value; Invalidate(); }
////        }

////        [Category("Appearance")]
////        [Description("Precision crosshair follows the cursor (Dashboard & Cyberpunk styles).")]
////        [DefaultValue(true)]
////        public bool ShowCrosshair
////        {
////            get { return _showCrosshair; }
////            set { if (_showCrosshair == value) return; _showCrosshair = value; Invalidate(); }
////        }

////        [Category("Behavior")]
////        [Description("Sampling quality of the rendered image. Use NearestNeighbor for pixel-accurate OCR inspection.")]
////        [DefaultValue(InterpolationMode.HighQualityBicubic)]
////        public InterpolationMode Interpolation
////        {
////            get { return _interpolation; }
////            set { _interpolation = value; Invalidate(); }
////        }

////        [Category("Behavior")]
////        [Description("Lower zoom clamp.")]
////        [DefaultValue(0.05f)]
////        public float MinZoom
////        {
////            get { return _minZoom; }
////            set { _minZoom = Math.Max(0.01f, value); Invalidate(); }
////        }

////        [Category("Behavior")]
////        [Description("Upper zoom clamp.")]
////        [DefaultValue(100f)]
////        public float MaxZoom
////        {
////            get { return _maxZoom; }
////            set { _maxZoom = Math.Max(_minZoom * 2f, value); Invalidate(); }
////        }

////        [Browsable(false)]
////        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
////        [Description("Displayed bitmap. Ownership transfers to the control — the previous bitmap is disposed on replacement.")]
////        public Bitmap Image
////        {
////            get { return _image; }
////            set
////            {
////                if (ReferenceEquals(_image, value)) return;
////                Bitmap old = _image;
////                _image = value;
////                if (_image != null && _needsAutoFit) AutoFit();
////                if (old != null) old.Dispose();     // instant disposal — zero GDI leak
////                Invalidate();
////            }
////        }

////        [Browsable(false)]
////        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
////        public float Zoom
////        {
////            get { return _zoom; }
////            set { SetZoom(value, new PointF(Width / 2f, Height / 2f)); }
////        }

////        [Browsable(false)]
////        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
////        public PointF PanOffset
////        {
////            get { return _pan; }
////            set { _pan = value; Invalidate(); }
////        }

////        // ====================================================================
////        //  EVENTS
////        // ====================================================================

////        /// <summary>Legacy-compatible hook (same signature as the previous control). Graphics is in control-space.</summary>
////        public event EventHandler<Graphics> OnCustomPaint;

////        /// <summary>Modern overlay hook — provides Graphics, image bounds, zoom, pan and the active palette.</summary>
////        public event EventHandler<OverlayPaintEventArgs> OverlayPaint;

////        // ====================================================================
////        //  PUBLIC METHODS & COORDINATE TRANSFORMS
////        // ====================================================================

////        /// <summary>Fits the image exactly into the viewport (PictureBox Zoom semantics), centered.</summary>
////        public void AutoFit()
////        {
////            if (_image == null || Width < 20 || Height < 20) return;

////            ViewerLayout lay = ComputeLayout();
////            RectangleF c = lay.Content;

////            float z = Math.Min(c.Width / _image.Width, c.Height / _image.Height);
////            _zoom = ClampF(z, MinZoom, MaxZoom);

////            float dw = _image.Width * _zoom;
////            float dh = _image.Height * _zoom;
////            _pan = new PointF(c.X + (c.Width - dw) / 2f, c.Y + (c.Height - dh) / 2f);

////            _needsAutoFit = false;
////            Invalidate();
////        }

////        /// <summary>Zooms to an exact factor while keeping <paramref name="anchor"/> (control-space) visually pinned.</summary>
////        public void SetZoom(float zoom, PointF anchor)
////        {
////            float z = ClampF(zoom, MinZoom, MaxZoom);
////            if (z == _zoom) return;

////            float k = z / _zoom;
////            _pan = new PointF(
////                anchor.X - (anchor.X - _pan.X) * k,
////                anchor.Y - (anchor.Y - _pan.Y) * k);
////            _zoom = z;
////            Invalidate();
////        }

////        public void PanBy(float dx, float dy)
////        {
////            _pan = new PointF(_pan.X + dx, _pan.Y + dy);
////            Invalidate();
////        }

////        /// <summary>Snapshot of the resolved palette — draw host-side ROIs in matching colors.</summary>
////        public ViewerPalette GetPalette() { return _palette; }

////        public PointF ScreenToImageF(PointF screenPoint)
////        {
////            return new PointF((screenPoint.X - _pan.X) / _zoom,
////                              (screenPoint.Y - _pan.Y) / _zoom);
////        }

////        public Point ScreenToImage(Point screenPoint)
////        {
////            return new Point(
////                (int)Math.Round((screenPoint.X - _pan.X) / _zoom),
////                (int)Math.Round((screenPoint.Y - _pan.Y) / _zoom));
////        }

////        public RectangleF ImageToScreenF(RectangleF imageRect)
////        {
////            return new RectangleF(
////                imageRect.X * _zoom + _pan.X,
////                imageRect.Y * _zoom + _pan.Y,
////                imageRect.Width * _zoom,
////                imageRect.Height * _zoom);
////        }

////        public Rectangle ImageToScreen(Rectangle imageRect)
////        {
////            RectangleF f = ImageToScreenF(imageRect);
////            return new Rectangle(
////                (int)Math.Round(f.X), (int)Math.Round(f.Y),
////                (int)Math.Round(f.Width), (int)Math.Round(f.Height));
////        }

////        public RectangleF GetImageScreenBounds()
////        {
////            if (_image == null) return RectangleF.Empty;
////            return new RectangleF(_pan.X, _pan.Y, _image.Width * _zoom, _image.Height * _zoom);
////        }

////        // ====================================================================
////        //  RENDERING PIPELINE — back layer to front layer
////        // ====================================================================
////        protected override void OnPaintBackground(PaintEventArgs pevent)
////        {
////            // Intentionally empty — the buffered surface is composed in OnPaint only.
////        }

////        //protected override void OnPaint(PaintEventArgs e)
////        //{
////        //    // base.OnPaint() intentionally NOT invoked — the entire visual
////        //    // hierarchy is composed here, back layer to front layer.
////        //    Graphics g = e.Graphics;

////        //    g.SmoothingMode = SmoothingMode.AntiAlias;
////        //    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
////        //    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
////        //    g.CompositingQuality = CompositingQuality.HighQuality;
////        //    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

////        //    if (Width < 6 || Height < 6)
////        //    {
////        //        using (SolidBrush b = new SolidBrush(_palette.Canvas)) g.FillRectangle(b, ClientRectangle);
////        //        return;
////        //    }

////        //    ViewerPalette pal = _palette;
////        //    ViewerLayout lay = ComputeLayout();

////        //    DrawCanvasLayer(g, pal, lay);    // 1 — ambient base
////        //    DrawCardLayer(g, pal, lay);      // 2 — viewport card, borders, shadows
////        //    DrawContentLayer(g, pal, lay);   // 3 — image / empty state + film effects

////        //    // 4 — host overlays (ROIs, OCR boxes). Control-space coordinates,
////        //    //     drawn above the image but below the HUD chrome.
////        //    EventHandler<Graphics> legacy = OnCustomPaint;
////        //    if (legacy != null) legacy(this, g);

////        //    EventHandler<OverlayPaintEventArgs> overlay = OverlayPaint;
////        //    if (overlay != null)
////        //    {
////        //        OverlayPaintEventArgs args = new OverlayPaintEventArgs(
////        //            g, GetImageScreenBounds(), _zoom, _pan, pal);
////        //        overlay(this, args);
////        //    }

////        //    DrawHudLayer(g, pal, lay);       // 5 — minimal modern readouts
////        //    DrawStateLayer(g, pal, lay);     // 6 — crosshair, focus ring, disabled veil

////        //    // Surface a dead-bitmap detection from this pass (outside all clipping).
////        //    if (_imageLostPending)
////        //    {
////        //        _imageLostPending = false;
////        //        EventHandler lost = ImageLost;
////        //        if (lost != null) lost(this, EventArgs.Empty);
////        //    }
////        //}


////        protected override void OnPaint(PaintEventArgs e)
////        {
////            if (IsDesignTime)
////            {
////                try { PaintCore(e.Graphics); }
////                catch (Exception ex) { PaintDesignFailure(e.Graphics, ex); }
////            }
////            else
////            {
////                PaintCore(e.Graphics);
////            }
////        }

////        private bool IsDesignTime
////        {
////            get { return Site != null && Site.DesignMode; }
////        }

////        private void PaintDesignFailure(Graphics g, Exception ex)
////        {
////            Debug.WriteLine("[PanZoomViewer design-time paint failure]\r\n" + ex);
////            try
////            {
////                g.SmoothingMode = SmoothingMode.None;
////                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

////                using (SolidBrush bg = new SolidBrush(Color.FromArgb(255, 34, 18, 28)))
////                    g.FillRectangle(bg, ClientRectangle);

////                using (Font f = new Font(FontFamily.GenericMonospace, 8.5f, FontStyle.Regular, GraphicsUnit.Point))
////                using (SolidBrush t = new SolidBrush(Color.FromArgb(255, 255, 105, 97)))
////                using (SolidBrush w = new SolidBrush(Color.White))
////                using (StringFormat fmt = new StringFormat())
////                {
////                    fmt.Trimming = StringTrimming.EllipsisCharacter;

////                    g.DrawString("PANZOOMVIEWER — DESIGN-TIME PAINT FAILURE", f, t, 12f, 12f, fmt);
////                    g.DrawString(ex.GetType().Name + ": " + ex.Message, f, w, 12f, 30f, fmt);

////                    string[] frames = ex.StackTrace.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
////                    float y = 48f;
////                    int shown = Math.Min(10, frames.Length);
////                    for (int i = 0; i < shown; i++)
////                    {
////                        g.DrawString(frames[i].Trim(), f, w, 12f, y, fmt);
////                        y += 15f;
////                        if (y > Height - 18f) break;
////                    }
////                }
////            }
////            catch { /* diagnostics must never throw */ }
////        }

////        // --------------------------------------------------------------------
////        //  CORE PAINT — the entire visual hierarchy, back layer to front layer.
////        //  Called by OnPaint; base.OnPaint() is intentionally never invoked.
////        // --------------------------------------------------------------------
////        private void PaintCore(Graphics g)
////        {
////            g.SmoothingMode = SmoothingMode.AntiAlias;
////            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
////            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
////            g.CompositingQuality = CompositingQuality.HighQuality;
////            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

////            if (Width < 6 || Height < 6)
////            {
////                using (SolidBrush b = new SolidBrush(_palette.Canvas)) g.FillRectangle(b, ClientRectangle);
////                return;
////            }

////            ViewerPalette pal = _palette;
////            ViewerLayout lay = ComputeLayout();

////            DrawCanvasLayer(g, pal, lay);    // 1 — ambient base
////            DrawCardLayer(g, pal, lay);      // 2 — viewport card, borders, shadows
////            DrawContentLayer(g, pal, lay);   // 3 — image / empty state + film effects

////            // 4 — host overlays (ROIs, OCR boxes). Control-space coordinates,
////            //     drawn above the image but below the HUD chrome.
////            EventHandler<Graphics> legacy = OnCustomPaint;
////            if (legacy != null) legacy(this, g);

////            EventHandler<OverlayPaintEventArgs> overlay = OverlayPaint;
////            if (overlay != null)
////            {
////                OverlayPaintEventArgs args = new OverlayPaintEventArgs(
////                    g, GetImageScreenBounds(), _zoom, _pan, pal);
////                overlay(this, args);
////            }

////            DrawHudLayer(g, pal, lay);       // 5 — minimal modern readouts
////            DrawStateLayer(g, pal, lay);     // 6 — crosshair, focus ring, disabled veil

////            // Surface a dead-bitmap detection from this pass (outside all clipping).
////            if (_imageLostPending)
////            {
////                _imageLostPending = false;
////                EventHandler lost = ImageLost;
////                if (lost != null) lost(this, EventArgs.Empty);
////            }
////        }

////        // --------------------------------------------------------------------
////        //  LAYER 1 — CANVAS
////        // --------------------------------------------------------------------
////        private void DrawCanvasLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF client = new RectangleF(0f, 0f, Width, Height);
////            using (SolidBrush b = new SolidBrush(pal.Canvas)) g.FillRectangle(b, client);

////            if (pal.BackdropGlow.A > 0)
////            {
////                using (LinearGradientBrush lg = new LinearGradientBrush(
////                    client, pal.BackdropGlow, Color.FromArgb(0, pal.BackdropGlow), 118f))
////                {
////                    g.FillRectangle(lg, client);
////                }
////            }

////            if (_controlStyle == ViewerStyle.MaterialFlat)
////            {
////                // Flat accent color block header — bold, gradient-free.
////                using (SolidBrush accent = new SolidBrush(pal.Accent))
////                    g.FillRectangle(accent, 0f, 0f, Width, 4f * lay.Dpi);
////            }
////        }

////        // --------------------------------------------------------------------
////        //  LAYER 2 — VIEWPORT CARD (per-style)
////        // --------------------------------------------------------------------
////        private void DrawCardLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            switch (_controlStyle)
////            {
////                case ViewerStyle.FluentGlass: DrawGlassCard(g, pal, lay); break;
////                case ViewerStyle.MaterialFlat: DrawMaterialCard(g, pal, lay); break;
////                case ViewerStyle.SoftNeumorphic: DrawNeumorphicCard(g, pal, lay); break;
////                case ViewerStyle.Cyberpunk: DrawCyberCard(g, pal, lay); break;
////                default: DrawDashboardCard(g, pal, lay); break;
////            }
////        }

////        // STYLE 1 — DASHBOARD PREMIUM -----------------------------------------
////        private void DrawDashboardCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF v = lay.View;
////            float r = lay.ViewRadius;

////            DrawSoftShadow(g, v, r, pal.Shadow, 7f * lay.Dpi, 4);

////            Color borderColor = (_hover && Enabled) ? Lerp(pal.Border, pal.Accent, 0.45f) : pal.Border;
////            using (GraphicsPath face = BuildRoundedPath(v, r))
////            {
////                using (SolidBrush fill = new SolidBrush(pal.ViewportFace)) g.FillPath(fill, face);
////                using (Pen border = new Pen(borderColor, 1f)) g.DrawPath(border, face);
////            }

////            // 1-px inner highlight along the lower curve — micro light-catch.
////            g.SetClip(new RectangleF(v.X, v.Y + v.Height * 0.55f, v.Width, v.Height * 0.45f));
////            using (GraphicsPath ip = BuildRoundedPath(Deflate(v, 1.4f), Math.Max(1f, r - 1.4f)))
////            using (Pen hi = new Pen(Color.FromArgb(80, pal.BorderHighlight), 1f))
////            {
////                g.DrawPath(hi, ip);
////            }
////            g.ResetClip();

////            // Multi-color threshold accent strip — follows the zoom zone.
////            float stripW = v.Width - 2f * r;
////            if (stripW > 4f)
////            {
////                RectangleF strip = new RectangleF(v.X + r, v.Y + 1.5f, stripW, 3f);
////                using (GraphicsPath sp = BuildRoundedPath(strip, 1.5f))
////                using (SolidBrush sb = new SolidBrush(Color.FromArgb(210, ZoomZoneColor(pal))))
////                {
////                    g.FillPath(sb, sp);
////                }
////            }
////        }

////        // STYLE 2 — FLUENT GLASS ----------------------------------------------
////        private void DrawGlassCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF v = lay.View;
////            float r = lay.ViewRadius;

////            DrawSoftShadow(g, v, r, pal.Shadow, 5f * lay.Dpi, 3);

////            // Frosted acrylic — stacked translucent layers build optical depth.
////            using (GraphicsPath p1 = BuildRoundedPath(v, r))
////            using (SolidBrush l1 = new SolidBrush(pal.ViewportFace))
////            {
////                g.FillPath(l1, p1);
////            }
////            using (GraphicsPath p2 = BuildRoundedPath(Deflate(v, 4f * lay.Dpi), Math.Max(2f, r - 4f * lay.Dpi)))
////            using (SolidBrush l2 = new SolidBrush(pal.ViewportFace))
////            {
////                g.FillPath(l2, p2);
////            }

////            //// Thin light-reflecting border (vertical fade, top-lit).
////            //using (LinearGradientBrush lb = new LinearGradientBrush(
////            //    v, Color.FromArgb(150, pal.BorderHighlight),
////            //    Color.FromArgb(28, pal.BorderHighlight), 90f))
////            //using (Pen border = new Pen(lb, 1.2f))
////            //using (GraphicsPath bp = BuildRoundedPath(Deflate(v, 0.6f), Math.Max(2f, r - 0.6f)))
////            //{
////            //    g.DrawPath(border, bp);
////            //}

////            if (v.Width > 1f && v.Height > 1f)
////            {
////                using (LinearGradientBrush lb = new LinearGradientBrush(
////                    v, Color.FromArgb(150, pal.BorderHighlight),
////                    Color.FromArgb(28, pal.BorderHighlight), 90f))
////                using (Pen border = new Pen(lb, 1.2f))
////                using (GraphicsPath bp = BuildRoundedPath(Deflate(v, 0.6f), Math.Max(2f, r - 0.6f)))
////                {
////                    g.DrawPath(border, bp);
////                }
////            }

////            // Vivid neon state glow on hover / press.
////            int glow = _pressed ? 3 : (_hover && Enabled ? 2 : 0);
////            for (int i = glow; i >= 1; i--)
////            {
////                using (GraphicsPath gp = BuildRoundedPath(Expand(v, 1.4f * i), r + 1.4f * i))
////                using (Pen pen = new Pen(Color.FromArgb(80 / i, pal.Accent), 1.4f))
////                {
////                    g.DrawPath(pen, gp);
////                }
////            }
////        }

////        // STYLE 3 — MATERIAL FLAT ---------------------------------------------
////        private void DrawMaterialCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            // Flat block — no border, no shadow, no gradient.
////            using (GraphicsPath face = BuildRoundedPath(lay.View, lay.ViewRadius))
////            using (SolidBrush fill = new SolidBrush(pal.ViewportFace))
////            {
////                g.FillPath(fill, face);
////            }
////        }

////        // STYLE 4 — SOFT NEUMORPHIC -------------------------------------------
////        private void DrawNeumorphicCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF v = lay.View;
////            float r = lay.ViewRadius;
////            bool pressed = _pressed && Enabled;
////            float off = (pressed ? 2.0f : (_hover && Enabled ? 2.8f : 3.4f)) * lay.Dpi;

////            Color tlC = pressed ? pal.ShadowDark : pal.ShadowLight;   // ambient light (top-left)
////            Color brC = pressed ? pal.ShadowLight : pal.ShadowDark;   // occlusion   (bottom-right)

////            for (int i = 2; i >= 1; i--)
////            {
////                float o = off * (i == 2 ? 1.5f : 0.7f);
////                int pct = i == 2 ? 55 : 115;

////                using (GraphicsPath pTL = BuildRoundedPath(OffsetRect(v, -o, -o), r))
////                using (SolidBrush bTL = new SolidBrush(AlphaScale(tlC, pct)))
////                {
////                    g.FillPath(bTL, pTL);
////                }
////                using (GraphicsPath pBR = BuildRoundedPath(OffsetRect(v, o, o), r))
////                using (SolidBrush bBR = new SolidBrush(AlphaScale(brC, pct)))
////                {
////                    g.FillPath(bBR, pBR);
////                }
////            }

////            using (GraphicsPath face = BuildRoundedPath(v, r))
////            {
////                using (SolidBrush fill = new SolidBrush(pal.ViewportFace)) g.FillPath(fill, face);
////                using (Pen hair = new Pen(Color.FromArgb(70, pal.BorderHighlight), 1f)) g.DrawPath(hair, face);
////            }

////            // Embossed center well (depth inverts when pressed).
////            using (GraphicsPath well = BuildRoundedPath(lay.Content, lay.ContentRadius))
////            {
////                using (SolidBrush wb = new SolidBrush(pal.ViewportWell)) g.FillPath(wb, well);

////                Color topCol = pressed ? pal.ShadowLight : pal.ShadowDark;
////                Color botCol = pressed ? pal.ShadowDark : pal.ShadowLight;

////                g.SetClip(new RectangleF(lay.Content.X, lay.Content.Y,
////                                         lay.Content.Width, lay.Content.Height * 0.5f));
////                using (Pen tp = new Pen(topCol, 2.2f)) g.DrawPath(tp, well);
////                g.ResetClip();

////                g.SetClip(new RectangleF(lay.Content.X, lay.Content.Y + lay.Content.Height * 0.5f,
////                                         lay.Content.Width, lay.Content.Height * 0.5f));
////                using (Pen bt = new Pen(botCol, 2.2f)) g.DrawPath(bt, well);
////                g.ResetClip();
////            }
////        }

////        // STYLE 5 — CYBERPUNK / INDUSTRIAL ------------------------------------
////        private void DrawCyberCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF v = lay.View;
////            float r = lay.ViewRadius;

////            using (GraphicsPath face = BuildRoundedPath(v, r))
////            using (SolidBrush fill = new SolidBrush(pal.ViewportFace))
////            {
////                g.FillPath(fill, face);
////            }

////            // Neon halo — always on, intensifies with interaction.
////            int glow = _pressed ? 3 : (_hover && Enabled ? 2 : 1);
////            for (int i = glow; i >= 1; i--)
////            {
////                using (GraphicsPath gp = BuildRoundedPath(Expand(v, 1.4f * i + 1f), r + 1.4f * i + 1f))
////                using (Pen pen = new Pen(Color.FromArgb(70 / i, pal.Accent), 1.6f))
////                {
////                    g.DrawPath(pen, gp);
////                }
////            }

////            using (GraphicsPath core = BuildRoundedPath(Deflate(v, 0.8f), Math.Max(2f, r - 0.8f)))
////            using (Pen neon = new Pen(pal.Accent, 1.7f))
////            {
////                g.DrawPath(neon, core);
////            }

////            // Tech-accented corner brackets.
////            DrawCornerBrackets(g, v, 16f * lay.Dpi, 2.6f, Color.FromArgb(220, pal.AccentAlt));
////        }

////        // --------------------------------------------------------------------
////        //  LAYER 3 — CONTENT (image / empty state / film effects)
////        // --------------------------------------------------------------------
////        private void DrawContentLayer_Old(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF c = lay.Content;
////            if (c.Width < 2f || c.Height < 2f) return;

////            using (GraphicsPath clip = BuildRoundedPath(c, lay.ContentRadius))
////            {
////                g.SetClip(clip);

////                using (SolidBrush well = new SolidBrush(pal.ViewportWell)) g.FillPath(well, clip);

////                if (_image != null)
////                {
////                    RectangleF dest = new RectangleF(
////                        _pan.X, _pan.Y, _image.Width * _zoom, _image.Height * _zoom);

////                    g.InterpolationMode = _interpolation;      // user-selected sampling
////                    g.PixelOffsetMode = PixelOffsetMode.Half;  // exact pixel alignment
////                    g.DrawImage(_image, dest.X, dest.Y, dest.Width, dest.Height);
////                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
////                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

////                    // Hairline frame so light images read against light faces.
////                    using (Pen ip = new Pen(Color.FromArgb(70, pal.TextDim), 1f))
////                        g.DrawRectangle(ip, dest.X, dest.Y, dest.Width, dest.Height);

////                    if (_controlStyle == ViewerStyle.Cyberpunk)
////                        DrawCornerBrackets(g, dest, 10f, 2.2f, Color.FromArgb(200, pal.AccentAlt));
////                }
////                else
////                {
////                    DrawEmptyState(g, pal, lay);
////                }

////                if (_controlStyle == ViewerStyle.DashboardPremium)
////                    DrawEdgeTicks(g, pal, c, lay.Dpi);

////                if (_controlStyle == ViewerStyle.Cyberpunk && _showScanlines)
////                    DrawScanlines(g, pal, c);

////                if (_controlStyle == ViewerStyle.MaterialFlat && Enabled)
////                {
////                    int alpha = _pressed ? 14 : (_hover ? 7 : 0);
////                    if (alpha > 0)
////                    {
////                        using (SolidBrush ov = new SolidBrush(Color.FromArgb(alpha, pal.HoverOverlay)))
////                            g.FillPath(ov, clip);
////                    }
////                }

////                g.ResetClip();
////            }
////        }

////        private void DrawContentLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF c = lay.Content;
////            if (c.Width < 2f || c.Height < 2f) return;

////            using (GraphicsPath clip = BuildRoundedPath(c, lay.ContentRadius))
////            {
////                g.SetClip(clip);

////                using (SolidBrush well = new SolidBrush(pal.ViewportWell)) g.FillPath(well, clip);

////                if (!TryDrawImage(g, pal))            // ← hardened image draw
////                {
////                    DrawEmptyState(g, pal, lay);      // covers: no image OR dead image
////                }

////                if (_controlStyle == ViewerStyle.DashboardPremium)
////                    DrawEdgeTicks(g, pal, c, lay.Dpi);

////                if (_controlStyle == ViewerStyle.Cyberpunk && _showScanlines)
////                    DrawScanlines(g, pal, c);

////                if (_controlStyle == ViewerStyle.MaterialFlat && Enabled)
////                {
////                    int alpha = _pressed ? 14 : (_hover ? 7 : 0);
////                    if (alpha > 0)
////                    {
////                        using (SolidBrush ov = new SolidBrush(Color.FromArgb(alpha, pal.HoverOverlay)))
////                            g.FillPath(ov, clip);
////                    }
////                }

////                g.ResetClip();
////            }
////        }

////        private bool TryDrawImage(Graphics g, ViewerPalette pal)
////        {
////            if (_image == null) return false;

////            g.InterpolationMode = _interpolation;
////            g.PixelOffsetMode = PixelOffsetMode.Half;

////            bool drawn = false;
////            try
////            {
////                // NOTE: even reading Width/Height on an externally-disposed bitmap
////                // throws ArgumentException — the catch below is the guard for that.
////                float dw = _image.Width * _zoom;
////                float dh = _image.Height * _zoom;

////                if (dw > 0f && dh > 0f && !float.IsNaN(dw) && !float.IsNaN(dh))
////                {
////                    g.DrawImage(_image, _pan.X, _pan.Y, dw, dh);

////                    using (Pen ip = new Pen(Color.FromArgb(70, pal.TextDim), 1f))
////                        g.DrawRectangle(ip, _pan.X, _pan.Y, dw, dh);

////                    if (_controlStyle == ViewerStyle.Cyberpunk)
////                        DrawCornerBrackets(g, new RectangleF(_pan.X, _pan.Y, dw, dh),
////                                           10f, 2.2f, Color.FromArgb(200, pal.AccentAlt));
////                    drawn = true;
////                }
////            }
////            catch (ArgumentException)
////            {
////                // The bitmap died outside our ownership. Degrade gracefully instead
////                // of crashing the application.
////                _image = null;            // already dead — do NOT Dispose() it again
////                _needsAutoFit = true;
////                _imageLostPending = true;
////            }
////            finally
////            {
////                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
////                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
////            }

////            return drawn;
////        }

////        private void DrawEmptyState_Old1(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF c = lay.Content;
////            float s = lay.Dpi;

////            // Faint blueprint grid.
////            using (Pen grid = new Pen(pal.Grid, 1f))
////            {
////                float step = 28f * s;
////                for (float x = c.X + step; x < c.Right; x += step) g.DrawLine(grid, x, c.Y, x, c.Bottom);
////                for (float y = c.Y + step; y < c.Bottom; y += step) g.DrawLine(grid, c.X, y, c.Right, y);
////            }

////            bool cyber = _controlStyle == ViewerStyle.Cyberpunk;
////            float cx = c.X + c.Width / 2f;
////            float cy = c.Y + c.Height / 2f;

////            // Vector camera glyph — mathematically centered.
////            RectangleF body = new RectangleF(cx - 43f * s, cy - 24f * s, 86f * s, 60f * s);
////            RectangleF bump = new RectangleF(cx - 15f * s, cy - 35f * s, 30f * s, 13f * s);
////            float lensR = 13f * s;
////            float innerR = 5f * s;
////            float lensCy = cy + 6f * s;
////            Color stroke = Color.FromArgb(165, pal.TextSecondary);

////            using (GraphicsPath bodyPath = BuildRoundedPath(body, 12f * s))
////            using (GraphicsPath bumpPath = BuildRoundedPath(bump, 5f * s))
////            using (Pen pen = new Pen(stroke, 2.6f))
////            {
////                g.DrawPath(pen, bumpPath);
////                g.DrawPath(pen, bodyPath);
////                using (SolidBrush fill = new SolidBrush(pal.ViewportWell))
////                {
////                    g.FillPath(fill, bumpPath);   // fill after stroke — hides interior seams
////                    g.FillPath(fill, bodyPath);
////                }
////                g.DrawEllipse(pen, cx - lensR, lensCy - lensR, lensR * 2f, lensR * 2f);
////                using (SolidBrush fill2 = new SolidBrush(pal.ViewportWell))
////                    g.FillEllipse(fill2, cx - lensR, lensCy - lensR, lensR * 2f, lensR * 2f);
////                using (Pen inner = new Pen(Color.FromArgb(120, pal.TextSecondary), 2f))
////                    g.DrawEllipse(inner, cx - innerR, lensCy - innerR, innerR * 2f, innerR * 2f);
////            }

////            string title = cyber ? "NO SIGNAL" : "NO IMAGE LOADED";
////            string hint = cyber ? "AWAITING INPUT · DROP FILE TO SCAN"
////                                : "Drag & drop an image file, or assign the Image property";
////            float ty = body.Bottom + 22f * s;

////            using (StringFormat fmt = new StringFormat())
////            {
////                fmt.Alignment = StringAlignment.Center;
////                fmt.LineAlignment = StringAlignment.Near;
////                fmt.FormatFlags |= StringFormatFlags.NoWrap;

////                using (Font tf = GetFont(cyber, 10.5f, FontStyle.Bold))
////                using (SolidBrush tb = new SolidBrush(pal.TextSecondary))
////                    g.DrawString(title, tf, tb, cx, ty, fmt);

////                using (Font sf = GetFont(false, 8.75f, FontStyle.Regular))
////                using (SolidBrush sb = new SolidBrush(pal.TextDim))
////                    g.DrawString(hint, sf, sb, cx, ty + 19f * s, fmt);
////            }
////        }

////        private void DrawEmptyState(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF c = lay.Content;
////            float s = lay.Dpi;

////            using (Pen grid = new Pen(pal.Grid, 1f))
////            {
////                float step = 28f * s;
////                for (float x = c.X + step; x < c.Right; x += step) g.DrawLine(grid, x, c.Y, x, c.Bottom);
////                for (float y = c.Y + step; y < c.Bottom; y += step) g.DrawLine(grid, c.X, y, c.Right, y);
////            }

////            bool cyber = _controlStyle == ViewerStyle.Cyberpunk;
////            float cx = c.X + c.Width / 2f;
////            float cy = c.Y + c.Height / 2f;

////            RectangleF body = new RectangleF(cx - 43f * s, cy - 24f * s, 86f * s, 60f * s);
////            RectangleF bump = new RectangleF(cx - 15f * s, cy - 35f * s, 30f * s, 13f * s);
////            float lensR = 13f * s;
////            float innerR = 5f * s;
////            float lensCy = cy + 6f * s;
////            Color stroke = Color.FromArgb(165, pal.TextSecondary);

////            using (GraphicsPath bodyPath = BuildRoundedPath(body, 12f * s))
////            using (GraphicsPath bumpPath = BuildRoundedPath(bump, 5f * s))
////            using (Pen pen = new Pen(stroke, 2.6f))
////            {
////                g.DrawPath(pen, bumpPath);
////                g.DrawPath(pen, bodyPath);
////                using (SolidBrush fill = new SolidBrush(pal.ViewportWell))
////                {
////                    g.FillPath(fill, bumpPath);
////                    g.FillPath(fill, bodyPath);
////                }
////                g.DrawEllipse(pen, cx - lensR, lensCy - lensR, lensR * 2f, lensR * 2f);
////                using (SolidBrush fill2 = new SolidBrush(pal.ViewportWell))
////                    g.FillEllipse(fill2, cx - lensR, lensCy - lensR, lensR * 2f, lensR * 2f);
////                using (Pen inner = new Pen(Color.FromArgb(120, pal.TextSecondary), 2f))
////                    g.DrawEllipse(inner, cx - innerR, lensCy - innerR, innerR * 2f, innerR * 2f);
////            }

////            string title = cyber ? "NO SIGNAL" : "NO IMAGE LOADED";
////            string hint = cyber ? "AWAITING INPUT · DROP FILE TO SCAN"
////                                : "Drag & drop an image file, or assign the Image property";
////            float ty = body.Bottom + 22f * s;

////            // -----------------------------------------------------------------
////            // FIX — fonts returned by GetFont are cache-owned. NEVER wrap them
////            // in `using`; that disposes the shared instance and poisons the
////            // cache. Only Dispose(bool) (which clears the cache) may release
////            // them. The Brushes and StringFormat below remain disposable.
////            // -----------------------------------------------------------------
////            Font tf = GetFont(cyber, 10.5f, FontStyle.Bold);
////            Font sf = GetFont(false, 8.75f, FontStyle.Regular);

////            using (StringFormat fmt = new StringFormat())
////            {
////                fmt.Alignment = StringAlignment.Center;
////                fmt.LineAlignment = StringAlignment.Near;
////                fmt.FormatFlags |= StringFormatFlags.NoWrap;

////                using (SolidBrush tb = new SolidBrush(pal.TextSecondary))
////                    g.DrawString(title, tf, tb, cx, ty, fmt);

////                using (SolidBrush sb = new SolidBrush(pal.TextDim))
////                    g.DrawString(hint, sf, sb, cx, ty + 19f * s, fmt);
////            }
////        }

////        // --------------------------------------------------------------------
////        //  LAYER 5 — HUD READOUTS
////        // --------------------------------------------------------------------
////        private void DrawHudLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            if (!_showHud || !Enabled) return;
////            RectangleF c = lay.Content;
////            if (c.Width < 160f || c.Height < 96f) return;

////            Font capFont = GetFont(false, 6.75f, FontStyle.Bold);
////            Font valFont = GetFont(true, 10f, FontStyle.Bold);

////            string source = _image != null
////                ? string.Format("{0} × {1}", _image.Width, _image.Height)
////                : "—";
////            DrawHudChip(g, pal, lay, ChipAnchor.TopLeft, "SOURCE", source, capFont, valFont,
////                        _image != null ? pal.Accent : pal.TextDim, false, pal.TextDim);

////            bool flat = _controlStyle == ViewerStyle.MaterialFlat;
////            Color zone = ZoomZoneColor(pal);
////            DrawHudChip(g, pal, lay, ChipAnchor.BottomRight, "ZOOM",
////                        (_zoom * 100f).ToString("0.#") + " %", capFont, valFont,
////                        flat ? (Color?)null : zone, true, flat ? pal.HudFore : zone);

////            if (_hover && _image != null)
////            {
////                PointF ip = ScreenToImageF(_hoverPos);
////                DrawHudChip(g, pal, lay, ChipAnchor.BottomLeft, "PROBE",
////                            string.Format("X {0:0}   Y {1:0}", ip.X, ip.Y),
////                            capFont, valFont, null, false, pal.TextDim);
////            }
////        }

////        private void DrawHudChip(Graphics g, ViewerPalette pal, ViewerLayout lay, ChipAnchor anchor,
////                                 string caption, string value, Font capFont, Font valFont,
////                                 Color? dot, bool showBar, Color barColor)
////        {
////            float dpi = lay.Dpi;
////            RectangleF c = lay.Content;
////            float padX = 9f * dpi;
////            float padY = 6f * dpi;

////            SizeF capS;
////            SizeF valS;
////            using (StringFormat fmt = new StringFormat(
////                StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces))
////            {
////                capS = g.MeasureString(caption, capFont, int.MaxValue, fmt);
////                valS = g.MeasureString(value, valFont, int.MaxValue, fmt);
////            }

////            float dotW = dot.HasValue ? 14f * dpi : 0f;
////            float w = Math.Max(capS.Width, valS.Width) + dotW + padX * 2f;
////            float h = padY + capS.Height + 2f * dpi + valS.Height + (showBar ? 6f * dpi : 0f) + padY;

////            if (w > c.Width - 12f || h > c.Height - 12f) return;   // never crowd tiny viewports

////            float inset = 10f * dpi;
////            PointF loc;
////            switch (anchor)
////            {
////                case ChipAnchor.BottomLeft: loc = new PointF(c.X + inset, c.Bottom - inset - h); break;
////                case ChipAnchor.BottomRight: loc = new PointF(c.Right - inset - w, c.Bottom - inset - h); break;
////                default: loc = new PointF(c.X + inset, c.Y + inset); break;
////            }
////            RectangleF chip = new RectangleF(loc.X, loc.Y, w, h);

////            using (GraphicsPath path = BuildRoundedPath(chip, 7f * dpi))
////            {
////                using (SolidBrush bg = new SolidBrush(pal.HudBack)) g.FillPath(bg, path);
////                if (pal.HudBorder.A > 0)
////                {
////                    using (Pen bp = new Pen(pal.HudBorder, 1f)) g.DrawPath(bp, path);
////                }

////                if (_controlStyle == ViewerStyle.SoftNeumorphic)
////                {
////                    g.SetClip(new RectangleF(chip.X, chip.Y, chip.Width, chip.Height * 0.5f));
////                    using (Pen tp = new Pen(pal.ShadowDark, 1f)) g.DrawPath(tp, path);
////                    g.ResetClip();
////                    g.SetClip(new RectangleF(chip.X, chip.Y + chip.Height * 0.5f,
////                                             chip.Width, chip.Height * 0.5f));
////                    using (Pen bt = new Pen(pal.ShadowLight, 1f)) g.DrawPath(bt, path);
////                    g.ResetClip();
////                }
////            }

////            using (StringFormat fmt = new StringFormat(StringFormatFlags.NoWrap))
////            {
////                float tx = chip.X + padX;
////                using (SolidBrush capBrush = new SolidBrush(pal.HudForeDim))
////                    g.DrawString(caption, capFont, capBrush, tx, chip.Y + padY, fmt);

////                float vy = chip.Y + padY + capS.Height + 2f * dpi;
////                using (SolidBrush valBrush = new SolidBrush(pal.HudFore))
////                    g.DrawString(value, valFont, valBrush, tx, vy, fmt);

////                if (dot.HasValue)
////                {
////                    float d = 6.8f * dpi;
////                    using (SolidBrush db = new SolidBrush(dot.Value))
////                        g.FillEllipse(db, chip.Right - padX - d, vy + valS.Height / 2f - d / 2f, d, d);
////                }

////                if (showBar)
////                {
////                    float by = chip.Bottom - padY - 2.2f * dpi;
////                    float trackW = w - padX * 2f;

////                    using (GraphicsPath track = BuildRoundedPath(
////                        new RectangleF(tx, by, trackW, 3f * dpi), 1.5f * dpi))
////                    using (SolidBrush tb = new SolidBrush(Color.FromArgb(70, pal.HudFore)))
////                        g.FillPath(tb, track);

////                    float t = ZoomBarT();
////                    if (t > 0.01f)
////                    {
////                        using (GraphicsPath fill = BuildRoundedPath(
////                            new RectangleF(tx, by, trackW * t, 3f * dpi), 1.5f * dpi))
////                        using (SolidBrush fb = new SolidBrush(barColor))
////                            g.FillPath(fb, fill);
////                    }
////                }
////            }
////        }

////        // --------------------------------------------------------------------
////        //  LAYER 6 — STATE CHROME
////        // --------------------------------------------------------------------
////        private void DrawStateLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            // Precision crosshair (Dashboard / Cyberpunk).
////            if (_showCrosshair && _hover && Enabled &&
////                (_controlStyle == ViewerStyle.Cyberpunk || _controlStyle == ViewerStyle.DashboardPremium))
////            {
////                using (GraphicsPath clip = BuildRoundedPath(lay.Content, lay.ContentRadius))
////                {
////                    g.SetClip(clip);
////                    float px = _hoverPos.X;
////                    float py = _hoverPos.Y;
////                    float gap = 11f * lay.Dpi;

////                    using (Pen pen = new Pen(Color.FromArgb(115, pal.Accent), 1f))
////                    {
////                        g.DrawLine(pen, lay.Content.X, py, px - gap, py);
////                        g.DrawLine(pen, px + gap, py, lay.Content.Right, py);
////                        g.DrawLine(pen, px, lay.Content.Y, px, py - gap);
////                        g.DrawLine(pen, px, py + gap, px, lay.Content.Bottom);
////                    }
////                    using (Pen cp = new Pen(Color.FromArgb(190, pal.Accent), 1.2f))
////                        g.DrawEllipse(cp, px - 3.5f, py - 3.5f, 7f, 7f);

////                    g.ResetClip();
////                }
////            }

////            // Focus ring.
////            if (Focused && Enabled)
////            {
////                using (GraphicsPath fp = BuildRoundedPath(Expand(lay.View, 3f), lay.ViewRadius + 3f))
////                using (Pen pen = new Pen(Color.FromArgb(215, pal.FocusRing), 1.4f))
////                    g.DrawPath(pen, fp);
////            }

////            // Disabled veil.
////            if (!Enabled)
////            {
////                using (SolidBrush veil = new SolidBrush(Color.FromArgb(110, pal.Canvas)))
////                    g.FillRectangle(veil, 0f, 0f, Width, Height);
////            }
////        }

////        // --------------------------------------------------------------------
////        //  STYLE MICRO-DETAIL PRIMITIVES
////        // --------------------------------------------------------------------
////        private static void DrawEdgeTicks(Graphics g, ViewerPalette pal, RectangleF c, float dpi)
////        {
////            SmoothingMode prev = g.SmoothingMode;
////            g.SmoothingMode = SmoothingMode.None;   // pixel-exact 1-px vector ticks

////            using (Pen minor = new Pen(Color.FromArgb(110, pal.Tick), 1f))
////            using (Pen major = new Pen(Color.FromArgb(210, pal.Tick), 1f))
////            {
////                float step = 9f * dpi;
////                int i = 0;
////                for (float x = c.X + 3f; x < c.Right - 2f; x += step)
////                {
////                    float len = (i % 5 == 0) ? 7f * dpi : 4f * dpi;
////                    g.DrawLine((i % 5 == 0) ? major : minor, x, c.Y + 1f, x, c.Y + 1f + len);
////                    i++;
////                }
////                i = 0;
////                for (float y = c.Y + 3f; y < c.Bottom - 2f; y += step)
////                {
////                    float len = (i % 5 == 0) ? 7f * dpi : 4f * dpi;
////                    g.DrawLine((i % 5 == 0) ? major : minor, c.X + 1f, y, c.X + 1f + len, y);
////                    i++;
////                }
////            }

////            g.SmoothingMode = prev;
////        }

////        private static void DrawScanlines(Graphics g, ViewerPalette pal, RectangleF c)
////        {
////            SmoothingMode prev = g.SmoothingMode;
////            g.SmoothingMode = SmoothingMode.None;

////            using (Pen pen = new Pen(Color.FromArgb(9, pal.Accent), 1f))
////            {
////                for (float y = c.Y + 2f; y < c.Bottom; y += 4f)
////                    g.DrawLine(pen, c.X, y, c.Right, y);
////            }

////            g.SmoothingMode = prev;
////        }

////        private static void DrawCornerBrackets(Graphics g, RectangleF rect, float len, float width, Color color)
////        {
////            using (Pen pen = new Pen(color, width))
////            {
////                pen.StartCap = LineCap.Round;
////                pen.EndCap = LineCap.Round;

////                float l = rect.Left, t = rect.Top, r = rect.Right, b = rect.Bottom;

////                g.DrawLine(pen, l, t, l + len, t); g.DrawLine(pen, l, t, l, t + len);
////                g.DrawLine(pen, r, t, r - len, t); g.DrawLine(pen, r, t, r, t + len);
////                g.DrawLine(pen, l, b, l + len, b); g.DrawLine(pen, l, b, l, b - len);
////                g.DrawLine(pen, r, b, r - len, b); g.DrawLine(pen, r, b, r, b - len);
////            }
////        }

////        /// <summary>Layered-alpha soft drop shadow (GDI+ has no blur — this is the premium approximation).</summary>
////        private static void DrawSoftShadow(Graphics g, RectangleF rect, float radius, Color shadow,
////                                           float depth, int steps)
////        {
////            int layerAlpha = Math.Max(4, shadow.A / steps);
////            for (int i = steps; i >= 1; i--)
////            {
////                float off = 1f + (depth * (i - 1) / steps);
////                using (GraphicsPath p = BuildRoundedPath(OffsetRect(rect, 0f, off), radius + i * 0.7f))
////                using (SolidBrush b = new SolidBrush(Color.FromArgb(layerAlpha, shadow)))
////                {
////                    g.FillPath(b, p);
////                }
////            }
////        }

////        // --------------------------------------------------------------------
////        //  LAYOUT ENGINE — fractional padding, per-style radii, DPI-aware
////        // --------------------------------------------------------------------
////        private ViewerLayout ComputeLayout_Old()
////        {
////            float dpi = Math.Max(1f, _dpiScale);

////            float padMul = _controlStyle == ViewerStyle.MaterialFlat ? 1.45f : 1f;
////            float basePad = Math.Min(Width, Height) * 0.035f;
////            float pad = Math.Max(11f * dpi, Math.Min(26f * dpi, basePad)) * padMul;

////            float right = Math.Max(pad + 2f, Width - pad);
////            float bottom = Math.Max(pad + 2f, Height - pad);
////            RectangleF view = RectangleF.FromLTRB(pad, pad, right, bottom);

////            float radius;
////            float inset;
////            switch (_controlStyle)
////            {
////                case ViewerStyle.FluentGlass: radius = 14f * dpi; inset = 1.5f * dpi; break;
////                case ViewerStyle.MaterialFlat: radius = 3f * dpi; inset = 0.75f * dpi; break;
////                case ViewerStyle.SoftNeumorphic: radius = 20f * dpi; inset = 11f * dpi; break;
////                case ViewerStyle.Cyberpunk: radius = 8f * dpi; inset = 1.25f * dpi; break;
////                default: radius = 10f * dpi; inset = 1.25f * dpi; break;
////            }

////            float cr = Math.Max(1f, radius - inset - 0.5f);
////            float minDim = Math.Min(view.Width, view.Height);
////            if (cr * 2f > minDim) cr = minDim / 2f;

////            return new ViewerLayout
////            {
////                View = view,
////                Content = Deflate(view, inset),
////                ViewRadius = radius,
////                ContentRadius = cr,
////                Dpi = dpi
////            };
////        }
////        private ViewerLayout ComputeLayout()
////        {
////            float dpi = Math.Max(1f, _dpiScale);

////            float padMul = _controlStyle == ViewerStyle.MaterialFlat ? 1.45f : 1f;
////            float basePad = Math.Min(Width, Height) * 0.035f;
////            float pad = Math.Max(11f * dpi, Math.Min(26f * dpi, basePad)) * padMul;

////            // FIX: padding may never consume the control (transient tiny sizes).
////            float maxPad = Math.Min(Math.Max(0f, (Width - 2f) / 2f),
////                                    Math.Max(0f, (Height - 2f) / 2f));
////            pad = Math.Min(pad, maxPad);

////            RectangleF view = RectangleF.FromLTRB(pad, pad,
////                Math.Max(pad + 2f, Width - pad),
////                Math.Max(pad + 2f, Height - pad));

////            float radius;
////            float inset;
////            switch (_controlStyle)
////            {
////                case ViewerStyle.FluentGlass: radius = 14f * dpi; inset = 1.5f * dpi; break;
////                case ViewerStyle.MaterialFlat: radius = 3f * dpi; inset = 0.75f * dpi; break;
////                case ViewerStyle.SoftNeumorphic: radius = 20f * dpi; inset = 11f * dpi; break;
////                case ViewerStyle.Cyberpunk: radius = 8f * dpi; inset = 1.25f * dpi; break;
////                default: radius = 10f * dpi; inset = 1.25f * dpi; break;
////            }

////            // FIX: the style inset may never push the content well below 2 x 2.
////            float maxInset = Math.Min(Math.Max(0f, (view.Width - 2f) / 2f),
////                                      Math.Max(0f, (view.Height - 2f) / 2f));
////            inset = Math.Min(inset, maxInset);

////            float cr = Math.Max(1f, radius - inset - 0.5f);
////            float minDim = Math.Min(view.Width, view.Height);
////            if (cr * 2f > minDim) cr = minDim / 2f;

////            return new ViewerLayout
////            {
////                View = view,
////                Content = Deflate(view, inset),
////                ViewRadius = radius,
////                ContentRadius = cr,
////                Dpi = dpi
////            };
////        }

////        /// <summary>Raised when the assigned bitmap was found dead at draw time
////        /// (disposed externally, or its backing stream/Mat was released).</summary>
////        public event EventHandler ImageLost;

////        private bool _imageLostPending;   // field — put it next to _dpiScale

////        private Color ZoomZoneColor(ViewerPalette pal)
////        {
////            if (_zoom < 2f) return pal.ZoneGood;   // native / overview
////            if (_zoom < 8f) return pal.ZoneWarn;   // inspection
////            return pal.ZoneHot;                    // deep pixel-level zoom
////        }

////        private float ZoomBarT()
////        {
////            double lo = Math.Log10((double)_minZoom);
////            double hi = Math.Log10((double)_maxZoom);
////            if (hi - lo < 0.0001) return 0f;

////            double t = (Math.Log10((double)_zoom) - lo) / (hi - lo);
////            if (t < 0.0) t = 0.0;
////            if (t > 1.0) t = 1.0;
////            return (float)t;
////        }

////        // --------------------------------------------------------------------
////        //  GEOMETRY & COLOR PRIMITIVES
////        // --------------------------------------------------------------------
////        private static GraphicsPath BuildRoundedPath_Old(RectangleF rect, float radius)
////        {
////            GraphicsPath path = new GraphicsPath();
////            float maxR = Math.Min(rect.Width, rect.Height) / 2f;
////            if (maxR < 0.5f || radius < 0.5f)
////            {
////                path.AddRectangle(rect);
////                return path;
////            }
////            if (radius > maxR) radius = maxR;
////            float d = radius * 2f;

////            path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
////            path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
////            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
////            path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
////            path.CloseFigure();
////            return path;
////        }

////        private static GraphicsPath BuildRoundedPath(RectangleF rect, float radius)
////        {
////            GraphicsPath path = new GraphicsPath();

////            // ---------------------------------------------------------------
////            // FIX for "System.ArgumentException: Parameter is not valid."
////            // GDI+ raises this exact exception when AddRectangle / AddArc
////            // receive negative, degenerate or non-finite dimensions. Controls
////            // legitimately pass through tiny transient sizes while the layout
////            // engine resolves — sanitize instead of throwing.
////            // ---------------------------------------------------------------
////            float w = rect.Width;
////            float h = rect.Height;
////            if (float.IsNaN(w) || float.IsNaN(h) || float.IsInfinity(w) || float.IsInfinity(h))
////                return path;                        // empty path → draw/fill = safe no-op
////            if (w < 0f) w = 0f;
////            if (h < 0f) h = 0f;
////            if (float.IsNaN(radius) || radius < 0f) radius = 0f;

////            if (w < 0.5f || h < 0.5f) return path;  // degenerate → no-op

////            rect = new RectangleF(rect.X, rect.Y, w, h);

////            float maxR = Math.Min(w, h) / 2f;
////            if (radius < 0.5f) { path.AddRectangle(rect); return path; }
////            if (radius > maxR) radius = maxR;

////            float d = radius * 2f;
////            path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
////            path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
////            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
////            path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
////            path.CloseFigure();
////            return path;
////        }

////        private static RectangleF Deflate(RectangleF r, float v)
////        { return new RectangleF(r.X + v, r.Y + v, r.Width - 2f * v, r.Height - 2f * v); }

////        private static RectangleF Expand(RectangleF r, float v)
////        { return new RectangleF(r.X - v, r.Y - v, r.Width + 2f * v, r.Height + 2f * v); }

////        private static RectangleF OffsetRect(RectangleF r, float dx, float dy)
////        { return new RectangleF(r.X + dx, r.Y + dy, r.Width, r.Height); }

////        private static Color Lerp(Color a, Color b, float t)
////        {
////            return Color.FromArgb(
////                a.R + (int)((b.R - a.R) * t),
////                a.G + (int)((b.G - a.G) * t),
////                a.B + (int)((b.B - a.B) * t));
////        }

////        private static Color AlphaScale(Color c, int percent)
////        {
////            int a = (int)(c.A * percent / 100.0);
////            if (a > 255) a = 255;
////            return Color.FromArgb(a, c);
////        }

////        private static float ClampF_old(float v, float lo, float hi)
////        { return v < lo ? lo : (v > hi ? hi : v); }

////        private static float ClampF(float v, float lo, float hi)
////        {
////            if (float.IsNaN(v)) return lo;
////            return v < lo ? lo : (v > hi ? hi : v);
////        }

////        // --------------------------------------------------------------------
////        //  PALETTE ENGINE — 5 styles × 2 themes
////        // --------------------------------------------------------------------
////        private static ViewerPalette BuildPalette(ThemeMode theme, ViewerStyle style)
////        {
////            bool dark = theme == ThemeMode.Dark;
////            switch (style)
////            {
////                case ViewerStyle.FluentGlass: return GlassPalette(dark);
////                case ViewerStyle.MaterialFlat: return MaterialPalette(dark);
////                case ViewerStyle.SoftNeumorphic: return NeumorphicPalette(dark);
////                case ViewerStyle.Cyberpunk: return CyberPalette();
////                default: return DashboardPalette(dark);
////            }
////        }

////        private static Color C(int rgb)
////        { return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF); }

////        private static ViewerPalette DashboardPalette(bool dark)
////        {
////            if (dark)
////            {
////                return new ViewerPalette
////                {
////                    Canvas = C(0x0F1218),
////                    BackdropGlow = Color.FromArgb(0, Color.White),
////                    ViewportFace = C(0x151A22),
////                    ViewportWell = C(0x11151C),
////                    Border = C(0x28303E),
////                    BorderHighlight = Color.White,
////                    TextPrimary = C(0xE8EDF5),
////                    TextSecondary = C(0x97A3B5),
////                    TextDim = C(0x5E6A80),
////                    Accent = C(0x4C8DFF),
////                    AccentAlt = C(0x38BDF8),
////                    ZoneGood = C(0x2FBF71),
////                    ZoneWarn = C(0xF5A524),
////                    ZoneHot = C(0xEF4B4B),
////                    Grid = C(0x1B202A),
////                    Tick = C(0x7A87A0),
////                    HudBack = Color.FromArgb(190, C(0x0B0E14)),
////                    HudBorder = Color.FromArgb(60, Color.White),
////                    HudFore = C(0xE8EDF5),
////                    HudForeDim = C(0x97A3B5),
////                    Shadow = Color.FromArgb(150, Color.Black),
////                    ShadowLight = Color.FromArgb(0, Color.White),
////                    ShadowDark = Color.FromArgb(0, Color.Black),
////                    HoverOverlay = Color.White,
////                    FocusRing = C(0x4C8DFF)
////                };
////            }
////            return new ViewerPalette
////            {
////                Canvas = C(0xF3F5F9),
////                BackdropGlow = Color.FromArgb(0, Color.White),
////                ViewportFace = C(0xFFFFFF),
////                ViewportWell = C(0xFAFBFE),
////                Border = C(0xDCE3EE),
////                BorderHighlight = Color.White,
////                TextPrimary = C(0x16202F),
////                TextSecondary = C(0x5F6C7E),
////                TextDim = C(0x9AA6B8),
////                Accent = C(0x2563EB),
////                AccentAlt = C(0x0EA5E9),
////                ZoneGood = C(0x16A34A),
////                ZoneWarn = C(0xD97706),
////                ZoneHot = C(0xDC2626),
////                Grid = C(0xE7ECF4),
////                Tick = C(0xB9C4D4),
////                HudBack = Color.FromArgb(172, C(0xF9FBFE)),
////                HudBorder = Color.FromArgb(170, Color.White),
////                HudFore = C(0x16202F),
////                HudForeDim = C(0x5F6C7E),
////                Shadow = Color.FromArgb(30, C(0x0E1626)),
////                ShadowLight = Color.FromArgb(0, Color.White),
////                ShadowDark = Color.FromArgb(0, Color.Black),
////                HoverOverlay = Color.Black,
////                FocusRing = C(0x2563EB)
////            };
////        }

////        private static ViewerPalette GlassPalette(bool dark)
////        {
////            if (dark)
////            {
////                return new ViewerPalette
////                {
////                    Canvas = C(0x171B24),
////                    BackdropGlow = Color.FromArgb(38, C(0x00C8FF)),
////                    ViewportFace = Color.FromArgb(46, C(0x2A3140)),
////                    ViewportWell = Color.FromArgb(235, C(0x1B2029)),
////                    Border = Color.FromArgb(0, Color.White),
////                    BorderHighlight = Color.White,
////                    TextPrimary = C(0xEBF1FB),
////                    TextSecondary = C(0x9BA8BD),
////                    TextDim = C(0x63718A),
////                    Accent = C(0x00C8FF),
////                    AccentAlt = C(0x7C6CFF),
////                    ZoneGood = C(0x1FBF6B),
////                    ZoneWarn = C(0xF5A524),
////                    ZoneHot = C(0xF4506C),
////                    Grid = Color.FromArgb(26, C(0x6E82A6)),
////                    Tick = Color.FromArgb(80, C(0x5D6F92)),
////                    HudBack = Color.FromArgb(125, C(0x0E1219)),
////                    HudBorder = Color.FromArgb(140, Color.White),
////                    HudFore = C(0xEBF1FB),
////                    HudForeDim = C(0x9BA8BD),
////                    Shadow = Color.FromArgb(80, Color.Black),
////                    ShadowLight = Color.FromArgb(0, Color.White),
////                    ShadowDark = Color.FromArgb(0, Color.Black),
////                    HoverOverlay = C(0x00C8FF),
////                    FocusRing = C(0x00C8FF)
////                };
////            }
////            return new ViewerPalette
////            {
////                Canvas = C(0xE8EEF7),
////                BackdropGlow = Color.FromArgb(70, Color.White),
////                ViewportFace = Color.FromArgb(52, Color.White),
////                ViewportWell = Color.FromArgb(150, Color.White),
////                Border = Color.FromArgb(0, Color.White),
////                BorderHighlight = Color.White,
////                TextPrimary = C(0x0F2440),
////                TextSecondary = C(0x5D6F8C),
////                TextDim = C(0x8FA0B8),
////                Accent = C(0x0A84FF),
////                AccentAlt = C(0x7C5CFF),
////                ZoneGood = C(0x0E9F6E),
////                ZoneWarn = C(0xF0A63A),
////                ZoneHot = C(0xE5484D),
////                Grid = Color.FromArgb(24, C(0x2A3C5E)),
////                Tick = Color.FromArgb(80, C(0x334666)),
////                HudBack = Color.FromArgb(150, Color.White),
////                HudBorder = Color.FromArgb(200, Color.White),
////                HudFore = C(0x0F2440),
////                HudForeDim = C(0x5D6F8C),
////                Shadow = Color.FromArgb(55, C(0x1E2E4C)),
////                ShadowLight = Color.FromArgb(0, Color.White),
////                ShadowDark = Color.FromArgb(0, Color.Black),
////                HoverOverlay = C(0x0A84FF),
////                FocusRing = C(0x0A84FF)
////            };
////        }

////        private static ViewerPalette MaterialPalette(bool dark)
////        {
////            if (dark)
////            {
////                return new ViewerPalette
////                {
////                    Canvas = C(0x121212),
////                    BackdropGlow = Color.FromArgb(0, Color.White),
////                    ViewportFace = C(0x1E1E1E),
////                    ViewportWell = C(0x1A1A1A),
////                    Border = Color.FromArgb(0, Color.White),
////                    BorderHighlight = Color.FromArgb(0, Color.White),
////                    TextPrimary = C(0xF2F2F2),
////                    TextSecondary = C(0xB0B0B0),
////                    TextDim = C(0x8A8A8A),
////                    Accent = C(0xBB86FC),
////                    AccentAlt = C(0x03DAC6),
////                    ZoneGood = C(0x66BB6A),
////                    ZoneWarn = C(0xFFB74D),
////                    ZoneHot = C(0xEF5350),
////                    Grid = C(0x232323),
////                    Tick = C(0x2E2E2E),
////                    HudBack = C(0xBB86FC),
////                    HudBorder = Color.FromArgb(0, Color.White),
////                    HudFore = C(0x141218),
////                    HudForeDim = Color.FromArgb(150, C(0x141218)),
////                    Shadow = Color.FromArgb(0, Color.Black),
////                    ShadowLight = Color.FromArgb(0, Color.White),
////                    ShadowDark = Color.FromArgb(0, Color.Black),
////                    HoverOverlay = Color.White,
////                    FocusRing = C(0xBB86FC)
////                };
////            }
////            return new ViewerPalette
////            {
////                Canvas = C(0xF5F5F5),
////                BackdropGlow = Color.FromArgb(0, Color.White),
////                ViewportFace = C(0xFFFFFF),
////                ViewportWell = C(0xFFFFFF),
////                Border = Color.FromArgb(0, Color.White),
////                BorderHighlight = Color.FromArgb(0, Color.White),
////                TextPrimary = C(0x212121),
////                TextSecondary = C(0x757575),
////                TextDim = C(0x9E9E9E),
////                Accent = C(0x6200EE),
////                AccentAlt = C(0x03DAC6),
////                ZoneGood = C(0x2E7D32),
////                ZoneWarn = C(0xEF6C00),
////                ZoneHot = C(0xC62828),
////                Grid = C(0xEEEEEE),
////                Tick = C(0xE0E0E0),
////                HudBack = C(0x6200EE),
////                HudBorder = Color.FromArgb(0, Color.White),
////                HudFore = Color.White,
////                HudForeDim = Color.FromArgb(178, Color.White),
////                Shadow = Color.FromArgb(0, Color.Black),
////                ShadowLight = Color.FromArgb(0, Color.White),
////                ShadowDark = Color.FromArgb(0, Color.Black),
////                HoverOverlay = Color.Black,
////                FocusRing = C(0x6200EE)
////            };
////        }

////        private static ViewerPalette NeumorphicPalette(bool dark)
////        {
////            if (dark)
////            {
////                return new ViewerPalette
////                {
////                    Canvas = C(0x2A2F3A),
////                    BackdropGlow = Color.FromArgb(0, Color.White),
////                    ViewportFace = C(0x2A2F3A),
////                    ViewportWell = C(0x252A33),
////                    Border = Color.FromArgb(0, Color.White),
////                    BorderHighlight = Color.FromArgb(30, Color.White),
////                    TextPrimary = C(0xD6DCE8),
////                    TextSecondary = C(0x8791A6),
////                    TextDim = C(0x5F6879),
////                    Accent = C(0x7C8CF8),
////                    AccentAlt = C(0x9EA8FA),
////                    ZoneGood = C(0x58B87E),
////                    ZoneWarn = C(0xC9A55A),
////                    ZoneHot = C(0xC96A5E),
////                    Grid = Color.FromArgb(24, C(0x6A748C)),
////                    Tick = Color.FromArgb(60, C(0x6A748C)),
////                    HudBack = C(0x2A2F3A),
////                    HudBorder = Color.FromArgb(0, Color.White),
////                    HudFore = C(0xD6DCE8),
////                    HudForeDim = C(0x8791A6),
////                    Shadow = Color.FromArgb(70, C(0x1C2028)),
////                    ShadowLight = Color.FromArgb(130, C(0x3B4250)),
////                    ShadowDark = Color.FromArgb(160, C(0x1C2028)),
////                    HoverOverlay = Color.White,
////                    FocusRing = C(0x7C8CF8)
////                };
////            }
////            return new ViewerPalette
////            {
////                Canvas = C(0xE4E9F1),
////                BackdropGlow = Color.FromArgb(0, Color.White),
////                ViewportFace = C(0xE4E9F1),
////                ViewportWell = C(0xDCE2EC),
////                Border = Color.FromArgb(0, Color.White),
////                BorderHighlight = Color.White,
////                TextPrimary = C(0x47536E),
////                TextSecondary = C(0x8B96AD),
////                TextDim = C(0xA9B3C7),
////                Accent = C(0x6C7BF2),
////                AccentAlt = C(0x9BA6F5),
////                ZoneGood = C(0x6FBF8E),
////                ZoneWarn = C(0xD2A24C),
////                ZoneHot = C(0xD26A5C),
////                Grid = Color.FromArgb(26, C(0x9FACC6)),
////                Tick = Color.FromArgb(60, C(0x9FACC6)),
////                HudBack = C(0xE4E9F1),
////                HudBorder = Color.FromArgb(0, Color.White),
////                HudFore = C(0x47536E),
////                HudForeDim = C(0x8B96AD),
////                Shadow = Color.FromArgb(60, C(0xC3CDDF)),
////                ShadowLight = Color.FromArgb(210, Color.White),
////                ShadowDark = Color.FromArgb(170, C(0xC3CDDF)),
////                HoverOverlay = Color.White,
////                FocusRing = C(0x6C7BF2)
////            };
////        }

////        // Cyberpunk is deliberately theme-invariant (onyx is part of the identity).
////        private static ViewerPalette CyberPalette()
////        {
////            return new ViewerPalette
////            {
////                Canvas = C(0x05070A),
////                BackdropGlow = Color.FromArgb(22, C(0x00E5FF)),
////                ViewportFace = C(0x0A0D13),
////                ViewportWell = C(0x080A10),
////                Border = C(0x00E5FF),
////                BorderHighlight = Color.FromArgb(0, Color.White),
////                TextPrimary = C(0xD9F5FF),
////                TextSecondary = C(0x6E8CA0),
////                TextDim = C(0x45586A),
////                Accent = C(0x00E5FF),
////                AccentAlt = C(0xFF2E97),
////                ZoneGood = C(0x00FFA3),
////                ZoneWarn = C(0xFFE066),
////                ZoneHot = C(0xFF3860),
////                Grid = Color.FromArgb(22, C(0x00E5FF)),
////                Tick = Color.FromArgb(80, C(0x00E5FF)),
////                HudBack = Color.FromArgb(200, C(0x05080E)),
////                HudBorder = Color.FromArgb(90, C(0x00E5FF)),
////                HudFore = C(0xD9F5FF),
////                HudForeDim = C(0x5E7E93),
////                Shadow = Color.FromArgb(140, Color.Black),
////                ShadowLight = Color.FromArgb(0, Color.White),
////                ShadowDark = Color.FromArgb(0, Color.Black),
////                HoverOverlay = C(0x00E5FF),
////                FocusRing = C(0xFF2E97)
////            };
////        }

////        // --------------------------------------------------------------------
////        //  FONT ENGINE — modern families with graceful fallback
////        // --------------------------------------------------------------------
////        private static string UiFamily
////        {
////            get
////            {
////                if (_uiFamily == null) _uiFamily = ResolveFontFamily(UiFontCandidates);
////                return _uiFamily;
////            }
////        }

////        private static string MonoFamily
////        {
////            get
////            {
////                if (_monoFamily == null) _monoFamily = ResolveFontFamily(MonoFontCandidates);
////                return _monoFamily;
////            }
////        }

////        private static string ResolveFontFamily(string[] candidates)
////        {
////            try
////            {
////                using (InstalledFontCollection installed = new InstalledFontCollection())
////                {
////                    HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
////                    foreach (FontFamily family in installed.Families) names.Add(family.Name);
////                    foreach (string candidate in candidates)
////                        if (names.Contains(candidate)) return candidate;
////                }
////            }
////            catch { /* fall back below */ }
////            return candidates[candidates.Length - 1];
////        }

////        private Font GetFont_old(bool mono, float sizePt, FontStyle style)
////        {
////            string family = mono ? MonoFamily : UiFamily;
////            string key = family + "|" + sizePt.ToString("0.###") + "|" + (int)style;

////            Font font;
////            if (!_fontCache.TryGetValue(key, out font))
////            {
////                font = new Font(family, sizePt, style, GraphicsUnit.Point);
////                _fontCache.Add(key, font);
////            }
////            return font;
////        }

////        private Font GetFont(bool mono, float sizePt, FontStyle style)
////        {
////            string family = mono ? MonoFamily : UiFamily;
////            string key = family + "|" + sizePt.ToString("0.###") + "|" + (int)style;

////            Font font;
////            if (_fontCache.TryGetValue(key, out font))
////                return font;   // cache-owned — callers must NOT dispose

////            try
////            {
////                font = new Font(family, sizePt, style, GraphicsUnit.Point);
////            }
////            catch
////            {
////                font = new Font(FontFamily.GenericSansSerif, sizePt, style, GraphicsUnit.Point);
////            }
////            _fontCache[key] = font;
////            return font;
////        }

////        // --------------------------------------------------------------------
////        //  INPUT — MOUSE
////        // --------------------------------------------------------------------
////        protected override void OnMouseEnter(EventArgs e)
////        {
////            base.OnMouseEnter(e);
////            _hover = true;


////            //if (Enabled && !Focused) Focus();   // wheel-ready without a click

////            if (!IsDesignTime && Enabled && !Focused) Focus();
////            Invalidate();
////        }

////        protected override void OnMouseLeave(EventArgs e)
////        {
////            base.OnMouseLeave(e);
////            _hover = false;
////            Invalidate();
////        }

////        protected override void OnMouseDown(MouseEventArgs e)
////        {
////            base.OnMouseDown(e);
////            if (!Enabled) return;
////            Focus();

////            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
////            {
////                _panning = true;
////                _pressed = true;
////                _lastMouse = e.Location;
////                Cursor = Cursors.SizeAll;
////                Invalidate();
////            }
////        }

////        protected override void OnMouseMove(MouseEventArgs e)
////        {
////            base.OnMouseMove(e);
////            _hoverPos = e.Location;
////            if (!Enabled) return;

////            if (_panning)
////            {
////                _pan.X += e.X - _lastMouse.X;
////                _pan.Y += e.Y - _lastMouse.Y;
////                _lastMouse = e.Location;
////            }
////            Invalidate();   // crosshair + probe readout
////        }

////        protected override void OnMouseUp(MouseEventArgs e)
////        {
////            base.OnMouseUp(e);
////            _panning = false;
////            _pressed = false;
////            Cursor = Cursors.Cross;
////            Invalidate();
////        }

////        protected override void OnMouseCaptureChanged(EventArgs e)
////        {
////            base.OnMouseCaptureChanged(e);
////            if (_panning)
////            {
////                _panning = false;
////                _pressed = false;
////                Cursor = Cursors.Cross;
////                Invalidate();
////            }
////        }

////        protected override void OnMouseWheel(MouseEventArgs e)
////        {
////            // Consume the gesture so parent AutoScroll containers stay still.
////            if (e is HandledMouseEventArgs hme) hme.Handled = true;
////            base.OnMouseWheel(e);

////            if (!Enabled || _image == null) return;

////            float factor = e.Delta > 0 ? 1.12f : 1f / 1.12f;
////            SetZoom(_zoom * factor, new PointF(e.X, e.Y));
////        }

////        protected override void OnDoubleClick(EventArgs e)
////        {
////            base.OnDoubleClick(e);
////            AutoFit();
////        }

////        // --------------------------------------------------------------------
////        //  INPUT — KEYBOARD
////        // --------------------------------------------------------------------
////        protected override bool IsInputKey(Keys keyData)
////        {
////            switch (keyData & Keys.KeyCode)
////            {
////                case Keys.Left:
////                case Keys.Right:
////                case Keys.Up:
////                case Keys.Down:
////                    return true;
////            }
////            return base.IsInputKey(keyData);
////        }

////        protected override void OnKeyDown(KeyEventArgs e)
////        {
////            base.OnKeyDown(e);
////            if (!Enabled) return;

////            float step = e.Shift ? 96f : 24f;
////            switch (e.KeyCode)
////            {
////                case Keys.Left: PanBy(-step, 0f); e.Handled = true; break;
////                case Keys.Right: PanBy(step, 0f); e.Handled = true; break;
////                case Keys.Up: PanBy(0f, -step); e.Handled = true; break;
////                case Keys.Down: PanBy(0f, step); e.Handled = true; break;
////                case Keys.Add:
////                case Keys.Oemplus: SetZoom(_zoom * 1.25f, ViewportCenter()); e.Handled = true; break;
////                case Keys.Subtract:
////                case Keys.OemMinus: SetZoom(_zoom / 1.25f, ViewportCenter()); e.Handled = true; break;
////                case Keys.D0:
////                case Keys.NumPad0: SetZoom(1f, ViewportCenter()); e.Handled = true; break;
////                case Keys.Home:
////                case Keys.F: AutoFit(); e.Handled = true; break;
////            }
////        }

////        private PointF ViewportCenter() { return new PointF(Width / 2f, Height / 2f); }

////        // --------------------------------------------------------------------
////        //  DRAG & DROP
////        // --------------------------------------------------------------------
////        protected override void OnDragEnter(DragEventArgs e)
////        {
////            base.OnDragEnter(e);
////            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
////        }

////        protected override void OnDragDrop(DragEventArgs e)
////        {
////            base.OnDragDrop(e);
////            if (!Enabled) return;

////            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
////            if (files != null && files.Length > 0) LoadImageFile(files[0]);
////        }

////        private void LoadImageFile(string path)
////        {
////            try
////            {
////                using (System.Drawing.Image src = System.Drawing.Image.FromFile(path))
////                {
////                    Image = new Bitmap(src);
////                }
////            }
////            catch (Exception) { /* unsupported / corrupt file — keep current state */ }
////        }

////        // --------------------------------------------------------------------
////        //  LIFECYCLE
////        // --------------------------------------------------------------------
////        protected override void OnHandleCreated(EventArgs e)
////        {
////            base.OnHandleCreated(e);
////            try
////            {
////                using (Graphics probe = CreateGraphics())
////                {
////                    _dpiScale = Math.Max(1f, probe.DpiX / 96f);
////                }
////            }
////            catch { _dpiScale = 1f; }
////            Invalidate();
////        }

////        protected override void OnResize(EventArgs e)
////        {
////            base.OnResize(e);
////            if (_image != null && _needsAutoFit) AutoFit();
////            Invalidate();
////        }

////        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
////        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
////        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

////        protected override void Dispose(bool disposing)
////        {
////            if (disposing)
////            {
////                foreach (Font font in _fontCache.Values) font.Dispose();
////                _fontCache.Clear();
////                if (_image != null) { _image.Dispose(); _image = null; }
////            }
////            base.Dispose(disposing);
////        }
////    }
////}



















////=======VER 1.2 ================



//// ============================================================================
////  PanZoomViewer.cs — Reference-quality pan/zoom image surface (v3)
////  ---------------------------------------------------------------------------
////  LOADING API   : LoadFromFile / LoadFromFileAsync / LoadFromStream /
////                  LoadFromBytes / LoadFromImage / PostImage (thread-safe) /
////                  DetachImage / Clear / Image property / drag & drop
////  PERF ENGINE   : visible-source-cropped DrawImage · cached PArgb chrome
////                  layer · bilinear during gesture + 140 ms HQ settle ·
////                  batched vector paths · cached HUD text measurement
////  DESIGN SYSTEM : 5 styles (ViewerStyle) × 2 themes (ThemeMode)
////  SAFETY        : zero-flicker buffered pipeline · strict GDI+ disposal ·
////                  dead-bitmap immunity · design-time diagnostic panel
////  Target: .NET Framework 4.7+ / .NET 6+ WinForms, C# 7.3+. No dependencies.
//// ============================================================================
////using System;
////using System.Collections.Generic;
////using System.ComponentModel;
////using System.Diagnostics;
////using System.Drawing;
////using System.Drawing.Drawing2D;
////using System.Drawing.Imaging;
////using System.Drawing.Text;
////using System.IO;
////using System.Threading.Tasks;
////using System.Windows.Forms;
////using Timer = System.Windows.Forms.Timer;

////namespace CsplCameraOcr.UserControls
////{
////    // ========================================================================
////    //  PUBLIC ENUMS
////    // ========================================================================

////    public enum ThemeMode { Light, Dark }

////    public enum ViewerStyle
////    {
////        DashboardPremium = 1,
////        FluentGlass = 2,
////        MaterialFlat = 3,
////        SoftNeumorphic = 4,
////        Cyberpunk = 5
////    }

////    // ========================================================================
////    //  PALETTE SNAPSHOT
////    // ========================================================================

////    public sealed class ViewerPalette
////    {
////        public Color Canvas { get; internal set; }
////        public Color BackdropGlow { get; internal set; }
////        public Color ViewportFace { get; internal set; }
////        public Color ViewportWell { get; internal set; }
////        public Color Border { get; internal set; }
////        public Color BorderHighlight { get; internal set; }
////        public Color TextPrimary { get; internal set; }
////        public Color TextSecondary { get; internal set; }
////        public Color TextDim { get; internal set; }
////        public Color Accent { get; internal set; }
////        public Color AccentAlt { get; internal set; }
////        public Color ZoneGood { get; internal set; }
////        public Color ZoneWarn { get; internal set; }
////        public Color ZoneHot { get; internal set; }
////        public Color Grid { get; internal set; }
////        public Color Tick { get; internal set; }
////        public Color HudBack { get; internal set; }
////        public Color HudBorder { get; internal set; }
////        public Color HudFore { get; internal set; }
////        public Color HudForeDim { get; internal set; }
////        public Color Shadow { get; internal set; }
////        public Color ShadowLight { get; internal set; }
////        public Color ShadowDark { get; internal set; }
////        public Color HoverOverlay { get; internal set; }
////        public Color FocusRing { get; internal set; }
////    }

////    public sealed class OverlayPaintEventArgs : EventArgs
////    {
////        internal OverlayPaintEventArgs(Graphics graphics, RectangleF imageBounds,
////                                       float zoom, PointF pan, ViewerPalette palette)
////        {
////            Graphics = graphics;
////            ImageBounds = imageBounds;
////            Zoom = zoom;
////            Pan = pan;
////            Palette = palette;
////        }

////        public Graphics Graphics { get; private set; }
////        public RectangleF ImageBounds { get; private set; }
////        public float Zoom { get; private set; }
////        public PointF Pan { get; private set; }
////        public ViewerPalette Palette { get; private set; }
////    }

////    // ========================================================================
////    //  CONTROL
////    // ========================================================================

////    [ToolboxItem(true)]
////    [Description("Reference-quality pan/zoom image surface with five design styles and a performance engine.")]
////    public partial class PanZoomViewer : Control
////    {
////        // ====================================================================
////        //  CORE DATA
////        // ====================================================================
////        private Bitmap _image;
////        private float _zoom = 1f;
////        private PointF _pan = PointF.Empty;
////        private bool _needsAutoFit = true;

////        private bool _hover;
////        private bool _pressed;
////        private bool _panning;
////        private Point _lastMouse;
////        private Point _hoverPos;
////        private bool _imageLostPending;

////        private ThemeMode _themeMode = ThemeMode.Dark;
////        private ViewerStyle _controlStyle = ViewerStyle.DashboardPremium;
////        private ViewerPalette _palette;

////        private bool _showHud = true;
////        private bool _showScanlines = true;
////        private bool _showCrosshair = true;
////        private InterpolationMode _interpolation = InterpolationMode.HighQualityBicubic;
////        private float _minZoom = 0.05f;
////        private float _maxZoom = 100f;
////        private float _dpiScale = 1f;

////        // ----------------------------------------------------------------
////        //  PERFORMANCE ENGINE
////        //  _chromeCache  : canvas + card layers baked into one premultiplied
////        //                 32bpp ARGB bitmap → one blit per frame instead of
////        //                 gradients/shadows/glow paths. Control-owned; created
////        //                 OUTSIDE OnPaint; disposed on invalidation & Dispose.
////        //  _interacting  : true during pan/wheel → bilinear sampling; a 140 ms
////        //                 settle timer triggers one final full-quality frame.
////        // ----------------------------------------------------------------
////        private Bitmap _chromeCache;
////        private bool _interacting;
////        private Timer _hqTimer;

////        // ----------------------------------------------------------------
////        //  HUD TEXT CACHES — strings regenerated only when values change;
////        //  measurement cached by reference so pan frames do zero
////        //  MeasureString / ToString work.
////        // ----------------------------------------------------------------
////        private string _hudZoomText = "100 %";
////        private string _hudSourceText = "—";
////        private string _hudProbeText = "";
////        private int _probeX = int.MinValue;
////        private int _probeY = int.MinValue;

////        private string _mCapSrc; private SizeF _szCapSrc;
////        private string _mValSrc; private SizeF _szValSrc;
////        private string _mCapZoom; private SizeF _szCapZoom;
////        private string _mValZoom; private SizeF _szValZoom;
////        private string _mCapProbe; private SizeF _szCapProbe;
////        private string _mValProbe; private SizeF _szValProbe;

////        // Font engine (cached for performance, disposed exactly once in Dispose)
////        private readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();
////        private static string _uiFamily;
////        private static string _monoFamily;
////        private static readonly string[] UiFontCandidates =
////            { "Segoe UI Variable Display", "Segoe UI Workspace", "Segoe UI", "Arial" };
////        private static readonly string[] MonoFontCandidates =
////            { "Cascadia Code", "Cascadia Mono", "JetBrains Mono", "Consolas", "Courier New" };

////        private struct ViewerLayout
////        {
////            public RectangleF View;
////            public RectangleF Content;
////            public float ViewRadius;
////            public float ContentRadius;
////            public float Dpi;
////        }

////        private enum ChipAnchor { TopLeft, BottomLeft, BottomRight }

////        // ====================================================================
////        //  CONSTRUCTION
////        // ====================================================================
////        public PanZoomViewer()
////        {
////            SetStyle(
////                ControlStyles.UserPaint |
////                ControlStyles.AllPaintingInWmPaint |
////                ControlStyles.DoubleBuffer |
////                ControlStyles.OptimizedDoubleBuffer |
////                ControlStyles.ResizeRedraw |
////                ControlStyles.Selectable, true);
////            UpdateStyles();

////            TabStop = true;
////            AllowDrop = true;
////            Cursor = Cursors.Cross;

////            _palette = BuildPalette(_themeMode, _controlStyle);
////        }

////        // ====================================================================
////        //  PROPERTIES
////        // ====================================================================
////        [Category("Appearance")]
////        [Description("Light or Dark surface theme.")]
////        [DefaultValue(ThemeMode.Dark)]
////        public ThemeMode ThemeMode
////        {
////            get { return _themeMode; }
////            set
////            {
////                if (_themeMode == value) return;
////                _themeMode = value;
////                _palette = BuildPalette(_themeMode, _controlStyle);
////                InvalidateChrome();
////            }
////        }

////        [Category("Appearance")]
////        [Description("Visual design system.")]
////        [DefaultValue(ViewerStyle.DashboardPremium)]
////        public ViewerStyle ControlStyle
////        {
////            get { return _controlStyle; }
////            set
////            {
////                if (_controlStyle == value) return;
////                _controlStyle = value;
////                _palette = BuildPalette(_themeMode, _controlStyle);
////                InvalidateChrome();
////            }
////        }

////        [Category("Appearance")]
////        [Description("Shows the minimal readout chips.")]
////        [DefaultValue(true)]
////        public bool ShowHud
////        {
////            get { return _showHud; }
////            set { if (_showHud == value) return; _showHud = value; Invalidate(); }
////        }

////        [Category("Appearance")]
////        [Description("Cyberpunk style: subtle CRT scanline film.")]
////        [DefaultValue(true)]
////        public bool ShowScanlines
////        {
////            get { return _showScanlines; }
////            set { if (_showScanlines == value) return; _showScanlines = value; Invalidate(); }
////        }

////        [Category("Appearance")]
////        [Description("Precision crosshair follows the cursor (Dashboard & Cyberpunk).")]
////        [DefaultValue(true)]
////        public bool ShowCrosshair
////        {
////            get { return _showCrosshair; }
////            set { if (_showCrosshair == value) return; _showCrosshair = value; Invalidate(); }
////        }

////        [Category("Behavior")]
////        [Description("Sampling quality of the settled image. NearestNeighbor for pixel-accurate OCR inspection.")]
////        [DefaultValue(InterpolationMode.HighQualityBicubic)]
////        public InterpolationMode Interpolation
////        {
////            get { return _interpolation; }
////            set { _interpolation = value; Invalidate(); }
////        }

////        [Category("Behavior")]
////        [Description("Uses fast bilinear sampling during active pan/zoom, settling to full quality on release.")]
////        [DefaultValue(true)]
////        public bool SmoothInteraction { get; set; } = true;

////        [Category("Behavior")]
////        [Description("Lower zoom clamp.")]
////        [DefaultValue(0.05f)]
////        public float MinZoom
////        {
////            get { return _minZoom; }
////            set { _minZoom = Math.Max(0.01f, value); Invalidate(); }
////        }

////        [Category("Behavior")]
////        [Description("Upper zoom clamp.")]
////        [DefaultValue(100f)]
////        public float MaxZoom
////        {
////            get { return _maxZoom; }
////            set { _maxZoom = Math.Max(_minZoom * 2f, value); Invalidate(); }
////        }

////        [Browsable(false)]
////        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
////        [Description("Displayed bitmap. Ownership transfers to the control; the previous bitmap is disposed on replacement.")]
////        public Bitmap Image
////        {
////            get { return _image; }
////            set
////            {
////                if (ReferenceEquals(_image, value)) return;
////                Bitmap old = _image;
////                _image = value;

////                if (_image != null && _needsAutoFit)
////                {
////                    try { AutoFit(); }
////                    catch { /* a bitmap that dies between decode and fit */ }
////                }

////                UpdateSourceText();
////                if (old != null) old.Dispose();      // instant disposal — zero GDI leak
////                Invalidate();
////            }
////        }

////        [Browsable(false)]
////        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
////        public float Zoom
////        {
////            get { return _zoom; }
////            set { SetZoom(value, new PointF(Width / 2f, Height / 2f)); }
////        }

////        [Browsable(false)]
////        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
////        public PointF PanOffset
////        {
////            get { return _pan; }
////            set { _pan = value; Invalidate(); }
////        }

////        // ====================================================================
////        //  EVENTS
////        // ====================================================================

////        /// <summary>Legacy hook — Graphics in control-space (same signature as the previous control).</summary>
////        public event EventHandler<Graphics> OnCustomPaint;

////        /// <summary>Modern overlay hook — Graphics, image bounds, zoom, pan, active palette.</summary>
////        public event EventHandler<OverlayPaintEventArgs> OverlayPaint;

////        /// <summary>Raised when the assigned bitmap was found dead at draw time.</summary>
////        public event EventHandler ImageLost;

////        // ====================================================================
////        //  LOADING API — multiple import paths, all ownership-safe
////        //  Every loader produces an independent, self-owned Bitmap copy, so
////        //  files unlock, streams close, and callers keep their sources alive.
////        // ====================================================================

////        /// <summary>Loads from a file path. The file is NOT kept locked after this returns.</summary>
////        public void LoadFromFile(string path)
////        {
////            Image = DecodeFile(path);
////        }

////        /// <summary>Decodes off the UI thread; the decoded frame is applied on the UI thread. Call from the UI thread.</summary>
////        public async Task LoadFromFileAsync(string path)
////        {
////            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path must not be empty.", "path");

////            Bitmap decoded = await Task.Run(() => DecodeFile(path));

////            if (IsDisposed || Disposing) { decoded.Dispose(); return; }
////            Image = decoded;
////        }

////        /// <summary>Loads from any stream (file, memory, network). The stream may close immediately after this returns.</summary>
////        public void LoadFromStream(Stream stream)
////        {
////            if (stream == null) throw new ArgumentNullException("stream");

////            // Image.FromStream keeps the stream alive internally — always copy
////            // out so the caller's stream can close the moment we return.
////            using (System.Drawing.Image decoded = System.Drawing.Image.FromStream(stream, false, true))
////            {
////                Image = new Bitmap(decoded);
////            }
////        }

////        /// <summary>Loads from raw encoded bytes (BMP / PNG / JPEG / GIF / TIFF).</summary>
////        public void LoadFromBytes(byte[] data)
////        {
////            if (data == null) throw new ArgumentNullException("data");
////            using (MemoryStream ms = new MemoryStream(data, false))
////            {
////                LoadFromStream(ms);
////            }
////        }

////        /// <summary>Loads from any System.Drawing.Image. The source stays caller-owned; the viewer keeps an independent copy.</summary>
////        public void LoadFromImage(System.Drawing.Image image)
////        {
////            if (image == null) throw new ArgumentNullException("image");
////            Image = new Bitmap(image);
////        }

////        /// <summary>Thread-safe assignment — safe to call directly from a camera/capture thread. The frame is marshalled to the UI thread and ownership transfers.</summary>
////        public void PostImage(Bitmap frame)
////        {
////            if (frame == null) return;
////            if (IsDisposed) { frame.Dispose(); return; }

////            if (IsHandleCreated && InvokeRequired)
////            {
////                BeginInvoke((Action)delegate
////                {
////                    if (IsDisposed) { frame.Dispose(); return; }
////                    Image = frame;
////                });
////            }
////            else
////            {
////                Image = frame;
////            }
////        }

////        /// <summary>Removes the current bitmap WITHOUT disposing it — ownership returns to the caller.</summary>
////        public Bitmap DetachImage()
////        {
////            Bitmap bmp = _image;
////            _image = null;
////            _needsAutoFit = true;
////            _hudSourceText = "—";
////            _hudProbeText = "";
////            _probeX = int.MinValue;
////            _probeY = int.MinValue;
////            Invalidate();
////            return bmp;
////        }

////        /// <summary>Clears the viewer back to the empty state.</summary>
////        public void Clear()
////        {
////            Bitmap old = _image;
////            _image = null;
////            _needsAutoFit = true;
////            _hudSourceText = "—";
////            _hudProbeText = "";
////            _probeX = int.MinValue;
////            _probeY = int.MinValue;
////            if (old != null) old.Dispose();
////            Invalidate();
////        }

////        private static Bitmap DecodeFile(string path)
////        {
////            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
////            using (System.Drawing.Image tmp = System.Drawing.Image.FromStream(fs, false, true))
////            {
////                return new Bitmap(tmp);   // copy — any file lock dies with this method
////            }
////        }

////        // ====================================================================
////        //  PUBLIC METHODS & COORDINATE TRANSFORMS
////        // ====================================================================

////        public void AutoFit()
////        {
////            if (_image == null || Width < 20 || Height < 20) return;

////            ViewerLayout lay = ComputeLayout();
////            RectangleF c = lay.Content;

////            float z = Math.Min(c.Width / _image.Width, c.Height / _image.Height);
////            _zoom = ClampF(z, MinZoom, MaxZoom);

////            float dw = _image.Width * _zoom;
////            float dh = _image.Height * _zoom;
////            _pan = new PointF(c.X + (c.Width - dw) / 2f, c.Y + (c.Height - dh) / 2f);

////            _needsAutoFit = false;
////            UpdateZoomText();
////            Invalidate();
////        }

////        public void SetZoom(float zoom, PointF anchor)
////        {
////            float z = ClampF(zoom, MinZoom, MaxZoom);
////            if (z == _zoom) return;

////            float k = z / _zoom;
////            _pan = new PointF(
////                anchor.X - (anchor.X - _pan.X) * k,
////                anchor.Y - (anchor.Y - _pan.Y) * k);
////            _zoom = z;
////            UpdateZoomText();
////            Invalidate();
////        }

////        public void PanBy(float dx, float dy)
////        {
////            _pan = new PointF(_pan.X + dx, _pan.Y + dy);
////            Invalidate();
////        }

////        public ViewerPalette GetPalette() { return _palette; }

////        public PointF ScreenToImageF(PointF screenPoint)
////        {
////            return new PointF((screenPoint.X - _pan.X) / _zoom,
////                              (screenPoint.Y - _pan.Y) / _zoom);
////        }

////        public Point ScreenToImage(Point screenPoint)
////        {
////            return new Point(
////                (int)Math.Round((screenPoint.X - _pan.X) / _zoom),
////                (int)Math.Round((screenPoint.Y - _pan.Y) / _zoom));
////        }

////        public RectangleF ImageToScreenF(RectangleF imageRect)
////        {
////            return new RectangleF(
////                imageRect.X * _zoom + _pan.X,
////                imageRect.Y * _zoom + _pan.Y,
////                imageRect.Width * _zoom,
////                imageRect.Height * _zoom);
////        }

////        public Rectangle ImageToScreen(Rectangle imageRect)
////        {
////            RectangleF f = ImageToScreenF(imageRect);
////            return new Rectangle(
////                (int)Math.Round(f.X), (int)Math.Round(f.Y),
////                (int)Math.Round(f.Width), (int)Math.Round(f.Height));
////        }

////        public RectangleF GetImageScreenBounds()
////        {
////            if (_image == null) return RectangleF.Empty;
////            return new RectangleF(_pan.X, _pan.Y, _image.Width * _zoom, _image.Height * _zoom);
////        }

////        // ====================================================================
////        //  RENDERING PIPELINE
////        // ====================================================================
////        protected override void OnPaintBackground(PaintEventArgs pevent)
////        {
////            // Intentionally empty — the buffered surface is composed in OnPaint only.
////        }

////        protected override void OnPaint(PaintEventArgs e)
////        {
////            if (IsDesignTime)
////            {
////                try { PaintCore(e.Graphics); }
////                catch (Exception ex) { PaintDesignFailure(e.Graphics, ex); }
////            }
////            else
////            {
////                PaintCore(e.Graphics);
////            }
////        }

////        private bool IsDesignTime
////        {
////            get { return Site != null && Site.DesignMode; }
////        }

////        private void PaintDesignFailure(Graphics g, Exception ex)
////        {
////            Debug.WriteLine("[PanZoomViewer design-time paint failure]\r\n" + ex);
////            try
////            {
////                g.SmoothingMode = SmoothingMode.None;
////                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

////                using (SolidBrush bg = new SolidBrush(Color.FromArgb(255, 34, 18, 28)))
////                    g.FillRectangle(bg, ClientRectangle);

////                using (Font f = new Font(FontFamily.GenericMonospace, 8.5f, FontStyle.Regular, GraphicsUnit.Point))
////                using (SolidBrush t = new SolidBrush(Color.FromArgb(255, 255, 105, 97)))
////                using (SolidBrush w = new SolidBrush(Color.White))
////                using (StringFormat fmt = new StringFormat())
////                {
////                    fmt.Trimming = StringTrimming.EllipsisCharacter;

////                    g.DrawString("PANZOOMVIEWER — DESIGN-TIME PAINT FAILURE", f, t, 12f, 12f, fmt);
////                    g.DrawString(ex.GetType().Name + ": " + ex.Message, f, w, 12f, 30f, fmt);

////                    string[] frames = ex.StackTrace.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
////                    float y = 48f;
////                    int shown = Math.Min(10, frames.Length);
////                    for (int i = 0; i < shown; i++)
////                    {
////                        g.DrawString(frames[i].Trim(), f, w, 12f, y, fmt);
////                        y += 15f;
////                        if (y > Height - 18f) break;
////                    }
////                }
////            }
////            catch { /* diagnostics must never throw */ }
////        }

////        private void PaintCore(Graphics g)
////        {
////            g.SmoothingMode = SmoothingMode.AntiAlias;
////            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
////            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
////            g.CompositingQuality = CompositingQuality.HighQuality;
////            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

////            if (Width < 6 || Height < 6)
////            {
////                using (SolidBrush b = new SolidBrush(_palette.Canvas)) g.FillRectangle(b, ClientRectangle);
////                return;
////            }

////            ViewerPalette pal = _palette;
////            ViewerLayout lay = ComputeLayout();

////            // 1 — static chrome: one blit from the premultiplied-ARGB cache.
////            EnsureChromeCache(lay);
////            if (_chromeCache != null) g.DrawImageUnscaled(_chromeCache, 0, 0);
////            else
////            {
////                DrawCanvasLayer(g, pal, lay);
////                DrawCardLayer(g, pal, lay);
////            }

////            DrawZoomStrip(g, pal, lay);      // 2 — dynamic zone strip (tracks zoom)
////            DrawContentLayer(g, pal, lay);   // 3 — cropped image / empty state

////            // 4 — host overlays (ROIs, OCR boxes).
////            EventHandler<Graphics> legacy = OnCustomPaint;
////            if (legacy != null) legacy(this, g);

////            EventHandler<OverlayPaintEventArgs> overlay = OverlayPaint;
////            if (overlay != null)
////            {
////                OverlayPaintEventArgs args = new OverlayPaintEventArgs(
////                    g, GetImageScreenBounds(), _zoom, _pan, pal);
////                overlay(this, args);
////            }

////            DrawHudLayer(g, pal, lay);       // 5 — minimal modern readouts
////            DrawStateLayer(g, pal, lay);     // 6 — crosshair, focus ring, disabled veil

////            if (_imageLostPending)
////            {
////                _imageLostPending = false;
////                EventHandler lost = ImageLost;
////                if (lost != null) lost(this, EventArgs.Empty);
////            }
////        }

////        // ----------------------------------------------------------------
////        //  PERFORMANCE ENGINE — chrome cache & interaction quality
////        // ----------------------------------------------------------------
////        private void InvalidateChrome()
////        {
////            if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }
////            Invalidate();
////        }

////        private void EnsureChromeCache(ViewerLayout lay)
////        {
////            if (_chromeCache != null &&
////                _chromeCache.Width == Width &&
////                _chromeCache.Height == Height) return;

////            if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }

////            // Format32bppPArgb = PREMULTIPLIED alpha — the fastest format GDI+
////            // can composite; the blit in PaintCore is essentially a memcpy.
////            Bitmap bmp = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format32bppPArgb);
////            using (Graphics cg = Graphics.FromImage(bmp))
////            {
////                cg.SmoothingMode = SmoothingMode.AntiAlias;
////                cg.InterpolationMode = InterpolationMode.HighQualityBicubic;
////                cg.PixelOffsetMode = PixelOffsetMode.HighQuality;
////                cg.CompositingQuality = CompositingQuality.HighQuality;

////                DrawCanvasLayer(cg, _palette, lay);
////                DrawCardLayer(cg, _palette, lay);
////            }
////            _chromeCache = bmp;
////        }

////        private InterpolationMode EffectiveInterpolation
////        {
////            get
////            {
////                return (SmoothInteraction && _interacting &&
////                        _interpolation == InterpolationMode.HighQualityBicubic)
////                    ? InterpolationMode.Bilinear          // ~5–10x cheaper mid-gesture
////                    : _interpolation;
////            }
////        }

////        private void BeginInteraction()
////        {
////            _interacting = true;
////            if (_hqTimer == null)
////            {
////                _hqTimer = new Timer { Interval = 140 };
////                _hqTimer.Tick += (s, e) =>
////                {
////                    _hqTimer.Stop();
////                    if (IsDisposed) return;
////                    if (_interacting) { _interacting = false; Invalidate(); }
////                };
////            }
////            _hqTimer.Stop();
////            _hqTimer.Start();
////        }

////        private void EndInteraction()
////        {
////            if (_hqTimer != null) _hqTimer.Stop();
////            if (_interacting)
////            {
////                _interacting = false;
////                Invalidate();     // immediate full-quality settle frame
////            }
////        }

////        // ----------------------------------------------------------------
////        //  LAYER 1 — CANVAS (baked into the chrome cache)
////        // ----------------------------------------------------------------
////        private void DrawCanvasLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF client = new RectangleF(0f, 0f, Width, Height);
////            using (SolidBrush b = new SolidBrush(pal.Canvas)) g.FillRectangle(b, client);

////            if (pal.BackdropGlow.A > 0)
////            {
////                using (LinearGradientBrush lg = new LinearGradientBrush(
////                    client, pal.BackdropGlow, Color.FromArgb(0, pal.BackdropGlow), 118f))
////                {
////                    g.FillRectangle(lg, client);
////                }
////            }

////            if (_controlStyle == ViewerStyle.MaterialFlat)
////            {
////                using (SolidBrush accent = new SolidBrush(pal.Accent))
////                    g.FillRectangle(accent, 0f, 0f, Width, 4f * lay.Dpi);
////            }
////        }

////        // ----------------------------------------------------------------
////        //  LAYER 2 — VIEWPORT CARD (baked into the chrome cache)
////        // ----------------------------------------------------------------
////        private void DrawCardLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            switch (_controlStyle)
////            {
////                case ViewerStyle.FluentGlass: DrawGlassCard(g, pal, lay); break;
////                case ViewerStyle.MaterialFlat: DrawMaterialCard(g, pal, lay); break;
////                case ViewerStyle.SoftNeumorphic: DrawNeumorphicCard(g, pal, lay); break;
////                case ViewerStyle.Cyberpunk: DrawCyberCard(g, pal, lay); break;
////                default: DrawDashboardCard(g, pal, lay); break;
////            }
////        }

////        // STYLE 1 — DASHBOARD PREMIUM
////        private void DrawDashboardCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF v = lay.View;
////            float r = lay.ViewRadius;

////            DrawSoftShadow(g, v, r, pal.Shadow, 7f * lay.Dpi, 4);

////            Color borderColor = (_hover && Enabled) ? Lerp(pal.Border, pal.Accent, 0.45f) : pal.Border;
////            using (GraphicsPath face = BuildRoundedPath(v, r))
////            {
////                using (SolidBrush fill = new SolidBrush(pal.ViewportFace)) g.FillPath(fill, face);
////                using (Pen border = new Pen(borderColor, 1f)) g.DrawPath(border, face);
////            }

////            g.SetClip(new RectangleF(v.X, v.Y + v.Height * 0.55f, v.Width, v.Height * 0.45f));
////            using (GraphicsPath ip = BuildRoundedPath(Deflate(v, 1.4f), Math.Max(1f, r - 1.4f)))
////            using (Pen hi = new Pen(Color.FromArgb(80, pal.BorderHighlight), 1f))
////            {
////                g.DrawPath(hi, ip);
////            }
////            g.ResetClip();
////        }

////        // Dynamic (NOT cached) — the zone color tracks the zoom value.
////        private void DrawZoomStrip(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            if (_controlStyle != ViewerStyle.DashboardPremium) return;

////            RectangleF v = lay.View;
////            float r = lay.ViewRadius;
////            float stripW = v.Width - 2f * r;
////            if (stripW <= 4f) return;

////            RectangleF strip = new RectangleF(v.X + r, v.Y + 1.5f, stripW, 3f);
////            using (GraphicsPath sp = BuildRoundedPath(strip, 1.5f))
////            using (SolidBrush sb = new SolidBrush(Color.FromArgb(210, ZoomZoneColor(pal))))
////            {
////                g.FillPath(sb, sp);
////            }
////        }

////        // STYLE 2 — FLUENT GLASS
////        private void DrawGlassCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF v = lay.View;
////            float r = lay.ViewRadius;

////            DrawSoftShadow(g, v, r, pal.Shadow, 5f * lay.Dpi, 3);

////            using (GraphicsPath p1 = BuildRoundedPath(v, r))
////            using (SolidBrush l1 = new SolidBrush(pal.ViewportFace))
////            {
////                g.FillPath(l1, p1);
////            }
////            using (GraphicsPath p2 = BuildRoundedPath(Deflate(v, 4f * lay.Dpi), Math.Max(2f, r - 4f * lay.Dpi)))
////            using (SolidBrush l2 = new SolidBrush(pal.ViewportFace))
////            {
////                g.FillPath(l2, p2);
////            }

////            if (v.Width > 1f && v.Height > 1f)
////            {
////                using (LinearGradientBrush lb = new LinearGradientBrush(
////                    v, Color.FromArgb(150, pal.BorderHighlight),
////                    Color.FromArgb(28, pal.BorderHighlight), 90f))
////                using (Pen border = new Pen(lb, 1.2f))
////                using (GraphicsPath bp = BuildRoundedPath(Deflate(v, 0.6f), Math.Max(2f, r - 0.6f)))
////                {
////                    g.DrawPath(border, bp);
////                }
////            }

////            int glow = _pressed ? 3 : (_hover && Enabled ? 2 : 0);
////            for (int i = glow; i >= 1; i--)
////            {
////                using (GraphicsPath gp = BuildRoundedPath(Expand(v, 1.4f * i), r + 1.4f * i))
////                using (Pen pen = new Pen(Color.FromArgb(80 / i, pal.Accent), 1.4f))
////                {
////                    g.DrawPath(pen, gp);
////                }
////            }
////        }

////        // STYLE 3 — MATERIAL FLAT
////        private void DrawMaterialCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            using (GraphicsPath face = BuildRoundedPath(lay.View, lay.ViewRadius))
////            using (SolidBrush fill = new SolidBrush(pal.ViewportFace))
////            {
////                g.FillPath(fill, face);
////            }
////        }

////        // STYLE 4 — SOFT NEUMORPHIC
////        private void DrawNeumorphicCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF v = lay.View;
////            float r = lay.ViewRadius;
////            bool pressed = _pressed && Enabled;
////            float off = (pressed ? 2.0f : (_hover && Enabled ? 2.8f : 3.4f)) * lay.Dpi;

////            Color tlC = pressed ? pal.ShadowDark : pal.ShadowLight;
////            Color brC = pressed ? pal.ShadowLight : pal.ShadowDark;

////            for (int i = 2; i >= 1; i--)
////            {
////                float o = off * (i == 2 ? 1.5f : 0.7f);
////                int pct = i == 2 ? 55 : 115;

////                using (GraphicsPath pTL = BuildRoundedPath(OffsetRect(v, -o, -o), r))
////                using (SolidBrush bTL = new SolidBrush(AlphaScale(tlC, pct)))
////                {
////                    g.FillPath(bTL, pTL);
////                }
////                using (GraphicsPath pBR = BuildRoundedPath(OffsetRect(v, o, o), r))
////                using (SolidBrush bBR = new SolidBrush(AlphaScale(brC, pct)))
////                {
////                    g.FillPath(bBR, pBR);
////                }
////            }

////            using (GraphicsPath face = BuildRoundedPath(v, r))
////            {
////                using (SolidBrush fill = new SolidBrush(pal.ViewportFace)) g.FillPath(fill, face);
////                using (Pen hair = new Pen(Color.FromArgb(70, pal.BorderHighlight), 1f)) g.DrawPath(hair, face);
////            }

////            using (GraphicsPath well = BuildRoundedPath(lay.Content, lay.ContentRadius))
////            {
////                using (SolidBrush wb = new SolidBrush(pal.ViewportWell)) g.FillPath(wb, well);

////                Color topCol = pressed ? pal.ShadowLight : pal.ShadowDark;
////                Color botCol = pressed ? pal.ShadowDark : pal.ShadowLight;

////                g.SetClip(new RectangleF(lay.Content.X, lay.Content.Y,
////                                         lay.Content.Width, lay.Content.Height * 0.5f));
////                using (Pen tp = new Pen(topCol, 2.2f)) g.DrawPath(tp, well);
////                g.ResetClip();

////                g.SetClip(new RectangleF(lay.Content.X, lay.Content.Y + lay.Content.Height * 0.5f,
////                                         lay.Content.Width, lay.Content.Height * 0.5f));
////                using (Pen bt = new Pen(botCol, 2.2f)) g.DrawPath(bt, well);
////                g.ResetClip();
////            }
////        }

////        // STYLE 5 — CYBERPUNK / INDUSTRIAL
////        private void DrawCyberCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF v = lay.View;
////            float r = lay.ViewRadius;

////            using (GraphicsPath face = BuildRoundedPath(v, r))
////            using (SolidBrush fill = new SolidBrush(pal.ViewportFace))
////            {
////                g.FillPath(fill, face);
////            }

////            int glow = _pressed ? 3 : (_hover && Enabled ? 2 : 1);
////            for (int i = glow; i >= 1; i--)
////            {
////                using (GraphicsPath gp = BuildRoundedPath(Expand(v, 1.4f * i + 1f), r + 1.4f * i + 1f))
////                using (Pen pen = new Pen(Color.FromArgb(70 / i, pal.Accent), 1.6f))
////                {
////                    g.DrawPath(pen, gp);
////                }
////            }

////            using (GraphicsPath core = BuildRoundedPath(Deflate(v, 0.8f), Math.Max(2f, r - 0.8f)))
////            using (Pen neon = new Pen(pal.Accent, 1.7f))
////            {
////                g.DrawPath(neon, core);
////            }

////            DrawCornerBrackets(g, v, 16f * lay.Dpi, 2.6f, Color.FromArgb(220, pal.AccentAlt));
////        }

////        // ----------------------------------------------------------------
////        //  LAYER 3 — CONTENT
////        // ----------------------------------------------------------------
////        private void DrawContentLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF c = lay.Content;
////            if (c.Width < 2f || c.Height < 2f) return;

////            using (GraphicsPath clip = BuildRoundedPath(c, lay.ContentRadius))
////            {
////                g.SetClip(clip);

////                using (SolidBrush well = new SolidBrush(pal.ViewportWell)) g.FillPath(well, clip);

////                if (!TryDrawImage(g, pal, lay))
////                    DrawEmptyState(g, pal, lay);

////                if (_controlStyle == ViewerStyle.DashboardPremium)
////                    DrawEdgeTicks(g, pal, c, lay.Dpi);

////                if (_controlStyle == ViewerStyle.Cyberpunk && _showScanlines)
////                    DrawScanlines(g, pal, c);

////                if (_controlStyle == ViewerStyle.MaterialFlat && Enabled)
////                {
////                    int alpha = _pressed ? 14 : (_hover ? 7 : 0);
////                    if (alpha > 0)
////                    {
////                        using (SolidBrush ov = new SolidBrush(Color.FromArgb(alpha, pal.HoverOverlay)))
////                            g.FillPath(ov, clip);
////                    }
////                }

////                g.ResetClip();
////            }
////        }

////        private bool TryDrawImage(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            if (_image == null) return false;

////            g.InterpolationMode = EffectiveInterpolation;
////            g.PixelOffsetMode = PixelOffsetMode.Half;

////            bool drawn = false;
////            try
////            {
////                int iw = _image.Width;
////                int ih = _image.Height;

////                RectangleF screen = new RectangleF(_pan.X, _pan.Y, iw * _zoom, ih * _zoom);
////                RectangleF vis = RectangleF.Intersect(screen, lay.Content);

////                if (vis.Width > 0f && vis.Height > 0f)
////                {
////                    // VISIBLE-SOURCE CROP: sample only the on-screen portion of
////                    // the bitmap. At 8x zoom this is a ~64x smaller blit than
////                    // resampling the full frame — the single biggest perf win.
////                    RectangleF src = new RectangleF(
////                        (vis.X - _pan.X) / _zoom,
////                        (vis.Y - _pan.Y) / _zoom,
////                        vis.Width / _zoom,
////                        vis.Height / _zoom);
////                    src = ClampToSource(src, iw, ih);

////                    g.DrawImage(_image, vis, src, GraphicsUnit.Pixel);

////                    using (Pen ip = new Pen(Color.FromArgb(70, pal.TextDim), 1f))
////                        g.DrawRectangle(ip, screen.X, screen.Y, screen.Width, screen.Height);

////                    if (_controlStyle == ViewerStyle.Cyberpunk)
////                        DrawCornerBrackets(g, screen, 10f, 2.2f, Color.FromArgb(200, pal.AccentAlt));

////                    drawn = true;
////                }
////                else if (screen.Width > 0f && screen.Height > 0f)
////                {
////                    drawn = true;   // image exists but is fully panned off-screen — no empty state
////                }
////            }
////            catch (ArgumentException)
////            {
////                _image = null;
////                _needsAutoFit = true;
////                _imageLostPending = true;
////                _hudSourceText = "—";
////            }
////            finally
////            {
////                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
////                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
////            }

////            return drawn;
////        }

////        private static RectangleF ClampToSource(RectangleF r, int iw, int ih)
////        {
////            float x = ClampF(r.X, 0f, iw);
////            float y = ClampF(r.Y, 0f, ih);
////            float right = ClampF(r.Right, x, iw);
////            float bottom = ClampF(r.Bottom, y, ih);
////            return RectangleF.FromLTRB(x, y, right, bottom);
////        }

////        private void DrawEmptyState(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            RectangleF c = lay.Content;
////            float s = lay.Dpi;

////            // Batched grid — one path, one stroke call.
////            using (GraphicsPath gridPath = new GraphicsPath())
////            using (Pen grid = new Pen(pal.Grid, 1f))
////            {
////                float step = 28f * s;
////                for (float x = c.X + step; x < c.Right; x += step)
////                {
////                    gridPath.AddLine(x, c.Y, x, c.Bottom);
////                    gridPath.StartFigure();
////                }
////                for (float y = c.Y + step; y < c.Bottom; y += step)
////                {
////                    gridPath.AddLine(c.X, y, c.Right, y);
////                    gridPath.StartFigure();
////                }
////                if (gridPath.PointCount > 0) g.DrawPath(grid, gridPath);
////            }

////            bool cyber = _controlStyle == ViewerStyle.Cyberpunk;
////            float cx = c.X + c.Width / 2f;
////            float cy = c.Y + c.Height / 2f;

////            RectangleF body = new RectangleF(cx - 43f * s, cy - 24f * s, 86f * s, 60f * s);
////            RectangleF bump = new RectangleF(cx - 15f * s, cy - 35f * s, 30f * s, 13f * s);
////            float lensR = 13f * s;
////            float innerR = 5f * s;
////            float lensCy = cy + 6f * s;
////            Color stroke = Color.FromArgb(165, pal.TextSecondary);

////            using (GraphicsPath bodyPath = BuildRoundedPath(body, 12f * s))
////            using (GraphicsPath bumpPath = BuildRoundedPath(bump, 5f * s))
////            using (Pen pen = new Pen(stroke, 2.6f))
////            {
////                g.DrawPath(pen, bumpPath);
////                g.DrawPath(pen, bodyPath);
////                using (SolidBrush fill = new SolidBrush(pal.ViewportWell))
////                {
////                    g.FillPath(fill, bumpPath);
////                    g.FillPath(fill, bodyPath);
////                }
////                g.DrawEllipse(pen, cx - lensR, lensCy - lensR, lensR * 2f, lensR * 2f);
////                using (SolidBrush fill2 = new SolidBrush(pal.ViewportWell))
////                    g.FillEllipse(fill2, cx - lensR, lensCy - lensR, lensR * 2f, lensR * 2f);
////                using (Pen inner = new Pen(Color.FromArgb(120, pal.TextSecondary), 2f))
////                    g.DrawEllipse(inner, cx - innerR, lensCy - innerR, innerR * 2f, innerR * 2f);
////            }

////            string title = cyber ? "NO SIGNAL" : "NO IMAGE LOADED";
////            string hint = cyber ? "AWAITING INPUT · DROP FILE TO SCAN"
////                                : "Drag & drop an image file, or assign the Image property";
////            float ty = body.Bottom + 22f * s;

////            // Cache-owned fonts — NEVER wrapped in using.
////            Font tf = GetFont(cyber, 10.5f, FontStyle.Bold);
////            Font sf = GetFont(false, 8.75f, FontStyle.Regular);

////            using (StringFormat fmt = new StringFormat())
////            {
////                fmt.Alignment = StringAlignment.Center;
////                fmt.LineAlignment = StringAlignment.Near;
////                fmt.FormatFlags |= StringFormatFlags.NoWrap;

////                using (SolidBrush tb = new SolidBrush(pal.TextSecondary))
////                    g.DrawString(title, tf, tb, cx, ty, fmt);
////                using (SolidBrush sb = new SolidBrush(pal.TextDim))
////                    g.DrawString(hint, sf, sb, cx, ty + 19f * s, fmt);
////            }
////        }

////        // ----------------------------------------------------------------
////        //  LAYER 5 — HUD READOUTS (cached text + cached measurement)
////        // ----------------------------------------------------------------
////        private void DrawHudLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            if (!_showHud || !Enabled) return;
////            RectangleF c = lay.Content;
////            if (c.Width < 160f || c.Height < 96f) return;

////            Font capFont = GetFont(false, 6.75f, FontStyle.Bold);
////            Font valFont = GetFont(true, 10f, FontStyle.Bold);

////            using (StringFormat fmt = new StringFormat(
////                StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces))
////            {
////                SizeF capS = MeasureCached(g, "SOURCE", capFont, fmt, ref _mCapSrc, ref _szCapSrc);
////                SizeF valS = MeasureCached(g, _hudSourceText, valFont, fmt, ref _mValSrc, ref _szValSrc);
////                DrawHudChip(g, pal, lay, ChipAnchor.TopLeft, "SOURCE", _hudSourceText,
////                            capFont, valFont, capS, valS,
////                            _image != null ? pal.Accent : pal.TextDim, false, pal.TextDim);

////                bool flat = _controlStyle == ViewerStyle.MaterialFlat;
////                Color zone = ZoomZoneColor(pal);

////                SizeF capZ = MeasureCached(g, "ZOOM", capFont, fmt, ref _mCapZoom, ref _szCapZoom);
////                SizeF valZ = MeasureCached(g, _hudZoomText, valFont, fmt, ref _mValZoom, ref _szValZoom);
////                DrawHudChip(g, pal, lay, ChipAnchor.BottomRight, "ZOOM", _hudZoomText,
////                            capFont, valFont, capZ, valZ,
////                            flat ? (Color?)null : zone, true, flat ? pal.HudFore : zone);

////                if (_hover && _image != null && _hudProbeText.Length > 0)
////                {
////                    SizeF capP = MeasureCached(g, "PROBE", capFont, fmt, ref _mCapProbe, ref _szCapProbe);
////                    SizeF valP = MeasureCached(g, _hudProbeText, valFont, fmt, ref _mValProbe, ref _szValProbe);
////                    DrawHudChip(g, pal, lay, ChipAnchor.BottomLeft, "PROBE", _hudProbeText,
////                                capFont, valFont, capP, valP,
////                                null, false, pal.TextDim);
////                }
////            }
////        }

////        private SizeF MeasureCached(Graphics g, string text, Font font, StringFormat fmt,
////                                    ref string key, ref SizeF size)
////        {
////            if (!ReferenceEquals(key, text))
////            {
////                size = g.MeasureString(text, font, int.MaxValue, fmt);
////                key = text;
////            }
////            return size;
////        }

////        private void DrawHudChip(Graphics g, ViewerPalette pal, ViewerLayout lay, ChipAnchor anchor,
////                                 string caption, string value, Font capFont, Font valFont,
////                                 SizeF capS, SizeF valS,
////                                 Color? dot, bool showBar, Color barColor)
////        {
////            float dpi = lay.Dpi;
////            RectangleF c = lay.Content;
////            float padX = 9f * dpi;
////            float padY = 6f * dpi;

////            float dotW = dot.HasValue ? 14f * dpi : 0f;
////            float w = Math.Max(capS.Width, valS.Width) + dotW + padX * 2f;
////            float h = padY + capS.Height + 2f * dpi + valS.Height + (showBar ? 6f * dpi : 0f) + padY;

////            if (w > c.Width - 12f || h > c.Height - 12f) return;

////            float inset = 10f * dpi;
////            PointF loc;
////            switch (anchor)
////            {
////                case ChipAnchor.BottomLeft: loc = new PointF(c.X + inset, c.Bottom - inset - h); break;
////                case ChipAnchor.BottomRight: loc = new PointF(c.Right - inset - w, c.Bottom - inset - h); break;
////                default: loc = new PointF(c.X + inset, c.Y + inset); break;
////            }
////            RectangleF chip = new RectangleF(loc.X, loc.Y, w, h);

////            using (GraphicsPath path = BuildRoundedPath(chip, 7f * dpi))
////            {
////                using (SolidBrush bg = new SolidBrush(pal.HudBack)) g.FillPath(bg, path);
////                if (pal.HudBorder.A > 0)
////                {
////                    using (Pen bp = new Pen(pal.HudBorder, 1f)) g.DrawPath(bp, path);
////                }

////                if (_controlStyle == ViewerStyle.SoftNeumorphic)
////                {
////                    g.SetClip(new RectangleF(chip.X, chip.Y, chip.Width, chip.Height * 0.5f));
////                    using (Pen tp = new Pen(pal.ShadowDark, 1f)) g.DrawPath(tp, path);
////                    g.ResetClip();
////                    g.SetClip(new RectangleF(chip.X, chip.Y + chip.Height * 0.5f,
////                                             chip.Width, chip.Height * 0.5f));
////                    using (Pen bt = new Pen(pal.ShadowLight, 1f)) g.DrawPath(bt, path);
////                    g.ResetClip();
////                }
////            }

////            using (StringFormat fmt = new StringFormat(StringFormatFlags.NoWrap))
////            {
////                float tx = chip.X + padX;
////                using (SolidBrush capBrush = new SolidBrush(pal.HudForeDim))
////                    g.DrawString(caption, capFont, capBrush, tx, chip.Y + padY, fmt);

////                float vy = chip.Y + padY + capS.Height + 2f * dpi;
////                using (SolidBrush valBrush = new SolidBrush(pal.HudFore))
////                    g.DrawString(value, valFont, valBrush, tx, vy, fmt);

////                if (dot.HasValue)
////                {
////                    float d = 6.8f * dpi;
////                    using (SolidBrush db = new SolidBrush(dot.Value))
////                        g.FillEllipse(db, chip.Right - padX - d, vy + valS.Height / 2f - d / 2f, d, d);
////                }

////                if (showBar)
////                {
////                    float by = chip.Bottom - padY - 2.2f * dpi;
////                    float trackW = w - padX * 2f;

////                    using (GraphicsPath track = BuildRoundedPath(
////                        new RectangleF(tx, by, trackW, 3f * dpi), 1.5f * dpi))
////                    using (SolidBrush tb = new SolidBrush(Color.FromArgb(70, pal.HudFore)))
////                        g.FillPath(tb, track);

////                    float t = ZoomBarT();
////                    if (t > 0.01f)
////                    {
////                        using (GraphicsPath fill = BuildRoundedPath(
////                            new RectangleF(tx, by, trackW * t, 3f * dpi), 1.5f * dpi))
////                        using (SolidBrush fb = new SolidBrush(barColor))
////                            g.FillPath(fb, fill);
////                    }
////                }
////            }
////        }

////        // ----------------------------------------------------------------
////        //  LAYER 6 — STATE CHROME
////        // ----------------------------------------------------------------
////        private void DrawStateLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
////        {
////            if (_showCrosshair && _hover && !_panning && Enabled &&
////                (_controlStyle == ViewerStyle.Cyberpunk || _controlStyle == ViewerStyle.DashboardPremium))
////            {
////                using (GraphicsPath clip = BuildRoundedPath(lay.Content, lay.ContentRadius))
////                {
////                    g.SetClip(clip);
////                    float px = _hoverPos.X;
////                    float py = _hoverPos.Y;
////                    float gap = 11f * lay.Dpi;

////                    using (Pen pen = new Pen(Color.FromArgb(115, pal.Accent), 1f))
////                    {
////                        g.DrawLine(pen, lay.Content.X, py, px - gap, py);
////                        g.DrawLine(pen, px + gap, py, lay.Content.Right, py);
////                        g.DrawLine(pen, px, lay.Content.Y, px, py - gap);
////                        g.DrawLine(pen, px, py + gap, px, lay.Content.Bottom);
////                    }
////                    using (Pen cp = new Pen(Color.FromArgb(190, pal.Accent), 1.2f))
////                        g.DrawEllipse(cp, px - 3.5f, py - 3.5f, 7f, 7f);

////                    g.ResetClip();
////                }
////            }

////            if (Focused && Enabled)
////            {
////                using (GraphicsPath fp = BuildRoundedPath(Expand(lay.View, 3f), lay.ViewRadius + 3f))
////                using (Pen pen = new Pen(Color.FromArgb(215, pal.FocusRing), 1.4f))
////                    g.DrawPath(pen, fp);
////            }

////            if (!Enabled)
////            {
////                using (SolidBrush veil = new SolidBrush(Color.FromArgb(110, pal.Canvas)))
////                    g.FillRectangle(veil, 0f, 0f, Width, Height);
////            }
////        }

////        // ----------------------------------------------------------------
////        //  BATCHED MICRO-DETAILS — one DrawPath call instead of hundreds
////        //  of DrawLine calls. StartFigure() keeps segments disconnected.
////        // ----------------------------------------------------------------
////        private static void DrawEdgeTicks(Graphics g, ViewerPalette pal, RectangleF c, float dpi)
////        {
////            SmoothingMode prev = g.SmoothingMode;
////            g.SmoothingMode = SmoothingMode.None;

////            using (GraphicsPath minorPath = new GraphicsPath())
////            using (GraphicsPath majorPath = new GraphicsPath())
////            using (Pen minor = new Pen(Color.FromArgb(110, pal.Tick), 1f))
////            using (Pen major = new Pen(Color.FromArgb(210, pal.Tick), 1f))
////            {
////                float step = 9f * dpi;

////                int i = 0;
////                for (float x = c.X + 3f; x < c.Right - 2f; x += step)
////                {
////                    GraphicsPath target = (i % 5 == 0) ? majorPath : minorPath;
////                    float len = (i % 5 == 0) ? 7f * dpi : 4f * dpi;
////                    target.AddLine(x, c.Y + 1f, x, c.Y + 1f + len);
////                    target.StartFigure();
////                    i++;
////                }

////                i = 0;
////                for (float y = c.Y + 3f; y < c.Bottom - 2f; y += step)
////                {
////                    GraphicsPath target = (i % 5 == 0) ? majorPath : minorPath;
////                    float len = (i % 5 == 0) ? 7f * dpi : 4f * dpi;
////                    target.AddLine(c.X + 1f, y, c.X + 1f + len, y);
////                    target.StartFigure();
////                    i++;
////                }

////                if (minorPath.PointCount > 0) g.DrawPath(minor, minorPath);
////                if (majorPath.PointCount > 0) g.DrawPath(major, majorPath);
////            }

////            g.SmoothingMode = prev;
////        }

////        private static void DrawScanlines(Graphics g, ViewerPalette pal, RectangleF c)
////        {
////            SmoothingMode prev = g.SmoothingMode;
////            g.SmoothingMode = SmoothingMode.None;

////            using (GraphicsPath path = new GraphicsPath())
////            using (Pen pen = new Pen(Color.FromArgb(9, pal.Accent), 1f))
////            {
////                for (float y = c.Y + 2f; y < c.Bottom; y += 4f)
////                {
////                    path.AddLine(c.X, y, c.Right, y);
////                    path.StartFigure();
////                }
////                if (path.PointCount > 0) g.DrawPath(pen, path);
////            }

////            g.SmoothingMode = prev;
////        }

////        private static void DrawCornerBrackets(Graphics g, RectangleF rect, float len, float width, Color color)
////        {
////            using (Pen pen = new Pen(color, width))
////            {
////                pen.StartCap = LineCap.Round;
////                pen.EndCap = LineCap.Round;

////                float l = rect.Left, t = rect.Top, r = rect.Right, b = rect.Bottom;

////                g.DrawLine(pen, l, t, l + len, t); g.DrawLine(pen, l, t, l, t + len);
////                g.DrawLine(pen, r, t, r - len, t); g.DrawLine(pen, r, t, r, t + len);
////                g.DrawLine(pen, l, b, l + len, b); g.DrawLine(pen, l, b, l, b - len);
////                g.DrawLine(pen, r, b, r - len, b); g.DrawLine(pen, r, b, r, b - len);
////            }
////        }

////        private static void DrawSoftShadow(Graphics g, RectangleF rect, float radius, Color shadow,
////                                           float depth, int steps)
////        {
////            int layerAlpha = Math.Max(4, shadow.A / steps);
////            for (int i = steps; i >= 1; i--)
////            {
////                float off = 1f + (depth * (i - 1) / steps);
////                using (GraphicsPath p = BuildRoundedPath(OffsetRect(rect, 0f, off), radius + i * 0.7f))
////                using (SolidBrush b = new SolidBrush(Color.FromArgb(layerAlpha, shadow)))
////                {
////                    g.FillPath(b, p);
////                }
////            }
////        }

////        // ----------------------------------------------------------------
////        //  LAYOUT ENGINE
////        // ----------------------------------------------------------------
////        private ViewerLayout ComputeLayout()
////        {
////            float dpi = Math.Max(1f, _dpiScale);

////            float padMul = _controlStyle == ViewerStyle.MaterialFlat ? 1.45f : 1f;
////            float basePad = Math.Min(Width, Height) * 0.035f;
////            float pad = Math.Max(11f * dpi, Math.Min(26f * dpi, basePad)) * padMul;

////            float maxPad = Math.Min(Math.Max(0f, (Width - 2f) / 2f),
////                                    Math.Max(0f, (Height - 2f) / 2f));
////            pad = Math.Min(pad, maxPad);

////            RectangleF view = RectangleF.FromLTRB(pad, pad,
////                Math.Max(pad + 2f, Width - pad),
////                Math.Max(pad + 2f, Height - pad));

////            float radius;
////            float inset;
////            switch (_controlStyle)
////            {
////                case ViewerStyle.FluentGlass: radius = 14f * dpi; inset = 1.5f * dpi; break;
////                case ViewerStyle.MaterialFlat: radius = 3f * dpi; inset = 0.75f * dpi; break;
////                case ViewerStyle.SoftNeumorphic: radius = 20f * dpi; inset = 11f * dpi; break;
////                case ViewerStyle.Cyberpunk: radius = 8f * dpi; inset = 1.25f * dpi; break;
////                default: radius = 10f * dpi; inset = 1.25f * dpi; break;
////            }

////            float maxInset = Math.Min(Math.Max(0f, (view.Width - 2f) / 2f),
////                                      Math.Max(0f, (view.Height - 2f) / 2f));
////            inset = Math.Min(inset, maxInset);

////            float cr = Math.Max(1f, radius - inset - 0.5f);
////            float minDim = Math.Min(view.Width, view.Height);
////            if (cr * 2f > minDim) cr = minDim / 2f;

////            return new ViewerLayout
////            {
////                View = view,
////                Content = Deflate(view, inset),
////                ViewRadius = radius,
////                ContentRadius = cr,
////                Dpi = dpi
////            };
////        }

////        private Color ZoomZoneColor(ViewerPalette pal)
////        {
////            if (_zoom < 2f) return pal.ZoneGood;
////            if (_zoom < 8f) return pal.ZoneWarn;
////            return pal.ZoneHot;
////        }

////        private float ZoomBarT()
////        {
////            double lo = Math.Log10((double)_minZoom);
////            double hi = Math.Log10((double)_maxZoom);
////            if (hi - lo < 0.0001) return 0f;

////            double t = (Math.Log10((double)_zoom) - lo) / (hi - lo);
////            if (t < 0.0) t = 0.0;
////            if (t > 1.0) t = 1.0;
////            return (float)t;
////        }

////        // ----------------------------------------------------------------
////        //  GEOMETRY & COLOR PRIMITIVES
////        // ----------------------------------------------------------------
////        private static GraphicsPath BuildRoundedPath(RectangleF rect, float radius)
////        {
////            GraphicsPath path = new GraphicsPath();

////            float w = rect.Width;
////            float h = rect.Height;
////            if (float.IsNaN(w) || float.IsNaN(h) || float.IsInfinity(w) || float.IsInfinity(h))
////                return path;
////            if (w < 0f) w = 0f;
////            if (h < 0f) h = 0f;
////            if (float.IsNaN(radius) || radius < 0f) radius = 0f;
////            if (w < 0.5f || h < 0.5f) return path;

////            rect = new RectangleF(rect.X, rect.Y, w, h);

////            float maxR = Math.Min(w, h) / 2f;
////            if (radius < 0.5f) { path.AddRectangle(rect); return path; }
////            if (radius > maxR) radius = maxR;

////            float d = radius * 2f;
////            path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
////            path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
////            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
////            path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
////            path.CloseFigure();
////            return path;
////        }

////        private static RectangleF Deflate(RectangleF r, float v)
////        { return new RectangleF(r.X + v, r.Y + v, r.Width - 2f * v, r.Height - 2f * v); }

////        private static RectangleF Expand(RectangleF r, float v)
////        { return new RectangleF(r.X - v, r.Y - v, r.Width + 2f * v, r.Height + 2f * v); }

////        private static RectangleF OffsetRect(RectangleF r, float dx, float dy)
////        { return new RectangleF(r.X + dx, r.Y + dy, r.Width, r.Height); }

////        private static Color Lerp(Color a, Color b, float t)
////        {
////            return Color.FromArgb(
////                a.R + (int)((b.R - a.R) * t),
////                a.G + (int)((b.G - a.G) * t),
////                a.B + (int)((b.B - a.B) * t));
////        }

////        private static Color AlphaScale(Color c, int percent)
////        {
////            int a = (int)(c.A * percent / 100.0);
////            if (a > 255) a = 255;
////            return Color.FromArgb(a, c);
////        }

////        private static float ClampF(float v, float lo, float hi)
////        {
////            if (float.IsNaN(v)) return lo;
////            return v < lo ? lo : (v > hi ? hi : v);
////        }

////        // ----------------------------------------------------------------
////        //  HUD TEXT UPDATES
////        // ----------------------------------------------------------------
////        private void UpdateZoomText()
////        {
////            _hudZoomText = (_zoom * 100f).ToString("0.#") + " %";
////        }

////        private void UpdateSourceText()
////        {
////            try
////            {
////                _hudSourceText = _image != null
////                    ? string.Format("{0} × {1}", _image.Width, _image.Height)
////                    : "—";
////            }
////            catch { _hudSourceText = "—"; }
////        }

////        private void UpdateProbeText()
////        {
////            if (_image == null) return;
////            try
////            {
////                PointF ip = ScreenToImageF(_hoverPos);
////                int px = (int)Math.Round(ip.X);
////                int py = (int)Math.Round(ip.Y);
////                if (px != _probeX || py != _probeY)
////                {
////                    _probeX = px;
////                    _probeY = py;
////                    _hudProbeText = string.Format("X {0}   Y {1}", px, py);
////                }
////            }
////            catch { }
////        }

////        // ----------------------------------------------------------------
////        //  PALETTE ENGINE — 5 styles × 2 themes
////        // ----------------------------------------------------------------
////        private static ViewerPalette BuildPalette(ThemeMode theme, ViewerStyle style)
////        {
////            bool dark = theme == ThemeMode.Dark;
////            switch (style)
////            {
////                case ViewerStyle.FluentGlass: return GlassPalette(dark);
////                case ViewerStyle.MaterialFlat: return MaterialPalette(dark);
////                case ViewerStyle.SoftNeumorphic: return NeumorphicPalette(dark);
////                case ViewerStyle.Cyberpunk: return CyberPalette();
////                default: return DashboardPalette(dark);
////            }
////        }

////        private static Color C(int rgb)
////        { return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF); }

////        private static ViewerPalette DashboardPalette(bool dark)
////        {
////            if (dark)
////            {
////                return new ViewerPalette
////                {
////                    Canvas = C(0x0F1218),
////                    BackdropGlow = Color.FromArgb(0, Color.White),
////                    ViewportFace = C(0x151A22),
////                    ViewportWell = C(0x11151C),
////                    Border = C(0x28303E),
////                    BorderHighlight = Color.White,
////                    TextPrimary = C(0xE8EDF5),
////                    TextSecondary = C(0x97A3B5),
////                    TextDim = C(0x5E6A80),
////                    Accent = C(0x4C8DFF),
////                    AccentAlt = C(0x38BDF8),
////                    ZoneGood = C(0x2FBF71),
////                    ZoneWarn = C(0xF5A524),
////                    ZoneHot = C(0xEF4B4B),
////                    Grid = C(0x1B202A),
////                    Tick = C(0x7A87A0),
////                    HudBack = Color.FromArgb(190, C(0x0B0E14)),
////                    HudBorder = Color.FromArgb(60, Color.White),
////                    HudFore = C(0xE8EDF5),
////                    HudForeDim = C(0x97A3B5),
////                    Shadow = Color.FromArgb(150, Color.Black),
////                    ShadowLight = Color.FromArgb(0, Color.White),
////                    ShadowDark = Color.FromArgb(0, Color.Black),
////                    HoverOverlay = Color.White,
////                    FocusRing = C(0x4C8DFF)
////                };
////            }
////            return new ViewerPalette
////            {
////                Canvas = C(0xF3F5F9),
////                BackdropGlow = Color.FromArgb(0, Color.White),
////                ViewportFace = C(0xFFFFFF),
////                ViewportWell = C(0xFAFBFE),
////                Border = C(0xDCE3EE),
////                BorderHighlight = Color.White,
////                TextPrimary = C(0x16202F),
////                TextSecondary = C(0x5F6C7E),
////                TextDim = C(0x9AA6B8),
////                Accent = C(0x2563EB),
////                AccentAlt = C(0x0EA5E9),
////                ZoneGood = C(0x16A34A),
////                ZoneWarn = C(0xD97706),
////                ZoneHot = C(0xDC2626),
////                Grid = C(0xE7ECF4),
////                Tick = C(0xB9C4D4),
////                HudBack = Color.FromArgb(172, C(0xF9FBFE)),
////                HudBorder = Color.FromArgb(170, Color.White),
////                HudFore = C(0x16202F),
////                HudForeDim = C(0x5F6C7E),
////                Shadow = Color.FromArgb(30, C(0x0E1626)),
////                ShadowLight = Color.FromArgb(0, Color.White),
////                ShadowDark = Color.FromArgb(0, Color.Black),
////                HoverOverlay = Color.Black,
////                FocusRing = C(0x2563EB)
////            };
////        }

////        private static ViewerPalette GlassPalette(bool dark)
////        {
////            if (dark)
////            {
////                return new ViewerPalette
////                {
////                    Canvas = C(0x171B24),
////                    BackdropGlow = Color.FromArgb(38, C(0x00C8FF)),
////                    ViewportFace = Color.FromArgb(46, C(0x2A3140)),
////                    ViewportWell = Color.FromArgb(235, C(0x1B2029)),
////                    Border = Color.FromArgb(0, Color.White),
////                    BorderHighlight = Color.White,
////                    TextPrimary = C(0xEBF1FB),
////                    TextSecondary = C(0x9BA8BD),
////                    TextDim = C(0x63718A),
////                    Accent = C(0x00C8FF),
////                    AccentAlt = C(0x7C6CFF),
////                    ZoneGood = C(0x1FBF6B),
////                    ZoneWarn = C(0xF5A524),
////                    ZoneHot = C(0xF4506C),
////                    Grid = Color.FromArgb(26, C(0x6E82A6)),
////                    Tick = Color.FromArgb(80, C(0x5D6F92)),
////                    HudBack = Color.FromArgb(125, C(0x0E1219)),
////                    HudBorder = Color.FromArgb(140, Color.White),
////                    HudFore = C(0xEBF1FB),
////                    HudForeDim = C(0x9BA8BD),
////                    Shadow = Color.FromArgb(80, Color.Black),
////                    ShadowLight = Color.FromArgb(0, Color.White),
////                    ShadowDark = Color.FromArgb(0, Color.Black),
////                    HoverOverlay = C(0x00C8FF),
////                    FocusRing = C(0x00C8FF)
////                };
////            }
////            return new ViewerPalette
////            {
////                Canvas = C(0xE8EEF7),
////                BackdropGlow = Color.FromArgb(70, Color.White),
////                ViewportFace = Color.FromArgb(52, Color.White),
////                ViewportWell = Color.FromArgb(150, Color.White),
////                Border = Color.FromArgb(0, Color.White),
////                BorderHighlight = Color.White,
////                TextPrimary = C(0x0F2440),
////                TextSecondary = C(0x5D6F8C),
////                TextDim = C(0x8FA0B8),
////                Accent = C(0x0A84FF),
////                AccentAlt = C(0x7C5CFF),
////                ZoneGood = C(0x0E9F6E),
////                ZoneWarn = C(0xF0A63A),
////                ZoneHot = C(0xE5484D),
////                Grid = Color.FromArgb(24, C(0x2A3C5E)),
////                Tick = Color.FromArgb(80, C(0x334666)),
////                HudBack = Color.FromArgb(150, Color.White),
////                HudBorder = Color.FromArgb(200, Color.White),
////                HudFore = C(0x0F2440),
////                HudForeDim = C(0x5D6F8C),
////                Shadow = Color.FromArgb(55, C(0x1E2E4C)),
////                ShadowLight = Color.FromArgb(0, Color.White),
////                ShadowDark = Color.FromArgb(0, Color.Black),
////                HoverOverlay = C(0x0A84FF),
////                FocusRing = C(0x0A84FF)
////            };
////        }

////        private static ViewerPalette MaterialPalette(bool dark)
////        {
////            if (dark)
////            {
////                return new ViewerPalette
////                {
////                    Canvas = C(0x121212),
////                    BackdropGlow = Color.FromArgb(0, Color.White),
////                    ViewportFace = C(0x1E1E1E),
////                    ViewportWell = C(0x1A1A1A),
////                    Border = Color.FromArgb(0, Color.White),
////                    BorderHighlight = Color.FromArgb(0, Color.White),
////                    TextPrimary = C(0xF2F2F2),
////                    TextSecondary = C(0xB0B0B0),
////                    TextDim = C(0x8A8A8A),
////                    Accent = C(0xBB86FC),
////                    AccentAlt = C(0x03DAC6),
////                    ZoneGood = C(0x66BB6A),
////                    ZoneWarn = C(0xFFB74D),
////                    ZoneHot = C(0xEF5350),
////                    Grid = C(0x232323),
////                    Tick = C(0x2E2E2E),
////                    HudBack = C(0xBB86FC),
////                    HudBorder = Color.FromArgb(0, Color.White),
////                    HudFore = C(0x141218),
////                    HudForeDim = Color.FromArgb(150, C(0x141218)),
////                    Shadow = Color.FromArgb(0, Color.Black),
////                    ShadowLight = Color.FromArgb(0, Color.White),
////                    ShadowDark = Color.FromArgb(0, Color.Black),
////                    HoverOverlay = Color.White,
////                    FocusRing = C(0xBB86FC)
////                };
////            }
////            return new ViewerPalette
////            {
////                Canvas = C(0xF5F5F5),
////                BackdropGlow = Color.FromArgb(0, Color.White),
////                ViewportFace = C(0xFFFFFF),
////                ViewportWell = C(0xFFFFFF),
////                Border = Color.FromArgb(0, Color.White),
////                BorderHighlight = Color.FromArgb(0, Color.White),
////                TextPrimary = C(0x212121),
////                TextSecondary = C(0x757575),
////                TextDim = C(0x9E9E9E),
////                Accent = C(0x6200EE),
////                AccentAlt = C(0x03DAC6),
////                ZoneGood = C(0x2E7D32),
////                ZoneWarn = C(0xEF6C00),
////                ZoneHot = C(0xC62828),
////                Grid = C(0xEEEEEE),
////                Tick = C(0xE0E0E0),
////                HudBack = C(0x6200EE),
////                HudBorder = Color.FromArgb(0, Color.White),
////                HudFore = Color.White,
////                HudForeDim = Color.FromArgb(178, Color.White),
////                Shadow = Color.FromArgb(0, Color.Black),
////                ShadowLight = Color.FromArgb(0, Color.White),
////                ShadowDark = Color.FromArgb(0, Color.Black),
////                HoverOverlay = Color.Black,
////                FocusRing = C(0x6200EE)
////            };
////        }

////        private static ViewerPalette NeumorphicPalette(bool dark)
////        {
////            if (dark)
////            {
////                return new ViewerPalette
////                {
////                    Canvas = C(0x2A2F3A),
////                    BackdropGlow = Color.FromArgb(0, Color.White),
////                    ViewportFace = C(0x2A2F3A),
////                    ViewportWell = C(0x252A33),
////                    Border = Color.FromArgb(0, Color.White),
////                    BorderHighlight = Color.FromArgb(30, Color.White),
////                    TextPrimary = C(0xD6DCE8),
////                    TextSecondary = C(0x8791A6),
////                    TextDim = C(0x5F6879),
////                    Accent = C(0x7C8CF8),
////                    AccentAlt = C(0x9EA8FA),
////                    ZoneGood = C(0x58B87E),
////                    ZoneWarn = C(0xC9A55A),
////                    ZoneHot = C(0xC96A5E),
////                    Grid = Color.FromArgb(24, C(0x6A748C)),
////                    Tick = Color.FromArgb(60, C(0x6A748C)),
////                    HudBack = C(0x2A2F3A),
////                    HudBorder = Color.FromArgb(0, Color.White),
////                    HudFore = C(0xD6DCE8),
////                    HudForeDim = C(0x8791A6),
////                    Shadow = Color.FromArgb(70, C(0x1C2028)),
////                    ShadowLight = Color.FromArgb(130, C(0x3B4250)),
////                    ShadowDark = Color.FromArgb(160, C(0x1C2028)),
////                    HoverOverlay = Color.White,
////                    FocusRing = C(0x7C8CF8)
////                };
////            }
////            return new ViewerPalette
////            {
////                Canvas = C(0xE4E9F1),
////                BackdropGlow = Color.FromArgb(0, Color.White),
////                ViewportFace = C(0xE4E9F1),
////                ViewportWell = C(0xDCE2EC),
////                Border = Color.FromArgb(0, Color.White),
////                BorderHighlight = Color.White,
////                TextPrimary = C(0x47536E),
////                TextSecondary = C(0x8B96AD),
////                TextDim = C(0xA9B3C7),
////                Accent = C(0x6C7BF2),
////                AccentAlt = C(0x9BA6F5),
////                ZoneGood = C(0x6FBF8E),
////                ZoneWarn = C(0xD2A24C),
////                ZoneHot = C(0xD26A5C),
////                Grid = Color.FromArgb(26, C(0x9FACC6)),
////                Tick = Color.FromArgb(60, C(0x9FACC6)),
////                HudBack = C(0xE4E9F1),
////                HudBorder = Color.FromArgb(0, Color.White),
////                HudFore = C(0x47536E),
////                HudForeDim = C(0x8B96AD),
////                Shadow = Color.FromArgb(60, C(0xC3CDDF)),
////                ShadowLight = Color.FromArgb(210, Color.White),
////                ShadowDark = Color.FromArgb(170, C(0xC3CDDF)),
////                HoverOverlay = Color.White,
////                FocusRing = C(0x6C7BF2)
////            };
////        }

////        private static ViewerPalette CyberPalette()
////        {
////            return new ViewerPalette
////            {
////                Canvas = C(0x05070A),
////                BackdropGlow = Color.FromArgb(22, C(0x00E5FF)),
////                ViewportFace = C(0x0A0D13),
////                ViewportWell = C(0x080A10),
////                Border = C(0x00E5FF),
////                BorderHighlight = Color.FromArgb(0, Color.White),
////                TextPrimary = C(0xD9F5FF),
////                TextSecondary = C(0x6E8CA0),
////                TextDim = C(0x45586A),
////                Accent = C(0x00E5FF),
////                AccentAlt = C(0xFF2E97),
////                ZoneGood = C(0x00FFA3),
////                ZoneWarn = C(0xFFE066),
////                ZoneHot = C(0xFF3860),
////                Grid = Color.FromArgb(22, C(0x00E5FF)),
////                Tick = Color.FromArgb(80, C(0x00E5FF)),
////                HudBack = Color.FromArgb(200, C(0x05080E)),
////                HudBorder = Color.FromArgb(90, C(0x00E5FF)),
////                HudFore = C(0xD9F5FF),
////                HudForeDim = C(0x5E7E93),
////                Shadow = Color.FromArgb(140, Color.Black),
////                ShadowLight = Color.FromArgb(0, Color.White),
////                ShadowDark = Color.FromArgb(0, Color.Black),
////                HoverOverlay = C(0x00E5FF),
////                FocusRing = C(0xFF2E97)
////            };
////        }

////        // ----------------------------------------------------------------
////        //  FONT ENGINE
////        // ----------------------------------------------------------------
////        private static string UiFamily
////        {
////            get
////            {
////                if (_uiFamily == null) _uiFamily = ResolveFontFamily(UiFontCandidates);
////                return _uiFamily;
////            }
////        }

////        private static string MonoFamily
////        {
////            get
////            {
////                if (_monoFamily == null) _monoFamily = ResolveFontFamily(MonoFontCandidates);
////                return _monoFamily;
////            }
////        }

////        private static string ResolveFontFamily(string[] candidates)
////        {
////            try
////            {
////                using (InstalledFontCollection installed = new InstalledFontCollection())
////                {
////                    HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
////                    foreach (FontFamily family in installed.Families) names.Add(family.Name);
////                    foreach (string candidate in candidates)
////                        if (names.Contains(candidate)) return candidate;
////                }
////            }
////            catch { }
////            return candidates[candidates.Length - 1];
////        }

////        private Font GetFont(bool mono, float sizePt, FontStyle style)
////        {
////            string family = mono ? MonoFamily : UiFamily;
////            string key = family + "|" + sizePt.ToString("0.###") + "|" + (int)style;

////            Font font;
////            if (_fontCache.TryGetValue(key, out font))
////                return font;   // cache-owned — callers must NOT dispose

////            try
////            {
////                font = new Font(family, sizePt, style, GraphicsUnit.Point);
////            }
////            catch
////            {
////                font = new Font(FontFamily.GenericSansSerif, sizePt, style, GraphicsUnit.Point);
////            }
////            _fontCache[key] = font;
////            return font;
////        }

////        // ----------------------------------------------------------------
////        //  INPUT — MOUSE
////        // ----------------------------------------------------------------
////        protected override void OnMouseEnter(EventArgs e)
////        {
////            base.OnMouseEnter(e);
////            _hover = true;
////            if (!IsDesignTime && Enabled && !Focused) Focus();
////            InvalidateChrome();          // hover chrome (glass glow, dash border lerp)
////        }

////        protected override void OnMouseLeave(EventArgs e)
////        {
////            base.OnMouseLeave(e);
////            _hover = false;
////            InvalidateChrome();
////        }

////        protected override void OnMouseDown(MouseEventArgs e)
////        {
////            base.OnMouseDown(e);
////            if (!Enabled) return;
////            Focus();

////            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
////            {
////                _panning = true;
////                _pressed = true;
////                _lastMouse = e.Location;
////                Cursor = Cursors.SizeAll;
////                BeginInteraction();
////                InvalidateChrome();      // pressed chrome (neu depth inversion, glow rings)
////            }
////        }

////        protected override void OnMouseMove(MouseEventArgs e)
////        {
////            base.OnMouseMove(e);
////            _hoverPos = e.Location;
////            if (!Enabled) return;

////            if (_panning)
////            {
////                _pan.X += e.X - _lastMouse.X;
////                _pan.Y += e.Y - _lastMouse.Y;
////                _lastMouse = e.Location;
////                BeginInteraction();      // keeps the HQ settle timer alive mid-gesture
////            }

////            UpdateProbeText();
////            Invalidate();                // crosshair + probe readout
////        }

////        protected override void OnMouseUp(MouseEventArgs e)
////        {
////            base.OnMouseUp(e);
////            _panning = false;
////            _pressed = false;
////            Cursor = Cursors.Cross;
////            EndInteraction();            // immediate full-quality settle frame
////            InvalidateChrome();
////        }

////        protected override void OnMouseCaptureChanged(EventArgs e)
////        {
////            base.OnMouseCaptureChanged(e);
////            if (_panning)
////            {
////                _panning = false;
////                _pressed = false;
////                Cursor = Cursors.Cross;
////                EndInteraction();
////                InvalidateChrome();
////            }
////        }

////        protected override void OnMouseWheel(MouseEventArgs e)
////        {
////            if (e is HandledMouseEventArgs hme) hme.Handled = true;
////            base.OnMouseWheel(e);

////            if (!Enabled || _image == null) return;

////            float factor = e.Delta > 0 ? 1.12f : 1f / 1.12f;
////            BeginInteraction();          // bilinear mid-gesture, settles after 140 ms idle
////            SetZoom(_zoom * factor, new PointF(e.X, e.Y));
////        }

////        protected override void OnDoubleClick(EventArgs e)
////        {
////            base.OnDoubleClick(e);
////            AutoFit();
////        }

////        // ----------------------------------------------------------------
////        //  INPUT — KEYBOARD
////        // ----------------------------------------------------------------
////        protected override bool IsInputKey(Keys keyData)
////        {
////            switch (keyData & Keys.KeyCode)
////            {
////                case Keys.Left:
////                case Keys.Right:
////                case Keys.Up:
////                case Keys.Down:
////                    return true;
////            }
////            return base.IsInputKey(keyData);
////        }

////        protected override void OnKeyDown(KeyEventArgs e)
////        {
////            base.OnKeyDown(e);
////            if (!Enabled) return;

////            float step = e.Shift ? 96f : 24f;
////            switch (e.KeyCode)
////            {
////                case Keys.Left: BeginInteraction(); PanBy(-step, 0f); e.Handled = true; break;
////                case Keys.Right: BeginInteraction(); PanBy(step, 0f); e.Handled = true; break;
////                case Keys.Up: BeginInteraction(); PanBy(0f, -step); e.Handled = true; break;
////                case Keys.Down: BeginInteraction(); PanBy(0f, step); e.Handled = true; break;
////                case Keys.Add:
////                case Keys.Oemplus: BeginInteraction(); SetZoom(_zoom * 1.25f, ViewportCenter()); e.Handled = true; break;
////                case Keys.Subtract:
////                case Keys.OemMinus: BeginInteraction(); SetZoom(_zoom / 1.25f, ViewportCenter()); e.Handled = true; break;
////                case Keys.D0:
////                case Keys.NumPad0: SetZoom(1f, ViewportCenter()); e.Handled = true; break;
////                case Keys.Home:
////                case Keys.F: AutoFit(); e.Handled = true; break;
////            }
////        }

////        private PointF ViewportCenter() { return new PointF(Width / 2f, Height / 2f); }

////        // ----------------------------------------------------------------
////        //  DRAG & DROP
////        // ----------------------------------------------------------------
////        protected override void OnDragEnter(DragEventArgs e)
////        {
////            base.OnDragEnter(e);
////            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
////        }

////        protected override void OnDragDrop(DragEventArgs e)
////        {
////            base.OnDragDrop(e);
////            if (!Enabled) return;

////            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
////            if (files != null && files.Length > 0)
////            {
////                try { LoadFromFile(files[0]); }
////                catch { /* unsupported / corrupt file — keep current state */ }
////            }
////        }

////        // ----------------------------------------------------------------
////        //  LIFECYCLE
////        // ----------------------------------------------------------------
////        protected override void OnHandleCreated(EventArgs e)
////        {
////            base.OnHandleCreated(e);
////            try
////            {
////                using (Graphics probe = CreateGraphics())
////                {
////                    _dpiScale = Math.Max(1f, probe.DpiX / 96f);
////                }
////            }
////            catch { _dpiScale = 1f; }
////            InvalidateChrome();          // rebuild chrome at the real DPI
////        }

////        protected override void OnResize(EventArgs e)
////        {
////            base.OnResize(e);
////            if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }
////            if (_image != null && _needsAutoFit) AutoFit();
////            Invalidate();
////        }

////        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
////        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

////        protected override void OnEnabledChanged(EventArgs e)
////        {
////            base.OnEnabledChanged(e);
////            InvalidateChrome();          // hover-dependent chrome reads Enabled
////        }

////        protected override void Dispose(bool disposing)
////        {
////            if (disposing)
////            {
////                if (_hqTimer != null) { _hqTimer.Stop(); _hqTimer.Dispose(); _hqTimer = null; }
////                if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }
////                foreach (Font font in _fontCache.Values) font.Dispose();
////                _fontCache.Clear();
////                if (_image != null) { _image.Dispose(); _image = null; }
////            }
////            base.Dispose(disposing);
////        }
////    }
////}












////======== VER 1.3 =============


//// ============================================================================
////  PanZoomViewer.cs — Reference-quality pan/zoom image surface (v4)
////  ---------------------------------------------------------------------------
////  FIDELITY     : EnhanceImage=false (DEFAULT) → pixel-perfect, zero blur,
////                 image displayed exactly as it is, fastest possible blit.
////                 EnhanceImage=true → HighQualityBicubic for photographs.
////  LOADING API  : LoadFromFile / LoadFromFileAsync / LoadFromStream /
////                 LoadFromBytes / LoadFromImage / PostImage (thread-safe) /
////                 DetachImage / Clear / Image property / drag & drop
////  PERF ENGINE  : visible-source-cropped DrawImage · cached PArgb chrome
////                 layer · batched vector paths · cached HUD measurement
////  DESIGN SYSTEM: 5 styles (ViewerStyle) × 2 themes (ThemeMode)
////  SAFETY       : zero-flicker buffered pipeline · strict GDI+ disposal ·
////                 dead-bitmap immunity · design-time diagnostic panel
////  Target: .NET Framework 4.7+ / .NET 6+ WinForms, C# 7.3+. No dependencies.
//// ============================================================================
//using System;
//using System.Collections.Generic;
//using System.ComponentModel;
//using System.Diagnostics;
//using System.Drawing;
//using System.Drawing.Drawing2D;
//using System.Drawing.Imaging;
//using System.Drawing.Text;
//using System.IO;
//using System.Threading.Tasks;
//using System.Windows.Forms;

//namespace CsplCameraOcr.UserControls
//{
//    // ========================================================================
//    //  PUBLIC ENUMS
//    // ========================================================================

//    public enum ThemeMode { Light, Dark }

//    public enum ViewerStyle
//    {
//        DashboardPremium = 1,
//        FluentGlass = 2,
//        MaterialFlat = 3,
//        SoftNeumorphic = 4,
//        Cyberpunk = 5
//    }

//    // ========================================================================
//    //  PALETTE SNAPSHOT
//    // ========================================================================

//    public sealed class ViewerPalette
//    {
//        public Color Canvas { get; internal set; }
//        public Color BackdropGlow { get; internal set; }
//        public Color ViewportFace { get; internal set; }
//        public Color ViewportWell { get; internal set; }
//        public Color Border { get; internal set; }
//        public Color BorderHighlight { get; internal set; }
//        public Color TextPrimary { get; internal set; }
//        public Color TextSecondary { get; internal set; }
//        public Color TextDim { get; internal set; }
//        public Color Accent { get; internal set; }
//        public Color AccentAlt { get; internal set; }
//        public Color ZoneGood { get; internal set; }
//        public Color ZoneWarn { get; internal set; }
//        public Color ZoneHot { get; internal set; }
//        public Color Grid { get; internal set; }
//        public Color Tick { get; internal set; }
//        public Color HudBack { get; internal set; }
//        public Color HudBorder { get; internal set; }
//        public Color HudFore { get; internal set; }
//        public Color HudForeDim { get; internal set; }
//        public Color Shadow { get; internal set; }
//        public Color ShadowLight { get; internal set; }
//        public Color ShadowDark { get; internal set; }
//        public Color HoverOverlay { get; internal set; }
//        public Color FocusRing { get; internal set; }
//    }

//    public sealed class OverlayPaintEventArgs : EventArgs
//    {
//        internal OverlayPaintEventArgs(Graphics graphics, RectangleF imageBounds,
//                                       float zoom, PointF pan, ViewerPalette palette)
//        {
//            Graphics = graphics;
//            ImageBounds = imageBounds;
//            Zoom = zoom;
//            Pan = pan;
//            Palette = palette;
//        }

//        public Graphics Graphics { get; private set; }
//        public RectangleF ImageBounds { get; private set; }
//        public float Zoom { get; private set; }
//        public PointF Pan { get; private set; }
//        public ViewerPalette Palette { get; private set; }
//    }

//    // ========================================================================
//    //  CONTROL
//    // ========================================================================

//    [ToolboxItem(true)]
//    [Description("Reference-quality pan/zoom image surface — pixel-perfect by default, five design styles.")]
//    public partial class PanZoomViewer : Control
//    {
//        // ====================================================================
//        //  CORE DATA
//        // ====================================================================
//        private Bitmap _image;
//        private float _zoom = 1f;
//        private PointF _pan = PointF.Empty;
//        private bool _needsAutoFit = true;

//        private bool _hover;
//        private bool _pressed;
//        private bool _panning;
//        private Point _lastMouse;
//        private Point _hoverPos;
//        private bool _imageLostPending;

//        private ThemeMode _themeMode = ThemeMode.Dark;
//        private ViewerStyle _controlStyle = ViewerStyle.DashboardPremium;
//        private ViewerPalette _palette;

//        private bool _showHud = true;
//        private bool _showScanlines = true;
//        private bool _showCrosshair = true;
//        private bool _enhanceImage = false;          // DEFAULT = pixel-perfect
//        private float _minZoom = 0.05f;
//        private float _maxZoom = 100f;
//        private float _dpiScale = 1f;

//        // ----------------------------------------------------------------
//        //  PERFORMANCE ENGINE
//        //  _chromeCache : canvas + card layers baked into one premultiplied
//        //                32bpp ARGB bitmap → one blit per frame instead of
//        //                gradients/shadows/glow paths. Control-owned; created
//        //                OUTSIDE OnPaint; disposed on invalidation & Dispose.
//        // ----------------------------------------------------------------
//        private Bitmap _chromeCache;

//        // ----------------------------------------------------------------
//        //  HUD TEXT CACHES — strings regenerate only when values change.
//        // ----------------------------------------------------------------
//        private string _hudZoomText = "100 %";
//        private string _hudSourceText = "—";
//        private string _hudProbeText = "";
//        private int _probeX = int.MinValue;
//        private int _probeY = int.MinValue;

//        private string _mCapSrc; private SizeF _szCapSrc;
//        private string _mValSrc; private SizeF _szValSrc;
//        private string _mCapZoom; private SizeF _szCapZoom;
//        private string _mValZoom; private SizeF _szValZoom;
//        private string _mCapProbe; private SizeF _szCapProbe;
//        private string _mValProbe; private SizeF _szValProbe;

//        // Font engine (cached for performance, disposed exactly once in Dispose)
//        private readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();
//        private static string _uiFamily;
//        private static string _monoFamily;
//        private static readonly string[] UiFontCandidates =
//            { "Segoe UI Variable Display", "Segoe UI Workspace", "Segoe UI", "Arial" };
//        private static readonly string[] MonoFontCandidates =
//            { "Cascadia Code", "Cascadia Mono", "JetBrains Mono", "Consolas", "Courier New" };

//        private struct ViewerLayout
//        {
//            public RectangleF View;
//            public RectangleF Content;
//            public float ViewRadius;
//            public float ContentRadius;
//            public float Dpi;
//        }

//        private enum ChipAnchor { TopLeft, BottomLeft, BottomRight }

//        // ====================================================================
//        //  CONSTRUCTION
//        // ====================================================================
//        public PanZoomViewer()
//        {
//            SetStyle(
//                ControlStyles.UserPaint |
//                ControlStyles.AllPaintingInWmPaint |
//                ControlStyles.DoubleBuffer |
//                ControlStyles.OptimizedDoubleBuffer |
//                ControlStyles.ResizeRedraw |
//                ControlStyles.Selectable, true);
//            UpdateStyles();

//            TabStop = true;
//            AllowDrop = true;
//            Cursor = Cursors.Cross;

//            _palette = BuildPalette(_themeMode, _controlStyle);
//        }

//        // ====================================================================
//        //  PROPERTIES
//        // ====================================================================
//        [Category("Behavior")]
//        [Description("false (default): pixel-perfect display — the image looks exactly as it is, zero resampling blur, fastest rendering. true: high-quality smooth scaling for photographs.")]
//        [DefaultValue(false)]
//        public bool EnhanceImage
//        {
//            get { return _enhanceImage; }
//            set { if (_enhanceImage == value) return; _enhanceImage = value; Invalidate(); }
//        }

//        // Compat shim — hidden. Maps the old knob onto the new single switch
//        // so previously generated designer/code files keep compiling.
//        [Browsable(false)]
//        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
//        public InterpolationMode Interpolation
//        {
//            get
//            {
//                return _enhanceImage ? InterpolationMode.HighQualityBicubic
//                                       : InterpolationMode.NearestNeighbor;
//            }
//            set { EnhanceImage = (value != InterpolationMode.NearestNeighbor); }
//        }

//        [Category("Appearance")]
//        [Description("Light or Dark surface theme.")]
//        [DefaultValue(ThemeMode.Dark)]
//        public ThemeMode ThemeMode
//        {
//            get { return _themeMode; }
//            set
//            {
//                if (_themeMode == value) return;
//                _themeMode = value;
//                _palette = BuildPalette(_themeMode, _controlStyle);
//                InvalidateChrome();
//            }
//        }

//        [Category("Appearance")]
//        [Description("Visual design system.")]
//        [DefaultValue(ViewerStyle.DashboardPremium)]
//        public ViewerStyle ControlStyle
//        {
//            get { return _controlStyle; }
//            set
//            {
//                if (_controlStyle == value) return;
//                _controlStyle = value;
//                _palette = BuildPalette(_themeMode, _controlStyle);
//                InvalidateChrome();
//            }
//        }

//        [Category("Appearance")]
//        [Description("Shows the minimal readout chips.")]
//        [DefaultValue(true)]
//        public bool ShowHud
//        {
//            get { return _showHud; }
//            set { if (_showHud == value) return; _showHud = value; Invalidate(); }
//        }

//        [Category("Appearance")]
//        [Description("Cyberpunk style: subtle CRT scanline film.")]
//        [DefaultValue(true)]
//        public bool ShowScanlines
//        {
//            get { return _showScanlines; }
//            set { if (_showScanlines == value) return; _showScanlines = value; Invalidate(); }
//        }

//        [Category("Appearance")]
//        [Description("Precision crosshair follows the cursor (Dashboard & Cyberpunk).")]
//        [DefaultValue(true)]
//        public bool ShowCrosshair
//        {
//            get { return _showCrosshair; }
//            set { if (_showCrosshair == value) return; _showCrosshair = value; Invalidate(); }
//        }

//        [Category("Behavior")]
//        [Description("Lower zoom clamp.")]
//        [DefaultValue(0.05f)]
//        public float MinZoom
//        {
//            get { return _minZoom; }
//            set { _minZoom = Math.Max(0.01f, value); Invalidate(); }
//        }

//        [Category("Behavior")]
//        [Description("Upper zoom clamp.")]
//        [DefaultValue(100f)]
//        public float MaxZoom
//        {
//            get { return _maxZoom; }
//            set { _maxZoom = Math.Max(_minZoom * 2f, value); Invalidate(); }
//        }

//        [Browsable(false)]
//        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
//        [Description("Displayed bitmap. Ownership transfers to the control; the previous bitmap is disposed on replacement.")]
//        public Bitmap Image
//        {
//            get { return _image; }
//            set
//            {
//                if (ReferenceEquals(_image, value)) return;
//                Bitmap old = _image;
//                _image = value;

//                if (_image != null && _needsAutoFit)
//                {
//                    try { AutoFit(); }
//                    catch { /* a bitmap that dies between decode and fit */ }
//                }

//                UpdateSourceText();
//                if (old != null) old.Dispose();      // instant disposal — zero GDI leak
//                Invalidate();
//            }
//        }

//        [Browsable(false)]
//        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
//        public float Zoom
//        {
//            get { return _zoom; }
//            set { SetZoom(value, new PointF(Width / 2f, Height / 2f)); }
//        }

//        [Browsable(false)]
//        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
//        public PointF PanOffset
//        {
//            get { return _pan; }
//            set { _pan = value; Invalidate(); }
//        }

//        // ====================================================================
//        //  EVENTS
//        // ====================================================================

//        /// <summary>Legacy hook — Graphics in control-space (same signature as the previous control).</summary>
//        public event EventHandler<Graphics> OnCustomPaint;

//        /// <summary>Modern overlay hook — Graphics, image bounds, zoom, pan, active palette.</summary>
//        public event EventHandler<OverlayPaintEventArgs> OverlayPaint;

//        /// <summary>Raised when the assigned bitmap was found dead at draw time.</summary>
//        public event EventHandler ImageLost;

//        // ====================================================================
//        //  LOADING API — multiple import paths, all ownership-safe
//        // ====================================================================

//        /// <summary>Loads from a file path. The file is NOT kept locked after this returns.</summary>
//        public void LoadFromFile(string path)
//        {
//            Image = DecodeFile(path);
//        }

//        /// <summary>Decodes off the UI thread; the decoded frame is applied on the UI thread. Call from the UI thread.</summary>
//        public async Task LoadFromFileAsync(string path)
//        {
//            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path must not be empty.", "path");

//            Bitmap decoded = await Task.Run(() => DecodeFile(path));

//            if (IsDisposed || Disposing) { decoded.Dispose(); return; }
//            Image = decoded;
//        }

//        /// <summary>Loads from any stream (file, memory, network). The stream may close immediately after this returns.</summary>
//        public void LoadFromStream(Stream stream)
//        {
//            if (stream == null) throw new ArgumentNullException("stream");

//            // Image.FromStream keeps the stream alive internally — always copy
//            // out so the caller's stream can close the moment we return.
//            using (System.Drawing.Image decoded = System.Drawing.Image.FromStream(stream, false, true))
//            {
//                Image = new Bitmap(decoded);
//            }
//        }

//        /// <summary>Loads from raw encoded bytes (BMP / PNG / JPEG / GIF / TIFF).</summary>
//        public void LoadFromBytes(byte[] data)
//        {
//            if (data == null) throw new ArgumentNullException("data");
//            using (MemoryStream ms = new MemoryStream(data, false))
//            {
//                LoadFromStream(ms);
//            }
//        }

//        /// <summary>Loads from any System.Drawing.Image. The source stays caller-owned; the viewer keeps an independent copy.</summary>
//        public void LoadFromImage(System.Drawing.Image image)
//        {
//            if (image == null) throw new ArgumentNullException("image");
//            Image = new Bitmap(image);
//        }

//        /// <summary>Thread-safe assignment — safe to call directly from a camera/capture thread. The frame is marshalled to the UI thread and ownership transfers.</summary>
//        public void PostImage(Bitmap frame)
//        {
//            if (frame == null) return;
//            if (IsDisposed) { frame.Dispose(); return; }

//            if (IsHandleCreated && InvokeRequired)
//            {
//                BeginInvoke((Action)delegate
//                {
//                    if (IsDisposed) { frame.Dispose(); return; }
//                    Image = frame;
//                });
//            }
//            else
//            {
//                Image = frame;
//            }
//        }

//        /// <summary>Removes the current bitmap WITHOUT disposing it — ownership returns to the caller.</summary>
//        public Bitmap DetachImage()
//        {
//            Bitmap bmp = _image;
//            _image = null;
//            _needsAutoFit = true;
//            _hudSourceText = "—";
//            _hudProbeText = "";
//            _probeX = int.MinValue;
//            _probeY = int.MinValue;
//            Invalidate();
//            return bmp;
//        }

//        /// <summary>Clears the viewer back to the empty state.</summary>
//        public void Clear()
//        {
//            Bitmap old = _image;
//            _image = null;
//            _needsAutoFit = true;
//            _hudSourceText = "—";
//            _hudProbeText = "";
//            _probeX = int.MinValue;
//            _probeY = int.MinValue;
//            if (old != null) old.Dispose();
//            Invalidate();
//        }

//        private static Bitmap DecodeFile(string path)
//        {
//            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
//            using (System.Drawing.Image tmp = System.Drawing.Image.FromStream(fs, false, true))
//            {
//                return new Bitmap(tmp);   // copy — any file lock dies with this method
//            }
//        }

//        // ====================================================================
//        //  PUBLIC METHODS & COORDINATE TRANSFORMS
//        // ====================================================================

//        public void AutoFit()
//        {
//            if (_image == null || Width < 20 || Height < 20) return;

//            ViewerLayout lay = ComputeLayout();
//            RectangleF c = lay.Content;

//            float z = Math.Min(c.Width / _image.Width, c.Height / _image.Height);
//            _zoom = ClampF(z, MinZoom, MaxZoom);

//            float dw = _image.Width * _zoom;
//            float dh = _image.Height * _zoom;
//            _pan = new PointF(c.X + (c.Width - dw) / 2f, c.Y + (c.Height - dh) / 2f);

//            _needsAutoFit = false;
//            UpdateZoomText();
//            Invalidate();
//        }

//        public void SetZoom(float zoom, PointF anchor)
//        {
//            float z = ClampF(zoom, MinZoom, MaxZoom);
//            if (z == _zoom) return;

//            float k = z / _zoom;
//            _pan = new PointF(
//                anchor.X - (anchor.X - _pan.X) * k,
//                anchor.Y - (anchor.Y - _pan.Y) * k);
//            _zoom = z;
//            UpdateZoomText();
//            Invalidate();
//        }

//        public void PanBy(float dx, float dy)
//        {
//            _pan = new PointF(_pan.X + dx, _pan.Y + dy);
//            Invalidate();
//        }

//        public ViewerPalette GetPalette() { return _palette; }

//        public PointF ScreenToImageF(PointF screenPoint)
//        {
//            return new PointF((screenPoint.X - _pan.X) / _zoom,
//                              (screenPoint.Y - _pan.Y) / _zoom);
//        }

//        public Point ScreenToImage(Point screenPoint)
//        {
//            return new Point(
//                (int)Math.Round((screenPoint.X - _pan.X) / _zoom),
//                (int)Math.Round((screenPoint.Y - _pan.Y) / _zoom));
//        }

//        public RectangleF ImageToScreenF(RectangleF imageRect)
//        {
//            return new RectangleF(
//                imageRect.X * _zoom + _pan.X,
//                imageRect.Y * _zoom + _pan.Y,
//                imageRect.Width * _zoom,
//                imageRect.Height * _zoom);
//        }

//        public Rectangle ImageToScreen(Rectangle imageRect)
//        {
//            RectangleF f = ImageToScreenF(imageRect);
//            return new Rectangle(
//                (int)Math.Round(f.X), (int)Math.Round(f.Y),
//                (int)Math.Round(f.Width), (int)Math.Round(f.Height));
//        }

//        public RectangleF GetImageScreenBounds()
//        {
//            if (_image == null) return RectangleF.Empty;
//            try
//            {
//                return new RectangleF(_pan.X, _pan.Y, _image.Width * _zoom, _image.Height * _zoom);
//            }
//            catch { return RectangleF.Empty; }
//        }

//        // ====================================================================
//        //  RENDERING PIPELINE
//        // ====================================================================
//        protected override void OnPaintBackground(PaintEventArgs pevent)
//        {
//            // Intentionally empty — the buffered surface is composed in OnPaint only.
//        }

//        protected override void OnPaint(PaintEventArgs e)
//        {
//            if (IsDesignTime)
//            {
//                try { PaintCore(e.Graphics); }
//                catch (Exception ex) { PaintDesignFailure(e.Graphics, ex); }
//            }
//            else
//            {
//                PaintCore(e.Graphics);
//            }
//        }

//        private bool IsDesignTime
//        {
//            get { return Site != null && Site.DesignMode; }
//        }

//        private void PaintDesignFailure(Graphics g, Exception ex)
//        {
//            Debug.WriteLine("[PanZoomViewer design-time paint failure]\r\n" + ex);
//            try
//            {
//                g.SmoothingMode = SmoothingMode.None;
//                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

//                using (SolidBrush bg = new SolidBrush(Color.FromArgb(255, 34, 18, 28)))
//                    g.FillRectangle(bg, ClientRectangle);

//                using (Font f = new Font(FontFamily.GenericMonospace, 8.5f, FontStyle.Regular, GraphicsUnit.Point))
//                using (SolidBrush t = new SolidBrush(Color.FromArgb(255, 255, 105, 97)))
//                using (SolidBrush w = new SolidBrush(Color.White))
//                using (StringFormat fmt = new StringFormat())
//                {
//                    fmt.Trimming = StringTrimming.EllipsisCharacter;

//                    g.DrawString("PANZOOMVIEWER — DESIGN-TIME PAINT FAILURE", f, t, 12f, 12f, fmt);
//                    g.DrawString(ex.GetType().Name + ": " + ex.Message, f, w, 12f, 30f, fmt);

//                    string[] frames = ex.StackTrace.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
//                    float y = 48f;
//                    int shown = Math.Min(10, frames.Length);
//                    for (int i = 0; i < shown; i++)
//                    {
//                        g.DrawString(frames[i].Trim(), f, w, 12f, y, fmt);
//                        y += 15f;
//                        if (y > Height - 18f) break;
//                    }
//                }
//            }
//            catch { /* diagnostics must never throw */ }
//        }

//        private void PaintCore(Graphics g)
//        {
//            g.SmoothingMode = SmoothingMode.AntiAlias;
//            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
//            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
//            g.CompositingQuality = CompositingQuality.HighQuality;
//            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

//            if (Width < 6 || Height < 6)
//            {
//                using (SolidBrush b = new SolidBrush(_palette.Canvas)) g.FillRectangle(b, ClientRectangle);
//                return;
//            }

//            ViewerPalette pal = _palette;
//            ViewerLayout lay = ComputeLayout();

//            // 1 — static chrome: one blit from the premultiplied-ARGB cache.
//            EnsureChromeCache(lay);
//            if (_chromeCache != null) g.DrawImageUnscaled(_chromeCache, 0, 0);
//            else
//            {
//                DrawCanvasLayer(g, pal, lay);
//                DrawCardLayer(g, pal, lay);
//            }

//            DrawZoomStrip(g, pal, lay);      // 2 — dynamic zone strip (tracks zoom)
//            DrawContentLayer(g, pal, lay);   // 3 — cropped image / empty state

//            // 4 — host overlays (ROIs, OCR boxes).
//            EventHandler<Graphics> legacy = OnCustomPaint;
//            if (legacy != null) legacy(this, g);

//            EventHandler<OverlayPaintEventArgs> overlay = OverlayPaint;
//            if (overlay != null)
//            {
//                OverlayPaintEventArgs args = new OverlayPaintEventArgs(
//                    g, GetImageScreenBounds(), _zoom, _pan, pal);
//                overlay(this, args);
//            }

//            DrawHudLayer(g, pal, lay);       // 5 — minimal modern readouts
//            DrawStateLayer(g, pal, lay);     // 6 — crosshair, focus ring, disabled veil

//            if (_imageLostPending)
//            {
//                _imageLostPending = false;
//                EventHandler lost = ImageLost;
//                if (lost != null) lost(this, EventArgs.Empty);
//            }
//        }

//        // ----------------------------------------------------------------
//        //  PERFORMANCE ENGINE — chrome cache
//        // ----------------------------------------------------------------
//        private void InvalidateChrome()
//        {
//            if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }
//            Invalidate();
//        }

//        private void EnsureChromeCache(ViewerLayout lay)
//        {
//            if (_chromeCache != null &&
//                _chromeCache.Width == Width &&
//                _chromeCache.Height == Height) return;

//            if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }

//            // Format32bppPArgb = PREMULTIPLIED alpha — the fastest format GDI+
//            // can composite; the blit in PaintCore is essentially a memcpy.
//            Bitmap bmp = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format32bppPArgb);
//            using (Graphics cg = Graphics.FromImage(bmp))
//            {
//                cg.SmoothingMode = SmoothingMode.AntiAlias;
//                cg.InterpolationMode = InterpolationMode.HighQualityBicubic;
//                cg.PixelOffsetMode = PixelOffsetMode.HighQuality;
//                cg.CompositingQuality = CompositingQuality.HighQuality;

//                DrawCanvasLayer(cg, _palette, lay);
//                DrawCardLayer(cg, _palette, lay);
//            }
//            _chromeCache = bmp;
//        }

//        // ----------------------------------------------------------------
//        //  LAYER 1 — CANVAS (baked into the chrome cache)
//        // ----------------------------------------------------------------
//        private void DrawCanvasLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF client = new RectangleF(0f, 0f, Width, Height);
//            using (SolidBrush b = new SolidBrush(pal.Canvas)) g.FillRectangle(b, client);

//            if (pal.BackdropGlow.A > 0)
//            {
//                using (LinearGradientBrush lg = new LinearGradientBrush(
//                    client, pal.BackdropGlow, Color.FromArgb(0, pal.BackdropGlow), 118f))
//                {
//                    g.FillRectangle(lg, client);
//                }
//            }

//            if (_controlStyle == ViewerStyle.MaterialFlat)
//            {
//                using (SolidBrush accent = new SolidBrush(pal.Accent))
//                    g.FillRectangle(accent, 0f, 0f, Width, 4f * lay.Dpi);
//            }
//        }

//        // ----------------------------------------------------------------
//        //  LAYER 2 — VIEWPORT CARD (baked into the chrome cache)
//        // ----------------------------------------------------------------
//        private void DrawCardLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            switch (_controlStyle)
//            {
//                case ViewerStyle.FluentGlass: DrawGlassCard(g, pal, lay); break;
//                case ViewerStyle.MaterialFlat: DrawMaterialCard(g, pal, lay); break;
//                case ViewerStyle.SoftNeumorphic: DrawNeumorphicCard(g, pal, lay); break;
//                case ViewerStyle.Cyberpunk: DrawCyberCard(g, pal, lay); break;
//                default: DrawDashboardCard(g, pal, lay); break;
//            }
//        }

//        // STYLE 1 — DASHBOARD PREMIUM
//        private void DrawDashboardCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF v = lay.View;
//            float r = lay.ViewRadius;

//            DrawSoftShadow(g, v, r, pal.Shadow, 7f * lay.Dpi, 4);

//            Color borderColor = (_hover && Enabled) ? Lerp(pal.Border, pal.Accent, 0.45f) : pal.Border;
//            using (GraphicsPath face = BuildRoundedPath(v, r))
//            {
//                using (SolidBrush fill = new SolidBrush(pal.ViewportFace)) g.FillPath(fill, face);
//                using (Pen border = new Pen(borderColor, 1f)) g.DrawPath(border, face);
//            }

//            g.SetClip(new RectangleF(v.X, v.Y + v.Height * 0.55f, v.Width, v.Height * 0.45f));
//            using (GraphicsPath ip = BuildRoundedPath(Deflate(v, 1.4f), Math.Max(1f, r - 1.4f)))
//            using (Pen hi = new Pen(Color.FromArgb(80, pal.BorderHighlight), 1f))
//            {
//                g.DrawPath(hi, ip);
//            }
//            g.ResetClip();
//        }

//        // Dynamic (NOT cached) — the zone color tracks the zoom value.
//        private void DrawZoomStrip(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            if (_controlStyle != ViewerStyle.DashboardPremium) return;

//            RectangleF v = lay.View;
//            float r = lay.ViewRadius;
//            float stripW = v.Width - 2f * r;
//            if (stripW <= 4f) return;

//            RectangleF strip = new RectangleF(v.X + r, v.Y + 1.5f, stripW, 3f);
//            using (GraphicsPath sp = BuildRoundedPath(strip, 1.5f))
//            using (SolidBrush sb = new SolidBrush(Color.FromArgb(210, ZoomZoneColor(pal))))
//            {
//                g.FillPath(sb, sp);
//            }
//        }

//        // STYLE 2 — FLUENT GLASS
//        private void DrawGlassCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF v = lay.View;
//            float r = lay.ViewRadius;

//            DrawSoftShadow(g, v, r, pal.Shadow, 5f * lay.Dpi, 3);

//            using (GraphicsPath p1 = BuildRoundedPath(v, r))
//            using (SolidBrush l1 = new SolidBrush(pal.ViewportFace))
//            {
//                g.FillPath(l1, p1);
//            }
//            using (GraphicsPath p2 = BuildRoundedPath(Deflate(v, 4f * lay.Dpi), Math.Max(2f, r - 4f * lay.Dpi)))
//            using (SolidBrush l2 = new SolidBrush(pal.ViewportFace))
//            {
//                g.FillPath(l2, p2);
//            }

//            if (v.Width > 1f && v.Height > 1f)
//            {
//                using (LinearGradientBrush lb = new LinearGradientBrush(
//                    v, Color.FromArgb(150, pal.BorderHighlight),
//                    Color.FromArgb(28, pal.BorderHighlight), 90f))
//                using (Pen border = new Pen(lb, 1.2f))
//                using (GraphicsPath bp = BuildRoundedPath(Deflate(v, 0.6f), Math.Max(2f, r - 0.6f)))
//                {
//                    g.DrawPath(border, bp);
//                }
//            }

//            int glow = _pressed ? 3 : (_hover && Enabled ? 2 : 0);
//            for (int i = glow; i >= 1; i--)
//            {
//                using (GraphicsPath gp = BuildRoundedPath(Expand(v, 1.4f * i), r + 1.4f * i))
//                using (Pen pen = new Pen(Color.FromArgb(80 / i, pal.Accent), 1.4f))
//                {
//                    g.DrawPath(pen, gp);
//                }
//            }
//        }

//        // STYLE 3 — MATERIAL FLAT
//        private void DrawMaterialCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            using (GraphicsPath face = BuildRoundedPath(lay.View, lay.ViewRadius))
//            using (SolidBrush fill = new SolidBrush(pal.ViewportFace))
//            {
//                g.FillPath(fill, face);
//            }
//        }

//        // STYLE 4 — SOFT NEUMORPHIC
//        private void DrawNeumorphicCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF v = lay.View;
//            float r = lay.ViewRadius;
//            bool pressed = _pressed && Enabled;
//            float off = (pressed ? 2.0f : (_hover && Enabled ? 2.8f : 3.4f)) * lay.Dpi;

//            Color tlC = pressed ? pal.ShadowDark : pal.ShadowLight;
//            Color brC = pressed ? pal.ShadowLight : pal.ShadowDark;

//            for (int i = 2; i >= 1; i--)
//            {
//                float o = off * (i == 2 ? 1.5f : 0.7f);
//                int pct = i == 2 ? 55 : 115;

//                using (GraphicsPath pTL = BuildRoundedPath(OffsetRect(v, -o, -o), r))
//                using (SolidBrush bTL = new SolidBrush(AlphaScale(tlC, pct)))
//                {
//                    g.FillPath(bTL, pTL);
//                }
//                using (GraphicsPath pBR = BuildRoundedPath(OffsetRect(v, o, o), r))
//                using (SolidBrush bBR = new SolidBrush(AlphaScale(brC, pct)))
//                {
//                    g.FillPath(bBR, pBR);
//                }
//            }

//            using (GraphicsPath face = BuildRoundedPath(v, r))
//            {
//                using (SolidBrush fill = new SolidBrush(pal.ViewportFace)) g.FillPath(fill, face);
//                using (Pen hair = new Pen(Color.FromArgb(70, pal.BorderHighlight), 1f)) g.DrawPath(hair, face);
//            }

//            using (GraphicsPath well = BuildRoundedPath(lay.Content, lay.ContentRadius))
//            {
//                using (SolidBrush wb = new SolidBrush(pal.ViewportWell)) g.FillPath(wb, well);

//                Color topCol = pressed ? pal.ShadowLight : pal.ShadowDark;
//                Color botCol = pressed ? pal.ShadowDark : pal.ShadowLight;

//                g.SetClip(new RectangleF(lay.Content.X, lay.Content.Y,
//                                         lay.Content.Width, lay.Content.Height * 0.5f));
//                using (Pen tp = new Pen(topCol, 2.2f)) g.DrawPath(tp, well);
//                g.ResetClip();

//                g.SetClip(new RectangleF(lay.Content.X, lay.Content.Y + lay.Content.Height * 0.5f,
//                                         lay.Content.Width, lay.Content.Height * 0.5f));
//                using (Pen bt = new Pen(botCol, 2.2f)) g.DrawPath(bt, well);
//                g.ResetClip();
//            }
//        }

//        // STYLE 5 — CYBERPUNK / INDUSTRIAL
//        private void DrawCyberCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF v = lay.View;
//            float r = lay.ViewRadius;

//            using (GraphicsPath face = BuildRoundedPath(v, r))
//            using (SolidBrush fill = new SolidBrush(pal.ViewportFace))
//            {
//                g.FillPath(fill, face);
//            }

//            int glow = _pressed ? 3 : (_hover && Enabled ? 2 : 1);
//            for (int i = glow; i >= 1; i--)
//            {
//                using (GraphicsPath gp = BuildRoundedPath(Expand(v, 1.4f * i + 1f), r + 1.4f * i + 1f))
//                using (Pen pen = new Pen(Color.FromArgb(70 / i, pal.Accent), 1.6f))
//                {
//                    g.DrawPath(pen, gp);
//                }
//            }

//            using (GraphicsPath core = BuildRoundedPath(Deflate(v, 0.8f), Math.Max(2f, r - 0.8f)))
//            using (Pen neon = new Pen(pal.Accent, 1.7f))
//            {
//                g.DrawPath(neon, core);
//            }

//            DrawCornerBrackets(g, v, 16f * lay.Dpi, 2.6f, Color.FromArgb(220, pal.AccentAlt));
//        }

//        // ----------------------------------------------------------------
//        //  LAYER 3 — CONTENT
//        // ----------------------------------------------------------------
//        private void DrawContentLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF c = lay.Content;
//            if (c.Width < 2f || c.Height < 2f) return;

//            using (GraphicsPath clip = BuildRoundedPath(c, lay.ContentRadius))
//            {
//                g.SetClip(clip);

//                using (SolidBrush well = new SolidBrush(pal.ViewportWell)) g.FillPath(well, clip);

//                if (!TryDrawImage(g, pal, lay))
//                    DrawEmptyState(g, pal, lay);

//                if (_controlStyle == ViewerStyle.DashboardPremium)
//                    DrawEdgeTicks(g, pal, c, lay.Dpi);

//                if (_controlStyle == ViewerStyle.Cyberpunk && _showScanlines)
//                    DrawScanlines(g, pal, c);

//                if (_controlStyle == ViewerStyle.MaterialFlat && Enabled)
//                {
//                    int alpha = _pressed ? 14 : (_hover ? 7 : 0);
//                    if (alpha > 0)
//                    {
//                        using (SolidBrush ov = new SolidBrush(Color.FromArgb(alpha, pal.HoverOverlay)))
//                            g.FillPath(ov, clip);
//                    }
//                }

//                g.ResetClip();
//            }
//        }

//        private bool TryDrawImage(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            if (_image == null) return false;

//            // ----------------------------------------------------------------
//            //  FIDELITY SWITCH — the single knob.
//            //
//            //  EnhanceImage == false (DEFAULT):
//            //      NearestNeighbor — every source pixel becomes an exact,
//            //      crisp block. Zero resampling blur. The image looks
//            //      EXACTLY as it is. This is also the fastest possible
//            //      blit GDI+ can perform (no filter kernel is evaluated).
//            //
//            //  EnhanceImage == true:
//            //      HighQualityBicubic — smooth, photo-grade resampling.
//            // ----------------------------------------------------------------
//            g.InterpolationMode = _enhanceImage
//                ? InterpolationMode.HighQualityBicubic
//                : InterpolationMode.NearestNeighbor;
//            g.PixelOffsetMode = PixelOffsetMode.Half;

//            bool drawn = false;
//            try
//            {
//                int iw = _image.Width;
//                int ih = _image.Height;

//                RectangleF screen = new RectangleF(_pan.X, _pan.Y, iw * _zoom, ih * _zoom);
//                RectangleF vis = RectangleF.Intersect(screen, lay.Content);

//                if (vis.Width > 0f && vis.Height > 0f)
//                {
//                    // VISIBLE-SOURCE CROP: sample only the on-screen portion of
//                    // the bitmap. At 8x zoom this is a ~64x smaller blit — the
//                    // single biggest perf win. Fidelity is untouched: the crop
//                    // only skips pixels that would land outside the viewport.
//                    RectangleF src = new RectangleF(
//                        (vis.X - _pan.X) / _zoom,
//                        (vis.Y - _pan.Y) / _zoom,
//                        vis.Width / _zoom,
//                        vis.Height / _zoom);
//                    src = ClampToSource(src, iw, ih);

//                    g.DrawImage(_image, vis, src, GraphicsUnit.Pixel);

//                    using (Pen ip = new Pen(Color.FromArgb(70, pal.TextDim), 1f))
//                        g.DrawRectangle(ip, screen.X, screen.Y, screen.Width, screen.Height);

//                    if (_controlStyle == ViewerStyle.Cyberpunk)
//                        DrawCornerBrackets(g, screen, 10f, 2.2f, Color.FromArgb(200, pal.AccentAlt));

//                    drawn = true;
//                }
//                else if (screen.Width > 0f && screen.Height > 0f)
//                {
//                    drawn = true;   // image exists but is fully panned off-screen
//                }
//            }
//            catch (ArgumentException)
//            {
//                _image = null;
//                _needsAutoFit = true;
//                _imageLostPending = true;
//                _hudSourceText = "—";
//            }
//            finally
//            {
//                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
//                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
//            }

//            return drawn;
//        }

//        private static RectangleF ClampToSource(RectangleF r, int iw, int ih)
//        {
//            float x = ClampF(r.X, 0f, iw);
//            float y = ClampF(r.Y, 0f, ih);
//            float right = ClampF(r.Right, x, iw);
//            float bottom = ClampF(r.Bottom, y, ih);
//            return RectangleF.FromLTRB(x, y, right, bottom);
//        }

//        private void DrawEmptyState(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF c = lay.Content;
//            float s = lay.Dpi;

//            using (GraphicsPath gridPath = new GraphicsPath())
//            using (Pen grid = new Pen(pal.Grid, 1f))
//            {
//                float step = 28f * s;
//                for (float x = c.X + step; x < c.Right; x += step)
//                {
//                    gridPath.AddLine(x, c.Y, x, c.Bottom);
//                    gridPath.StartFigure();
//                }
//                for (float y = c.Y + step; y < c.Bottom; y += step)
//                {
//                    gridPath.AddLine(c.X, y, c.Right, y);
//                    gridPath.StartFigure();
//                }
//                if (gridPath.PointCount > 0) g.DrawPath(grid, gridPath);
//            }

//            bool cyber = _controlStyle == ViewerStyle.Cyberpunk;
//            float cx = c.X + c.Width / 2f;
//            float cy = c.Y + c.Height / 2f;

//            RectangleF body = new RectangleF(cx - 43f * s, cy - 24f * s, 86f * s, 60f * s);
//            RectangleF bump = new RectangleF(cx - 15f * s, cy - 35f * s, 30f * s, 13f * s);
//            float lensR = 13f * s;
//            float innerR = 5f * s;
//            float lensCy = cy + 6f * s;
//            Color stroke = Color.FromArgb(165, pal.TextSecondary);

//            using (GraphicsPath bodyPath = BuildRoundedPath(body, 12f * s))
//            using (GraphicsPath bumpPath = BuildRoundedPath(bump, 5f * s))
//            using (Pen pen = new Pen(stroke, 2.6f))
//            {
//                g.DrawPath(pen, bumpPath);
//                g.DrawPath(pen, bodyPath);
//                using (SolidBrush fill = new SolidBrush(pal.ViewportWell))
//                {
//                    g.FillPath(fill, bumpPath);
//                    g.FillPath(fill, bodyPath);
//                }
//                g.DrawEllipse(pen, cx - lensR, lensCy - lensR, lensR * 2f, lensR * 2f);
//                using (SolidBrush fill2 = new SolidBrush(pal.ViewportWell))
//                    g.FillEllipse(fill2, cx - lensR, lensCy - lensR, lensR * 2f, lensR * 2f);
//                using (Pen inner = new Pen(Color.FromArgb(120, pal.TextSecondary), 2f))
//                    g.DrawEllipse(inner, cx - innerR, lensCy - innerR, innerR * 2f, innerR * 2f);
//            }

//            string title = cyber ? "NO SIGNAL" : "NO IMAGE LOADED";
//            string hint = cyber ? "AWAITING INPUT · DROP FILE TO SCAN"
//                                : "Drag & drop an image file, or assign the Image property";
//            float ty = body.Bottom + 22f * s;

//            // Cache-owned fonts — NEVER wrapped in using.
//            Font tf = GetFont(cyber, 10.5f, FontStyle.Bold);
//            Font sf = GetFont(false, 8.75f, FontStyle.Regular);

//            using (StringFormat fmt = new StringFormat())
//            {
//                fmt.Alignment = StringAlignment.Center;
//                fmt.LineAlignment = StringAlignment.Near;
//                fmt.FormatFlags |= StringFormatFlags.NoWrap;

//                using (SolidBrush tb = new SolidBrush(pal.TextSecondary))
//                    g.DrawString(title, tf, tb, cx, ty, fmt);
//                using (SolidBrush sb = new SolidBrush(pal.TextDim))
//                    g.DrawString(hint, sf, sb, cx, ty + 19f * s, fmt);
//            }
//        }

//        // ----------------------------------------------------------------
//        //  LAYER 5 — HUD READOUTS (cached text + cached measurement)
//        // ----------------------------------------------------------------
//        private void DrawHudLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            if (!_showHud || !Enabled) return;
//            RectangleF c = lay.Content;
//            if (c.Width < 160f || c.Height < 96f) return;

//            Font capFont = GetFont(false, 6.75f, FontStyle.Bold);
//            Font valFont = GetFont(true, 10f, FontStyle.Bold);

//            using (StringFormat fmt = new StringFormat(
//                StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces))
//            {
//                SizeF capS = MeasureCached(g, "SOURCE", capFont, fmt, ref _mCapSrc, ref _szCapSrc);
//                SizeF valS = MeasureCached(g, _hudSourceText, valFont, fmt, ref _mValSrc, ref _szValSrc);
//                DrawHudChip(g, pal, lay, ChipAnchor.TopLeft, "SOURCE", _hudSourceText,
//                            capFont, valFont, capS, valS,
//                            _image != null ? pal.Accent : pal.TextDim, false, pal.TextDim);

//                bool flat = _controlStyle == ViewerStyle.MaterialFlat;
//                Color zone = ZoomZoneColor(pal);

//                SizeF capZ = MeasureCached(g, "ZOOM", capFont, fmt, ref _mCapZoom, ref _szCapZoom);
//                SizeF valZ = MeasureCached(g, _hudZoomText, valFont, fmt, ref _mValZoom, ref _szValZoom);
//                DrawHudChip(g, pal, lay, ChipAnchor.BottomRight, "ZOOM", _hudZoomText,
//                            capFont, valFont, capZ, valZ,
//                            flat ? (Color?)null : zone, true, flat ? pal.HudFore : zone);

//                if (_hover && _image != null && _hudProbeText.Length > 0)
//                {
//                    SizeF capP = MeasureCached(g, "PROBE", capFont, fmt, ref _mCapProbe, ref _szCapProbe);
//                    SizeF valP = MeasureCached(g, _hudProbeText, valFont, fmt, ref _mValProbe, ref _szValProbe);
//                    DrawHudChip(g, pal, lay, ChipAnchor.BottomLeft, "PROBE", _hudProbeText,
//                                capFont, valFont, capP, valP,
//                                null, false, pal.TextDim);
//                }
//            }
//        }

//        private SizeF MeasureCached(Graphics g, string text, Font font, StringFormat fmt,
//                                    ref string key, ref SizeF size)
//        {
//            if (!ReferenceEquals(key, text))
//            {
//                size = g.MeasureString(text, font, int.MaxValue, fmt);
//                key = text;
//            }
//            return size;
//        }

//        private void DrawHudChip(Graphics g, ViewerPalette pal, ViewerLayout lay, ChipAnchor anchor,
//                                 string caption, string value, Font capFont, Font valFont,
//                                 SizeF capS, SizeF valS,
//                                 Color? dot, bool showBar, Color barColor)
//        {
//            float dpi = lay.Dpi;
//            RectangleF c = lay.Content;
//            float padX = 9f * dpi;
//            float padY = 6f * dpi;

//            float dotW = dot.HasValue ? 14f * dpi : 0f;
//            float w = Math.Max(capS.Width, valS.Width) + dotW + padX * 2f;
//            float h = padY + capS.Height + 2f * dpi + valS.Height + (showBar ? 6f * dpi : 0f) + padY;

//            if (w > c.Width - 12f || h > c.Height - 12f) return;

//            float inset = 10f * dpi;
//            PointF loc;
//            switch (anchor)
//            {
//                case ChipAnchor.BottomLeft: loc = new PointF(c.X + inset, c.Bottom - inset - h); break;
//                case ChipAnchor.BottomRight: loc = new PointF(c.Right - inset - w, c.Bottom - inset - h); break;
//                default: loc = new PointF(c.X + inset, c.Y + inset); break;
//            }
//            RectangleF chip = new RectangleF(loc.X, loc.Y, w, h);

//            using (GraphicsPath path = BuildRoundedPath(chip, 7f * dpi))
//            {
//                using (SolidBrush bg = new SolidBrush(pal.HudBack)) g.FillPath(bg, path);
//                if (pal.HudBorder.A > 0)
//                {
//                    using (Pen bp = new Pen(pal.HudBorder, 1f)) g.DrawPath(bp, path);
//                }

//                if (_controlStyle == ViewerStyle.SoftNeumorphic)
//                {
//                    g.SetClip(new RectangleF(chip.X, chip.Y, chip.Width, chip.Height * 0.5f));
//                    using (Pen tp = new Pen(pal.ShadowDark, 1f)) g.DrawPath(tp, path);
//                    g.ResetClip();
//                    g.SetClip(new RectangleF(chip.X, chip.Y + chip.Height * 0.5f,
//                                             chip.Width, chip.Height * 0.5f));
//                    using (Pen bt = new Pen(pal.ShadowLight, 1f)) g.DrawPath(bt, path);
//                    g.ResetClip();
//                }
//            }

//            using (StringFormat fmt = new StringFormat(StringFormatFlags.NoWrap))
//            {
//                float tx = chip.X + padX;
//                using (SolidBrush capBrush = new SolidBrush(pal.HudForeDim))
//                    g.DrawString(caption, capFont, capBrush, tx, chip.Y + padY, fmt);

//                float vy = chip.Y + padY + capS.Height + 2f * dpi;
//                using (SolidBrush valBrush = new SolidBrush(pal.HudFore))
//                    g.DrawString(value, valFont, valBrush, tx, vy, fmt);

//                if (dot.HasValue)
//                {
//                    float d = 6.8f * dpi;
//                    using (SolidBrush db = new SolidBrush(dot.Value))
//                        g.FillEllipse(db, chip.Right - padX - d, vy + valS.Height / 2f - d / 2f, d, d);
//                }

//                if (showBar)
//                {
//                    float by = chip.Bottom - padY - 2.2f * dpi;
//                    float trackW = w - padX * 2f;

//                    using (GraphicsPath track = BuildRoundedPath(
//                        new RectangleF(tx, by, trackW, 3f * dpi), 1.5f * dpi))
//                    using (SolidBrush tb = new SolidBrush(Color.FromArgb(70, pal.HudFore)))
//                        g.FillPath(tb, track);

//                    float t = ZoomBarT();
//                    if (t > 0.01f)
//                    {
//                        using (GraphicsPath fill = BuildRoundedPath(
//                            new RectangleF(tx, by, trackW * t, 3f * dpi), 1.5f * dpi))
//                        using (SolidBrush fb = new SolidBrush(barColor))
//                            g.FillPath(fb, fill);
//                    }
//                }
//            }
//        }

//        // ----------------------------------------------------------------
//        //  LAYER 6 — STATE CHROME
//        // ----------------------------------------------------------------
//        private void DrawStateLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            if (_showCrosshair && _hover && !_panning && Enabled &&
//                (_controlStyle == ViewerStyle.Cyberpunk || _controlStyle == ViewerStyle.DashboardPremium))
//            {
//                using (GraphicsPath clip = BuildRoundedPath(lay.Content, lay.ContentRadius))
//                {
//                    g.SetClip(clip);
//                    float px = _hoverPos.X;
//                    float py = _hoverPos.Y;
//                    float gap = 11f * lay.Dpi;

//                    using (Pen pen = new Pen(Color.FromArgb(115, pal.Accent), 1f))
//                    {
//                        g.DrawLine(pen, lay.Content.X, py, px - gap, py);
//                        g.DrawLine(pen, px + gap, py, lay.Content.Right, py);
//                        g.DrawLine(pen, px, lay.Content.Y, px, py - gap);
//                        g.DrawLine(pen, px, py + gap, px, lay.Content.Bottom);
//                    }
//                    using (Pen cp = new Pen(Color.FromArgb(190, pal.Accent), 1.2f))
//                        g.DrawEllipse(cp, px - 3.5f, py - 3.5f, 7f, 7f);

//                    g.ResetClip();
//                }
//            }

//            if (Focused && Enabled)
//            {
//                using (GraphicsPath fp = BuildRoundedPath(Expand(lay.View, 3f), lay.ViewRadius + 3f))
//                using (Pen pen = new Pen(Color.FromArgb(215, pal.FocusRing), 1.4f))
//                    g.DrawPath(pen, fp);
//            }

//            if (!Enabled)
//            {
//                using (SolidBrush veil = new SolidBrush(Color.FromArgb(110, pal.Canvas)))
//                    g.FillRectangle(veil, 0f, 0f, Width, Height);
//            }
//        }

//        // ----------------------------------------------------------------
//        //  BATCHED MICRO-DETAILS — one DrawPath call instead of hundreds
//        // ----------------------------------------------------------------
//        private static void DrawEdgeTicks(Graphics g, ViewerPalette pal, RectangleF c, float dpi)
//        {
//            SmoothingMode prev = g.SmoothingMode;
//            g.SmoothingMode = SmoothingMode.None;

//            using (GraphicsPath minorPath = new GraphicsPath())
//            using (GraphicsPath majorPath = new GraphicsPath())
//            using (Pen minor = new Pen(Color.FromArgb(110, pal.Tick), 1f))
//            using (Pen major = new Pen(Color.FromArgb(210, pal.Tick), 1f))
//            {
//                float step = 9f * dpi;

//                int i = 0;
//                for (float x = c.X + 3f; x < c.Right - 2f; x += step)
//                {
//                    GraphicsPath target = (i % 5 == 0) ? majorPath : minorPath;
//                    float len = (i % 5 == 0) ? 7f * dpi : 4f * dpi;
//                    target.AddLine(x, c.Y + 1f, x, c.Y + 1f + len);
//                    target.StartFigure();
//                    i++;
//                }

//                i = 0;
//                for (float y = c.Y + 3f; y < c.Bottom - 2f; y += step)
//                {
//                    GraphicsPath target = (i % 5 == 0) ? majorPath : minorPath;
//                    float len = (i % 5 == 0) ? 7f * dpi : 4f * dpi;
//                    target.AddLine(c.X + 1f, y, c.X + 1f + len, y);
//                    target.StartFigure();
//                    i++;
//                }

//                if (minorPath.PointCount > 0) g.DrawPath(minor, minorPath);
//                if (majorPath.PointCount > 0) g.DrawPath(major, majorPath);
//            }

//            g.SmoothingMode = prev;
//        }

//        private static void DrawScanlines(Graphics g, ViewerPalette pal, RectangleF c)
//        {
//            SmoothingMode prev = g.SmoothingMode;
//            g.SmoothingMode = SmoothingMode.None;

//            using (GraphicsPath path = new GraphicsPath())
//            using (Pen pen = new Pen(Color.FromArgb(9, pal.Accent), 1f))
//            {
//                for (float y = c.Y + 2f; y < c.Bottom; y += 4f)
//                {
//                    path.AddLine(c.X, y, c.Right, y);
//                    path.StartFigure();
//                }
//                if (path.PointCount > 0) g.DrawPath(pen, path);
//            }

//            g.SmoothingMode = prev;
//        }

//        private static void DrawCornerBrackets(Graphics g, RectangleF rect, float len, float width, Color color)
//        {
//            using (Pen pen = new Pen(color, width))
//            {
//                pen.StartCap = LineCap.Round;
//                pen.EndCap = LineCap.Round;

//                float l = rect.Left, t = rect.Top, r = rect.Right, b = rect.Bottom;

//                g.DrawLine(pen, l, t, l + len, t); g.DrawLine(pen, l, t, l, t + len);
//                g.DrawLine(pen, r, t, r - len, t); g.DrawLine(pen, r, t, r, t + len);
//                g.DrawLine(pen, l, b, l + len, b); g.DrawLine(pen, l, b, l, b - len);
//                g.DrawLine(pen, r, b, r - len, b); g.DrawLine(pen, r, b, r, b - len);
//            }
//        }

//        private static void DrawSoftShadow(Graphics g, RectangleF rect, float radius, Color shadow,
//                                           float depth, int steps)
//        {
//            int layerAlpha = Math.Max(4, shadow.A / steps);
//            for (int i = steps; i >= 1; i--)
//            {
//                float off = 1f + (depth * (i - 1) / steps);
//                using (GraphicsPath p = BuildRoundedPath(OffsetRect(rect, 0f, off), radius + i * 0.7f))
//                using (SolidBrush b = new SolidBrush(Color.FromArgb(layerAlpha, shadow)))
//                {
//                    g.FillPath(b, p);
//                }
//            }
//        }

//        // ----------------------------------------------------------------
//        //  LAYOUT ENGINE
//        // ----------------------------------------------------------------
//        private ViewerLayout ComputeLayout()
//        {
//            float dpi = Math.Max(1f, _dpiScale);

//            float padMul = _controlStyle == ViewerStyle.MaterialFlat ? 1.45f : 1f;
//            float basePad = Math.Min(Width, Height) * 0.035f;
//            float pad = Math.Max(11f * dpi, Math.Min(26f * dpi, basePad)) * padMul;

//            float maxPad = Math.Min(Math.Max(0f, (Width - 2f) / 2f),
//                                    Math.Max(0f, (Height - 2f) / 2f));
//            pad = Math.Min(pad, maxPad);

//            RectangleF view = RectangleF.FromLTRB(pad, pad,
//                Math.Max(pad + 2f, Width - pad),
//                Math.Max(pad + 2f, Height - pad));

//            float radius;
//            float inset;
//            switch (_controlStyle)
//            {
//                case ViewerStyle.FluentGlass: radius = 14f * dpi; inset = 1.5f * dpi; break;
//                case ViewerStyle.MaterialFlat: radius = 3f * dpi; inset = 0.75f * dpi; break;
//                case ViewerStyle.SoftNeumorphic: radius = 20f * dpi; inset = 11f * dpi; break;
//                case ViewerStyle.Cyberpunk: radius = 8f * dpi; inset = 1.25f * dpi; break;
//                default: radius = 10f * dpi; inset = 1.25f * dpi; break;
//            }

//            float maxInset = Math.Min(Math.Max(0f, (view.Width - 2f) / 2f),
//                                      Math.Max(0f, (view.Height - 2f) / 2f));
//            inset = Math.Min(inset, maxInset);

//            float cr = Math.Max(1f, radius - inset - 0.5f);
//            float minDim = Math.Min(view.Width, view.Height);
//            if (cr * 2f > minDim) cr = minDim / 2f;

//            return new ViewerLayout
//            {
//                View = view,
//                Content = Deflate(view, inset),
//                ViewRadius = radius,
//                ContentRadius = cr,
//                Dpi = dpi
//            };
//        }

//        private Color ZoomZoneColor(ViewerPalette pal)
//        {
//            if (_zoom < 2f) return pal.ZoneGood;
//            if (_zoom < 8f) return pal.ZoneWarn;
//            return pal.ZoneHot;
//        }

//        private float ZoomBarT()
//        {
//            double lo = Math.Log10((double)_minZoom);
//            double hi = Math.Log10((double)_maxZoom);
//            if (hi - lo < 0.0001) return 0f;

//            double t = (Math.Log10((double)_zoom) - lo) / (hi - lo);
//            if (t < 0.0) t = 0.0;
//            if (t > 1.0) t = 1.0;
//            return (float)t;
//        }

//        // ----------------------------------------------------------------
//        //  GEOMETRY & COLOR PRIMITIVES
//        // ----------------------------------------------------------------
//        private static GraphicsPath BuildRoundedPath(RectangleF rect, float radius)
//        {
//            GraphicsPath path = new GraphicsPath();

//            float w = rect.Width;
//            float h = rect.Height;
//            if (float.IsNaN(w) || float.IsNaN(h) || float.IsInfinity(w) || float.IsInfinity(h))
//                return path;
//            if (w < 0f) w = 0f;
//            if (h < 0f) h = 0f;
//            if (float.IsNaN(radius) || radius < 0f) radius = 0f;
//            if (w < 0.5f || h < 0.5f) return path;

//            rect = new RectangleF(rect.X, rect.Y, w, h);

//            float maxR = Math.Min(w, h) / 2f;
//            if (radius < 0.5f) { path.AddRectangle(rect); return path; }
//            if (radius > maxR) radius = maxR;

//            float d = radius * 2f;
//            path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
//            path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
//            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
//            path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
//            path.CloseFigure();
//            return path;
//        }

//        private static RectangleF Deflate(RectangleF r, float v)
//        { return new RectangleF(r.X + v, r.Y + v, r.Width - 2f * v, r.Height - 2f * v); }

//        private static RectangleF Expand(RectangleF r, float v)
//        { return new RectangleF(r.X - v, r.Y - v, r.Width + 2f * v, r.Height + 2f * v); }

//        private static RectangleF OffsetRect(RectangleF r, float dx, float dy)
//        { return new RectangleF(r.X + dx, r.Y + dy, r.Width, r.Height); }

//        private static Color Lerp(Color a, Color b, float t)
//        {
//            return Color.FromArgb(
//                a.R + (int)((b.R - a.R) * t),
//                a.G + (int)((b.G - a.G) * t),
//                a.B + (int)((b.B - a.B) * t));
//        }

//        private static Color AlphaScale(Color c, int percent)
//        {
//            int a = (int)(c.A * percent / 100.0);
//            if (a > 255) a = 255;
//            return Color.FromArgb(a, c);
//        }

//        private static float ClampF(float v, float lo, float hi)
//        {
//            if (float.IsNaN(v)) return lo;
//            return v < lo ? lo : (v > hi ? hi : v);
//        }

//        // ----------------------------------------------------------------
//        //  HUD TEXT UPDATES
//        // ----------------------------------------------------------------
//        private void UpdateZoomText()
//        {
//            _hudZoomText = (_zoom * 100f).ToString("0.#") + " %";
//        }

//        private void UpdateSourceText()
//        {
//            try
//            {
//                _hudSourceText = _image != null
//                    ? string.Format("{0} × {1}", _image.Width, _image.Height)
//                    : "—";
//            }
//            catch { _hudSourceText = "—"; }
//        }

//        private bool UpdateProbeText()
//        {
//            if (_image == null) return false;
//            try
//            {
//                PointF ip = ScreenToImageF(_hoverPos);
//                int px = (int)Math.Round(ip.X);
//                int py = (int)Math.Round(ip.Y);
//                if (px != _probeX || py != _probeY)
//                {
//                    _probeX = px;
//                    _probeY = py;
//                    _hudProbeText = string.Format("X {0}   Y {1}", px, py);
//                    return true;
//                }
//            }
//            catch { }
//            return false;
//        }

//        // ----------------------------------------------------------------
//        //  PALETTE ENGINE — 5 styles × 2 themes
//        // ----------------------------------------------------------------
//        private static ViewerPalette BuildPalette(ThemeMode theme, ViewerStyle style)
//        {
//            bool dark = theme == ThemeMode.Dark;
//            switch (style)
//            {
//                case ViewerStyle.FluentGlass: return GlassPalette(dark);
//                case ViewerStyle.MaterialFlat: return MaterialPalette(dark);
//                case ViewerStyle.SoftNeumorphic: return NeumorphicPalette(dark);
//                case ViewerStyle.Cyberpunk: return CyberPalette();
//                default: return DashboardPalette(dark);
//            }
//        }

//        private static Color C(int rgb)
//        { return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF); }

//        private static ViewerPalette DashboardPalette(bool dark)
//        {
//            if (dark)
//            {
//                return new ViewerPalette
//                {
//                    Canvas = C(0x0F1218),
//                    BackdropGlow = Color.FromArgb(0, Color.White),
//                    ViewportFace = C(0x151A22),
//                    ViewportWell = C(0x11151C),
//                    Border = C(0x28303E),
//                    BorderHighlight = Color.White,
//                    TextPrimary = C(0xE8EDF5),
//                    TextSecondary = C(0x97A3B5),
//                    TextDim = C(0x5E6A80),
//                    Accent = C(0x4C8DFF),
//                    AccentAlt = C(0x38BDF8),
//                    ZoneGood = C(0x2FBF71),
//                    ZoneWarn = C(0xF5A524),
//                    ZoneHot = C(0xEF4B4B),
//                    Grid = C(0x1B202A),
//                    Tick = C(0x7A87A0),
//                    HudBack = Color.FromArgb(190, C(0x0B0E14)),
//                    HudBorder = Color.FromArgb(60, Color.White),
//                    HudFore = C(0xE8EDF5),
//                    HudForeDim = C(0x97A3B5),
//                    Shadow = Color.FromArgb(150, Color.Black),
//                    ShadowLight = Color.FromArgb(0, Color.White),
//                    ShadowDark = Color.FromArgb(0, Color.Black),
//                    HoverOverlay = Color.White,
//                    FocusRing = C(0x4C8DFF)
//                };
//            }
//            return new ViewerPalette
//            {
//                Canvas = C(0xF3F5F9),
//                BackdropGlow = Color.FromArgb(0, Color.White),
//                ViewportFace = C(0xFFFFFF),
//                ViewportWell = C(0xFAFBFE),
//                Border = C(0xDCE3EE),
//                BorderHighlight = Color.White,
//                TextPrimary = C(0x16202F),
//                TextSecondary = C(0x5F6C7E),
//                TextDim = C(0x9AA6B8),
//                Accent = C(0x2563EB),
//                AccentAlt = C(0x0EA5E9),
//                ZoneGood = C(0x16A34A),
//                ZoneWarn = C(0xD97706),
//                ZoneHot = C(0xDC2626),
//                Grid = C(0xE7ECF4),
//                Tick = C(0xB9C4D4),
//                HudBack = Color.FromArgb(172, C(0xF9FBFE)),
//                HudBorder = Color.FromArgb(170, Color.White),
//                HudFore = C(0x16202F),
//                HudForeDim = C(0x5F6C7E),
//                Shadow = Color.FromArgb(30, C(0x0E1626)),
//                ShadowLight = Color.FromArgb(0, Color.White),
//                ShadowDark = Color.FromArgb(0, Color.Black),
//                HoverOverlay = Color.Black,
//                FocusRing = C(0x2563EB)
//            };
//        }

//        private static ViewerPalette GlassPalette(bool dark)
//        {
//            if (dark)
//            {
//                return new ViewerPalette
//                {
//                    Canvas = C(0x171B24),
//                    BackdropGlow = Color.FromArgb(38, C(0x00C8FF)),
//                    ViewportFace = Color.FromArgb(46, C(0x2A3140)),
//                    ViewportWell = Color.FromArgb(235, C(0x1B2029)),
//                    Border = Color.FromArgb(0, Color.White),
//                    BorderHighlight = Color.White,
//                    TextPrimary = C(0xEBF1FB),
//                    TextSecondary = C(0x9BA8BD),
//                    TextDim = C(0x63718A),
//                    Accent = C(0x00C8FF),
//                    AccentAlt = C(0x7C6CFF),
//                    ZoneGood = C(0x1FBF6B),
//                    ZoneWarn = C(0xF5A524),
//                    ZoneHot = C(0xF4506C),
//                    Grid = Color.FromArgb(26, C(0x6E82A6)),
//                    Tick = Color.FromArgb(80, C(0x5D6F92)),
//                    HudBack = Color.FromArgb(125, C(0x0E1219)),
//                    HudBorder = Color.FromArgb(140, Color.White),
//                    HudFore = C(0xEBF1FB),
//                    HudForeDim = C(0x9BA8BD),
//                    Shadow = Color.FromArgb(80, Color.Black),
//                    ShadowLight = Color.FromArgb(0, Color.White),
//                    ShadowDark = Color.FromArgb(0, Color.Black),
//                    HoverOverlay = C(0x00C8FF),
//                    FocusRing = C(0x00C8FF)
//                };
//            }
//            return new ViewerPalette
//            {
//                Canvas = C(0xE8EEF7),
//                BackdropGlow = Color.FromArgb(70, Color.White),
//                ViewportFace = Color.FromArgb(52, Color.White),
//                ViewportWell = Color.FromArgb(150, Color.White),
//                Border = Color.FromArgb(0, Color.White),
//                BorderHighlight = Color.White,
//                TextPrimary = C(0x0F2440),
//                TextSecondary = C(0x5D6F8C),
//                TextDim = C(0x8FA0B8),
//                Accent = C(0x0A84FF),
//                AccentAlt = C(0x7C5CFF),
//                ZoneGood = C(0x0E9F6E),
//                ZoneWarn = C(0xF0A63A),
//                ZoneHot = C(0xE5484D),
//                Grid = Color.FromArgb(24, C(0x2A3C5E)),
//                Tick = Color.FromArgb(80, C(0x334666)),
//                HudBack = Color.FromArgb(150, Color.White),
//                HudBorder = Color.FromArgb(200, Color.White),
//                HudFore = C(0x0F2440),
//                HudForeDim = C(0x5D6F8C),
//                Shadow = Color.FromArgb(55, C(0x1E2E4C)),
//                ShadowLight = Color.FromArgb(0, Color.White),
//                ShadowDark = Color.FromArgb(0, Color.Black),
//                HoverOverlay = C(0x0A84FF),
//                FocusRing = C(0x0A84FF)
//            };
//        }

//        private static ViewerPalette MaterialPalette(bool dark)
//        {
//            if (dark)
//            {
//                return new ViewerPalette
//                {
//                    Canvas = C(0x121212),
//                    BackdropGlow = Color.FromArgb(0, Color.White),
//                    ViewportFace = C(0x1E1E1E),
//                    ViewportWell = C(0x1A1A1A),
//                    Border = Color.FromArgb(0, Color.White),
//                    BorderHighlight = Color.FromArgb(0, Color.White),
//                    TextPrimary = C(0xF2F2F2),
//                    TextSecondary = C(0xB0B0B0),
//                    TextDim = C(0x8A8A8A),
//                    Accent = C(0xBB86FC),
//                    AccentAlt = C(0x03DAC6),
//                    ZoneGood = C(0x66BB6A),
//                    ZoneWarn = C(0xFFB74D),
//                    ZoneHot = C(0xEF5350),
//                    Grid = C(0x232323),
//                    Tick = C(0x2E2E2E),
//                    HudBack = C(0xBB86FC),
//                    HudBorder = Color.FromArgb(0, Color.White),
//                    HudFore = C(0x141218),
//                    HudForeDim = Color.FromArgb(150, C(0x141218)),
//                    Shadow = Color.FromArgb(0, Color.Black),
//                    ShadowLight = Color.FromArgb(0, Color.White),
//                    ShadowDark = Color.FromArgb(0, Color.Black),
//                    HoverOverlay = Color.White,
//                    FocusRing = C(0xBB86FC)
//                };
//            }
//            return new ViewerPalette
//            {
//                Canvas = C(0xF5F5F5),
//                BackdropGlow = Color.FromArgb(0, Color.White),
//                ViewportFace = C(0xFFFFFF),
//                ViewportWell = C(0xFFFFFF),
//                Border = Color.FromArgb(0, Color.White),
//                BorderHighlight = Color.FromArgb(0, Color.White),
//                TextPrimary = C(0x212121),
//                TextSecondary = C(0x757575),
//                TextDim = C(0x9E9E9E),
//                Accent = C(0x6200EE),
//                AccentAlt = C(0x03DAC6),
//                ZoneGood = C(0x2E7D32),
//                ZoneWarn = C(0xEF6C00),
//                ZoneHot = C(0xC62828),
//                Grid = C(0xEEEEEE),
//                Tick = C(0xE0E0E0),
//                HudBack = C(0x6200EE),
//                HudBorder = Color.FromArgb(0, Color.White),
//                HudFore = Color.White,
//                HudForeDim = Color.FromArgb(178, Color.White),
//                Shadow = Color.FromArgb(0, Color.Black),
//                ShadowLight = Color.FromArgb(0, Color.White),
//                ShadowDark = Color.FromArgb(0, Color.Black),
//                HoverOverlay = Color.Black,
//                FocusRing = C(0x6200EE)
//            };
//        }

//        private static ViewerPalette NeumorphicPalette(bool dark)
//        {
//            if (dark)
//            {
//                return new ViewerPalette
//                {
//                    Canvas = C(0x2A2F3A),
//                    BackdropGlow = Color.FromArgb(0, Color.White),
//                    ViewportFace = C(0x2A2F3A),
//                    ViewportWell = C(0x252A33),
//                    Border = Color.FromArgb(0, Color.White),
//                    BorderHighlight = Color.FromArgb(30, Color.White),
//                    TextPrimary = C(0xD6DCE8),
//                    TextSecondary = C(0x8791A6),
//                    TextDim = C(0x5F6879),
//                    Accent = C(0x7C8CF8),
//                    AccentAlt = C(0x9EA8FA),
//                    ZoneGood = C(0x58B87E),
//                    ZoneWarn = C(0xC9A55A),
//                    ZoneHot = C(0xC96A5E),
//                    Grid = Color.FromArgb(24, C(0x6A748C)),
//                    Tick = Color.FromArgb(60, C(0x6A748C)),
//                    HudBack = C(0x2A2F3A),
//                    HudBorder = Color.FromArgb(0, Color.White),
//                    HudFore = C(0xD6DCE8),
//                    HudForeDim = C(0x8791A6),
//                    Shadow = Color.FromArgb(70, C(0x1C2028)),
//                    ShadowLight = Color.FromArgb(130, C(0x3B4250)),
//                    ShadowDark = Color.FromArgb(160, C(0x1C2028)),
//                    HoverOverlay = Color.White,
//                    FocusRing = C(0x7C8CF8)
//                };
//            }
//            return new ViewerPalette
//            {
//                Canvas = C(0xE4E9F1),
//                BackdropGlow = Color.FromArgb(0, Color.White),
//                ViewportFace = C(0xE4E9F1),
//                ViewportWell = C(0xDCE2EC),
//                Border = Color.FromArgb(0, Color.White),
//                BorderHighlight = Color.White,
//                TextPrimary = C(0x47536E),
//                TextSecondary = C(0x8B96AD),
//                TextDim = C(0xA9B3C7),
//                Accent = C(0x6C7BF2),
//                AccentAlt = C(0x9BA6F5),
//                ZoneGood = C(0x6FBF8E),
//                ZoneWarn = C(0xD2A24C),
//                ZoneHot = C(0xD26A5C),
//                Grid = Color.FromArgb(26, C(0x9FACC6)),
//                Tick = Color.FromArgb(60, C(0x9FACC6)),
//                HudBack = C(0xE4E9F1),
//                HudBorder = Color.FromArgb(0, Color.White),
//                HudFore = C(0x47536E),
//                HudForeDim = C(0x8B96AD),
//                Shadow = Color.FromArgb(60, C(0xC3CDDF)),
//                ShadowLight = Color.FromArgb(210, Color.White),
//                ShadowDark = Color.FromArgb(170, C(0xC3CDDF)),
//                HoverOverlay = Color.White,
//                FocusRing = C(0x6C7BF2)
//            };
//        }

//        private static ViewerPalette CyberPalette()
//        {
//            return new ViewerPalette
//            {
//                Canvas = C(0x05070A),
//                BackdropGlow = Color.FromArgb(22, C(0x00E5FF)),
//                ViewportFace = C(0x0A0D13),
//                ViewportWell = C(0x080A10),
//                Border = C(0x00E5FF),
//                BorderHighlight = Color.FromArgb(0, Color.White),
//                TextPrimary = C(0xD9F5FF),
//                TextSecondary = C(0x6E8CA0),
//                TextDim = C(0x45586A),
//                Accent = C(0x00E5FF),
//                AccentAlt = C(0xFF2E97),
//                ZoneGood = C(0x00FFA3),
//                ZoneWarn = C(0xFFE066),
//                ZoneHot = C(0xFF3860),
//                Grid = Color.FromArgb(22, C(0x00E5FF)),
//                Tick = Color.FromArgb(80, C(0x00E5FF)),
//                HudBack = Color.FromArgb(200, C(0x05080E)),
//                HudBorder = Color.FromArgb(90, C(0x00E5FF)),
//                HudFore = C(0xD9F5FF),
//                HudForeDim = C(0x5E7E93),
//                Shadow = Color.FromArgb(140, Color.Black),
//                ShadowLight = Color.FromArgb(0, Color.White),
//                ShadowDark = Color.FromArgb(0, Color.Black),
//                HoverOverlay = C(0x00E5FF),
//                FocusRing = C(0xFF2E97)
//            };
//        }

//        // ----------------------------------------------------------------
//        //  FONT ENGINE
//        // ----------------------------------------------------------------
//        private static string UiFamily
//        {
//            get
//            {
//                if (_uiFamily == null) _uiFamily = ResolveFontFamily(UiFontCandidates);
//                return _uiFamily;
//            }
//        }

//        private static string MonoFamily
//        {
//            get
//            {
//                if (_monoFamily == null) _monoFamily = ResolveFontFamily(MonoFontCandidates);
//                return _monoFamily;
//            }
//        }

//        private static string ResolveFontFamily(string[] candidates)
//        {
//            try
//            {
//                using (InstalledFontCollection installed = new InstalledFontCollection())
//                {
//                    HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
//                    foreach (FontFamily family in installed.Families) names.Add(family.Name);
//                    foreach (string candidate in candidates)
//                        if (names.Contains(candidate)) return candidate;
//                }
//            }
//            catch { }
//            return candidates[candidates.Length - 1];
//        }

//        private Font GetFont(bool mono, float sizePt, FontStyle style)
//        {
//            string family = mono ? MonoFamily : UiFamily;
//            string key = family + "|" + sizePt.ToString("0.###") + "|" + (int)style;

//            Font font;
//            if (_fontCache.TryGetValue(key, out font))
//                return font;   // cache-owned — callers must NOT dispose

//            try
//            {
//                font = new Font(family, sizePt, style, GraphicsUnit.Point);
//            }
//            catch
//            {
//                font = new Font(FontFamily.GenericSansSerif, sizePt, style, GraphicsUnit.Point);
//            }
//            _fontCache[key] = font;
//            return font;
//        }

//        // ----------------------------------------------------------------
//        //  INPUT — MOUSE
//        // ----------------------------------------------------------------
//        protected override void OnMouseEnter(EventArgs e)
//        {
//            base.OnMouseEnter(e);
//            _hover = true;
//            if (!IsDesignTime && Enabled && !Focused) Focus();
//            InvalidateChrome();          // hover chrome (glass glow, dash border lerp)
//        }

//        protected override void OnMouseLeave(EventArgs e)
//        {
//            base.OnMouseLeave(e);
//            _hover = false;
//            InvalidateChrome();
//        }

//        protected override void OnMouseDown(MouseEventArgs e)
//        {
//            base.OnMouseDown(e);
//            if (!Enabled) return;
//            Focus();

//            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
//            {
//                _panning = true;
//                _pressed = true;
//                _lastMouse = e.Location;
//                Cursor = Cursors.SizeAll;
//                InvalidateChrome();      // pressed chrome (neu depth inversion, glow rings)
//            }
//        }

//        protected override void OnMouseMove(MouseEventArgs e)
//        {
//            base.OnMouseMove(e);
//            _hoverPos = e.Location;
//            if (!Enabled) return;

//            bool needsRepaint = false;

//            if (_panning)
//            {
//                _pan.X += e.X - _lastMouse.X;
//                _pan.Y += e.Y - _lastMouse.Y;
//                _lastMouse = e.Location;
//                needsRepaint = true;
//            }

//            if (UpdateProbeText()) needsRepaint = true;

//            // Crosshair tracks the cursor pixel-for-pixel → repaint on move
//            // when active. Paint is cheap now (chrome blit + cropped blit).
//            bool crosshairActive = _showCrosshair &&
//                (_controlStyle == ViewerStyle.DashboardPremium || _controlStyle == ViewerStyle.Cyberpunk);

//            if (needsRepaint || crosshairActive) Invalidate();
//        }

//        protected override void OnMouseUp(MouseEventArgs e)
//        {
//            base.OnMouseUp(e);
//            _panning = false;
//            _pressed = false;
//            Cursor = Cursors.Cross;
//            InvalidateChrome();
//        }

//        protected override void OnMouseCaptureChanged(EventArgs e)
//        {
//            base.OnMouseCaptureChanged(e);
//            if (_panning)
//            {
//                _panning = false;
//                _pressed = false;
//                Cursor = Cursors.Cross;
//                InvalidateChrome();
//            }
//        }

//        protected override void OnMouseWheel(MouseEventArgs e)
//        {
//            if (e is HandledMouseEventArgs hme) hme.Handled = true;
//            base.OnMouseWheel(e);

//            if (!Enabled || _image == null) return;

//            float factor = e.Delta > 0 ? 1.12f : 1f / 1.12f;
//            SetZoom(_zoom * factor, new PointF(e.X, e.Y));
//        }

//        protected override void OnDoubleClick(EventArgs e)
//        {
//            base.OnDoubleClick(e);
//            AutoFit();
//        }

//        // ----------------------------------------------------------------
//        //  INPUT — KEYBOARD
//        // ----------------------------------------------------------------
//        protected override bool IsInputKey(Keys keyData)
//        {
//            switch (keyData & Keys.KeyCode)
//            {
//                case Keys.Left:
//                case Keys.Right:
//                case Keys.Up:
//                case Keys.Down:
//                    return true;
//            }
//            return base.IsInputKey(keyData);
//        }

//        protected override void OnKeyDown(KeyEventArgs e)
//        {
//            base.OnKeyDown(e);
//            if (!Enabled) return;

//            float step = e.Shift ? 96f : 24f;
//            switch (e.KeyCode)
//            {
//                case Keys.Left: PanBy(-step, 0f); e.Handled = true; break;
//                case Keys.Right: PanBy(step, 0f); e.Handled = true; break;
//                case Keys.Up: PanBy(0f, -step); e.Handled = true; break;
//                case Keys.Down: PanBy(0f, step); e.Handled = true; break;
//                case Keys.Add:
//                case Keys.Oemplus: SetZoom(_zoom * 1.25f, ViewportCenter()); e.Handled = true; break;
//                case Keys.Subtract:
//                case Keys.OemMinus: SetZoom(_zoom / 1.25f, ViewportCenter()); e.Handled = true; break;
//                case Keys.D0:
//                case Keys.NumPad0: SetZoom(1f, ViewportCenter()); e.Handled = true; break;
//                case Keys.Home:
//                case Keys.F: AutoFit(); e.Handled = true; break;
//            }
//        }

//        private PointF ViewportCenter() { return new PointF(Width / 2f, Height / 2f); }

//        // ----------------------------------------------------------------
//        //  DRAG & DROP
//        // ----------------------------------------------------------------
//        protected override void OnDragEnter(DragEventArgs e)
//        {
//            base.OnDragEnter(e);
//            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
//        }

//        protected override void OnDragDrop(DragEventArgs e)
//        {
//            base.OnDragDrop(e);
//            if (!Enabled) return;

//            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
//            if (files != null && files.Length > 0)
//            {
//                try { LoadFromFile(files[0]); }
//                catch { /* unsupported / corrupt file — keep current state */ }
//            }
//        }

//        // ----------------------------------------------------------------
//        //  LIFECYCLE
//        // ----------------------------------------------------------------
//        protected override void OnHandleCreated(EventArgs e)
//        {
//            base.OnHandleCreated(e);
//            try
//            {
//                using (Graphics probe = CreateGraphics())
//                {
//                    _dpiScale = Math.Max(1f, probe.DpiX / 96f);
//                }
//            }
//            catch { _dpiScale = 1f; }
//            InvalidateChrome();          // rebuild chrome at the real DPI
//        }

//        protected override void OnResize(EventArgs e)
//        {
//            base.OnResize(e);
//            if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }
//            if (_image != null && _needsAutoFit) AutoFit();
//            Invalidate();
//        }

//        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
//        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

//        protected override void OnEnabledChanged(EventArgs e)
//        {
//            base.OnEnabledChanged(e);
//            InvalidateChrome();          // hover-dependent chrome reads Enabled
//        }

//        protected override void Dispose(bool disposing)
//        {
//            if (disposing)
//            {
//                if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }
//                foreach (Font font in _fontCache.Values) font.Dispose();
//                _fontCache.Clear();
//                if (_image != null) { _image.Dispose(); _image = null; }
//            }
//            base.Dispose(disposing);
//        }
//    }
//}










//         ========  VER 1.5 ================

// ============================================================================
//  PanZoomViewer.cs — Reference-quality pan/zoom image surface (v5 PRO)
//  ---------------------------------------------------------------------------
//  FIDELITY     : EnhanceImage=false (DEFAULT) → pixel-perfect, zero blur,
//                 exactly as your original control. true → photo-grade.
//  FORMATS      : BMP, GIF, JPEG, PNG, TIFF (multipage), EXIF, WMF, EMF, ICO
//  DOCUMENTS    : single image · multipage (lazy async page decode + LRU) ·
//                 image collections — unified frame navigation (buttons,
//                 PageUp/PageDown, GoToFrame/NextFrame/PreviousFrame)
//  LICENSING    : PanZoomViewerLicense — SHA-256 signed PZV1 keys; features
//                 L=LargeImages, M=Multipage, C=Collections; EnforceLicense
//                 property toggles gating (false = open dev mode)
//  PERF         : visible-crop blit · cached PArgb chrome · cached HUD text
//                 · background decode · page LRU · mipmap chain (Enhanced)
//  Target: .NET Framework 4.7+ / .NET 6+ WinForms, C# 7.3+. No dependencies.
// ============================================================================
//using System;
//using System.Collections.Generic;
//using System.ComponentModel;
//using System.Diagnostics;
//using System.Drawing;
//using System.Drawing.Drawing2D;
//using System.Drawing.Imaging;
//using System.Drawing.Text;
//using System.IO;
//using System.Security.Cryptography;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows.Forms;

//namespace CsplCameraOcr.UserControls
//{
//    // ========================================================================
//    //  PUBLIC ENUMS
//    // ========================================================================

//    public enum ThemeMode { Light, Dark }

//    public enum ViewerStyle
//    {
//        DashboardPremium = 1,
//        FluentGlass = 2,
//        MaterialFlat = 3,
//        SoftNeumorphic = 4,
//        Cyberpunk = 5
//    }

//    /// <summary>Paid feature identifiers used by licensing and the FeatureLocked event.</summary>
//    public enum ViewerFeature
//    {
//        /// <summary>Images/files larger than LargeImageThresholdMB.</summary>
//        LargeImages,
//        /// <summary>Multipage documents (e.g. multipage TIFF) with more than one page.</summary>
//        Multipage,
//        /// <summary>Collections of images with embedded navigation.</summary>
//        Collections
//    }

//    // ========================================================================
//    //  LICENSING — vendor tool + runtime validation
//    //  Key format: PZV1-<FEATURES>-<EXPYYYYMMDD|0>-<SHA256-prefix16>
//    //  e.g.       PZV1-LMC-0-4F19AB23C0D8E517   (all features, perpetual)
//    // ========================================================================

//    /// <summary>Immutable license snapshot granted by a validated PZV1 key.</summary>
//    public sealed class ViewerLicense
//    {
//        internal ViewerLicense(bool largeImages, bool multipage, bool collections, DateTime? expiresUtc)
//        {
//            LargeImages = largeImages;
//            Multipage = multipage;
//            Collections = collections;
//            ExpiresUtc = expiresUtc;
//        }

//        public bool LargeImages { get; private set; }
//        public bool Multipage { get; private set; }
//        public bool Collections { get; private set; }
//        public DateTime? ExpiresUtc { get; private set; }
//        public bool IsValid { get { DateTime? e = ExpiresUtc; return !(e.HasValue && DateTime.UtcNow.Date > e.Value); } }
//    }

//    /// <summary>
//    /// Global license registry. One key activates every PanZoomViewer in the process.
//    /// VENDOR: use GenerateKey() in your internal admin tool to issue customer keys.
//    /// SECURITY NOTE: the secret lives inside your assembly — that is obfuscation,
//    /// not strong security. For real distribution, move key validation server-side
//    /// or sign keys asymmetrically. This scheme stops casual sharing, not reverse
//    /// engineers.
//    /// </summary>
//    public static class PanZoomViewerLicense
//    {
//        private const string Secret = "CSPL-PZV1-CHANGE-ME-b48a9f2e7d";   // ← REPLACE before shipping

//        private static readonly object Gate = new object();
//        private static ViewerLicense _current = new ViewerLicense(false, false, false, null);

//        public static ViewerLicense Current
//        {
//            get { lock (Gate) { return _current; } }
//        }

//        /// <summary>Generates a customer key. Call from YOUR admin/license tool.</summary>
//        public static string GenerateKey(bool largeImages, bool multipage, bool collections,
//                                         DateTime? expiresUtc)
//        {
//            string feat = (largeImages ? "L" : "") + (multipage ? "M" : "") + (collections ? "C" : "");
//            if (feat.Length == 0) throw new ArgumentException("At least one feature must be enabled.");
//            string exp = expiresUtc.HasValue ? expiresUtc.Value.ToUniversalTime().ToString("yyyyMMdd") : "0";
//            string payload = "PZV1-" + feat + "-" + exp;
//            return payload + "-" + HashPayload(payload);
//        }

//        /// <summary>Applies a customer key. Returns false for malformed/invalid keys.</summary>
//        public static bool ApplyKey(string licenseKey)
//        {
//            ViewerLicense parsed = ParseKey(licenseKey);
//            if (parsed == null) return false;
//            lock (Gate) { _current = parsed; }
//            return true;
//        }

//        public static void Revoke()
//        {
//            lock (Gate) { _current = new ViewerLicense(false, false, false, null); }
//        }

//        private static string HashPayload(string payload)
//        {
//            using (SHA256 sha = SHA256.Create())
//            {
//                byte[] raw = Encoding.UTF8.GetBytes(Secret + "|" + payload);
//                byte[] digest = sha.ComputeHash(raw);
//                return BitConverter.ToString(digest).Replace("-", "").Substring(0, 16);
//            }
//        }

//        internal static ViewerLicense ParseKey(string licenseKey)
//        {
//            try
//            {
//                if (string.IsNullOrEmpty(licenseKey)) return null;
//                string key = licenseKey.Trim().ToUpperInvariant();
//                if (!key.StartsWith("PZV1-", StringComparison.Ordinal)) return null;

//                string[] parts = key.Split('-');
//                if (parts.Length != 4) return null;

//                string feat = parts[1];
//                string exp = parts[2];
//                string hash = parts[3];

//                if (feat.Length < 1 || feat.Length > 3) return null;
//                for (int i = 0; i < feat.Length; i++)
//                {
//                    char ch = feat[i];
//                    if (ch != 'L' && ch != 'M' && ch != 'C') return null;
//                }
//                if (exp != "0" && exp.Length != 8) return null;
//                if (hash.Length != 16) return null;
//                for (int i = 0; i < hash.Length; i++)
//                {
//                    char ch = hash[i];
//                    bool hex = (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F');
//                    if (!hex) return null;
//                }

//                string payload = "PZV1-" + feat + "-" + exp;
//                if (!string.Equals(HashPayload(payload), hash, StringComparison.Ordinal)) return null;

//                bool large = feat.IndexOf('L') >= 0;
//                bool multi = feat.IndexOf('M') >= 0;
//                bool coll = feat.IndexOf('C') >= 0;

//                DateTime? expiry = null;
//                if (exp != "0")
//                {
//                    int y = int.Parse(exp.Substring(0, 4));
//                    int m = int.Parse(exp.Substring(4, 2));
//                    int d = int.Parse(exp.Substring(6, 2));
//                    expiry = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc);
//                }
//                return new ViewerLicense(large, multi, coll, expiry);
//            }
//            catch { return null; }
//        }
//    }

//    public sealed class FeatureLockedEventArgs : EventArgs
//    {
//        internal FeatureLockedEventArgs(ViewerFeature feature, string message)
//        {
//            Feature = feature;
//            Message = message;
//        }
//        public ViewerFeature Feature { get; private set; }
//        public string Message { get; private set; }
//    }

//    // ========================================================================
//    //  PALETTE SNAPSHOT
//    // ========================================================================

//    public sealed class ViewerPalette
//    {
//        public Color Canvas { get; internal set; }
//        public Color BackdropGlow { get; internal set; }
//        public Color ViewportFace { get; internal set; }
//        public Color ViewportWell { get; internal set; }
//        public Color Border { get; internal set; }
//        public Color BorderHighlight { get; internal set; }
//        public Color TextPrimary { get; internal set; }
//        public Color TextSecondary { get; internal set; }
//        public Color TextDim { get; internal set; }
//        public Color Accent { get; internal set; }
//        public Color AccentAlt { get; internal set; }
//        public Color ZoneGood { get; internal set; }
//        public Color ZoneWarn { get; internal set; }
//        public Color ZoneHot { get; internal set; }
//        public Color Grid { get; internal set; }
//        public Color Tick { get; internal set; }
//        public Color HudBack { get; internal set; }
//        public Color HudBorder { get; internal set; }
//        public Color HudFore { get; internal set; }
//        public Color HudForeDim { get; internal set; }
//        public Color Shadow { get; internal set; }
//        public Color ShadowLight { get; internal set; }
//        public Color ShadowDark { get; internal set; }
//        public Color HoverOverlay { get; internal set; }
//        public Color FocusRing { get; internal set; }
//    }

//    public sealed class OverlayPaintEventArgs : EventArgs
//    {
//        internal OverlayPaintEventArgs(Graphics graphics, RectangleF imageBounds,
//                                       float zoom, PointF pan, ViewerPalette palette)
//        {
//            Graphics = graphics;
//            ImageBounds = imageBounds;
//            Zoom = zoom;
//            Pan = pan;
//            Palette = palette;
//        }

//        public Graphics Graphics { get; private set; }
//        public RectangleF ImageBounds { get; private set; }
//        public float Zoom { get; private set; }
//        public PointF Pan { get; private set; }
//        public ViewerPalette Palette { get; private set; }
//    }

//    // ========================================================================
//    //  CONTROL
//    // ========================================================================

//    [ToolboxItem(true)]
//    [Description("Reference-quality pan/zoom image surface — pixel-perfect by default, multipage/collection navigation, license-gated PRO features.")]
//    public partial class PanZoomViewer : Control
//    {
//        // ====================================================================
//        //  CORE DATA
//        // ====================================================================
//        private Bitmap _image;                 // currently displayed frame
//        private float _zoom = 1f;
//        private PointF _pan = PointF.Empty;
//        private bool _needsAutoFit = true;

//        private bool _hover;
//        private bool _pressed;
//        private bool _panning;
//        private Point _lastMouse;
//        private Point _hoverPos;
//        private bool _imageLostPending;

//        private ThemeMode _themeMode = ThemeMode.Dark;
//        private ViewerStyle _controlStyle = ViewerStyle.DashboardPremium;
//        private ViewerPalette _palette;

//        private bool _showHud = true;
//        private bool _showScanlines = true;
//        private bool _showCrosshair = true;
//        private bool _enhanceImage = false;          // DEFAULT = pixel-perfect
//        private float _minZoom = 0.05f;
//        private float _maxZoom = 100f;
//        private float _dpiScale = 1f;

//        // ----------------------------------------------------------------
//        //  DOCUMENT MODEL — unified "frames"
//        //    Single     : 1 frame, _image is owned directly
//        //    Multipage  : _frameCount pages; frames owned by _pageCache
//        //    Collection : _frameCount items; frames owned by _collection
//        // ----------------------------------------------------------------
//        private enum ContentMode { Single, Multipage, Collection }

//        private ContentMode _mode = ContentMode.Single;
//        private int _frameCount;
//        private int _frameIndex;
//        private int _desiredFrame;

//        private Image _mpSource;                    // multipage container (TIFF etc.)
//        private MemoryStream _mpStream;             // MUST outlive _mpSource
//        private readonly Dictionary<int, Bitmap> _pageCache = new Dictionary<int, Bitmap>();
//        private readonly LinkedList<int> _pageLru = new LinkedList<int>();
//        private readonly object _mpGate = new object();
//        private int _loadGeneration;                // invalidates in-flight decodes
//        private bool _frameDecoding;
//        private bool _multipageLocked;              // multipage present, license missing

//        private readonly List<Bitmap> _collection = new List<Bitmap>();

//        // License gating
//        private bool _enforceLicense = true;
//        private bool _largeLockActive;              // large-image blocked card
//        private string _largeLockSizeText = "";

//        // Config
//        private bool _autoFitOnLoad = true;
//        private bool _showNavigationBar = true;
//        private bool _useMipmaps = true;
//        private bool _showCheckerboard = false;
//        private int _pageCacheSize = 3;
//        private int _largeThresholdMB = 5;
//        private int _viewportPadding = -1;          // -1 = auto
//        private Color _customAccent = Color.Empty;  // Empty = style default

//        // Navigation hit rects (updated during paint, consumed by mouse)
//        private int _navHover;                      // 0 none, 1 prev, 2 next
//        private RectangleF _navPrevRect;
//        private RectangleF _navNextRect;
//        private bool _navRectsValid;

//        // Mipmap chain (Enhanced path only — crisp path never uses it)
//        private List<Bitmap> _mips;

//        // Chrome cache — control-owned, created OUTSIDE OnPaint
//        private Bitmap _chromeCache;
//        private Bitmap _checkerTile;

//        // HUD text caches
//        private string _hudZoomText = "100 %";
//        private string _hudSourceText = "—";
//        private string _hudProbeText = "";
//        private int _probeX = int.MinValue;
//        private int _probeY = int.MinValue;

//        private string _mCapSrc; private SizeF _szCapSrc;
//        private string _mValSrc; private SizeF _szValSrc;
//        private string _mCapZoom; private SizeF _szCapZoom;
//        private string _mValZoom; private SizeF _szValZoom;
//        private string _mCapProbe; private SizeF _szCapProbe;
//        private string _mValProbe; private SizeF _szValProbe;
//        private string _mNavKey; private SizeF _szNavSize;

//        // Font engine (cache-owned; disposed exactly once in Dispose)
//        private readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();
//        private static string _uiFamily;
//        private static string _monoFamily;
//        private static readonly string[] UiFontCandidates =
//            { "Segoe UI Variable Display", "Segoe UI Workspace", "Segoe UI", "Arial" };
//        private static readonly string[] MonoFontCandidates =
//            { "Cascadia Code", "Cascadia Mono", "JetBrains Mono", "Consolas", "Courier New" };

//        private struct ViewerLayout
//        {
//            public RectangleF View;
//            public RectangleF Content;
//            public float ViewRadius;
//            public float ContentRadius;
//            public float Dpi;
//        }

//        private enum ChipAnchor { TopLeft, BottomLeft, BottomRight }

//        private sealed class FrameLoad
//        {
//            public Bitmap Bitmap;
//            public List<Bitmap> Mips;
//            public void DisposeAll()
//            {
//                if (Bitmap != null) { Bitmap.Dispose(); Bitmap = null; }
//                if (Mips != null) { foreach (Bitmap m in Mips) m.Dispose(); Mips = null; }
//            }
//        }

//        // ====================================================================
//        //  CONSTRUCTION
//        // ====================================================================
//        public PanZoomViewer()
//        {
//            SetStyle(
//                ControlStyles.UserPaint |
//                ControlStyles.AllPaintingInWmPaint |
//                ControlStyles.DoubleBuffer |
//                ControlStyles.OptimizedDoubleBuffer |
//                ControlStyles.ResizeRedraw |
//                ControlStyles.Selectable, true);
//            UpdateStyles();

//            TabStop = true;
//            AllowDrop = true;
//            Cursor = Cursors.Cross;

//            _palette = BuildPalette(_themeMode, _controlStyle);
//        }

//        // ====================================================================
//        //  PROPERTIES — FIDELITY / BEHAVIOR
//        // ====================================================================
//        [Category("Behavior")]
//        [Description("false (default): pixel-perfect display — image looks exactly as it is, zero blur, fastest rendering. true: high-quality smooth scaling for photographs.")]
//        [DefaultValue(false)]
//        public bool EnhanceImage
//        {
//            get { return _enhanceImage; }
//            set { if (_enhanceImage == value) return; _enhanceImage = value; Invalidate(); }
//        }

//        [Category("Behavior")]
//        [Description("Re-fits the view every time a new document is loaded (fixes view-state carryover between images).")]
//        [DefaultValue(true)]
//        public bool AutoFitOnLoad
//        {
//            get { return _autoFitOnLoad; }
//            set { if (_autoFitOnLoad == value) return; _autoFitOnLoad = value; Invalidate(); }
//        }

//        [Category("Behavior")]
//        [Description("Lower zoom clamp.")]
//        [DefaultValue(0.05f)]
//        public float MinZoom
//        {
//            get { return _minZoom; }
//            set { _minZoom = Math.Max(0.01f, value); Invalidate(); }
//        }

//        [Category("Behavior")]
//        [Description("Upper zoom clamp.")]
//        [DefaultValue(100f)]
//        public float MaxZoom
//        {
//            get { return _maxZoom; }
//            set { _maxZoom = Math.Max(_minZoom * 2f, value); Invalidate(); }
//        }

//        [Category("Behavior")]
//        [Description("Builds a background mipmap chain for very large images (24 MP+). Used ONLY by the Enhanced render path — the pixel-perfect path never touches it.")]
//        [DefaultValue(true)]
//        public bool UseMipmaps
//        {
//            get { return _useMipmaps; }
//            set { if (_useMipmaps == value) return; _useMipmaps = value; Invalidate(); }
//        }

//        [Category("Behavior")]
//        [Description("Number of decoded pages kept in the LRU cache for multipage documents.")]
//        [DefaultValue(3)]
//        public int PageCacheSize
//        {
//            get { return _pageCacheSize; }
//            set { _pageCacheSize = Math.Max(2, value); Invalidate(); }
//        }

//        [Category("Behavior")]
//        [Description("Images larger than this (in MB, by file/byte size) require the LargeImages license feature. Applies to file/stream/bytes loaders; direct Bitmap assignment (camera streaming) is never gated.")]
//        [DefaultValue(5)]
//        public int LargeImageThresholdMB
//        {
//            get { return _largeThresholdMB; }
//            set { _largeThresholdMB = Math.Max(1, value); Invalidate(); }
//        }

//        // ====================================================================
//        //  PROPERTIES — APPEARANCE / NAVIGATION
//        // ====================================================================
//        [Category("Appearance")]
//        [Description("Light or Dark surface theme.")]
//        [DefaultValue(ThemeMode.Dark)]
//        public ThemeMode ThemeMode
//        {
//            get { return _themeMode; }
//            set
//            {
//                if (_themeMode == value) return;
//                _themeMode = value;
//                _palette = BuildPalette(_themeMode, _controlStyle);
//                if (_checkerTile != null) { _checkerTile.Dispose(); _checkerTile = null; }
//                InvalidateChrome();
//            }
//        }

//        [Category("Appearance")]
//        [Description("Visual design system.")]
//        [DefaultValue(ViewerStyle.DashboardPremium)]
//        public ViewerStyle ControlStyle
//        {
//            get { return _controlStyle; }
//            set
//            {
//                if (_controlStyle == value) return;
//                _controlStyle = value;
//                _palette = BuildPalette(_themeMode, _controlStyle);
//                InvalidateChrome();
//            }
//        }

//        [Category("Appearance")]
//        [Description("Shows the minimal readout chips.")]
//        [DefaultValue(true)]
//        public bool ShowHud
//        {
//            get { return _showHud; }
//            set { if (_showHud == value) return; _showHud = value; Invalidate(); }
//        }

//        [Category("Appearance")]
//        [Description("Cyberpunk style: subtle CRT scanline film.")]
//        [DefaultValue(true)]
//        public bool ShowScanlines
//        {
//            get { return _showScanlines; }
//            set { if (_showScanlines == value) return; _showScanlines = value; Invalidate(); }
//        }

//        [Category("Appearance")]
//        [Description("Precision crosshair follows the cursor (Dashboard & Cyberpunk).")]
//        [DefaultValue(true)]
//        public bool ShowCrosshair
//        {
//            get { return _showCrosshair; }
//            set { if (_showCrosshair == value) return; _showCrosshair = value; Invalidate(); }
//        }

//        [Category("Appearance")]
//        [Description("Shows the embedded frame navigation bar (pages / collection items).")]
//        [DefaultValue(true)]
//        public bool ShowNavigationBar
//        {
//            get { return _showNavigationBar; }
//            set { if (_showNavigationBar == value) return; _showNavigationBar = value; Invalidate(); }
//        }

//        [Category("Appearance")]
//        [Description("Checkerboard behind images with transparency (PNG, GIF, TIFF with alpha).")]
//        [DefaultValue(false)]
//        public bool ShowCheckerboard
//        {
//            get { return _showCheckerboard; }
//            set { if (_showCheckerboard == value) return; _showCheckerboard = value; Invalidate(); }
//        }

//        [Category("Appearance")]
//        [Description("Custom accent color overriding the style's accent. Empty = style default.")]
//        public Color CustomAccent
//        {
//            get { return _customAccent; }
//            set
//            {
//                if (_customAccent == value) return;
//                _customAccent = value;
//                InvalidateChrome();
//            }
//        }
//        private bool ShouldSerializeCustomAccent() { return _customAccent != Color.Empty; }
//        private void ResetCustomAccent() { _customAccent = Color.Empty; }

//        [Category("Appearance")]
//        [Description("Viewport padding in pixels. -1 = automatic proportional padding.")]
//        [DefaultValue(-1)]
//        public int ViewportPadding
//        {
//            get { return _viewportPadding; }
//            set { if (_viewportPadding == value) return; _viewportPadding = Math.Max(0, Math.Min(200, value)); InvalidateChrome(); }
//        }

//        // ====================================================================
//        //  PROPERTIES — LICENSING
//        // ====================================================================
//        [Category("License")]
//        [Description("Applies a PZV1 license key (generate with PanZoomViewerLicense.GenerateKey).")]
//        [Browsable(true)]
//        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
//        public string LicenseKey
//        {
//            set
//            {
//                if (PanZoomViewerLicense.ApplyKey(value))
//                {
//                    ReevaluateLocks();
//                    EventHandler h = LicenseApplied;
//                    if (h != null) h(this, EventArgs.Empty);
//                    Invalidate();
//                }
//            }
//        }

//        [Category("License")]
//        [Description("true (default): locked features are gated and raise FeatureLocked. false: all features active (development / trial build).")]
//        [DefaultValue(true)]
//        public bool EnforceLicense
//        {
//            get { return _enforceLicense; }
//            set
//            {
//                if (_enforceLicense == value) return;
//                _enforceLicense = value;
//                ReevaluateLocks();
//            }
//        }

//        [Browsable(false)]
//        public ViewerLicense License { get { return PanZoomViewerLicense.Current; } }

//        // ====================================================================
//        //  PROPERTIES — CONTENT
//        // ====================================================================
//        [Browsable(false)]
//        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
//        [Description("Displayed bitmap (single-image mode). Ownership transfers to the control; the previous document is fully released.")]
//        public Bitmap Image
//        {
//            get { return _image; }
//            set
//            {
//                if (ReferenceEquals(_image, value)) return;
//                EnterSingleMode(value, false);
//            }
//        }

//        [Browsable(false)]
//        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
//        public float Zoom
//        {
//            get { return _zoom; }
//            set { SetZoom(value, new PointF(Width / 2f, Height / 2f)); }
//        }

//        [Browsable(false)]
//        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
//        public PointF PanOffset
//        {
//            get { return _pan; }
//            set { _pan = value; Invalidate(); }
//        }

//        [Browsable(false)]
//        public int FrameCount { get { return _frameCount; } }

//        [Browsable(false)]
//        public int FrameIndex { get { return _frameIndex; } }

//        [Browsable(false)]
//        public int CollectionCount { get { return _mode == ContentMode.Collection ? _collection.Count : 0; } }

//        /// <summary>Extensions decodable by the loading API.</summary>
//        public static string SupportedExtensions
//        {
//            get { return "*.bmp;*.gif;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.exif;*.wmf;*.emf;*.ico"; }
//        }

//        // ====================================================================
//        //  EVENTS
//        // ====================================================================

//        public event EventHandler<Graphics> OnCustomPaint;
//        public event EventHandler<OverlayPaintEventArgs> OverlayPaint;
//        public event EventHandler ImageLost;
//        public event EventHandler FrameChanged;
//        public event EventHandler FrameCountChanged;
//        public event EventHandler LicenseApplied;
//        public event EventHandler<FeatureLockedEventArgs> FeatureLocked;

//        private void OnFrameChanged() { EventHandler h = FrameChanged; if (h != null) h(this, EventArgs.Empty); }
//        private void OnFrameCountChanged() { EventHandler h = FrameCountChanged; if (h != null) h(this, EventArgs.Empty); }
//        private void RaiseLocked(ViewerFeature feature, string message)
//        {
//            EventHandler<FeatureLockedEventArgs> h = FeatureLocked;
//            if (h != null) h(this, new FeatureLockedEventArgs(feature, message));
//        }

//        // ====================================================================
//        //  LOADING API — single documents (all formats incl. multipage TIFF)
//        //  Every loader decodes into self-owned bitmaps: files unlock, streams
//        //  close, caller sources stay alive.
//        // ====================================================================

//        /// <summary>Synchronous load from a file. Returns false if blocked (license) or undecodable.</summary>
//        public bool LoadFromFile(string path)
//        {
//            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path must not be empty.", "path");

//            long size = 0;
//            try { size = new FileInfo(path).Length; }
//            catch { return false; }

//            if (IsLargeBlocked(size)) { ShowLargeLock(size); return false; }

//            try
//            {
//                Bitmap b = DecodeFile(path);
//                EnterSingleMode(b, false);
//                return true;
//            }
//            catch { return false; }
//        }

//        /// <summary>Decodes off the UI thread (mipmaps for huge images are built there too). Call from the UI thread.</summary>
//        public async Task<bool> LoadFromFileAsync(string path)
//        {
//            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path must not be empty.", "path");

//            long size = 0;
//            try { size = new FileInfo(path).Length; }
//            catch { return false; }

//            if (IsLargeBlocked(size)) { ShowLargeLock(size); return false; }

//            FrameLoad load = await Task.Run(() => DecodeFileFrame(path));

//            if (IsDisposed || Disposing) { load.DisposeAll(); return false; }
//            if (load.Bitmap == null) return false;

//            EnterSingleMode(load.Bitmap, false);
//            DisposeMips();
//            _mips = load.Mips;
//            Invalidate();
//            return true;
//        }

//        /// <summary>Loads from any stream (read from the current position). The caller's stream may close right after.</summary>
//        public bool LoadFromStream(Stream stream)
//        {
//            if (stream == null) throw new ArgumentNullException("stream");

//            byte[] data;
//            using (MemoryStream ms = new MemoryStream())
//            {
//                stream.CopyTo(ms);
//                data = ms.ToArray();
//            }
//            return LoadFromBytesCore(data);
//        }

//        /// <summary>Loads from raw encoded bytes (BMP / GIF / JPEG / PNG / multipage TIFF / EXIF / WMF / EMF / ICO).</summary>
//        public bool LoadFromBytes(byte[] data)
//        {
//            if (data == null) throw new ArgumentNullException("data");
//            return LoadFromBytesCore(data);
//        }

//        /// <summary>Loads from any System.Drawing.Image. The source stays caller-owned; the viewer keeps a copy.</summary>
//        public void LoadFromImage(System.Drawing.Image image)
//        {
//            if (image == null) throw new ArgumentNullException("image");
//            EnterSingleMode(new Bitmap(image), false);
//        }

//        /// <summary>Thread-safe frame streaming (camera loops). Preserves zoom/pan; re-fits automatically if resolution changes.</summary>
//        public void PostImage(Bitmap frame)
//        {
//            if (frame == null) return;
//            if (IsDisposed) { frame.Dispose(); return; }

//            if (IsHandleCreated && InvokeRequired)
//            {
//                BeginInvoke((Action)delegate { PostImageCore(frame); });
//            }
//            else
//            {
//                PostImageCore(frame);
//            }
//        }

//        private void PostImageCore(Bitmap frame)
//        {
//            if (IsDisposed || Disposing) { frame.Dispose(); return; }

//            int oldW = 0, oldH = 0;
//            try { if (_image != null) { oldW = _image.Width; oldH = _image.Height; } } catch { }

//            EnterSingleMode(frame, true);

//            if (_autoFitOnLoad && frame != null && (oldW != frame.Width || oldH != frame.Height))
//                AutoFit();                     // resolution change → refit once

//            Invalidate();
//        }

//        /// <summary>Removes the current bitmap WITHOUT disposing it — ownership returns to the caller. Single-image mode only.</summary>
//        public Bitmap DetachImage()
//        {
//            if (_mode != ContentMode.Single) return null;

//            Bitmap bmp = _image;
//            _image = null;
//            _frameCount = 0;
//            _frameIndex = 0;
//            DisposeMips();
//            _needsAutoFit = true;
//            UpdateSourceText();
//            ResetProbe();
//            OnFrameCountChanged();
//            Invalidate();
//            return bmp;
//        }

//        /// <summary>Clears everything back to the empty state.</summary>
//        public void Clear()
//        {
//            ReleaseModeState();
//            _needsAutoFit = true;
//            UpdateSourceText();
//            OnFrameCountChanged();
//            Invalidate();
//        }

//        // ----------------------------------------------------------------
//        //  DECODE CORE
//        // ----------------------------------------------------------------
//        private bool LoadFromBytesCore(byte[] data)
//        {
//            if (data == null || data.Length == 0) return false;

//            if (IsLargeBlocked(data.LongLength)) { ShowLargeLock(data.LongLength); return false; }

//            try
//            {
//                MemoryStream ms = new MemoryStream(data, false);
//                Image img = System.Drawing.Image.FromStream(ms, false, true);

//                int pages = 1;
//                try { pages = img.GetFrameCount(FrameDimension.Page); }
//                catch { /* single-frame formats */ }

//                if (pages > 1)
//                {
//                    // ---- MULTIPAGE DOCUMENT (e.g. TIFF) ----
//                    bool allowed = MultipageAllowed;
//                    ReleaseModeState();
//                    _mode = ContentMode.Multipage;
//                    _mpStream = ms;                 // must outlive _mpSource
//                    _mpSource = img;
//                    _frameCount = pages;
//                    _frameIndex = 0;
//                    _desiredFrame = 0;
//                    _multipageLocked = !allowed;    // locked → page 1 shows, navigation gated
//                    UpdateSourceText();
//                    OnFrameCountChanged();
//                    RequestPageAsync(0);
//                    return true;
//                }

//                // ---- SINGLE-FRAME ----
//                Bitmap bmp = new Bitmap(img);
//                img.Dispose();
//                ms.Dispose();
//                EnterSingleMode(bmp, false);
//                return true;
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine("[PanZoomViewer] decode failed: " + ex.Message);
//                return false;
//            }
//        }

//        private static Bitmap DecodeFile(string path)
//        {
//            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
//            using (Image tmp = System.Drawing.Image.FromStream(fs, false, true))
//            {
//                return new Bitmap(tmp);   // copy — any file lock dies with this method
//            }
//        }

//        private static FrameLoad DecodeFileFrame(string path)
//        {
//            FrameLoad load = new FrameLoad();
//            try
//            {
//                Bitmap main = DecodeFile(path);
//                load.Bitmap = main;
//                if ((long)main.Width * main.Height >= 24000000L)   // ~24 MP+: build mips
//                    load.Mips = BuildMipChain(main);
//            }
//            catch { load.DisposeAll(); }
//            return load;
//        }

//        private static List<Bitmap> BuildMipChain(Bitmap source)
//        {
//            List<Bitmap> mips = new List<Bitmap>();
//            try
//            {
//                int w = source.Width, h = source.Height;
//                Bitmap prev = source;
//                while (w / 2 >= 1024 && h / 2 >= 1024 && mips.Count < 8)
//                {
//                    w = Math.Max(1, w / 2);
//                    h = Math.Max(1, h / 2);
//                    Bitmap mip = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
//                    using (Graphics cg = Graphics.FromImage(mip))
//                    {
//                        cg.InterpolationMode = InterpolationMode.HighQualityBicubic;
//                        cg.CompositingQuality = CompositingQuality.HighQuality;
//                        cg.PixelOffsetMode = PixelOffsetMode.HighQuality;
//                        cg.DrawImage(prev, 0, 0, w, h);
//                    }
//                    mips.Add(mip);
//                    prev = mip;
//                }
//                return mips;
//            }
//            catch
//            {
//                foreach (Bitmap m in mips) m.Dispose();
//                return null;
//            }
//        }

//        // ====================================================================
//        //  COLLECTION API (license-gated: Collections feature)
//        // ====================================================================

//        /// <summary>Adds a caller-owned bitmap to the collection (ownership transfers). Returns false when the feature is locked.</summary>
//        public bool AddCollectionImage(Bitmap image)
//        {
//            if (image == null) throw new ArgumentNullException("image");
//            if (!CollectionsAllowed)
//            {
//                RaiseLocked(ViewerFeature.Collections, "Image collections require a license (feature 'C').");
//                return false;
//            }

//            if (_mode != ContentMode.Collection)
//            {
//                ReleaseModeState();
//                _mode = ContentMode.Collection;
//                _frameIndex = 0;
//                _desiredFrame = 0;
//            }

//            _collection.Add(image);
//            _frameCount = _collection.Count;
//            if (_collection.Count == 1) ApplyCollectionIndex(0);
//            OnFrameCountChanged();
//            return true;
//        }

//        /// <summary>Decodes a file and adds it to the collection. Returns false when locked or undecodable.</summary>
//        public bool AddCollectionFile(string path)
//        {
//            if (!CollectionsAllowed)
//            {
//                RaiseLocked(ViewerFeature.Collections, "Image collections require a license (feature 'C').");
//                return false;
//            }
//            try
//            {
//                Bitmap b = DecodeFile(path);
//                return AddCollectionImage(b);
//            }
//            catch { return false; }
//        }

//        /// <summary>Bulk-loads files into the collection off the UI thread. Call from the UI thread. Returns the number added.</summary>
//        public async Task<int> LoadCollectionFilesAsync(string[] paths)
//        {
//            if (paths == null) throw new ArgumentNullException("paths");
//            if (!CollectionsAllowed)
//            {
//                RaiseLocked(ViewerFeature.Collections, "Image collections require a license (feature 'C').");
//                return 0;
//            }

//            int added = 0;
//            foreach (string p in paths)
//            {
//                if (string.IsNullOrEmpty(p)) continue;

//                Bitmap decoded = await Task.Run(() =>
//                {
//                    try { return DecodeFile(p); }
//                    catch { return null; }
//                });

//                if (IsDisposed || Disposing) { if (decoded != null) decoded.Dispose(); return added; }
//                if (decoded != null && AddCollectionImage(decoded)) added++;
//            }
//            return added;
//        }

//        /// <summary>Releases the whole collection and its bitmaps.</summary>
//        public void ClearCollection()
//        {
//            if (_mode != ContentMode.Collection) return;
//            ReleaseModeState();
//            UpdateSourceText();
//            OnFrameCountChanged();
//            Invalidate();
//        }

//        // ====================================================================
//        //  FRAME NAVIGATION (pages + collection items)
//        // ====================================================================

//        public void NextFrame() { GoToFrame(_frameIndex + 1); }
//        public void PreviousFrame() { GoToFrame(_frameIndex - 1); }

//        /// <summary>Navigates to a frame (page or collection item). Returns false when out of range or locked.</summary>
//        public bool GoToFrame(int index)
//        {
//            if (_frameCount <= 1) return false;
//            if (index < 0 || index >= _frameCount) return false;

//            if (_mode == ContentMode.Multipage)
//            {
//                if (_multipageLocked)
//                {
//                    RaiseLocked(ViewerFeature.Multipage, "Multipage navigation requires a license (feature 'M').");
//                    return false;
//                }
//                GoToPage(index);
//                return true;
//            }

//            if (_mode == ContentMode.Collection && index < _collection.Count)
//            {
//                ApplyCollectionIndex(index);
//                return true;
//            }
//            return false;
//        }

//        private void GoToPage(int index)
//        {
//            Bitmap cached;
//            if (_pageCache.TryGetValue(index, out cached) && !_frameDecoding)
//            {
//                ApplyPage(index, cached);
//                return;
//            }
//            RequestPageAsync(index);
//        }

//        private async void RequestPageAsync(int index)
//        {
//            int gen = _loadGeneration;
//            _frameDecoding = true;
//            _desiredFrame = index;
//            Invalidate();

//            Bitmap page = await Task.Run(() => SafeDecodePage(index, gen));

//            try
//            {
//                if (IsDisposed || Disposing || gen != _loadGeneration)
//                {
//                    if (page != null) page.Dispose();
//                    return;
//                }
//                _frameDecoding = false;

//                if (page == null) { Invalidate(); return; }   // decode failed → keep current

//                CachePage(index, page);
//                if (_desiredFrame == index) ApplyPage(index, page);
//                Invalidate();
//            }
//            catch (Exception ex) { Debug.WriteLine("[PanZoomViewer] page apply failed: " + ex.Message); }
//        }

//        private Bitmap SafeDecodePage(int index, int gen)
//        {
//            try
//            {
//                lock (_mpGate)
//                {
//                    if (gen != _loadGeneration || _mpSource == null) return null;
//                    _mpSource.SelectActiveFrame(FrameDimension.Page, index);
//                    return new Bitmap(_mpSource);
//                }
//            }
//            catch { return null; }
//        }

//        private void CachePage(int index, Bitmap page)
//        {
//            _pageCache[index] = page;
//            _pageLru.Remove(index);
//            _pageLru.AddFirst(index);

//            int capacity = Math.Max(2, _pageCacheSize);
//            int guard = _pageCache.Count + 2;
//            while (_pageCache.Count > capacity && guard-- > 0)
//            {
//                LinkedListNode<int> last = _pageLru.Last;
//                if (last == null) break;
//                int victim = last.Value;

//                if (victim == _frameIndex || victim == _desiredFrame)
//                {
//                    _pageLru.RemoveLast();
//                    _pageLru.AddFirst(victim);        // protected → keep, scan older
//                    continue;
//                }

//                _pageLru.RemoveLast();
//                Bitmap evicted;
//                if (_pageCache.TryGetValue(victim, out evicted))
//                {
//                    _pageCache.Remove(victim);
//                    evicted.Dispose();
//                }
//            }
//        }

//        private void ApplyPage(int index, Bitmap page)
//        {
//            _pageLru.Remove(index);
//            _pageLru.AddFirst(index);

//            _image = page;                    // cache-owned
//            _frameIndex = index;
//            _desiredFrame = index;
//            DisposeMips();                    // per-frame mips (async file loads only)
//            if (_autoFitOnLoad) AutoFit();
//            UpdateSourceText();
//            ResetProbe();
//            OnFrameChanged();
//        }

//        private void ApplyCollectionIndex(int index)
//        {
//            _image = _collection[index];      // list-owned
//            _frameIndex = index;
//            _desiredFrame = index;
//            DisposeMips();
//            if (_autoFitOnLoad) AutoFit();
//            UpdateSourceText();
//            ResetProbe();
//            OnFrameChanged();
//        }

//        // ====================================================================
//        //  MODE / OWNERSHIP MANAGEMENT
//        // ====================================================================
//        private void EnterSingleMode(Bitmap bmp, bool preserveView)
//        {
//            bool neverFitted = _needsAutoFit;
//            ReleaseModeState();

//            _image = bmp;
//            _mode = ContentMode.Single;
//            _frameCount = bmp != null ? 1 : 0;
//            _frameIndex = 0;
//            _desiredFrame = 0;
//            UpdateSourceText();
//            ResetProbe();
//            OnFrameCountChanged();

//            if (bmp == null)
//            {
//                _needsAutoFit = true;
//                Invalidate();
//                return;
//            }

//            // BUG FIX: a NEW DOCUMENT always re-fits (previously the fit flag
//            // stayed false after a huge image, leaving later images invisible).
//            if (neverFitted || (!preserveView && _autoFitOnLoad))
//            {
//                AutoFit();
//            }
//            Invalidate();
//        }

//        private void ReleaseModeState()
//        {
//            _loadGeneration++;                 // kills every in-flight decode
//            _frameDecoding = false;
//            _desiredFrame = 0;
//            _multipageLocked = false;
//            _largeLockActive = false;

//            if (_mode == ContentMode.Multipage)
//            {
//                foreach (Bitmap b in _pageCache.Values) { try { b.Dispose(); } catch { } }
//                _pageCache.Clear();
//                _pageLru.Clear();
//                _image = null;                 // page frames were cache-owned
//                if (_mpSource != null) { try { _mpSource.Dispose(); } catch { } _mpSource = null; }
//                if (_mpStream != null) { try { _mpStream.Dispose(); } catch { } _mpStream = null; }
//            }
//            else if (_mode == ContentMode.Collection)
//            {
//                foreach (Bitmap b in _collection) { try { b.Dispose(); } catch { } }
//                _collection.Clear();
//                _image = null;                 // frames are list-owned
//            }
//            else
//            {
//                if (_image != null) { try { _image.Dispose(); } catch { } _image = null; }
//            }

//            DisposeMips();
//            _frameCount = 0;
//            _frameIndex = 0;
//            _mode = ContentMode.Single;
//            ResetProbe();
//        }

//        private void DisposeMips()
//        {
//            if (_mips != null)
//            {
//                foreach (Bitmap m in _mips) { if (m != null) m.Dispose(); }
//                _mips = null;
//            }
//        }

//        private void ReevaluateLocks()
//        {
//            if (_mode == ContentMode.Multipage) _multipageLocked = !MultipageAllowed;
//            // A large-image lock card cannot auto-restore (the file was never
//            // decoded) — reload once licensed.
//            Invalidate();
//        }

//        // ----------------------------------------------------------------
//        //  LICENSE GATES
//        // ----------------------------------------------------------------
//        private bool LargeImagesAllowed
//        {
//            get
//            {
//                if (!_enforceLicense) return true;
//                ViewerLicense l = PanZoomViewerLicense.Current;
//                return l.IsValid && l.LargeImages;
//            }
//        }

//        private bool MultipageAllowed
//        {
//            get
//            {
//                if (!_enforceLicense) return true;
//                ViewerLicense l = PanZoomViewerLicense.Current;
//                return l.IsValid && l.Multipage;
//            }
//        }

//        private bool CollectionsAllowed
//        {
//            get
//            {
//                if (!_enforceLicense) return true;
//                ViewerLicense l = PanZoomViewerLicense.Current;
//                return l.IsValid && l.Collections;
//            }
//        }

//        private bool IsLargeBlocked(long byteCount)
//        {
//            return byteCount > (long)_largeThresholdMB * 1024L * 1024L && !LargeImagesAllowed;
//        }

//        private void ShowLargeLock(long byteCount)
//        {
//            ReleaseModeState();
//            _mode = ContentMode.Single;
//            _largeLockActive = true;
//            _largeLockSizeText = FormatBytes(byteCount);
//            UpdateSourceText();
//            OnFrameCountChanged();
//            RaiseLocked(ViewerFeature.LargeImages,
//                string.Format("Image is {0} — the free limit is {1} MB.", _largeLockSizeText, _largeThresholdMB));
//            Invalidate();
//        }

//        private static string FormatBytes(long b)
//        {
//            if (b >= 1024L * 1024L * 1024L) return (b / (1024.0 * 1024.0 * 1024.0)).ToString("0.##") + " GB";
//            if (b >= 1024L * 1024L) return (b / (1024.0 * 1024.0)).ToString("0.##") + " MB";
//            return (b / 1024.0).ToString("0.#") + " KB";
//        }

//        // ====================================================================
//        //  PUBLIC METHODS & COORDINATE TRANSFORMS
//        // ====================================================================

//        public void AutoFit()
//        {
//            if (_image == null || Width < 20 || Height < 20) return;

//            ViewerLayout lay = ComputeLayout();
//            RectangleF c = lay.Content;

//            float z = Math.Min(c.Width / _image.Width, c.Height / _image.Height);
//            _zoom = ClampF(z, MinZoom, MaxZoom);

//            float dw = _image.Width * _zoom;
//            float dh = _image.Height * _zoom;
//            _pan = new PointF(c.X + (c.Width - dw) / 2f, c.Y + (c.Height - dh) / 2f);

//            _needsAutoFit = false;
//            UpdateZoomText();
//            Invalidate();
//        }

//        public void SetZoom(float zoom, PointF anchor)
//        {
//            float z = ClampF(zoom, MinZoom, MaxZoom);
//            if (z == _zoom) return;

//            float k = z / _zoom;
//            _pan = new PointF(
//                anchor.X - (anchor.X - _pan.X) * k,
//                anchor.Y - (anchor.Y - _pan.Y) * k);
//            _zoom = z;
//            UpdateZoomText();
//            Invalidate();
//        }

//        public void PanBy(float dx, float dy)
//        {
//            _pan = new PointF(_pan.X + dx, _pan.Y + dy);
//            Invalidate();
//        }

//        public ViewerPalette GetPalette() { return _palette; }

//        public PointF ScreenToImageF(PointF screenPoint)
//        {
//            return new PointF((screenPoint.X - _pan.X) / _zoom,
//                              (screenPoint.Y - _pan.Y) / _zoom);
//        }

//        public Point ScreenToImage(Point screenPoint)
//        {
//            return new Point(
//                (int)Math.Round((screenPoint.X - _pan.X) / _zoom),
//                (int)Math.Round((screenPoint.Y - _pan.Y) / _zoom));
//        }

//        public RectangleF ImageToScreenF(RectangleF imageRect)
//        {
//            return new RectangleF(
//                imageRect.X * _zoom + _pan.X,
//                imageRect.Y * _zoom + _pan.Y,
//                imageRect.Width * _zoom,
//                imageRect.Height * _zoom);
//        }

//        public Rectangle ImageToScreen(Rectangle imageRect)
//        {
//            RectangleF f = ImageToScreenF(imageRect);
//            return new Rectangle(
//                (int)Math.Round(f.X), (int)Math.Round(f.Y),
//                (int)Math.Round(f.Width), (int)Math.Round(f.Height));
//        }

//        public RectangleF GetImageScreenBounds()
//        {
//            if (_image == null) return RectangleF.Empty;
//            try
//            {
//                return new RectangleF(_pan.X, _pan.Y, _image.Width * _zoom, _image.Height * _zoom);
//            }
//            catch { return RectangleF.Empty; }
//        }

//        // ====================================================================
//        //  RENDERING PIPELINE
//        // ====================================================================
//        protected override void OnPaintBackground(PaintEventArgs pevent)
//        {
//            // Intentionally empty — the buffered surface is composed in OnPaint only.
//        }

//        protected override void OnPaint(PaintEventArgs e)
//        {
//            if (IsDesignTime)
//            {
//                try { PaintCore(e.Graphics); }
//                catch (Exception ex) { PaintDesignFailure(e.Graphics, ex); }
//            }
//            else
//            {
//                PaintCore(e.Graphics);
//            }
//        }

//        private bool IsDesignTime
//        {
//            get { return Site != null && Site.DesignMode; }
//        }

//        private void PaintDesignFailure(Graphics g, Exception ex)
//        {
//            Debug.WriteLine("[PanZoomViewer design-time paint failure]\r\n" + ex);
//            try
//            {
//                g.SmoothingMode = SmoothingMode.None;
//                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

//                using (SolidBrush bg = new SolidBrush(Color.FromArgb(255, 34, 18, 28)))
//                    g.FillRectangle(bg, ClientRectangle);

//                using (Font f = new Font(FontFamily.GenericMonospace, 8.5f, FontStyle.Regular, GraphicsUnit.Point))
//                using (SolidBrush t = new SolidBrush(Color.FromArgb(255, 255, 105, 97)))
//                using (SolidBrush w = new SolidBrush(Color.White))
//                using (StringFormat fmt = new StringFormat())
//                {
//                    fmt.Trimming = StringTrimming.EllipsisCharacter;

//                    g.DrawString("PANZOOMVIEWER — DESIGN-TIME PAINT FAILURE", f, t, 12f, 12f, fmt);
//                    g.DrawString(ex.GetType().Name + ": " + ex.Message, f, w, 12f, 30f, fmt);

//                    string[] frames = ex.StackTrace.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
//                    float y = 48f;
//                    int shown = Math.Min(10, frames.Length);
//                    for (int i = 0; i < shown; i++)
//                    {
//                        g.DrawString(frames[i].Trim(), f, w, 12f, y, fmt);
//                        y += 15f;
//                        if (y > Height - 18f) break;
//                    }
//                }
//            }
//            catch { /* diagnostics must never throw */ }
//        }

//        private void PaintCore(Graphics g)
//        {
//            g.SmoothingMode = SmoothingMode.AntiAlias;
//            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
//            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
//            g.CompositingQuality = CompositingQuality.HighQuality;
//            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

//            if (Width < 6 || Height < 6)
//            {
//                using (SolidBrush b = new SolidBrush(_palette.Canvas)) g.FillRectangle(b, ClientRectangle);
//                return;
//            }

//            ViewerPalette pal = _palette;
//            ViewerLayout lay = ComputeLayout();

//            // 1 — static chrome: one blit from the premultiplied-ARGB cache.
//            EnsureChromeCache(lay);
//            if (_chromeCache != null) g.DrawImageUnscaled(_chromeCache, 0, 0);
//            else
//            {
//                DrawCanvasLayer(g, pal, lay);
//                DrawCardLayer(g, pal, lay);
//            }

//            DrawZoomStrip(g, pal, lay);      // 2 — dynamic zone strip
//            DrawContentLayer(g, pal, lay);   // 3 — image / empty / lock / decoding

//            // 4 — host overlays (ROIs, OCR boxes).
//            EventHandler<Graphics> legacy = OnCustomPaint;
//            if (legacy != null) legacy(this, g);

//            EventHandler<OverlayPaintEventArgs> overlay = OverlayPaint;
//            if (overlay != null)
//            {
//                OverlayPaintEventArgs args = new OverlayPaintEventArgs(
//                    g, GetImageScreenBounds(), _zoom, _pan, pal);
//                overlay(this, args);
//            }

//            DrawHudLayer(g, pal, lay);       // 5 — readouts
//            DrawStateLayer(g, pal, lay);     // 6 — crosshair, focus ring, disabled veil
//            DrawNavigationBar(g, pal, lay);  // 7 — embedded page/collection navigation (topmost)

//            if (_imageLostPending)
//            {
//                _imageLostPending = false;
//                EventHandler lost = ImageLost;
//                if (lost != null) lost(this, EventArgs.Empty);
//            }
//        }

//        // ----------------------------------------------------------------
//        //  PERFORMANCE ENGINE — chrome cache
//        // ----------------------------------------------------------------
//        private void InvalidateChrome()
//        {
//            if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }
//            Invalidate();
//        }

//        private void EnsureChromeCache(ViewerLayout lay)
//        {
//            if (_chromeCache != null &&
//                _chromeCache.Width == Width &&
//                _chromeCache.Height == Height) return;

//            if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }

//            Bitmap bmp = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format32bppPArgb);
//            using (Graphics cg = Graphics.FromImage(bmp))
//            {
//                cg.SmoothingMode = SmoothingMode.AntiAlias;
//                cg.InterpolationMode = InterpolationMode.HighQualityBicubic;
//                cg.PixelOffsetMode = PixelOffsetMode.HighQuality;
//                cg.CompositingQuality = CompositingQuality.HighQuality;

//                DrawCanvasLayer(cg, _palette, lay);
//                DrawCardLayer(cg, _palette, lay);
//            }
//            _chromeCache = bmp;
//        }

//        private Color AccentOf(ViewerPalette pal)
//        {
//            return _customAccent != Color.Empty ? _customAccent : pal.Accent;
//        }

//        // ----------------------------------------------------------------
//        //  LAYER 1 — CANVAS (baked into the chrome cache)
//        // ----------------------------------------------------------------
//        private void DrawCanvasLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF client = new RectangleF(0f, 0f, Width, Height);
//            using (SolidBrush b = new SolidBrush(pal.Canvas)) g.FillRectangle(b, client);

//            if (pal.BackdropGlow.A > 0)
//            {
//                using (LinearGradientBrush lg = new LinearGradientBrush(
//                    client, pal.BackdropGlow, Color.FromArgb(0, pal.BackdropGlow), 118f))
//                {
//                    g.FillRectangle(lg, client);
//                }
//            }

//            if (_controlStyle == ViewerStyle.MaterialFlat)
//            {
//                using (SolidBrush accent = new SolidBrush(AccentOf(pal)))
//                    g.FillRectangle(accent, 0f, 0f, Width, 4f * lay.Dpi);
//            }
//        }

//        // ----------------------------------------------------------------
//        //  LAYER 2 — VIEWPORT CARD (baked into the chrome cache)
//        // ----------------------------------------------------------------
//        private void DrawCardLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            switch (_controlStyle)
//            {
//                case ViewerStyle.FluentGlass: DrawGlassCard(g, pal, lay); break;
//                case ViewerStyle.MaterialFlat: DrawMaterialCard(g, pal, lay); break;
//                case ViewerStyle.SoftNeumorphic: DrawNeumorphicCard(g, pal, lay); break;
//                case ViewerStyle.Cyberpunk: DrawCyberCard(g, pal, lay); break;
//                default: DrawDashboardCard(g, pal, lay); break;
//            }
//        }

//        // STYLE 1 — DASHBOARD PREMIUM
//        private void DrawDashboardCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF v = lay.View;
//            float r = lay.ViewRadius;

//            DrawSoftShadow(g, v, r, pal.Shadow, 7f * lay.Dpi, 4);

//            Color borderColor = (_hover && Enabled) ? Lerp(pal.Border, AccentOf(pal), 0.45f) : pal.Border;
//            using (GraphicsPath face = BuildRoundedPath(v, r))
//            {
//                using (SolidBrush fill = new SolidBrush(pal.ViewportFace)) g.FillPath(fill, face);
//                using (Pen border = new Pen(borderColor, 1f)) g.DrawPath(border, face);
//            }

//            g.SetClip(new RectangleF(v.X, v.Y + v.Height * 0.55f, v.Width, v.Height * 0.45f));
//            using (GraphicsPath ip = BuildRoundedPath(Deflate(v, 1.4f), Math.Max(1f, r - 1.4f)))
//            using (Pen hi = new Pen(Color.FromArgb(80, pal.BorderHighlight), 1f))
//            {
//                g.DrawPath(hi, ip);
//            }
//            g.ResetClip();
//        }

//        // Dynamic (NOT cached) — the zone color tracks the zoom value.
//        private void DrawZoomStrip(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            if (_controlStyle != ViewerStyle.DashboardPremium) return;

//            RectangleF v = lay.View;
//            float r = lay.ViewRadius;
//            float stripW = v.Width - 2f * r;
//            if (stripW <= 4f) return;

//            RectangleF strip = new RectangleF(v.X + r, v.Y + 1.5f, stripW, 3f);
//            using (GraphicsPath sp = BuildRoundedPath(strip, 1.5f))
//            using (SolidBrush sb = new SolidBrush(Color.FromArgb(210, ZoomZoneColor(pal))))
//            {
//                g.FillPath(sb, sp);
//            }
//        }

//        // STYLE 2 — FLUENT GLASS
//        private void DrawGlassCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF v = lay.View;
//            float r = lay.ViewRadius;

//            DrawSoftShadow(g, v, r, pal.Shadow, 5f * lay.Dpi, 3);

//            using (GraphicsPath p1 = BuildRoundedPath(v, r))
//            using (SolidBrush l1 = new SolidBrush(pal.ViewportFace))
//            {
//                g.FillPath(l1, p1);
//            }
//            using (GraphicsPath p2 = BuildRoundedPath(Deflate(v, 4f * lay.Dpi), Math.Max(2f, r - 4f * lay.Dpi)))
//            using (SolidBrush l2 = new SolidBrush(pal.ViewportFace))
//            {
//                g.FillPath(l2, p2);
//            }

//            if (v.Width > 1f && v.Height > 1f)
//            {
//                using (LinearGradientBrush lb = new LinearGradientBrush(
//                    v, Color.FromArgb(150, pal.BorderHighlight),
//                    Color.FromArgb(28, pal.BorderHighlight), 90f))
//                using (Pen border = new Pen(lb, 1.2f))
//                using (GraphicsPath bp = BuildRoundedPath(Deflate(v, 0.6f), Math.Max(2f, r - 0.6f)))
//                {
//                    g.DrawPath(border, bp);
//                }
//            }

//            int glow = _pressed ? 3 : (_hover && Enabled ? 2 : 0);
//            for (int i = glow; i >= 1; i--)
//            {
//                using (GraphicsPath gp = BuildRoundedPath(Expand(v, 1.4f * i), r + 1.4f * i))
//                using (Pen pen = new Pen(Color.FromArgb(80 / i, AccentOf(pal)), 1.4f))
//                {
//                    g.DrawPath(pen, gp);
//                }
//            }
//        }

//        // STYLE 3 — MATERIAL FLAT
//        private void DrawMaterialCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            using (GraphicsPath face = BuildRoundedPath(lay.View, lay.ViewRadius))
//            using (SolidBrush fill = new SolidBrush(pal.ViewportFace))
//            {
//                g.FillPath(fill, face);
//            }
//        }

//        // STYLE 4 — SOFT NEUMORPHIC
//        private void DrawNeumorphicCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF v = lay.View;
//            float r = lay.ViewRadius;
//            bool pressed = _pressed && Enabled;
//            float off = (pressed ? 2.0f : (_hover && Enabled ? 2.8f : 3.4f)) * lay.Dpi;

//            Color tlC = pressed ? pal.ShadowDark : pal.ShadowLight;
//            Color brC = pressed ? pal.ShadowLight : pal.ShadowDark;

//            for (int i = 2; i >= 1; i--)
//            {
//                float o = off * (i == 2 ? 1.5f : 0.7f);
//                int pct = i == 2 ? 55 : 115;

//                using (GraphicsPath pTL = BuildRoundedPath(OffsetRect(v, -o, -o), r))
//                using (SolidBrush bTL = new SolidBrush(AlphaScale(tlC, pct)))
//                {
//                    g.FillPath(bTL, pTL);
//                }
//                using (GraphicsPath pBR = BuildRoundedPath(OffsetRect(v, o, o), r))
//                using (SolidBrush bBR = new SolidBrush(AlphaScale(brC, pct)))
//                {
//                    g.FillPath(bBR, pBR);
//                }
//            }

//            using (GraphicsPath face = BuildRoundedPath(v, r))
//            {
//                using (SolidBrush fill = new SolidBrush(pal.ViewportFace)) g.FillPath(fill, face);
//                using (Pen hair = new Pen(Color.FromArgb(70, pal.BorderHighlight), 1f)) g.DrawPath(hair, face);
//            }

//            using (GraphicsPath well = BuildRoundedPath(lay.Content, lay.ContentRadius))
//            {
//                using (SolidBrush wb = new SolidBrush(pal.ViewportWell)) g.FillPath(wb, well);

//                Color topCol = pressed ? pal.ShadowLight : pal.ShadowDark;
//                Color botCol = pressed ? pal.ShadowDark : pal.ShadowLight;

//                g.SetClip(new RectangleF(lay.Content.X, lay.Content.Y,
//                                         lay.Content.Width, lay.Content.Height * 0.5f));
//                using (Pen tp = new Pen(topCol, 2.2f)) g.DrawPath(tp, well);
//                g.ResetClip();

//                g.SetClip(new RectangleF(lay.Content.X, lay.Content.Y + lay.Content.Height * 0.5f,
//                                         lay.Content.Width, lay.Content.Height * 0.5f));
//                using (Pen bt = new Pen(botCol, 2.2f)) g.DrawPath(bt, well);
//                g.ResetClip();
//            }
//        }

//        // STYLE 5 — CYBERPUNK / INDUSTRIAL
//        private void DrawCyberCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF v = lay.View;
//            float r = lay.ViewRadius;

//            using (GraphicsPath face = BuildRoundedPath(v, r))
//            using (SolidBrush fill = new SolidBrush(pal.ViewportFace))
//            {
//                g.FillPath(fill, face);
//            }

//            int glow = _pressed ? 3 : (_hover && Enabled ? 2 : 1);
//            for (int i = glow; i >= 1; i--)
//            {
//                using (GraphicsPath gp = BuildRoundedPath(Expand(v, 1.4f * i + 1f), r + 1.4f * i + 1f))
//                using (Pen pen = new Pen(Color.FromArgb(70 / i, AccentOf(pal)), 1.6f))
//                {
//                    g.DrawPath(pen, gp);
//                }
//            }

//            using (GraphicsPath core = BuildRoundedPath(Deflate(v, 0.8f), Math.Max(2f, r - 0.8f)))
//            using (Pen neon = new Pen(AccentOf(pal), 1.7f))
//            {
//                g.DrawPath(neon, core);
//            }

//            DrawCornerBrackets(g, v, 16f * lay.Dpi, 2.6f, Color.FromArgb(220, pal.AccentAlt));
//        }

//        // ----------------------------------------------------------------
//        //  LAYER 3 — CONTENT
//        // ----------------------------------------------------------------
//        private void DrawContentLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF c = lay.Content;
//            if (c.Width < 2f || c.Height < 2f) return;

//            using (GraphicsPath clip = BuildRoundedPath(c, lay.ContentRadius))
//            {
//                g.SetClip(clip);

//                using (SolidBrush well = new SolidBrush(pal.ViewportWell)) g.FillPath(well, clip);

//                if (_largeLockActive)
//                {
//                    DrawLargeLockCard(g, pal, lay);       // paid gate: large image blocked
//                }
//                else if (_frameDecoding && _image == null)
//                {
//                    DrawDecodingState(g, pal, lay);       // first page still decoding
//                }
//                else
//                {
//                    if (_showCheckerboard && _image != null)
//                        DrawCheckerboard(g, c);

//                    if (!TryDrawImage(g, pal, lay))
//                        DrawEmptyState(g, pal, lay);
//                }

//                if (_controlStyle == ViewerStyle.DashboardPremium)
//                    DrawEdgeTicks(g, pal, c, lay.Dpi);

//                if (_controlStyle == ViewerStyle.Cyberpunk && _showScanlines)
//                    DrawScanlines(g, pal, c);

//                if (_controlStyle == ViewerStyle.MaterialFlat && Enabled)
//                {
//                    int alpha = _pressed ? 14 : (_hover ? 7 : 0);
//                    if (alpha > 0)
//                    {
//                        using (SolidBrush ov = new SolidBrush(Color.FromArgb(alpha, pal.HoverOverlay)))
//                            g.FillPath(ov, clip);
//                    }
//                }

//                g.ResetClip();
//            }
//        }

//        private void DrawCheckerboard(Graphics g, RectangleF content)
//        {
//            try
//            {
//                RectangleF ir = new RectangleF(_pan.X, _pan.Y,
//                    _image.Width * _zoom, _image.Height * _zoom);
//                RectangleF fill = RectangleF.Intersect(ir, content);
//                if (fill.Width <= 0f || fill.Height <= 0f) return;

//                EnsureCheckerTile();
//                if (_checkerTile == null) return;

//                using (TextureBrush tb = new TextureBrush(_checkerTile, WrapMode.Tile))
//                {
//                    g.FillRectangle(tb, fill);
//                }
//            }
//            catch { /* dead bitmap — the draw path handles it */ }
//        }

//        private void EnsureCheckerTile()
//        {
//            if (_checkerTile != null) return;

//            _checkerTile = new Bitmap(16, 16, PixelFormat.Format32bppPArgb);
//            using (Graphics cg = Graphics.FromImage(_checkerTile))
//            {
//                bool dark = _themeMode == ThemeMode.Dark;
//                Color a = dark ? C(0x191D26) : C(0xFFFFFF);
//                Color b = dark ? C(0x22262F) : C(0xE4E8F0);
//                using (SolidBrush ba = new SolidBrush(a)) cg.FillRectangle(ba, 0, 0, 16, 16);
//                using (SolidBrush bb = new SolidBrush(b))
//                {
//                    cg.FillRectangle(bb, 0, 0, 8, 8);
//                    cg.FillRectangle(bb, 8, 8, 8, 8);
//                }
//            }
//        }

//        private bool TryDrawImage(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            if (_image == null) return false;

//            try
//            {
//                int iw = _image.Width;
//                int ih = _image.Height;
//                float swf = iw * _zoom;
//                float shf = ih * _zoom;
//                if (swf <= 0f || shf <= 0f) return false;

//                RectangleF screen = new RectangleF(_pan.X, _pan.Y, swf, shf);
//                RectangleF vis = RectangleF.Intersect(screen, lay.Content);

//                if (vis.Width < 1f || vis.Height < 1f)
//                    return true;   // exists but fully off-screen

//                if (!_enhanceImage)
//                {
//                    // =========================================================
//                    //  PIXEL-PERFECT PATH — byte-identical to your original
//                    //  control. NearestNeighbor + integer source + integer
//                    //  destination: every source pixel becomes ONE exact
//                    //  integer block. Zero interpolation. Zero blur. Fastest
//                    //  blit GDI+ can perform. Mipmaps NEVER touch this path.
//                    // =========================================================
//                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
//                    g.PixelOffsetMode = PixelOffsetMode.Half;

//                    int sx = (int)Math.Floor((vis.X - _pan.X) / _zoom);
//                    int sy = (int)Math.Floor((vis.Y - _pan.Y) / _zoom);
//                    int ex = (int)Math.Ceiling((vis.Right - _pan.X) / _zoom);
//                    int ey = (int)Math.Ceiling((vis.Bottom - _pan.Y) / _zoom);

//                    if (sx < 0) sx = 0;
//                    if (sy < 0) sy = 0;
//                    if (ex > iw) ex = iw;
//                    if (ey > ih) ey = ih;

//                    int cw = ex - sx;
//                    int ch = ey - sy;
//                    if (cw < 1 || ch < 1) return true;

//                    int dx = (int)Math.Round(_pan.X + sx * _zoom);
//                    int dy = (int)Math.Round(_pan.Y + sy * _zoom);
//                    int dw = Math.Max(1, (int)Math.Round(cw * _zoom));
//                    int dh = Math.Max(1, (int)Math.Round(ch * _zoom));

//                    g.DrawImage(_image,
//                                new Rectangle(dx, dy, dw, dh),
//                                new Rectangle(sx, sy, cw, ch),
//                                GraphicsUnit.Pixel);

//                    using (Pen ip = new Pen(Color.FromArgb(70, pal.TextDim), 1f))
//                        g.DrawRectangle(ip,
//                                        (int)Math.Round(_pan.X), (int)Math.Round(_pan.Y),
//                                        (int)Math.Round(swf), (int)Math.Round(shf));

//                    if (_controlStyle == ViewerStyle.Cyberpunk)
//                        DrawCornerBrackets(g, screen, 10f, 2.2f,
//                                           Color.FromArgb(200, pal.AccentAlt));

//                    return true;
//                }

//                // =========================================================
//                //  ENHANCED PATH — opt-in only (EnhanceImage == true).
//                //  Mipmaps accelerate heavy downscaling here (2x+ zoom-out
//                //  on 24 MP+ images decoded through LoadFromFileAsync).
//                // =========================================================
//                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
//                g.PixelOffsetMode = PixelOffsetMode.Half;

//                Bitmap source = _image;
//                RectangleF src = ClampToSource(new RectangleF(
//                    (vis.X - _pan.X) / _zoom,
//                    (vis.Y - _pan.Y) / _zoom,
//                    vis.Width / _zoom,
//                    vis.Height / _zoom), iw, ih);

//                if (_useMipmaps && _mips != null && _mips.Count > 0 && _zoom <= 0.5f)
//                {
//                    double ratio = 1.0 / (double)_zoom;
//                    int level = 0;
//                    while (level + 1 < _mips.Count && Math.Pow(2, level + 2) <= ratio) level++;

//                    Bitmap mip = _mips[level];
//                    if (mip != null && mip.Width > 1 && mip.Height > 1)
//                    {
//                        float kx = (float)mip.Width / iw;
//                        float ky = (float)mip.Height / ih;
//                        src = RectangleF.FromLTRB(src.X * kx, src.Y * ky,
//                                                  src.Right * kx, src.Bottom * ky);
//                        src = ClampToSource(src, mip.Width, mip.Height);
//                        source = mip;
//                    }
//                }

//                g.DrawImage(source, vis, src, GraphicsUnit.Pixel);
//                return true;
//            }
//            catch (ArgumentException)
//            {
//                _image = null;
//                _needsAutoFit = true;
//                _imageLostPending = true;
//                _hudSourceText = "—";
//                return false;
//            }
//            finally
//            {
//                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
//                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
//            }
//        }

//        private static RectangleF ClampToSource(RectangleF r, int iw, int ih)
//        {
//            float x = ClampF(r.X, 0f, iw);
//            float y = ClampF(r.Y, 0f, ih);
//            float right = ClampF(r.Right, x, iw);
//            float bottom = ClampF(r.Bottom, y, ih);
//            return RectangleF.FromLTRB(x, y, right, bottom);
//        }

//        private void DrawDecodingState(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF c = lay.Content;
//            float cx = c.X + c.Width / 2f;
//            float cy = c.Y + c.Height / 2f;

//            Font f = GetFont(true, 10f, FontStyle.Bold);   // cache-owned — no using
//            using (StringFormat fmt = new StringFormat())
//            {
//                fmt.Alignment = StringAlignment.Center;
//                fmt.LineAlignment = StringAlignment.Center;
//                using (SolidBrush b = new SolidBrush(pal.TextSecondary))
//                    g.DrawString("DECODING…", f, b, cx, cy, fmt);
//            }
//        }

//        private void DrawLargeLockCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF c = lay.Content;
//            float s = lay.Dpi;

//            float w = 340f * s, h = 170f * s;
//            if (w > c.Width - 16f) w = c.Width - 16f;
//            if (h > c.Height - 16f) h = c.Height - 16f;
//            if (w < 80f || h < 60f) return;

//            RectangleF card = new RectangleF(
//                c.X + (c.Width - w) / 2f, c.Y + (c.Height - h) / 2f, w, h);

//            using (GraphicsPath cp = BuildRoundedPath(card, 14f * s))
//            {
//                using (SolidBrush cb = new SolidBrush(Color.FromArgb(242, pal.Canvas)))
//                    g.FillPath(cb, cp);
//                using (Pen border = new Pen(Color.FromArgb(160, pal.ZoneWarn), 1.2f))
//                    g.DrawPath(border, cp);
//            }

//            float lockW = Math.Min(26f * s, w * 0.14f);
//            float lockH = lockW * 0.78f;
//            RectangleF lockR = new RectangleF(
//                card.X + card.Width / 2f - lockW / 2f, card.Y + 24f * s, lockW, lockH);
//            DrawPadlock(g, lockR, pal.ZoneWarn, Color.FromArgb(210, pal.HudFore));

//            Font tf = GetFont(false, 11f, FontStyle.Bold);
//            Font sf = GetFont(false, 8.75f, FontStyle.Regular);
//            float tx = card.X + 14f * s;
//            float tw = card.Width - 28f * s;

//            using (StringFormat fmt = new StringFormat())
//            {
//                fmt.Trimming = StringTrimming.EllipsisCharacter;
//                fmt.FormatFlags |= StringFormatFlags.NoWrap;

//                float y = lockR.Bottom + 12f * s;
//                using (SolidBrush tb = new SolidBrush(pal.TextPrimary))
//                    g.DrawString("LARGE IMAGE — PRO FEATURE", tf, tb, new RectangleF(tx, y, tw, 18f * s), fmt);

//                y += 24f * s;
//                using (SolidBrush db = new SolidBrush(pal.TextSecondary))
//                    g.DrawString(_largeLockSizeText + " exceeds the " + _largeThresholdMB + " MB free limit.",
//                                 sf, db, new RectangleF(tx, y, tw, 15f * s), fmt);

//                y += 19f * s;
//                using (SolidBrush hb = new SolidBrush(pal.TextDim))
//                    g.DrawString("Activate a license with the Large Images feature (L), or set EnforceLicense = false for development.",
//                                 sf, hb, new RectangleF(tx, y, tw, 15f * s), fmt);
//            }
//        }

//        private static void DrawPadlock(Graphics g, RectangleF r, Color body, Color shackle)
//        {
//            using (GraphicsPath bp = BuildRoundedPath(r, Math.Min(r.Width, r.Height) * 0.28f))
//            using (SolidBrush b = new SolidBrush(body))
//            {
//                g.FillPath(b, bp);
//            }

//            float sw = r.Width * 0.62f;
//            RectangleF sr = new RectangleF(
//                r.X + (r.Width - sw) / 2f, r.Y - r.Height * 0.52f, sw, r.Height * 0.72f);
//            using (Pen p = new Pen(shackle, Math.Max(1.6f, r.Width * 0.11f)))
//            {
//                g.DrawArc(p, sr, 180f, 180f);
//            }

//            float kd = r.Width * 0.22f;
//            using (SolidBrush kb = new SolidBrush(Color.FromArgb(140, shackle)))
//            {
//                g.FillEllipse(kb, r.X + r.Width / 2f - kd / 2f, r.Y + r.Height * 0.38f, kd, kd);
//            }
//        }

//        private void DrawEmptyState(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            RectangleF c = lay.Content;
//            float s = lay.Dpi;

//            using (GraphicsPath gridPath = new GraphicsPath())
//            using (Pen grid = new Pen(pal.Grid, 1f))
//            {
//                float step = 28f * s;
//                for (float x = c.X + step; x < c.Right; x += step)
//                {
//                    gridPath.AddLine(x, c.Y, x, c.Bottom);
//                    gridPath.StartFigure();
//                }
//                for (float y = c.Y + step; y < c.Bottom; y += step)
//                {
//                    gridPath.AddLine(c.X, y, c.Right, y);
//                    gridPath.StartFigure();
//                }
//                if (gridPath.PointCount > 0) g.DrawPath(grid, gridPath);
//            }

//            bool cyber = _controlStyle == ViewerStyle.Cyberpunk;
//            float cx = c.X + c.Width / 2f;
//            float cy = c.Y + c.Height / 2f;

//            RectangleF body = new RectangleF(cx - 43f * s, cy - 24f * s, 86f * s, 60f * s);
//            RectangleF bump = new RectangleF(cx - 15f * s, cy - 35f * s, 30f * s, 13f * s);
//            float lensR = 13f * s;
//            float innerR = 5f * s;
//            float lensCy = cy + 6f * s;
//            Color stroke = Color.FromArgb(165, pal.TextSecondary);

//            using (GraphicsPath bodyPath = BuildRoundedPath(body, 12f * s))
//            using (GraphicsPath bumpPath = BuildRoundedPath(bump, 5f * s))
//            using (Pen pen = new Pen(stroke, 2.6f))
//            {
//                g.DrawPath(pen, bumpPath);
//                g.DrawPath(pen, bodyPath);
//                using (SolidBrush fill = new SolidBrush(pal.ViewportWell))
//                {
//                    g.FillPath(fill, bumpPath);
//                    g.FillPath(fill, bodyPath);
//                }
//                g.DrawEllipse(pen, cx - lensR, lensCy - lensR, lensR * 2f, lensR * 2f);
//                using (SolidBrush fill2 = new SolidBrush(pal.ViewportWell))
//                    g.FillEllipse(fill2, cx - lensR, lensCy - lensR, lensR * 2f, lensR * 2f);
//                using (Pen inner = new Pen(Color.FromArgb(120, pal.TextSecondary), 2f))
//                    g.DrawEllipse(inner, cx - innerR, lensCy - innerR, innerR * 2f, innerR * 2f);
//            }

//            string title = cyber ? "NO SIGNAL" : "NO IMAGE LOADED";
//            string hint = cyber ? "AWAITING INPUT · DROP FILE TO SCAN"
//                                : "Drag & drop an image file, or assign the Image property";
//            float ty = body.Bottom + 22f * s;

//            // Cache-owned fonts — NEVER wrapped in using.
//            Font tf = GetFont(cyber, 10.5f, FontStyle.Bold);
//            Font sf = GetFont(false, 8.75f, FontStyle.Regular);

//            using (StringFormat fmt = new StringFormat())
//            {
//                fmt.Alignment = StringAlignment.Center;
//                fmt.LineAlignment = StringAlignment.Near;
//                fmt.FormatFlags |= StringFormatFlags.NoWrap;

//                using (SolidBrush tb = new SolidBrush(pal.TextSecondary))
//                    g.DrawString(title, tf, tb, cx, ty, fmt);
//                using (SolidBrush sb = new SolidBrush(pal.TextDim))
//                    g.DrawString(hint, sf, sb, cx, ty + 19f * s, fmt);
//            }
//        }

//        // ----------------------------------------------------------------
//        //  LAYER 5 — HUD READOUTS
//        // ----------------------------------------------------------------
//        private void DrawHudLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            if (!_showHud || !Enabled) return;
//            RectangleF c = lay.Content;
//            if (c.Width < 160f || c.Height < 96f) return;

//            Font capFont = GetFont(false, 6.75f, FontStyle.Bold);
//            Font valFont = GetFont(true, 10f, FontStyle.Bold);

//            using (StringFormat fmt = new StringFormat(
//                StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces))
//            {
//                SizeF capS = MeasureCached(g, "SOURCE", capFont, fmt, ref _mCapSrc, ref _szCapSrc);
//                SizeF valS = MeasureCached(g, _hudSourceText, valFont, fmt, ref _mValSrc, ref _szValSrc);
//                DrawHudChip(g, pal, lay, ChipAnchor.TopLeft, "SOURCE", _hudSourceText,
//                            capFont, valFont, capS, valS,
//                            _image != null ? AccentOf(pal) : pal.TextDim, false, pal.TextDim);

//                bool flat = _controlStyle == ViewerStyle.MaterialFlat;
//                Color zone = ZoomZoneColor(pal);

//                SizeF capZ = MeasureCached(g, "ZOOM", capFont, fmt, ref _mCapZoom, ref _szCapZoom);
//                SizeF valZ = MeasureCached(g, _hudZoomText, valFont, fmt, ref _mValZoom, ref _szValZoom);
//                DrawHudChip(g, pal, lay, ChipAnchor.BottomRight, "ZOOM", _hudZoomText,
//                            capFont, valFont, capZ, valZ,
//                            flat ? (Color?)null : zone, true, flat ? pal.HudFore : zone);

//                if (_hover && _image != null && _hudProbeText.Length > 0)
//                {
//                    SizeF capP = MeasureCached(g, "PROBE", capFont, fmt, ref _mCapProbe, ref _szCapProbe);
//                    SizeF valP = MeasureCached(g, _hudProbeText, valFont, fmt, ref _mValProbe, ref _szValProbe);
//                    DrawHudChip(g, pal, lay, ChipAnchor.BottomLeft, "PROBE", _hudProbeText,
//                                capFont, valFont, capP, valP,
//                                null, false, pal.TextDim);
//                }
//            }
//        }

//        private SizeF MeasureCached(Graphics g, string text, Font font, StringFormat fmt,
//                                    ref string key, ref SizeF size)
//        {
//            if (!ReferenceEquals(key, text))
//            {
//                size = g.MeasureString(text, font, int.MaxValue, fmt);
//                key = text;
//            }
//            return size;
//        }

//        private void DrawHudChip(Graphics g, ViewerPalette pal, ViewerLayout lay, ChipAnchor anchor,
//                                 string caption, string value, Font capFont, Font valFont,
//                                 SizeF capS, SizeF valS,
//                                 Color? dot, bool showBar, Color barColor)
//        {
//            float dpi = lay.Dpi;
//            RectangleF c = lay.Content;
//            float padX = 9f * dpi;
//            float padY = 6f * dpi;

//            float dotW = dot.HasValue ? 14f * dpi : 0f;
//            float w = Math.Max(capS.Width, valS.Width) + dotW + padX * 2f;
//            float h = padY + capS.Height + 2f * dpi + valS.Height + (showBar ? 6f * dpi : 0f) + padY;

//            if (w > c.Width - 12f || h > c.Height - 12f) return;

//            float inset = 10f * dpi;
//            PointF loc;
//            switch (anchor)
//            {
//                case ChipAnchor.BottomLeft: loc = new PointF(c.X + inset, c.Bottom - inset - h); break;
//                case ChipAnchor.BottomRight: loc = new PointF(c.Right - inset - w, c.Bottom - inset - h); break;
//                default: loc = new PointF(c.X + inset, c.Y + inset); break;
//            }
//            RectangleF chip = new RectangleF(loc.X, loc.Y, w, h);

//            using (GraphicsPath path = BuildRoundedPath(chip, 7f * dpi))
//            {
//                using (SolidBrush bg = new SolidBrush(pal.HudBack)) g.FillPath(bg, path);
//                if (pal.HudBorder.A > 0)
//                {
//                    using (Pen bp = new Pen(pal.HudBorder, 1f)) g.DrawPath(bp, path);
//                }

//                if (_controlStyle == ViewerStyle.SoftNeumorphic)
//                {
//                    g.SetClip(new RectangleF(chip.X, chip.Y, chip.Width, chip.Height * 0.5f));
//                    using (Pen tp = new Pen(pal.ShadowDark, 1f)) g.DrawPath(tp, path);
//                    g.ResetClip();
//                    g.SetClip(new RectangleF(chip.X, chip.Y + chip.Height * 0.5f,
//                                             chip.Width, chip.Height * 0.5f));
//                    using (Pen bt = new Pen(pal.ShadowLight, 1f)) g.DrawPath(bt, path);
//                    g.ResetClip();
//                }
//            }

//            using (StringFormat fmt = new StringFormat(StringFormatFlags.NoWrap))
//            {
//                float tx = chip.X + padX;
//                using (SolidBrush capBrush = new SolidBrush(pal.HudForeDim))
//                    g.DrawString(caption, capFont, capBrush, tx, chip.Y + padY, fmt);

//                float vy = chip.Y + padY + capS.Height + 2f * dpi;
//                using (SolidBrush valBrush = new SolidBrush(pal.HudFore))
//                    g.DrawString(value, valFont, valBrush, tx, vy, fmt);

//                if (dot.HasValue)
//                {
//                    float d = 6.8f * dpi;
//                    using (SolidBrush db = new SolidBrush(dot.Value))
//                        g.FillEllipse(db, chip.Right - padX - d, vy + valS.Height / 2f - d / 2f, d, d);
//                }

//                if (showBar)
//                {
//                    float by = chip.Bottom - padY - 2.2f * dpi;
//                    float trackW = w - padX * 2f;

//                    using (GraphicsPath track = BuildRoundedPath(
//                        new RectangleF(tx, by, trackW, 3f * dpi), 1.5f * dpi))
//                    using (SolidBrush tb = new SolidBrush(Color.FromArgb(70, pal.HudFore)))
//                        g.FillPath(tb, track);

//                    float t = ZoomBarT();
//                    if (t > 0.01f)
//                    {
//                        using (GraphicsPath fill = BuildRoundedPath(
//                            new RectangleF(tx, by, trackW * t, 3f * dpi), 1.5f * dpi))
//                        using (SolidBrush fb = new SolidBrush(barColor))
//                            g.FillPath(fb, fill);
//                    }
//                }
//            }
//        }

//        // ----------------------------------------------------------------
//        //  LAYER 6 — STATE CHROME
//        // ----------------------------------------------------------------
//        private void DrawStateLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            if (_showCrosshair && _hover && !_panning && Enabled &&
//                (_controlStyle == ViewerStyle.Cyberpunk || _controlStyle == ViewerStyle.DashboardPremium))
//            {
//                using (GraphicsPath clip = BuildRoundedPath(lay.Content, lay.ContentRadius))
//                {
//                    g.SetClip(clip);
//                    float px = _hoverPos.X;
//                    float py = _hoverPos.Y;
//                    float gap = 11f * lay.Dpi;
//                    Color accent = AccentOf(pal);

//                    using (Pen pen = new Pen(Color.FromArgb(115, accent), 1f))
//                    {
//                        g.DrawLine(pen, lay.Content.X, py, px - gap, py);
//                        g.DrawLine(pen, px + gap, py, lay.Content.Right, py);
//                        g.DrawLine(pen, px, lay.Content.Y, px, py - gap);
//                        g.DrawLine(pen, px, py + gap, px, lay.Content.Bottom);
//                    }
//                    using (Pen cp = new Pen(Color.FromArgb(190, accent), 1.2f))
//                        g.DrawEllipse(cp, px - 3.5f, py - 3.5f, 7f, 7f);

//                    g.ResetClip();
//                }
//            }

//            if (Focused && Enabled)
//            {
//                using (GraphicsPath fp = BuildRoundedPath(Expand(lay.View, 3f), lay.ViewRadius + 3f))
//                using (Pen pen = new Pen(Color.FromArgb(215, AccentOf(pal)), 1.4f))
//                    g.DrawPath(pen, fp);
//            }

//            if (!Enabled)
//            {
//                using (SolidBrush veil = new SolidBrush(Color.FromArgb(110, pal.Canvas)))
//                    g.FillRectangle(veil, 0f, 0f, Width, Height);
//            }
//        }

//        // ----------------------------------------------------------------
//        //  LAYER 7 — EMBEDDED FRAME NAVIGATION (pages / collection items)
//        // ----------------------------------------------------------------
//        private void DrawNavigationBar(Graphics g, ViewerPalette pal, ViewerLayout lay)
//        {
//            _navRectsValid = false;
//            if (!_showNavigationBar || !Enabled) return;
//            if (_frameCount <= 1) return;
//            if (_mode != ContentMode.Multipage && _mode != ContentMode.Collection) return;

//            RectangleF c = lay.Content;
//            float s = lay.Dpi;
//            if (c.Width < 220f * s || c.Height < 150f * s) return;

//            bool locked = _multipageLocked;
//            string label = _frameDecoding ? "DECODING"
//                : (_mode == ContentMode.Multipage ? "PAGE " : "IMAGE ") +
//                  (_frameIndex + 1).ToString() + " / " + _frameCount.ToString();

//            Font f = GetFont(true, 9f, FontStyle.Bold);       // cache-owned — no using
//            Color accent = AccentOf(pal);

//            using (StringFormat fmt = new StringFormat(
//                StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces))
//            {
//                SizeF txt = MeasureCached(g, label, f, fmt, ref _mNavKey, ref _szNavSize);

//                float padX = 10f * s;
//                float padY = 7f * s;
//                float lockW = locked ? 18f * s : 0f;
//                float barW = txt.Width + padX * 2f + lockW;
//                float barH = txt.Height + padY * 2f;
//                float btnD = Math.Max(28f * s, barH);
//                float gap = 8f * s;
//                float totalW = btnD * 2f + gap * 2f + barW;
//                if (totalW > c.Width - 24f) return;

//                float x = c.X + c.Width / 2f - totalW / 2f;
//                float y = c.Bottom - 12f * s - btnD;

//                // ---- prev button ----
//                RectangleF prevR = new RectangleF(x, y, btnD, btnD);
//                DrawNavButton(g, pal, prevR, -1, _navHover == 1, locked, accent);

//                // ---- label pill ----
//                RectangleF barR = new RectangleF(prevR.Right + gap, y + (btnD - barH) / 2f, barW, barH);
//                using (GraphicsPath bp = BuildRoundedPath(barR, barH / 2f))
//                {
//                    using (SolidBrush bb = new SolidBrush(pal.HudBack)) g.FillPath(bb, bp);
//                    if (pal.HudBorder.A > 0)
//                    {
//                        using (Pen p = new Pen(pal.HudBorder, 1f)) g.DrawPath(p, bp);
//                    }
//                }
//                using (StringFormat sf = new StringFormat(StringFormatFlags.NoWrap))
//                {
//                    using (SolidBrush tb = new SolidBrush(
//                        _frameDecoding ? pal.HudForeDim : pal.HudFore))
//                    {
//                        g.DrawString(label, f, tb, barR.X + padX, barR.Y + padY, sf);
//                    }
//                }
//                if (locked)
//                {
//                    RectangleF lockR = new RectangleF(
//                        barR.Right - padX - 8f * s, barR.Y + (barH - 11f * s) / 2f, 8f * s, 10.5f * s);
//                    DrawPadlock(g, lockR, Color.FromArgb(200, pal.ZoneWarn),
//                                Color.FromArgb(170, pal.HudFore));
//                }

//                // ---- next button ----
//                RectangleF nextR = new RectangleF(barR.Right + gap, y, btnD, btnD);
//                DrawNavButton(g, pal, nextR, +1, _navHover == 2, locked, accent);

//                _navPrevRect = prevR;
//                _navNextRect = nextR;
//                _navRectsValid = true;
//            }
//        }

//        private void DrawNavButton(Graphics g, ViewerPalette pal, RectangleF r, int dir,
//                                   bool hover, bool locked, Color accent)
//        {
//            using (GraphicsPath p = BuildRoundedPath(r, r.Width / 2f))
//            {
//                Color bg = (hover && !locked) ? Color.FromArgb(70, accent) : pal.HudBack;
//                using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
//                if (pal.HudBorder.A > 0)
//                {
//                    Color bc = (hover && !locked) ? accent : pal.HudBorder;
//                    using (Pen bp = new Pen(bc, 1f)) g.DrawPath(bp, p);
//                }

//                float m = r.Width * 0.30f;
//                PointF ctr = new PointF(r.X + r.Width / 2f, r.Y + r.Height / 2f);
//                using (GraphicsPath tri = new GraphicsPath())
//                {
//                    if (dir < 0)
//                    {
//                        tri.AddLine(ctr.X + m * 0.6f, ctr.Y - m, ctr.X - m * 0.7f, ctr.Y);
//                        tri.AddLine(ctr.X - m * 0.7f, ctr.Y, ctr.X + m * 0.6f, ctr.Y + m);
//                    }
//                    else
//                    {
//                        tri.AddLine(ctr.X - m * 0.6f, ctr.Y - m, ctr.X + m * 0.7f, ctr.Y);
//                        tri.AddLine(ctr.X + m * 0.7f, ctr.Y, ctr.X - m * 0.6f, ctr.Y + m);
//                    }
//                    tri.CloseFigure();

//                    Color glyph = locked ? Color.FromArgb(110, pal.HudFore) : pal.HudFore;
//                    using (SolidBrush gb = new SolidBrush(glyph)) g.FillPath(gb, tri);
//                }
//            }
//        }

//        private bool NavHitTest(Point p, out int direction)
//        {
//            direction = 0;
//            if (!_navRectsValid) return false;
//            if (_navPrevRect.Contains(p)) { direction = -1; return true; }
//            if (_navNextRect.Contains(p)) { direction = 1; return true; }
//            return false;
//        }

//        // ----------------------------------------------------------------
//        //  BATCHED MICRO-DETAILS
//        // ----------------------------------------------------------------
//        private static void DrawEdgeTicks(Graphics g, ViewerPalette pal, RectangleF c, float dpi)
//        {
//            SmoothingMode prev = g.SmoothingMode;
//            g.SmoothingMode = SmoothingMode.None;

//            using (GraphicsPath minorPath = new GraphicsPath())
//            using (GraphicsPath majorPath = new GraphicsPath())
//            using (Pen minor = new Pen(Color.FromArgb(110, pal.Tick), 1f))
//            using (Pen major = new Pen(Color.FromArgb(210, pal.Tick), 1f))
//            {
//                float step = 9f * dpi;

//                int i = 0;
//                for (float x = c.X + 3f; x < c.Right - 2f; x += step)
//                {
//                    GraphicsPath target = (i % 5 == 0) ? majorPath : minorPath;
//                    float len = (i % 5 == 0) ? 7f * dpi : 4f * dpi;
//                    target.AddLine(x, c.Y + 1f, x, c.Y + 1f + len);
//                    target.StartFigure();
//                    i++;
//                }

//                i = 0;
//                for (float y = c.Y + 3f; y < c.Bottom - 2f; y += step)
//                {
//                    GraphicsPath target = (i % 5 == 0) ? majorPath : minorPath;
//                    float len = (i % 5 == 0) ? 7f * dpi : 4f * dpi;
//                    target.AddLine(c.X + 1f, y, c.X + 1f + len, y);
//                    target.StartFigure();
//                    i++;
//                }

//                if (minorPath.PointCount > 0) g.DrawPath(minor, minorPath);
//                if (majorPath.PointCount > 0) g.DrawPath(major, majorPath);
//            }

//            g.SmoothingMode = prev;
//        }

//        private static void DrawScanlines(Graphics g, ViewerPalette pal, RectangleF c)
//        {
//            SmoothingMode prev = g.SmoothingMode;
//            g.SmoothingMode = SmoothingMode.None;

//            using (GraphicsPath path = new GraphicsPath())
//            using (Pen pen = new Pen(Color.FromArgb(9, pal.Accent), 1f))
//            {
//                for (float y = c.Y + 2f; y < c.Bottom; y += 4f)
//                {
//                    path.AddLine(c.X, y, c.Right, y);
//                    path.StartFigure();
//                }
//                if (path.PointCount > 0) g.DrawPath(pen, path);
//            }

//            g.SmoothingMode = prev;
//        }

//        private static void DrawCornerBrackets(Graphics g, RectangleF rect, float len, float width, Color color)
//        {
//            using (Pen pen = new Pen(color, width))
//            {
//                pen.StartCap = LineCap.Round;
//                pen.EndCap = LineCap.Round;

//                float l = rect.Left, t = rect.Top, r = rect.Right, b = rect.Bottom;

//                g.DrawLine(pen, l, t, l + len, t); g.DrawLine(pen, l, t, l, t + len);
//                g.DrawLine(pen, r, t, r - len, t); g.DrawLine(pen, r, t, r, t + len);
//                g.DrawLine(pen, l, b, l + len, b); g.DrawLine(pen, l, b, l, b - len);
//                g.DrawLine(pen, r, b, r - len, b); g.DrawLine(pen, r, b, r, b - len);
//            }
//        }

//        private static void DrawSoftShadow(Graphics g, RectangleF rect, float radius, Color shadow,
//                                           float depth, int steps)
//        {
//            int layerAlpha = Math.Max(4, shadow.A / steps);
//            for (int i = steps; i >= 1; i--)
//            {
//                float off = 1f + (depth * (i - 1) / steps);
//                using (GraphicsPath p = BuildRoundedPath(OffsetRect(rect, 0f, off), radius + i * 0.7f))
//                using (SolidBrush b = new SolidBrush(Color.FromArgb(layerAlpha, shadow)))
//                {
//                    g.FillPath(b, p);
//                }
//            }
//        }

//        // ----------------------------------------------------------------
//        //  LAYOUT ENGINE
//        // ----------------------------------------------------------------
//        private ViewerLayout ComputeLayout()
//        {
//            float dpi = Math.Max(1f, _dpiScale);

//            float maxPad = Math.Min(Math.Max(0f, (Width - 2f) / 2f),
//                                    Math.Max(0f, (Height - 2f) / 2f));

//            float pad;
//            if (_viewportPadding >= 0)
//            {
//                pad = Math.Min(_viewportPadding * dpi, maxPad);
//            }
//            else
//            {
//                float padMul = _controlStyle == ViewerStyle.MaterialFlat ? 1.45f : 1f;
//                float basePad = Math.Min(Width, Height) * 0.035f;
//                pad = Math.Max(11f * dpi, Math.Min(26f * dpi, basePad)) * padMul;
//                pad = Math.Min(pad, maxPad);
//            }

//            RectangleF view = RectangleF.FromLTRB(pad, pad,
//                Math.Max(pad + 2f, Width - pad),
//                Math.Max(pad + 2f, Height - pad));

//            float radius;
//            float inset;
//            switch (_controlStyle)
//            {
//                case ViewerStyle.FluentGlass: radius = 14f * dpi; inset = 1.5f * dpi; break;
//                case ViewerStyle.MaterialFlat: radius = 3f * dpi; inset = 0.75f * dpi; break;
//                case ViewerStyle.SoftNeumorphic: radius = 20f * dpi; inset = 11f * dpi; break;
//                case ViewerStyle.Cyberpunk: radius = 8f * dpi; inset = 1.25f * dpi; break;
//                default: radius = 10f * dpi; inset = 1.25f * dpi; break;
//            }

//            float maxInset = Math.Min(Math.Max(0f, (view.Width - 2f) / 2f),
//                                      Math.Max(0f, (view.Height - 2f) / 2f));
//            inset = Math.Min(inset, maxInset);

//            float cr = Math.Max(1f, radius - inset - 0.5f);
//            float minDim = Math.Min(view.Width, view.Height);
//            if (cr * 2f > minDim) cr = minDim / 2f;

//            return new ViewerLayout
//            {
//                View = view,
//                Content = Deflate(view, inset),
//                ViewRadius = radius,
//                ContentRadius = cr,
//                Dpi = dpi
//            };
//        }

//        private Color ZoomZoneColor(ViewerPalette pal)
//        {
//            if (_zoom < 2f) return pal.ZoneGood;
//            if (_zoom < 8f) return pal.ZoneWarn;
//            return pal.ZoneHot;
//        }

//        private float ZoomBarT()
//        {
//            double lo = Math.Log10((double)_minZoom);
//            double hi = Math.Log10((double)_maxZoom);
//            if (hi - lo < 0.0001) return 0f;

//            double t = (Math.Log10((double)_zoom) - lo) / (hi - lo);
//            if (t < 0.0) t = 0.0;
//            if (t > 1.0) t = 1.0;
//            return (float)t;
//        }

//        // ----------------------------------------------------------------
//        //  GEOMETRY & COLOR PRIMITIVES
//        // ----------------------------------------------------------------
//        private static GraphicsPath BuildRoundedPath(RectangleF rect, float radius)
//        {
//            GraphicsPath path = new GraphicsPath();

//            float w = rect.Width;
//            float h = rect.Height;
//            if (float.IsNaN(w) || float.IsNaN(h) || float.IsInfinity(w) || float.IsInfinity(h))
//                return path;
//            if (w < 0f) w = 0f;
//            if (h < 0f) h = 0f;
//            if (float.IsNaN(radius) || radius < 0f) radius = 0f;
//            if (w < 0.5f || h < 0.5f) return path;

//            rect = new RectangleF(rect.X, rect.Y, w, h);

//            float maxR = Math.Min(w, h) / 2f;
//            if (radius < 0.5f) { path.AddRectangle(rect); return path; }
//            if (radius > maxR) radius = maxR;

//            float d = radius * 2f;
//            path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
//            path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
//            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
//            path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
//            path.CloseFigure();
//            return path;
//        }

//        private static RectangleF Deflate(RectangleF r, float v)
//        { return new RectangleF(r.X + v, r.Y + v, r.Width - 2f * v, r.Height - 2f * v); }

//        private static RectangleF Expand(RectangleF r, float v)
//        { return new RectangleF(r.X - v, r.Y - v, r.Width + 2f * v, r.Height + 2f * v); }

//        private static RectangleF OffsetRect(RectangleF r, float dx, float dy)
//        { return new RectangleF(r.X + dx, r.Y + dy, r.Width, r.Height); }

//        private static Color Lerp(Color a, Color b, float t)
//        {
//            return Color.FromArgb(
//                a.R + (int)((b.R - a.R) * t),
//                a.G + (int)((b.G - a.G) * t),
//                a.B + (int)((b.B - a.B) * t));
//        }

//        private static Color AlphaScale(Color c, int percent)
//        {
//            int a = (int)(c.A * percent / 100.0);
//            if (a > 255) a = 255;
//            return Color.FromArgb(a, c);
//        }

//        private static float ClampF(float v, float lo, float hi)
//        {
//            if (float.IsNaN(v)) return lo;
//            return v < lo ? lo : (v > hi ? hi : v);
//        }

//        // ----------------------------------------------------------------
//        //  HUD TEXT UPDATES
//        // ----------------------------------------------------------------
//        private void UpdateZoomText()
//        {
//            _hudZoomText = (_zoom * 100f).ToString("0.#") + " %";
//        }

//        private void UpdateSourceText()
//        {
//            try
//            {
//                _hudSourceText = _image != null
//                    ? string.Format("{0} × {1}", _image.Width, _image.Height)
//                    : "—";
//            }
//            catch { _hudSourceText = "—"; }
//        }

//        private bool UpdateProbeText()
//        {
//            if (_image == null) return false;
//            try
//            {
//                PointF ip = ScreenToImageF(_hoverPos);
//                int px = (int)Math.Round(ip.X);
//                int py = (int)Math.Round(ip.Y);
//                if (px != _probeX || py != _probeY)
//                {
//                    _probeX = px;
//                    _probeY = py;
//                    _hudProbeText = string.Format("X {0}   Y {1}", px, py);
//                    return true;
//                }
//            }
//            catch { }
//            return false;
//        }

//        private void ResetProbe()
//        {
//            _probeX = int.MinValue;
//            _probeY = int.MinValue;
//            _hudProbeText = "";
//        }

//        // ----------------------------------------------------------------
//        //  PALETTE ENGINE — 5 styles × 2 themes
//        // ----------------------------------------------------------------
//        private static ViewerPalette BuildPalette(ThemeMode theme, ViewerStyle style)
//        {
//            bool dark = theme == ThemeMode.Dark;
//            switch (style)
//            {
//                case ViewerStyle.FluentGlass: return GlassPalette(dark);
//                case ViewerStyle.MaterialFlat: return MaterialPalette(dark);
//                case ViewerStyle.SoftNeumorphic: return NeumorphicPalette(dark);
//                case ViewerStyle.Cyberpunk: return CyberPalette();
//                default: return DashboardPalette(dark);
//            }
//        }

//        private static Color C(int rgb)
//        { return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF); }

//        private static ViewerPalette DashboardPalette(bool dark)
//        {
//            if (dark)
//            {
//                return new ViewerPalette
//                {
//                    Canvas = C(0x0F1218),
//                    BackdropGlow = Color.FromArgb(0, Color.White),
//                    ViewportFace = C(0x151A22),
//                    ViewportWell = C(0x11151C),
//                    Border = C(0x28303E),
//                    BorderHighlight = Color.White,
//                    TextPrimary = C(0xE8EDF5),
//                    TextSecondary = C(0x97A3B5),
//                    TextDim = C(0x5E6A80),
//                    Accent = C(0x4C8DFF),
//                    AccentAlt = C(0x38BDF8),
//                    ZoneGood = C(0x2FBF71),
//                    ZoneWarn = C(0xF5A524),
//                    ZoneHot = C(0xEF4B4B),
//                    Grid = C(0x1B202A),
//                    Tick = C(0x7A87A0),
//                    HudBack = Color.FromArgb(190, C(0x0B0E14)),
//                    HudBorder = Color.FromArgb(60, Color.White),
//                    HudFore = C(0xE8EDF5),
//                    HudForeDim = C(0x97A3B5),
//                    Shadow = Color.FromArgb(150, Color.Black),
//                    ShadowLight = Color.FromArgb(0, Color.White),
//                    ShadowDark = Color.FromArgb(0, Color.Black),
//                    HoverOverlay = Color.White,
//                    FocusRing = C(0x4C8DFF)
//                };
//            }
//            return new ViewerPalette
//            {
//                Canvas = C(0xF3F5F9),
//                BackdropGlow = Color.FromArgb(0, Color.White),
//                ViewportFace = C(0xFFFFFF),
//                ViewportWell = C(0xFAFBFE),
//                Border = C(0xDCE3EE),
//                BorderHighlight = Color.White,
//                TextPrimary = C(0x16202F),
//                TextSecondary = C(0x5F6C7E),
//                TextDim = C(0x9AA6B8),
//                Accent = C(0x2563EB),
//                AccentAlt = C(0x0EA5E9),
//                ZoneGood = C(0x16A34A),
//                ZoneWarn = C(0xD97706),
//                ZoneHot = C(0xDC2626),
//                Grid = C(0xE7ECF4),
//                Tick = C(0xB9C4D4),
//                HudBack = Color.FromArgb(172, C(0xF9FBFE)),
//                HudBorder = Color.FromArgb(170, Color.White),
//                HudFore = C(0x16202F),
//                HudForeDim = C(0x5F6C7E),
//                Shadow = Color.FromArgb(30, C(0x0E1626)),
//                ShadowLight = Color.FromArgb(0, Color.White),
//                ShadowDark = Color.FromArgb(0, Color.Black),
//                HoverOverlay = Color.Black,
//                FocusRing = C(0x2563EB)
//            };
//        }

//        private static ViewerPalette GlassPalette(bool dark)
//        {
//            if (dark)
//            {
//                return new ViewerPalette
//                {
//                    Canvas = C(0x171B24),
//                    BackdropGlow = Color.FromArgb(38, C(0x00C8FF)),
//                    ViewportFace = Color.FromArgb(46, C(0x2A3140)),
//                    ViewportWell = Color.FromArgb(235, C(0x1B2029)),
//                    Border = Color.FromArgb(0, Color.White),
//                    BorderHighlight = Color.White,
//                    TextPrimary = C(0xEBF1FB),
//                    TextSecondary = C(0x9BA8BD),
//                    TextDim = C(0x63718A),
//                    Accent = C(0x00C8FF),
//                    AccentAlt = C(0x7C6CFF),
//                    ZoneGood = C(0x1FBF6B),
//                    ZoneWarn = C(0xF5A524),
//                    ZoneHot = C(0xF4506C),
//                    Grid = Color.FromArgb(26, C(0x6E82A6)),
//                    Tick = Color.FromArgb(80, C(0x5D6F92)),
//                    HudBack = Color.FromArgb(125, C(0x0E1219)),
//                    HudBorder = Color.FromArgb(140, Color.White),
//                    HudFore = C(0xEBF1FB),
//                    HudForeDim = C(0x9BA8BD),
//                    Shadow = Color.FromArgb(80, Color.Black),
//                    ShadowLight = Color.FromArgb(0, Color.White),
//                    ShadowDark = Color.FromArgb(0, Color.Black),
//                    HoverOverlay = C(0x00C8FF),
//                    FocusRing = C(0x00C8FF)
//                };
//            }
//            return new ViewerPalette
//            {
//                Canvas = C(0xE8EEF7),
//                BackdropGlow = Color.FromArgb(70, Color.White),
//                ViewportFace = Color.FromArgb(52, Color.White),
//                ViewportWell = Color.FromArgb(150, Color.White),
//                Border = Color.FromArgb(0, Color.White),
//                BorderHighlight = Color.White,
//                TextPrimary = C(0x0F2440),
//                TextSecondary = C(0x5D6F8C),
//                TextDim = C(0x8FA0B8),
//                Accent = C(0x0A84FF),
//                AccentAlt = C(0x7C5CFF),
//                ZoneGood = C(0x0E9F6E),
//                ZoneWarn = C(0xF0A63A),
//                ZoneHot = C(0xE5484D),
//                Grid = Color.FromArgb(24, C(0x2A3C5E)),
//                Tick = Color.FromArgb(80, C(0x334666)),
//                HudBack = Color.FromArgb(150, Color.White),
//                HudBorder = Color.FromArgb(200, Color.White),
//                HudFore = C(0x0F2440),
//                HudForeDim = C(0x5D6F8C),
//                Shadow = Color.FromArgb(55, C(0x1E2E4C)),
//                ShadowLight = Color.FromArgb(0, Color.White),
//                ShadowDark = Color.FromArgb(0, Color.Black),
//                HoverOverlay = C(0x0A84FF),
//                FocusRing = C(0x0A84FF)
//            };
//        }

//        private static ViewerPalette MaterialPalette(bool dark)
//        {
//            if (dark)
//            {
//                return new ViewerPalette
//                {
//                    Canvas = C(0x121212),
//                    BackdropGlow = Color.FromArgb(0, Color.White),
//                    ViewportFace = C(0x1E1E1E),
//                    ViewportWell = C(0x1A1A1A),
//                    Border = Color.FromArgb(0, Color.White),
//                    BorderHighlight = Color.FromArgb(0, Color.White),
//                    TextPrimary = C(0xF2F2F2),
//                    TextSecondary = C(0xB0B0B0),
//                    TextDim = C(0x8A8A8A),
//                    Accent = C(0xBB86FC),
//                    AccentAlt = C(0x03DAC6),
//                    ZoneGood = C(0x66BB6A),
//                    ZoneWarn = C(0xFFB74D),
//                    ZoneHot = C(0xEF5350),
//                    Grid = C(0x232323),
//                    Tick = C(0x2E2E2E),
//                    HudBack = C(0xBB86FC),
//                    HudBorder = Color.FromArgb(0, Color.White),
//                    HudFore = C(0x141218),
//                    HudForeDim = Color.FromArgb(150, C(0x141218)),
//                    Shadow = Color.FromArgb(0, Color.Black),
//                    ShadowLight = Color.FromArgb(0, Color.White),
//                    ShadowDark = Color.FromArgb(0, Color.Black),
//                    HoverOverlay = Color.White,
//                    FocusRing = C(0xBB86FC)
//                };
//            }
//            return new ViewerPalette
//            {
//                Canvas = C(0xF5F5F5),
//                BackdropGlow = Color.FromArgb(0, Color.White),
//                ViewportFace = C(0xFFFFFF),
//                ViewportWell = C(0xFFFFFF),
//                Border = Color.FromArgb(0, Color.White),
//                BorderHighlight = Color.FromArgb(0, Color.White),
//                TextPrimary = C(0x212121),
//                TextSecondary = C(0x757575),
//                TextDim = C(0x9E9E9E),
//                Accent = C(0x6200EE),
//                AccentAlt = C(0x03DAC6),
//                ZoneGood = C(0x2E7D32),
//                ZoneWarn = C(0xEF6C00),
//                ZoneHot = C(0xC62828),
//                Grid = C(0xEEEEEE),
//                Tick = C(0xE0E0E0),
//                HudBack = C(0x6200EE),
//                HudBorder = Color.FromArgb(0, Color.White),
//                HudFore = Color.White,
//                HudForeDim = Color.FromArgb(178, Color.White),
//                Shadow = Color.FromArgb(0, Color.Black),
//                ShadowLight = Color.FromArgb(0, Color.White),
//                ShadowDark = Color.FromArgb(0, Color.Black),
//                HoverOverlay = Color.Black,
//                FocusRing = C(0x6200EE)
//            };
//        }

//        private static ViewerPalette NeumorphicPalette(bool dark)
//        {
//            if (dark)
//            {
//                return new ViewerPalette
//                {
//                    Canvas = C(0x2A2F3A),
//                    BackdropGlow = Color.FromArgb(0, Color.White),
//                    ViewportFace = C(0x2A2F3A),
//                    ViewportWell = C(0x252A33),
//                    Border = Color.FromArgb(0, Color.White),
//                    BorderHighlight = Color.FromArgb(30, Color.White),
//                    TextPrimary = C(0xD6DCE8),
//                    TextSecondary = C(0x8791A6),
//                    TextDim = C(0x5F6879),
//                    Accent = C(0x7C8CF8),
//                    AccentAlt = C(0x9EA8FA),
//                    ZoneGood = C(0x58B87E),
//                    ZoneWarn = C(0xC9A55A),
//                    ZoneHot = C(0xC96A5E),
//                    Grid = Color.FromArgb(24, C(0x6A748C)),
//                    Tick = Color.FromArgb(60, C(0x6A748C)),
//                    HudBack = C(0x2A2F3A),
//                    HudBorder = Color.FromArgb(0, Color.White),
//                    HudFore = C(0xD6DCE8),
//                    HudForeDim = C(0x8791A6),
//                    Shadow = Color.FromArgb(70, C(0x1C2028)),
//                    ShadowLight = Color.FromArgb(130, C(0x3B4250)),
//                    ShadowDark = Color.FromArgb(160, C(0x1C2028)),
//                    HoverOverlay = Color.White,
//                    FocusRing = C(0x7C8CF8)
//                };
//            }
//            return new ViewerPalette
//            {
//                Canvas = C(0xE4E9F1),
//                BackdropGlow = Color.FromArgb(0, Color.White),
//                ViewportFace = C(0xE4E9F1),
//                ViewportWell = C(0xDCE2EC),
//                Border = Color.FromArgb(0, Color.White),
//                BorderHighlight = Color.White,
//                TextPrimary = C(0x47536E),
//                TextSecondary = C(0x8B96AD),
//                TextDim = C(0xA9B3C7),
//                Accent = C(0x6C7BF2),
//                AccentAlt = C(0x9BA6F5),
//                ZoneGood = C(0x6FBF8E),
//                ZoneWarn = C(0xD2A24C),
//                ZoneHot = C(0xD26A5C),
//                Grid = Color.FromArgb(26, C(0x9FACC6)),
//                Tick = Color.FromArgb(60, C(0x9FACC6)),
//                HudBack = C(0xE4E9F1),
//                HudBorder = Color.FromArgb(0, Color.White),
//                HudFore = C(0x47536E),
//                HudForeDim = C(0x8B96AD),
//                Shadow = Color.FromArgb(60, C(0xC3CDDF)),
//                ShadowLight = Color.FromArgb(210, Color.White),
//                ShadowDark = Color.FromArgb(170, C(0xC3CDDF)),
//                HoverOverlay = Color.White,
//                FocusRing = C(0x6C7BF2)
//            };
//        }

//        private static ViewerPalette CyberPalette()
//        {
//            return new ViewerPalette
//            {
//                Canvas = C(0x05070A),
//                BackdropGlow = Color.FromArgb(22, C(0x00E5FF)),
//                ViewportFace = C(0x0A0D13),
//                ViewportWell = C(0x080A10),
//                Border = C(0x00E5FF),
//                BorderHighlight = Color.FromArgb(0, Color.White),
//                TextPrimary = C(0xD9F5FF),
//                TextSecondary = C(0x6E8CA0),
//                TextDim = C(0x45586A),
//                Accent = C(0x00E5FF),
//                AccentAlt = C(0xFF2E97),
//                ZoneGood = C(0x00FFA3),
//                ZoneWarn = C(0xFFE066),
//                ZoneHot = C(0xFF3860),
//                Grid = Color.FromArgb(22, C(0x00E5FF)),
//                Tick = Color.FromArgb(80, C(0x00E5FF)),
//                HudBack = Color.FromArgb(200, C(0x05080E)),
//                HudBorder = Color.FromArgb(90, C(0x00E5FF)),
//                HudFore = C(0xD9F5FF),
//                HudForeDim = C(0x5E7E93),
//                Shadow = Color.FromArgb(140, Color.Black),
//                ShadowLight = Color.FromArgb(0, Color.White),
//                ShadowDark = Color.FromArgb(0, Color.Black),
//                HoverOverlay = C(0x00E5FF),
//                FocusRing = C(0xFF2E97)
//            };
//        }

//        // ----------------------------------------------------------------
//        //  FONT ENGINE
//        // ----------------------------------------------------------------
//        private static string UiFamily
//        {
//            get
//            {
//                if (_uiFamily == null) _uiFamily = ResolveFontFamily(UiFontCandidates);
//                return _uiFamily;
//            }
//        }

//        private static string MonoFamily
//        {
//            get
//            {
//                if (_monoFamily == null) _monoFamily = ResolveFontFamily(MonoFontCandidates);
//                return _monoFamily;
//            }
//        }

//        private static string ResolveFontFamily(string[] candidates)
//        {
//            try
//            {
//                using (InstalledFontCollection installed = new InstalledFontCollection())
//                {
//                    HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
//                    foreach (FontFamily family in installed.Families) names.Add(family.Name);
//                    foreach (string candidate in candidates)
//                        if (names.Contains(candidate)) return candidate;
//                }
//            }
//            catch { }
//            return candidates[candidates.Length - 1];
//        }

//        private Font GetFont(bool mono, float sizePt, FontStyle style)
//        {
//            string family = mono ? MonoFamily : UiFamily;
//            string key = family + "|" + sizePt.ToString("0.###") + "|" + (int)style;

//            Font font;
//            if (_fontCache.TryGetValue(key, out font))
//                return font;   // cache-owned — callers must NOT dispose

//            try
//            {
//                font = new Font(family, sizePt, style, GraphicsUnit.Point);
//            }
//            catch
//            {
//                font = new Font(FontFamily.GenericSansSerif, sizePt, style, GraphicsUnit.Point);
//            }
//            _fontCache[key] = font;
//            return font;
//        }

//        // ----------------------------------------------------------------
//        //  INPUT — MOUSE
//        // ----------------------------------------------------------------
//        protected override void OnMouseEnter(EventArgs e)
//        {
//            base.OnMouseEnter(e);
//            _hover = true;
//            if (!IsDesignTime && Enabled && !Focused) Focus();
//            InvalidateChrome();
//        }

//        protected override void OnMouseLeave(EventArgs e)
//        {
//            base.OnMouseLeave(e);
//            _hover = false;
//            if (_navHover != 0) _navHover = 0;
//            _navRectsValid = false;
//            InvalidateChrome();
//        }

//        protected override void OnMouseDown(MouseEventArgs e)
//        {
//            base.OnMouseDown(e);
//            if (!Enabled) return;
//            Focus();

//            // Embedded navigation takes priority over panning.
//            int dir;
//            if (NavHitTest(e.Location, out dir))
//            {
//                if (_multipageLocked)
//                {
//                    RaiseLocked(ViewerFeature.Multipage,
//                        "Multipage navigation requires a license (feature 'M').");
//                }
//                else if (dir < 0)
//                {
//                    PreviousFrame();
//                }
//                else
//                {
//                    NextFrame();
//                }
//                return;
//            }

//            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
//            {
//                _panning = true;
//                _pressed = true;
//                _lastMouse = e.Location;
//                Cursor = Cursors.SizeAll;
//                InvalidateChrome();
//            }
//        }

//        protected override void OnMouseMove(MouseEventArgs e)
//        {
//            base.OnMouseMove(e);
//            _hoverPos = e.Location;
//            if (!Enabled) return;

//            // Navigation hover + cursor
//            int dir;
//            bool overNav = NavHitTest(e.Location, out dir);
//            int newHover = overNav ? (dir < 0 ? 1 : 2) : 0;
//            if (newHover != _navHover) { _navHover = newHover; Invalidate(); }
//            if (!_panning) Cursor = overNav ? Cursors.Hand : Cursors.Cross;

//            bool needsRepaint = false;

//            if (_panning)
//            {
//                _pan.X += e.X - _lastMouse.X;
//                _pan.Y += e.Y - _lastMouse.Y;
//                _lastMouse = e.Location;
//                needsRepaint = true;
//            }

//            if (UpdateProbeText()) needsRepaint = true;

//            bool crosshairActive = _showCrosshair &&
//                (_controlStyle == ViewerStyle.DashboardPremium || _controlStyle == ViewerStyle.Cyberpunk);

//            if (needsRepaint || crosshairActive) Invalidate();
//        }

//        protected override void OnMouseUp(MouseEventArgs e)
//        {
//            base.OnMouseUp(e);
//            _panning = false;
//            _pressed = false;
//            Cursor = Cursors.Cross;
//            InvalidateChrome();
//        }

//        protected override void OnMouseCaptureChanged(EventArgs e)
//        {
//            base.OnMouseCaptureChanged(e);
//            if (_panning)
//            {
//                _panning = false;
//                _pressed = false;
//                Cursor = Cursors.Cross;
//                InvalidateChrome();
//            }
//        }

//        protected override void OnMouseWheel(MouseEventArgs e)
//        {
//            if (e is HandledMouseEventArgs hme) hme.Handled = true;
//            base.OnMouseWheel(e);

//            if (!Enabled || _image == null) return;

//            float factor = e.Delta > 0 ? 1.12f : 1f / 1.12f;
//            SetZoom(_zoom * factor, new PointF(e.X, e.Y));
//        }

//        protected override void OnDoubleClick(EventArgs e)
//        {
//            base.OnDoubleClick(e);
//            AutoFit();
//        }

//        // ----------------------------------------------------------------
//        //  INPUT — KEYBOARD
//        // ----------------------------------------------------------------
//        protected override bool IsInputKey(Keys keyData)
//        {
//            switch (keyData & Keys.KeyCode)
//            {
//                case Keys.Left:
//                case Keys.Right:
//                case Keys.Up:
//                case Keys.Down:
//                case Keys.PageUp:
//                case Keys.PageDown:
//                    return true;
//            }
//            return base.IsInputKey(keyData);
//        }

//        protected override void OnKeyDown(KeyEventArgs e)
//        {
//            base.OnKeyDown(e);
//            if (!Enabled) return;

//            float step = e.Shift ? 96f : 24f;
//            switch (e.KeyCode)
//            {
//                case Keys.PageDown: NextFrame(); e.Handled = true; break;
//                case Keys.PageUp: PreviousFrame(); e.Handled = true; break;
//                case Keys.Left: PanBy(-step, 0f); e.Handled = true; break;
//                case Keys.Right: PanBy(step, 0f); e.Handled = true; break;
//                case Keys.Up: PanBy(0f, -step); e.Handled = true; break;
//                case Keys.Down: PanBy(0f, step); e.Handled = true; break;
//                case Keys.Add:
//                case Keys.Oemplus: SetZoom(_zoom * 1.25f, ViewportCenter()); e.Handled = true; break;
//                case Keys.Subtract:
//                case Keys.OemMinus: SetZoom(_zoom / 1.25f, ViewportCenter()); e.Handled = true; break;
//                case Keys.D0:
//                case Keys.NumPad0: SetZoom(1f, ViewportCenter()); e.Handled = true; break;
//                case Keys.Home:
//                case Keys.F: AutoFit(); e.Handled = true; break;
//            }
//        }

//        private PointF ViewportCenter() { return new PointF(Width / 2f, Height / 2f); }

//        // ----------------------------------------------------------------
//        //  DRAG & DROP
//        // ----------------------------------------------------------------
//        protected override void OnDragEnter(DragEventArgs e)
//        {
//            base.OnDragEnter(e);
//            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
//        }

//        protected override void OnDragDrop(DragEventArgs e)
//        {
//            base.OnDragDrop(e);
//            if (!Enabled) return;

//            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
//            if (files != null && files.Length > 0)
//            {
//                try { LoadFromFile(files[0]); }
//                catch { /* unsupported / corrupt file — keep current state */ }
//            }
//        }

//        // ----------------------------------------------------------------
//        //  LIFECYCLE
//        // ----------------------------------------------------------------
//        protected override void OnHandleCreated(EventArgs e)
//        {
//            base.OnHandleCreated(e);
//            try
//            {
//                using (Graphics probe = CreateGraphics())
//                {
//                    _dpiScale = Math.Max(1f, probe.DpiX / 96f);
//                }
//            }
//            catch { _dpiScale = 1f; }
//            InvalidateChrome();
//        }

//        protected override void OnResize(EventArgs e)
//        {
//            base.OnResize(e);
//            if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }
//            if (_image != null && _needsAutoFit) AutoFit();
//            Invalidate();
//        }

//        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
//        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

//        protected override void OnEnabledChanged(EventArgs e)
//        {
//            base.OnEnabledChanged(e);
//            InvalidateChrome();
//        }

//        protected override void Dispose(bool disposing)
//        {
//            if (disposing)
//            {
//                ReleaseModeState();               // image + pages + collection + mips + source
//                if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }
//                if (_checkerTile != null) { _checkerTile.Dispose(); _checkerTile = null; }
//                foreach (Font font in _fontCache.Values) font.Dispose();
//                _fontCache.Clear();
//            }
//            base.Dispose(disposing);
//        }
//    }
//}






















//============ VER 1.6 =======================


// ============================================================================
//  PanZoomViewer.cs — Reference-quality pan/zoom image surface (v6 PRO)
//  ---------------------------------------------------------------------------
//  FIDELITY    : EnhanceImage=false (DEFAULT) → pixel-perfect, zero blur.
//                true → photo-grade bicubic.
//  FORMATS     : BMP, GIF, JPEG, PNG, TIFF (MULTIPAGE), EXIF, WMF, EMF, ICO
//  DOCUMENTS   : single · multipage (lazy async page decode + LRU cache) ·
//                collections & folder collections — unified navigation with
//                an embedded controller (« first · ‹ prev · n/N · next › · »)
//  LICENSING   : PZV1 keys, features L=LargeImages, M=Multipage,
//                C=Collections, A=Advanced(streaming/detach/mipmaps)
//  Target: .NET Framework 4.7+ / .NET 6+ WinForms, C# 7.3+. No dependencies.
// ============================================================================
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CsplCameraOcr.UserControls
{
    // ========================================================================
    //  PUBLIC ENUMS
    // ========================================================================

    public enum ThemeMode { Light, Dark }

    public enum ViewerStyle
    {
        DashboardPremium = 1,
        FluentGlass = 2,
        MaterialFlat = 3,
        SoftNeumorphic = 4,
        Cyberpunk = 5
    }

    public enum ViewerFeature
    {
        LargeImages,
        Multipage,
        Collections,
        Advanced
    }

    // ========================================================================
    //  LICENSING
    // ========================================================================

    public sealed class ViewerLicense
    {
        internal ViewerLicense(bool largeImages, bool multipage, bool collections,
                               bool advanced, DateTime? expiresUtc)
        {
            LargeImages = largeImages;
            Multipage = multipage;
            Collections = collections;
            Advanced = advanced;
            ExpiresUtc = expiresUtc;
        }

        public bool LargeImages { get; private set; }
        public bool Multipage { get; private set; }
        public bool Collections { get; private set; }
        public bool Advanced { get; private set; }
        public DateTime? ExpiresUtc { get; private set; }
        public bool IsValid
        {
            get { DateTime? e = ExpiresUtc; return !(e.HasValue && DateTime.UtcNow.Date > e.Value); }
        }
    }

    /// <summary>
    /// Global license registry. One key activates every PanZoomViewer in the process.
    /// VENDOR: call GenerateKey() from your admin tool to issue customer keys.
    /// SECURITY NOTE: the in-assembly secret is obfuscation, not strong security.
    /// </summary>
    public static class PanZoomViewerLicense
    {
        private const string Secret = "CSPL-PZV1-CHANGE-ME-b48a9f2e7d";   // ← REPLACE before shipping

        private static readonly object Gate = new object();
        private static ViewerLicense _current =
            new ViewerLicense(false, false, false, false, null);

        public static ViewerLicense Current { get { lock (Gate) { return _current; } } }

        public static string GenerateKey(bool largeImages, bool multipage,
                                         bool collections, bool advanced,
                                         DateTime? expiresUtc)
        {
            string feat = (largeImages ? "L" : "") + (multipage ? "M" : "") +
                          (collections ? "C" : "") + (advanced ? "A" : "");
            if (feat.Length == 0) throw new ArgumentException("At least one feature must be enabled.");
            string exp = expiresUtc.HasValue
                ? expiresUtc.Value.ToUniversalTime().ToString("yyyyMMdd") : "0";
            string payload = "PZV1-" + feat + "-" + exp;
            return payload + "-" + HashPayload(payload);
        }

        public static bool ApplyKey(string licenseKey)
        {
            ViewerLicense parsed = ParseKey(licenseKey);
            if (parsed == null) return false;
            lock (Gate) { _current = parsed; }
            return true;
        }

        public static void Revoke()
        {
            lock (Gate) { _current = new ViewerLicense(false, false, false, false, null); }
        }

        private static string HashPayload(string payload)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] raw = Encoding.UTF8.GetBytes(Secret + "|" + payload);
                byte[] digest = sha.ComputeHash(raw);
                return BitConverter.ToString(digest).Replace("-", "").Substring(0, 16);
            }
        }

        internal static ViewerLicense ParseKey(string licenseKey)
        {
            try
            {
                if (string.IsNullOrEmpty(licenseKey)) return null;
                string key = licenseKey.Trim().ToUpperInvariant();
                if (!key.StartsWith("PZV1-", StringComparison.Ordinal)) return null;

                string[] parts = key.Split('-');
                if (parts.Length != 4) return null;

                string feat = parts[1];
                string exp = parts[2];
                string hash = parts[3];

                if (feat.Length < 1 || feat.Length > 4) return null;
                for (int i = 0; i < feat.Length; i++)
                {
                    char ch = feat[i];
                    if (ch != 'L' && ch != 'M' && ch != 'C' && ch != 'A') return null;
                }
                if (exp != "0" && exp.Length != 8) return null;
                if (hash.Length != 16) return null;
                for (int i = 0; i < hash.Length; i++)
                {
                    char ch = hash[i];
                    if (!((ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F'))) return null;
                }

                string payload = "PZV1-" + feat + "-" + exp;
                if (!string.Equals(HashPayload(payload), hash, StringComparison.Ordinal)) return null;

                DateTime? expiry = null;
                if (exp != "0")
                {
                    int y = int.Parse(exp.Substring(0, 4));
                    int m = int.Parse(exp.Substring(4, 2));
                    int d = int.Parse(exp.Substring(6, 2));
                    expiry = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc);
                }
                return new ViewerLicense(feat.IndexOf('L') >= 0, feat.IndexOf('M') >= 0,
                                         feat.IndexOf('C') >= 0, feat.IndexOf('A') >= 0, expiry);
            }
            catch { return null; }
        }
    }

    public sealed class FeatureLockedEventArgs : EventArgs
    {
        internal FeatureLockedEventArgs(ViewerFeature feature, string message)
        {
            Feature = feature;
            Message = message;
        }
        public ViewerFeature Feature { get; private set; }
        public string Message { get; private set; }
    }

    // ========================================================================
    //  PALETTE SNAPSHOT
    // ========================================================================

    public sealed class ViewerPalette
    {
        public Color Canvas { get; internal set; }
        public Color BackdropGlow { get; internal set; }
        public Color ViewportFace { get; internal set; }
        public Color ViewportWell { get; internal set; }
        public Color Border { get; internal set; }
        public Color BorderHighlight { get; internal set; }
        public Color TextPrimary { get; internal set; }
        public Color TextSecondary { get; internal set; }
        public Color TextDim { get; internal set; }
        public Color Accent { get; internal set; }
        public Color AccentAlt { get; internal set; }
        public Color ZoneGood { get; internal set; }
        public Color ZoneWarn { get; internal set; }
        public Color ZoneHot { get; internal set; }
        public Color Grid { get; internal set; }
        public Color Tick { get; internal set; }
        public Color HudBack { get; internal set; }
        public Color HudBorder { get; internal set; }
        public Color HudFore { get; internal set; }
        public Color HudForeDim { get; internal set; }
        public Color Shadow { get; internal set; }
        public Color ShadowLight { get; internal set; }
        public Color ShadowDark { get; internal set; }
        public Color HoverOverlay { get; internal set; }
        public Color FocusRing { get; internal set; }
    }

    public sealed class OverlayPaintEventArgs : EventArgs
    {
        internal OverlayPaintEventArgs(Graphics graphics, RectangleF imageBounds,
                                       float zoom, PointF pan, ViewerPalette palette)
        {
            Graphics = graphics;
            ImageBounds = imageBounds;
            Zoom = zoom;
            Pan = pan;
            Palette = palette;
        }

        public Graphics Graphics { get; private set; }
        public RectangleF ImageBounds { get; private set; }
        public float Zoom { get; private set; }
        public PointF Pan { get; private set; }
        public ViewerPalette Palette { get; private set; }
    }

    // ========================================================================
    //  CONTROL
    // ========================================================================

    [ToolboxItem(true)]
    [Description("Reference-quality pan/zoom image surface — pixel-perfect by default, multipage & collection navigation, license-gated PRO features.")]
    public partial class PanZoomViewer : Control
    {
        // ====================================================================
        //  CORE DATA
        // ====================================================================
        private Bitmap _image;                 // currently displayed frame
        private float _zoom = 1f;
        private PointF _pan = PointF.Empty;
        private bool _needsAutoFit = true;

        private bool _hover;
        private bool _pressed;
        private bool _panning;
        private Point _lastMouse;
        private Point _hoverPos;
        private bool _imageLostPending;

        private ThemeMode _themeMode = ThemeMode.Dark;
        private ViewerStyle _controlStyle = ViewerStyle.DashboardPremium;
        private ViewerPalette _palette;

        private bool _showHud = true;
        private bool _showScanlines = true;
        private bool _showCrosshair = true;
        private bool _enhanceImage = false;          // DEFAULT = pixel-perfect
        private bool _useMipmaps = true;
        private float _minZoom = 0.05f;
        private float _maxZoom = 100f;
        private float _dpiScale = 1f;

        // ----------------------------------------------------------------
        //  DOCUMENT MODEL — unified "frames"
        //    Single     : 1 frame, owned directly
        //    Multipage  : pages decoded lazily, owned by _pageCache
        //    Collection : frames owned by _collection
        // ----------------------------------------------------------------
        private enum ContentMode { Single, Multipage, Collection }

        private ContentMode _mode = ContentMode.Single;
        private int _frameCount;
        private int _frameIndex;
        private int _desiredFrame;

        private Image _mpSource;                    // multipage container (TIFF etc.)
        private MemoryStream _mpStream;             // MUST outlive _mpSource
        private readonly Dictionary<int, Bitmap> _pageCache = new Dictionary<int, Bitmap>();
        private readonly LinkedList<int> _pageLru = new LinkedList<int>();
        private readonly object _mpGate = new object();
        private int _loadGeneration;                // invalidates in-flight decodes
        private bool _frameDecoding;
        private bool _multipageLocked;

        private readonly List<Bitmap> _collection = new List<Bitmap>();

        // License gating
        private bool _enforceLicense = true;
        private bool _largeLockActive;
        private string _largeLockSizeText = "";

        // Config
        private bool _autoFitOnLoad = true;
        private bool _showNavigationBar = true;
        private bool _showCheckerboard = false;
        private int _pageCacheSize = 3;
        private int _largeThresholdMB = 5;
        private int _viewportPadding = -1;          // -1 = auto
        private Color _customAccent = Color.Empty;  // Empty = style default

        // Navigation controller hit rects (paint → mouse)
        private int _navHover;                      // 0 none, 1 first, 2 prev, 3 next, 4 last
        private readonly RectangleF[] _navRects = new RectangleF[4];
        private bool _navRectsValid;

        // Mipmap chain (Enhanced path ONLY — crisp path never uses it)
        private List<Bitmap> _mips;

        // Chrome cache — control-owned, created OUTSIDE OnPaint
        private Bitmap _chromeCache;
        private Bitmap _checkerTile;

        // HUD text caches
        private string _hudZoomText = "100 %";
        private string _hudSourceText = "—";
        private string _hudProbeText = "";
        private int _probeX = int.MinValue;
        private int _probeY = int.MinValue;

        private string _mCapSrc; private SizeF _szCapSrc;
        private string _mValSrc; private SizeF _szValSrc;
        private string _mCapZoom; private SizeF _szCapZoom;
        private string _mValZoom; private SizeF _szValZoom;
        private string _mCapProbe; private SizeF _szCapProbe;
        private string _mValProbe; private SizeF _szValProbe;
        private string _mNavKey; private SizeF _szNavSize;

        // Font engine (cache-owned; disposed exactly once in Dispose)
        private readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();
        private static string _uiFamily;
        private static string _monoFamily;
        private static readonly string[] UiFontCandidates =
            { "Segoe UI Variable Display", "Segoe UI Workspace", "Segoe UI", "Arial" };
        private static readonly string[] MonoFontCandidates =
            { "Cascadia Code", "Cascadia Mono", "JetBrains Mono", "Consolas", "Courier New" };

        private struct ViewerLayout
        {
            public RectangleF View;
            public RectangleF Content;
            public float ViewRadius;
            public float ContentRadius;
            public float Dpi;
        }

        private enum ChipAnchor { TopLeft, BottomLeft, BottomRight }

        /// <summary>Off-thread decode result for a whole document.</summary>
        private sealed class DocumentLoad
        {
            public Image Source;          // multipage container (null for single)
            public MemoryStream Stream;   // must outlive Source
            public int PageCount = 1;
            public Bitmap Frame;          // first frame to display
            public List<Bitmap> Mips;     // optional mipmap chain for Frame

            public void DisposeAll()
            {
                if (Frame != null) { Frame.Dispose(); Frame = null; }
                if (Mips != null) { foreach (Bitmap m in Mips) m.Dispose(); Mips = null; }
                if (Source != null) { Source.Dispose(); Source = null; }
                if (Stream != null) { Stream.Dispose(); Stream = null; }
                PageCount = 1;
            }
        }

        // ====================================================================
        //  CONSTRUCTION
        // ====================================================================
        public PanZoomViewer()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.DoubleBuffer |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);
            UpdateStyles();

            TabStop = true;
            AllowDrop = true;
            Cursor = Cursors.Cross;

            _palette = BuildPalette(_themeMode, _controlStyle);
        }

        // ====================================================================
        //  PROPERTIES — FIDELITY / BEHAVIOR
        // ====================================================================
        [Category("Behavior")]
        [Description("false (default): pixel-perfect display — image looks exactly as it is, zero blur, fastest rendering. true: high-quality smooth scaling for photographs.")]
        [DefaultValue(false)]
        public bool EnhanceImage
        {
            get { return _enhanceImage; }
            set { if (_enhanceImage == value) return; _enhanceImage = value; Invalidate(); }
        }

        [Category("Behavior")]
        [Description("Re-fits the view every time a new document is loaded.")]
        [DefaultValue(true)]
        public bool AutoFitOnLoad
        {
            get { return _autoFitOnLoad; }
            set { if (_autoFitOnLoad == value) return; _autoFitOnLoad = value; Invalidate(); }
        }

        [Category("Behavior")]
        [Description("Lower zoom clamp.")]
        [DefaultValue(0.05f)]
        public float MinZoom
        {
            get { return _minZoom; }
            set { _minZoom = Math.Max(0.01f, value); Invalidate(); }
        }

        [Category("Behavior")]
        [Description("Upper zoom clamp.")]
        [DefaultValue(100f)]
        public float MaxZoom
        {
            get { return _maxZoom; }
            set { _maxZoom = Math.Max(_minZoom * 2f, value); Invalidate(); }
        }

        [Category("Behavior")]
        [Description("Mipmap acceleration for very large images (Enhanced path only — never the pixel-perfect path). Paid feature 'A'.")]
        [DefaultValue(true)]
        public bool UseMipmaps
        {
            get { return _useMipmaps; }
            set
            {
                if (_useMipmaps == value) return;
                if (value && !AdvancedAllowed)
                {
                    RaiseLocked(ViewerFeature.Advanced,
                        "Mipmap acceleration requires a license (feature 'A').");
                    return;
                }
                _useMipmaps = value;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [Description("Number of decoded pages kept in the LRU cache for multipage documents.")]
        [DefaultValue(3)]
        public int PageCacheSize
        {
            get { return _pageCacheSize; }
            set { _pageCacheSize = Math.Max(2, value); Invalidate(); }
        }

        [Category("Behavior")]
        [Description("Images larger than this (MB, by file/byte size) require the LargeImages license feature. Direct Bitmap assignment is never gated.")]
        [DefaultValue(5)]
        public int LargeImageThresholdMB
        {
            get { return _largeThresholdMB; }
            set { _largeThresholdMB = Math.Max(1, value); Invalidate(); }
        }

        // ====================================================================
        //  PROPERTIES — APPEARANCE / NAVIGATION
        // ====================================================================
        [Category("Appearance")]
        [Description("Light or Dark surface theme.")]
        [DefaultValue(ThemeMode.Dark)]
        public ThemeMode ThemeMode
        {
            get { return _themeMode; }
            set
            {
                if (_themeMode == value) return;
                _themeMode = value;
                _palette = BuildPalette(_themeMode, _controlStyle);
                if (_checkerTile != null) { _checkerTile.Dispose(); _checkerTile = null; }
                InvalidateChrome();
            }
        }

        [Category("Appearance")]
        [Description("Visual design system.")]
        [DefaultValue(ViewerStyle.DashboardPremium)]
        public ViewerStyle ControlStyle
        {
            get { return _controlStyle; }
            set
            {
                if (_controlStyle == value) return;
                _controlStyle = value;
                _palette = BuildPalette(_themeMode, _controlStyle);
                InvalidateChrome();
            }
        }

        [Category("Appearance")]
        [Description("Shows the minimal readout chips.")]
        [DefaultValue(true)]
        public bool ShowHud
        {
            get { return _showHud; }
            set { if (_showHud == value) return; _showHud = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Cyberpunk style: subtle CRT scanline film.")]
        [DefaultValue(true)]
        public bool ShowScanlines
        {
            get { return _showScanlines; }
            set { if (_showScanlines == value) return; _showScanlines = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Precision crosshair follows the cursor (Dashboard & Cyberpunk).")]
        [DefaultValue(true)]
        public bool ShowCrosshair
        {
            get { return _showCrosshair; }
            set { if (_showCrosshair == value) return; _showCrosshair = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Shows the embedded navigation controller (« ‹ PAGE n/N › ») for multipage documents and collections.")]
        [DefaultValue(true)]
        public bool ShowNavigationBar
        {
            get { return _showNavigationBar; }
            set { if (_showNavigationBar == value) return; _showNavigationBar = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Checkerboard behind images with transparency (PNG, GIF, TIFF with alpha).")]
        [DefaultValue(false)]
        public bool ShowCheckerboard
        {
            get { return _showCheckerboard; }
            set { if (_showCheckerboard == value) return; _showCheckerboard = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Custom accent color overriding the style's accent. Empty = style default.")]
        public Color CustomAccent
        {
            get { return _customAccent; }
            set
            {
                if (_customAccent == value) return;
                _customAccent = value;
                InvalidateChrome();
            }
        }
        private bool ShouldSerializeCustomAccent() { return _customAccent != Color.Empty; }
        private void ResetCustomAccent() { _customAccent = Color.Empty; }

        [Category("Appearance")]
        [Description("Viewport padding in pixels. -1 = automatic proportional padding.")]
        [DefaultValue(-1)]
        public int ViewportPadding
        {
            get { return _viewportPadding; }
            set
            {
                if (_viewportPadding == value) return;
                _viewportPadding = Math.Max(0, Math.Min(200, value));
                InvalidateChrome();
            }
        }

        // ====================================================================
        //  PROPERTIES — LICENSING
        // ====================================================================
        [Category("License")]
        [Description("Applies a PZV1 license key (generate with PanZoomViewerLicense.GenerateKey).")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string LicenseKey
        {
            set
            {
                if (PanZoomViewerLicense.ApplyKey(value))
                {
                    ReevaluateLocks();
                    EventHandler h = LicenseApplied;
                    if (h != null) h(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [Category("License")]
        [Description("true (default): locked features are gated and raise FeatureLocked. false: all features active (development / trial build).")]
        [DefaultValue(true)]
        public bool EnforceLicense
        {
            get { return _enforceLicense; }
            set
            {
                if (_enforceLicense == value) return;
                _enforceLicense = value;
                ReevaluateLocks();
            }
        }

        [Browsable(false)]
        public ViewerLicense License { get { return PanZoomViewerLicense.Current; } }

        // ====================================================================
        //  PROPERTIES — CONTENT
        // ====================================================================
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Description("Displayed bitmap (single-image mode). Ownership transfers to the control.")]
        public Bitmap Image
        {
            get { return _image; }
            set
            {
                if (ReferenceEquals(_image, value)) return;
                EnterSingleMode(value, false);
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float Zoom
        {
            get { return _zoom; }
            set { SetZoom(value, new PointF(Width / 2f, Height / 2f)); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public PointF PanOffset
        {
            get { return _pan; }
            set { _pan = value; Invalidate(); }
        }

        [Browsable(false)]
        public int FrameCount { get { return _frameCount; } }

        [Browsable(false)]
        public int FrameIndex { get { return _frameIndex; } }

        [Browsable(false)]
        public int CollectionCount { get { return _mode == ContentMode.Collection ? _collection.Count : 0; } }

        public static string SupportedExtensions
        {
            get { return "*.bmp;*.gif;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.exif;*.wmf;*.emf;*.ico"; }
        }

        // ====================================================================
        //  EVENTS
        // ====================================================================

        public event EventHandler<Graphics> OnCustomPaint;
        public event EventHandler<OverlayPaintEventArgs> OverlayPaint;
        public event EventHandler ImageLost;
        public event EventHandler FrameChanged;
        public event EventHandler FrameCountChanged;
        public event EventHandler LicenseApplied;
        public event EventHandler<FeatureLockedEventArgs> FeatureLocked;

        private void OnFrameChanged() { EventHandler h = FrameChanged; if (h != null) h(this, EventArgs.Empty); }
        private void OnFrameCountChanged() { EventHandler h = FrameCountChanged; if (h != null) h(this, EventArgs.Empty); }
        private void RaiseLocked(ViewerFeature feature, string message)
        {
            EventHandler<FeatureLockedEventArgs> h = FeatureLocked;
            if (h != null) h(this, new FeatureLockedEventArgs(feature, message));
        }

        // ====================================================================
        //  LOADING API — every loader routes through ONE multipage-aware core.
        //  FIX (v6): LoadFromFile/Async previously flattened multipage TIFFs
        //  to their first frame; they now install the full document.
        // ====================================================================

        public bool LoadFromFile(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path must not be empty.", "path");

            long size = 0;
            try { size = new FileInfo(path).Length; }
            catch { return false; }

            if (IsLargeBlocked(size)) { ShowLargeLock(size); return false; }

            return InstallDocument(DecodeDocument(path));
        }

        public async Task<bool> LoadFromFileAsync(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path must not be empty.", "path");

            long size = 0;
            try { size = new FileInfo(path).Length; }
            catch { return false; }

            if (IsLargeBlocked(size)) { ShowLargeLock(size); return false; }

            DocumentLoad load = await Task.Run(() => DecodeDocument(path));

            if (IsDisposed || Disposing) { load.DisposeAll(); return false; }
            return InstallDocument(load);
        }

        public bool LoadFromStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");

            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                data = ms.ToArray();
            }
            return LoadFromBytes(data);
        }

        public bool LoadFromBytes(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");
            if (data.Length == 0) return false;
            if (IsLargeBlocked(data.LongLength)) { ShowLargeLock(data.LongLength); return false; }

            return InstallDocument(DecodeDocumentCore(new MemoryStream(data, false)));
        }

        public void LoadFromImage(System.Drawing.Image image)
        {
            if (image == null) throw new ArgumentNullException("image");
            // A multipage Image is flattened to its active frame here — mutating
            // the caller's Image is not acceptable. Use LoadFromFile/LoadFromBytes
            // for full multipage support.
            EnterSingleMode(new Bitmap(image), false);
        }

        /// <summary>Thread-safe frame streaming (camera loops) — preserves zoom/pan; re-fits on resolution change. Paid feature 'A'.</summary>
        public void PostImage(Bitmap frame)
        {
            if (frame == null) return;

            if (!AdvancedAllowed)
            {
                RaiseLocked(ViewerFeature.Advanced,
                    "Thread-safe frame streaming (PostImage) requires a license (feature 'A').");
                frame.Dispose();
                return;
            }

            if (IsDisposed) { frame.Dispose(); return; }

            if (IsHandleCreated && InvokeRequired)
            {
                BeginInvoke((Action)delegate
                {
                    if (IsDisposed || Disposing) { frame.Dispose(); return; }
                    PostImageCore(frame);
                });
            }
            else
            {
                PostImageCore(frame);
            }
        }

        private void PostImageCore(Bitmap frame)
        {
            if (frame == null) return;

            int oldW = 0, oldH = 0;
            try { if (_image != null) { oldW = _image.Width; oldH = _image.Height; } } catch { }

            EnterSingleMode(frame, true);      // preserve zoom/pan while streaming

            if (_autoFitOnLoad && (oldW != frame.Width || oldH != frame.Height))
                AutoFit();                     // resolution change → refit once
        }

        /// <summary>Removes the current bitmap WITHOUT disposing it — ownership returns to the caller. Single-image mode only. Paid feature 'A'.</summary>
        public Bitmap DetachImage()
        {
            if (!AdvancedAllowed)
            {
                RaiseLocked(ViewerFeature.Advanced, "DetachImage requires a license (feature 'A').");
                return null;
            }
            if (_mode != ContentMode.Single) return null;

            Bitmap bmp = _image;
            _image = null;
            _frameCount = 0;
            _frameIndex = 0;
            DisposeMips();
            _needsAutoFit = true;
            UpdateSourceText();
            ResetProbe();
            OnFrameCountChanged();
            Invalidate();
            return bmp;
        }

        /// <summary>Clears everything back to the empty state.</summary>
        public void Clear()
        {
            ReleaseModeState();
            _needsAutoFit = true;
            UpdateSourceText();
            OnFrameCountChanged();
            Invalidate();
        }

        // ----------------------------------------------------------------
        //  DECODE + INSTALL CORE
        // ----------------------------------------------------------------
        private static DocumentLoad DecodeDocument(string path)
        {
            MemoryStream ms = null;
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    ms = new MemoryStream();
                    fs.CopyTo(ms);
                }
                ms.Position = 0;
                return DecodeDocumentCore(ms);
            }
            catch
            {
                if (ms != null) ms.Dispose();
                return new DocumentLoad();     // Frame == null → install fails safely
            }
        }

        private static DocumentLoad DecodeDocumentCore(MemoryStream ms)
        {
            DocumentLoad load = new DocumentLoad();
            Image img = null;
            try
            {
                img = System.Drawing.Image.FromStream(ms, false, true);

                int pages = 1;
                try { pages = img.GetFrameCount(FrameDimension.Page); }
                catch { /* single-frame formats */ }

                if (pages > 1)
                {
                    try { img.SelectActiveFrame(FrameDimension.Page, 0); }
                    catch { }

                    load.Frame = new Bitmap(img);        // page 0 decoded up-front
                    load.Source = img; img = null;      // ownership → document
                    load.Stream = ms; ms = null;       // stream must outlive Source
                    load.PageCount = pages;
                }
                else
                {
                    load.Frame = new Bitmap(img);
                    if ((long)load.Frame.Width * load.Frame.Height >= 24000000L)
                        load.Mips = BuildMipChain(load.Frame);
                }
            }
            catch
            {
                load.DisposeAll();
            }
            finally
            {
                if (img != null) img.Dispose();
                if (ms != null) ms.Dispose();            // null after ownership transfer
            }
            return load;
        }

        private bool InstallDocument(DocumentLoad load)
        {
            if (load == null || load.Frame == null)
            {
                if (load != null) load.DisposeAll();
                return false;
            }

            if (load.PageCount > 1)
            {
                // ---------------- MULTIPAGE DOCUMENT ----------------
                ReleaseModeState();

                _mode = ContentMode.Multipage;
                _mpSource = load.Source; load.Source = null;
                _mpStream = load.Stream; load.Stream = null;
                _frameCount = load.PageCount;
                _multipageLocked = !MultipageAllowed;
                OnFrameCountChanged();

                Bitmap page0 = load.Frame;
                load.Frame = null;
                CachePage(0, page0);
                ApplyPage(0, page0);

                DisposeMips();                    // per-frame mips
                _mips = load.Mips;
                load.Mips = null;

                Invalidate();
                return true;
            }

            // ---------------- SINGLE-FRAME DOCUMENT ----------------
            Bitmap bmp = load.Frame;
            load.Frame = null;
            List<Bitmap> mips = load.Mips;
            load.Mips = null;
            load.DisposeAll();                    // safety no-op

            EnterSingleMode(bmp, false);
            DisposeMips();
            _mips = mips;
            Invalidate();
            return true;
        }

        private static Bitmap DecodeSingleFrame(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (Image tmp = System.Drawing.Image.FromStream(fs, false, true))
            {
                return new Bitmap(tmp);           // independent copy — no file lock
            }
        }

        private static List<Bitmap> BuildMipChain(Bitmap source)
        {
            List<Bitmap> mips = new List<Bitmap>();
            try
            {
                int w = source.Width, h = source.Height;
                Bitmap prev = source;
                while (w / 2 >= 1024 && h / 2 >= 1024 && mips.Count < 8)
                {
                    w = Math.Max(1, w / 2);
                    h = Math.Max(1, h / 2);
                    Bitmap mip = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                    using (Graphics cg = Graphics.FromImage(mip))
                    {
                        cg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        cg.CompositingQuality = CompositingQuality.HighQuality;
                        cg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        cg.DrawImage(prev, 0, 0, w, h);
                    }
                    mips.Add(mip);
                    prev = mip;
                }
                return mips;
            }
            catch
            {
                foreach (Bitmap m in mips) m.Dispose();
                return null;
            }
        }

        // ====================================================================
        //  COLLECTION API (paid feature C)
        // ====================================================================

        public bool AddCollectionImage(Bitmap image)
        {
            if (image == null) throw new ArgumentNullException("image");
            if (!CollectionsAllowed)
            {
                RaiseLocked(ViewerFeature.Collections, "Image collections require a license (feature 'C').");
                return false;
            }

            if (_mode != ContentMode.Collection)
            {
                ReleaseModeState();
                _mode = ContentMode.Collection;
                _frameIndex = 0;
                _desiredFrame = 0;
            }

            _collection.Add(image);
            _frameCount = _collection.Count;
            if (_collection.Count == 1) ApplyCollectionIndex(0);
            OnFrameCountChanged();
            return true;
        }

        public bool AddCollectionFile(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path must not be empty.", "path");
            if (!CollectionsAllowed)
            {
                RaiseLocked(ViewerFeature.Collections, "Image collections require a license (feature 'C').");
                return false;
            }
            long size = 0;
            try { size = new FileInfo(path).Length; }
            catch { return false; }
            if (IsLargeBlocked(size))
            {
                RaiseLocked(ViewerFeature.LargeImages, "File exceeds the free size limit.");
                return false;
            }
            try { return AddCollectionImage(DecodeSingleFrame(path)); }
            catch { return false; }
        }

        public async Task<int> LoadCollectionFilesAsync(string[] paths)
        {
            if (paths == null) throw new ArgumentNullException("paths");
            if (!CollectionsAllowed)
            {
                RaiseLocked(ViewerFeature.Collections, "Image collections require a license (feature 'C').");
                return 0;
            }

            int added = 0;
            foreach (string p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                Bitmap decoded = await Task.Run(() =>
                {
                    try { return DecodeSingleFrame(p); }
                    catch { return null; }
                });
                if (IsDisposed || Disposing) { if (decoded != null) decoded.Dispose(); return added; }
                if (decoded != null && AddCollectionImage(decoded)) added++;
            }
            return added;
        }

        /// <summary>Adds every supported image in a folder to the collection (sync). Paid feature 'C'.</summary>
        public bool AddCollectionFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) throw new ArgumentException("Folder path must not be empty.", "folderPath");
            if (!CollectionsAllowed)
            {
                RaiseLocked(ViewerFeature.Collections, "Folder collections require a license (feature 'C').");
                return false;
            }
            string[] files = ListSupportedFiles(folderPath);
            int added = 0;
            foreach (string f in files) if (AddCollectionFile(f)) added++;
            return added > 0;
        }

        /// <summary>Decodes a whole folder into the collection off the UI thread. Paid feature 'C'.</summary>
        public Task<int> LoadCollectionFolderAsync(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) throw new ArgumentException("Folder path must not be empty.", "folderPath");
            if (!CollectionsAllowed)
            {
                RaiseLocked(ViewerFeature.Collections, "Folder collections require a license (feature 'C').");
                return Task.FromResult(0);
            }
            return LoadCollectionFilesAsync(ListSupportedFiles(folderPath));
        }

        public void ClearCollection()
        {
            if (_mode != ContentMode.Collection) return;
            ReleaseModeState();
            UpdateSourceText();
            OnFrameCountChanged();
            Invalidate();
        }

        private static string[] ListSupportedFiles(string folderPath)
        {
            try
            {
                List<string> result = new List<string>();
                foreach (string f in Directory.GetFiles(folderPath))
                    if (IsSupportedExtension(Path.GetExtension(f))) result.Add(f);
                return result.ToArray();
            }
            catch { return new string[0]; }
        }

        private static bool IsSupportedExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            switch (ext.ToLowerInvariant())
            {
                case ".bmp":
                case ".gif":
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".tif":
                case ".tiff":
                case ".exif":
                case ".wmf":
                case ".emf":
                case ".ico":
                    return true;
            }
            return false;
        }

        // ====================================================================
        //  FRAME NAVIGATION (pages + collection items)
        // ====================================================================

        public void NextFrame() { GoToFrame(_frameIndex + 1); }
        public void PreviousFrame() { GoToFrame(_frameIndex - 1); }

        /// <summary>Navigates to a frame (page or collection item). Returns false when out of range or locked.</summary>
        public bool GoToFrame(int index)
        {
            if (_frameCount <= 1) return false;
            if (index < 0 || index >= _frameCount) return false;

            if (_mode == ContentMode.Multipage)
            {
                if (_multipageLocked)
                {
                    RaiseLocked(ViewerFeature.Multipage, "Multipage navigation requires a license (feature 'M').");
                    return false;
                }
                GoToPage(index);
                return true;
            }

            if (_mode == ContentMode.Collection && index < _collection.Count)
            {
                ApplyCollectionIndex(index);
                return true;
            }
            return false;
        }

        private void GoToPage(int index)
        {
            Bitmap cached;
            if (!_frameDecoding && _pageCache.TryGetValue(index, out cached))
            {
                ApplyPage(index, cached);
                return;
            }
            RequestPageAsync(index);
        }

        private async void RequestPageAsync(int index)
        {
            int gen = _loadGeneration;
            _frameDecoding = true;
            _desiredFrame = index;
            Invalidate();

            Bitmap page = await Task.Run(() => SafeDecodePage(index, gen));

            try
            {
                if (IsDisposed || Disposing || gen != _loadGeneration)
                {
                    if (page != null) page.Dispose();
                    return;
                }
                _frameDecoding = false;

                if (page == null) { Invalidate(); return; }   // decode failed → keep current

                CachePage(index, page);
                if (_desiredFrame == index) ApplyPage(index, page);
                Invalidate();
            }
            catch (Exception ex) { Debug.WriteLine("[PanZoomViewer] page apply failed: " + ex.Message); }
        }

        private Bitmap SafeDecodePage(int index, int gen)
        {
            lock (_mpGate)
            {
                try
                {
                    if (gen != _loadGeneration || _mpSource == null) return null;
                    _mpSource.SelectActiveFrame(FrameDimension.Page, index);
                    return new Bitmap(_mpSource);
                }
                catch { return null; }
            }
        }

        private void CachePage(int index, Bitmap page)
        {
            _pageCache[index] = page;
            _pageLru.Remove(index);
            _pageLru.AddFirst(index);

            int capacity = Math.Max(2, _pageCacheSize);
            int guard = _pageCache.Count + 2;
            while (_pageCache.Count > capacity && guard-- > 0)
            {
                LinkedListNode<int> last = _pageLru.Last;
                if (last == null) break;
                int victim = last.Value;

                if (victim == _frameIndex || victim == _desiredFrame)
                {
                    _pageLru.RemoveLast();
                    _pageLru.AddFirst(victim);        // protected → keep, scan older
                    continue;
                }

                _pageLru.RemoveLast();
                Bitmap evicted;
                if (_pageCache.TryGetValue(victim, out evicted))
                {
                    _pageCache.Remove(victim);
                    evicted.Dispose();
                }
            }
        }

        private void ApplyPage(int index, Bitmap page)
        {
            _pageLru.Remove(index);
            _pageLru.AddFirst(index);

            _image = page;                    // cache-owned
            _frameIndex = index;
            _desiredFrame = index;
            DisposeMips();
            if (_autoFitOnLoad) AutoFit();
            UpdateSourceText();
            ResetProbe();
            OnFrameChanged();
        }

        private void ApplyCollectionIndex(int index)
        {
            _image = _collection[index];      // list-owned
            _frameIndex = index;
            _desiredFrame = index;
            DisposeMips();
            if (_autoFitOnLoad) AutoFit();
            UpdateSourceText();
            ResetProbe();
            OnFrameChanged();
        }

        // ====================================================================
        //  MODE / OWNERSHIP MANAGEMENT
        // ====================================================================
        private void EnterSingleMode(Bitmap bmp, bool preserveView)
        {
            bool neverFitted = _needsAutoFit;
            ReleaseModeState();

            _image = bmp;
            _mode = ContentMode.Single;
            _frameCount = bmp != null ? 1 : 0;
            _frameIndex = 0;
            _desiredFrame = 0;
            UpdateSourceText();
            ResetProbe();
            OnFrameCountChanged();

            if (bmp == null)
            {
                _needsAutoFit = true;
                Invalidate();
                return;
            }

            // A NEW DOCUMENT always re-fits (view-state carryover fix).
            if (neverFitted || (!preserveView && _autoFitOnLoad))
            {
                AutoFit();
            }
            Invalidate();
        }

        private void ReleaseModeState()
        {
            _loadGeneration++;                 // kills every in-flight decode
            _frameDecoding = false;
            _desiredFrame = 0;
            _multipageLocked = false;
            _largeLockActive = false;

            lock (_mpGate)
            {
                if (_mode == ContentMode.Multipage)
                {
                    foreach (Bitmap b in _pageCache.Values) { try { b.Dispose(); } catch { } }
                    _pageCache.Clear();
                    _pageLru.Clear();
                    _image = null;             // page frames were cache-owned
                    if (_mpSource != null) { try { _mpSource.Dispose(); } catch { } _mpSource = null; }
                    if (_mpStream != null) { try { _mpStream.Dispose(); } catch { } _mpStream = null; }
                }
                else if (_mode == ContentMode.Collection)
                {
                    foreach (Bitmap b in _collection) { try { b.Dispose(); } catch { } }
                    _collection.Clear();
                    _image = null;             // frames are list-owned
                }
                else
                {
                    if (_image != null) { try { _image.Dispose(); } catch { } _image = null; }
                }
            }

            DisposeMips();
            _frameCount = 0;
            _frameIndex = 0;
            _mode = ContentMode.Single;
            ResetProbe();
        }

        private void DisposeMips()
        {
            if (_mips != null)
            {
                foreach (Bitmap m in _mips) { if (m != null) m.Dispose(); }
                _mips = null;
            }
        }

        private void ReevaluateLocks()
        {
            if (_mode == ContentMode.Multipage) _multipageLocked = !MultipageAllowed;
            Invalidate();
        }

        // ----------------------------------------------------------------
        //  LICENSE GATES
        // ----------------------------------------------------------------
        private bool LargeImagesAllowed
        {
            get
            {
                if (!_enforceLicense) return true;
                ViewerLicense l = PanZoomViewerLicense.Current;
                return l.IsValid && l.LargeImages;
            }
        }

        private bool MultipageAllowed
        {
            get
            {
                if (!_enforceLicense) return true;
                ViewerLicense l = PanZoomViewerLicense.Current;
                return l.IsValid && l.Multipage;
            }
        }

        private bool CollectionsAllowed
        {
            get
            {
                if (!_enforceLicense) return true;
                ViewerLicense l = PanZoomViewerLicense.Current;
                return l.IsValid && l.Collections;
            }
        }

        private bool AdvancedAllowed
        {
            get
            {
                if (!_enforceLicense) return true;
                ViewerLicense l = PanZoomViewerLicense.Current;
                return l.IsValid && l.Advanced;
            }
        }

        private bool IsLargeBlocked(long byteCount)
        {
            return byteCount > (long)_largeThresholdMB * 1024L * 1024L && !LargeImagesAllowed;
        }

        private void ShowLargeLock(long byteCount)
        {
            ReleaseModeState();
            _mode = ContentMode.Single;
            _largeLockActive = true;
            _largeLockSizeText = FormatBytes(byteCount);
            UpdateSourceText();
            OnFrameCountChanged();
            RaiseLocked(ViewerFeature.LargeImages,
                string.Format("Image is {0} — the free limit is {1} MB.", _largeLockSizeText, _largeThresholdMB));
            Invalidate();
        }

        private static string FormatBytes(long b)
        {
            if (b >= 1024L * 1024L * 1024L) return (b / (1024.0 * 1024.0 * 1024.0)).ToString("0.##") + " GB";
            if (b >= 1024L * 1024L) return (b / (1024.0 * 1024.0)).ToString("0.##") + " MB";
            return (b / 1024.0).ToString("0.#") + " KB";
        }

        // ====================================================================
        //  PUBLIC METHODS & COORDINATE TRANSFORMS
        // ====================================================================

        public void AutoFit()
        {
            if (_image == null || Width < 20 || Height < 20) return;

            ViewerLayout lay = ComputeLayout();
            RectangleF c = lay.Content;

            float z = Math.Min(c.Width / _image.Width, c.Height / _image.Height);
            _zoom = ClampF(z, MinZoom, MaxZoom);

            float dw = _image.Width * _zoom;
            float dh = _image.Height * _zoom;
            _pan = new PointF(c.X + (c.Width - dw) / 2f, c.Y + (c.Height - dh) / 2f);

            _needsAutoFit = false;
            UpdateZoomText();
            Invalidate();
        }

        public void SetZoom(float zoom, PointF anchor)
        {
            float z = ClampF(zoom, MinZoom, MaxZoom);
            if (z == _zoom) return;

            float k = z / _zoom;
            _pan = new PointF(
                anchor.X - (anchor.X - _pan.X) * k,
                anchor.Y - (anchor.Y - _pan.Y) * k);
            _zoom = z;
            UpdateZoomText();
            Invalidate();
        }

        public void PanBy(float dx, float dy)
        {
            _pan = new PointF(_pan.X + dx, _pan.Y + dy);
            Invalidate();
        }

        public ViewerPalette GetPalette() { return _palette; }

        public PointF ScreenToImageF(PointF screenPoint)
        {
            return new PointF((screenPoint.X - _pan.X) / _zoom,
                              (screenPoint.Y - _pan.Y) / _zoom);
        }

        public Point ScreenToImage(Point screenPoint)
        {
            return new Point(
                (int)Math.Round((screenPoint.X - _pan.X) / _zoom),
                (int)Math.Round((screenPoint.Y - _pan.Y) / _zoom));
        }

        public RectangleF ImageToScreenF(RectangleF imageRect)
        {
            return new RectangleF(
                imageRect.X * _zoom + _pan.X,
                imageRect.Y * _zoom + _pan.Y,
                imageRect.Width * _zoom,
                imageRect.Height * _zoom);
        }

        public Rectangle ImageToScreen(Rectangle imageRect)
        {
            RectangleF f = ImageToScreenF(imageRect);
            return new Rectangle(
                (int)Math.Round(f.X), (int)Math.Round(f.Y),
                (int)Math.Round(f.Width), (int)Math.Round(f.Height));
        }

        public RectangleF GetImageScreenBounds()
        {
            if (_image == null) return RectangleF.Empty;
            try
            {
                return new RectangleF(_pan.X, _pan.Y, _image.Width * _zoom, _image.Height * _zoom);
            }
            catch { return RectangleF.Empty; }
        }

        // ====================================================================
        //  RENDERING PIPELINE
        // ====================================================================
        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Intentionally empty — the buffered surface is composed in OnPaint only.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (IsDesignTime)
            {
                try { PaintCore(e.Graphics); }
                catch (Exception ex) { PaintDesignFailure(e.Graphics, ex); }
            }
            else
            {
                PaintCore(e.Graphics);
            }
        }

        private bool IsDesignTime
        {
            get { return Site != null && Site.DesignMode; }
        }

        private void PaintDesignFailure(Graphics g, Exception ex)
        {
            Debug.WriteLine("[PanZoomViewer design-time paint failure]\r\n" + ex);
            try
            {
                g.SmoothingMode = SmoothingMode.None;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                using (SolidBrush bg = new SolidBrush(Color.FromArgb(255, 34, 18, 28)))
                    g.FillRectangle(bg, ClientRectangle);

                using (Font f = new Font(FontFamily.GenericMonospace, 8.5f, FontStyle.Regular, GraphicsUnit.Point))
                using (SolidBrush t = new SolidBrush(Color.FromArgb(255, 255, 105, 97)))
                using (SolidBrush w = new SolidBrush(Color.White))
                using (StringFormat fmt = new StringFormat())
                {
                    fmt.Trimming = StringTrimming.EllipsisCharacter;

                    g.DrawString("PANZOOMVIEWER — DESIGN-TIME PAINT FAILURE", f, t, 12f, 12f, fmt);
                    g.DrawString(ex.GetType().Name + ": " + ex.Message, f, w, 12f, 30f, fmt);

                    string[] frames = ex.StackTrace.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    float y = 48f;
                    int shown = Math.Min(10, frames.Length);
                    for (int i = 0; i < shown; i++)
                    {
                        g.DrawString(frames[i].Trim(), f, w, 12f, y, fmt);
                        y += 15f;
                        if (y > Height - 18f) break;
                    }
                }
            }
            catch { /* diagnostics must never throw */ }
        }

        private void PaintCore(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            if (Width < 6 || Height < 6)
            {
                using (SolidBrush b = new SolidBrush(_palette.Canvas)) g.FillRectangle(b, ClientRectangle);
                return;
            }

            ViewerPalette pal = _palette;
            ViewerLayout lay = ComputeLayout();

            // 1 — static chrome: one blit from the premultiplied-ARGB cache.
            EnsureChromeCache(lay);
            if (_chromeCache != null) g.DrawImageUnscaled(_chromeCache, 0, 0);
            else
            {
                DrawCanvasLayer(g, pal, lay);
                DrawCardLayer(g, pal, lay);
            }

            DrawZoomStrip(g, pal, lay);      // 2 — dynamic zone strip
            DrawContentLayer(g, pal, lay);   // 3 — image / empty / lock / decoding

            // 4 — host overlays (ROIs, OCR boxes).
            EventHandler<Graphics> legacy = OnCustomPaint;
            if (legacy != null) legacy(this, g);

            EventHandler<OverlayPaintEventArgs> overlay = OverlayPaint;
            if (overlay != null)
            {
                OverlayPaintEventArgs args = new OverlayPaintEventArgs(
                    g, GetImageScreenBounds(), _zoom, _pan, pal);
                overlay(this, args);
            }

            DrawHudLayer(g, pal, lay);       // 5 — readouts
            DrawStateLayer(g, pal, lay);     // 6 — crosshair, focus ring, disabled veil
            DrawNavigationBar(g, pal, lay);  // 7 — embedded navigation controller

            if (_imageLostPending)
            {
                _imageLostPending = false;
                EventHandler lost = ImageLost;
                if (lost != null) lost(this, EventArgs.Empty);
            }
        }

        // ----------------------------------------------------------------
        //  PERFORMANCE ENGINE — chrome cache
        // ----------------------------------------------------------------
        private void InvalidateChrome()
        {
            if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }
            Invalidate();
        }

        private void EnsureChromeCache(ViewerLayout lay)
        {
            if (_chromeCache != null &&
                _chromeCache.Width == Width &&
                _chromeCache.Height == Height) return;

            if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }

            Bitmap bmp = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format32bppPArgb);
            using (Graphics cg = Graphics.FromImage(bmp))
            {
                cg.SmoothingMode = SmoothingMode.AntiAlias;
                cg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                cg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                cg.CompositingQuality = CompositingQuality.HighQuality;

                DrawCanvasLayer(cg, _palette, lay);
                DrawCardLayer(cg, _palette, lay);
            }
            _chromeCache = bmp;
        }

        private Color AccentOf(ViewerPalette pal)
        {
            return _customAccent != Color.Empty ? _customAccent : pal.Accent;
        }

        // ----------------------------------------------------------------
        //  LAYER 1 — CANVAS (baked into the chrome cache)
        // ----------------------------------------------------------------
        private void DrawCanvasLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            RectangleF client = new RectangleF(0f, 0f, Width, Height);
            using (SolidBrush b = new SolidBrush(pal.Canvas)) g.FillRectangle(b, client);

            if (pal.BackdropGlow.A > 0)
            {
                using (LinearGradientBrush lg = new LinearGradientBrush(
                    client, pal.BackdropGlow, Color.FromArgb(0, pal.BackdropGlow), 118f))
                {
                    g.FillRectangle(lg, client);
                }
            }

            if (_controlStyle == ViewerStyle.MaterialFlat)
            {
                using (SolidBrush accent = new SolidBrush(AccentOf(pal)))
                    g.FillRectangle(accent, 0f, 0f, Width, 4f * lay.Dpi);
            }
        }

        // ----------------------------------------------------------------
        //  LAYER 2 — VIEWPORT CARD (baked into the chrome cache)
        // ----------------------------------------------------------------
        private void DrawCardLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            switch (_controlStyle)
            {
                case ViewerStyle.FluentGlass: DrawGlassCard(g, pal, lay); break;
                case ViewerStyle.MaterialFlat: DrawMaterialCard(g, pal, lay); break;
                case ViewerStyle.SoftNeumorphic: DrawNeumorphicCard(g, pal, lay); break;
                case ViewerStyle.Cyberpunk: DrawCyberCard(g, pal, lay); break;
                default: DrawDashboardCard(g, pal, lay); break;
            }
        }

        // STYLE 1 — DASHBOARD PREMIUM
        private void DrawDashboardCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            RectangleF v = lay.View;
            float r = lay.ViewRadius;

            DrawSoftShadow(g, v, r, pal.Shadow, 7f * lay.Dpi, 4);

            Color borderColor = (_hover && Enabled) ? Lerp(pal.Border, AccentOf(pal), 0.45f) : pal.Border;
            using (GraphicsPath face = BuildRoundedPath(v, r))
            {
                using (SolidBrush fill = new SolidBrush(pal.ViewportFace)) g.FillPath(fill, face);
                using (Pen border = new Pen(borderColor, 1f)) g.DrawPath(border, face);
            }

            g.SetClip(new RectangleF(v.X, v.Y + v.Height * 0.55f, v.Width, v.Height * 0.45f));
            using (GraphicsPath ip = BuildRoundedPath(Deflate(v, 1.4f), Math.Max(1f, r - 1.4f)))
            using (Pen hi = new Pen(Color.FromArgb(80, pal.BorderHighlight), 1f))
            {
                g.DrawPath(hi, ip);
            }
            g.ResetClip();
        }

        // Dynamic (NOT cached) — the zone color tracks the zoom value.
        private void DrawZoomStrip(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            if (_controlStyle != ViewerStyle.DashboardPremium) return;

            RectangleF v = lay.View;
            float r = lay.ViewRadius;
            float stripW = v.Width - 2f * r;
            if (stripW <= 4f) return;

            RectangleF strip = new RectangleF(v.X + r, v.Y + 1.5f, stripW, 3f);
            using (GraphicsPath sp = BuildRoundedPath(strip, 1.5f))
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(210, ZoomZoneColor(pal))))
            {
                g.FillPath(sb, sp);
            }
        }

        // STYLE 2 — FLUENT GLASS
        private void DrawGlassCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            RectangleF v = lay.View;
            float r = lay.ViewRadius;

            DrawSoftShadow(g, v, r, pal.Shadow, 5f * lay.Dpi, 3);

            using (GraphicsPath p1 = BuildRoundedPath(v, r))
            using (SolidBrush l1 = new SolidBrush(pal.ViewportFace))
            {
                g.FillPath(l1, p1);
            }
            using (GraphicsPath p2 = BuildRoundedPath(Deflate(v, 4f * lay.Dpi), Math.Max(2f, r - 4f * lay.Dpi)))
            using (SolidBrush l2 = new SolidBrush(pal.ViewportFace))
            {
                g.FillPath(l2, p2);
            }

            if (v.Width > 1f && v.Height > 1f)
            {
                using (LinearGradientBrush lb = new LinearGradientBrush(
                    v, Color.FromArgb(150, pal.BorderHighlight),
                    Color.FromArgb(28, pal.BorderHighlight), 90f))
                using (Pen border = new Pen(lb, 1.2f))
                using (GraphicsPath bp = BuildRoundedPath(Deflate(v, 0.6f), Math.Max(2f, r - 0.6f)))
                {
                    g.DrawPath(border, bp);
                }
            }

            int glow = _pressed ? 3 : (_hover && Enabled ? 2 : 0);
            for (int i = glow; i >= 1; i--)
            {
                using (GraphicsPath gp = BuildRoundedPath(Expand(v, 1.4f * i), r + 1.4f * i))
                using (Pen pen = new Pen(Color.FromArgb(80 / i, AccentOf(pal)), 1.4f))
                {
                    g.DrawPath(pen, gp);
                }
            }
        }

        // STYLE 3 — MATERIAL FLAT
        private void DrawMaterialCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            using (GraphicsPath face = BuildRoundedPath(lay.View, lay.ViewRadius))
            using (SolidBrush fill = new SolidBrush(pal.ViewportFace))
            {
                g.FillPath(fill, face);
            }
        }

        // STYLE 4 — SOFT NEUMORPHIC
        private void DrawNeumorphicCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            RectangleF v = lay.View;
            float r = lay.ViewRadius;
            bool pressed = _pressed && Enabled;
            float off = (pressed ? 2.0f : (_hover && Enabled ? 2.8f : 3.4f)) * lay.Dpi;

            Color tlC = pressed ? pal.ShadowDark : pal.ShadowLight;
            Color brC = pressed ? pal.ShadowLight : pal.ShadowDark;

            for (int i = 2; i >= 1; i--)
            {
                float o = off * (i == 2 ? 1.5f : 0.7f);
                int pct = i == 2 ? 55 : 115;

                using (GraphicsPath pTL = BuildRoundedPath(OffsetRect(v, -o, -o), r))
                using (SolidBrush bTL = new SolidBrush(AlphaScale(tlC, pct)))
                {
                    g.FillPath(bTL, pTL);
                }
                using (GraphicsPath pBR = BuildRoundedPath(OffsetRect(v, o, o), r))
                using (SolidBrush bBR = new SolidBrush(AlphaScale(brC, pct)))
                {
                    g.FillPath(bBR, pBR);
                }
            }

            using (GraphicsPath face = BuildRoundedPath(v, r))
            {
                using (SolidBrush fill = new SolidBrush(pal.ViewportFace)) g.FillPath(fill, face);
                using (Pen hair = new Pen(Color.FromArgb(70, pal.BorderHighlight), 1f)) g.DrawPath(hair, face);
            }

            using (GraphicsPath well = BuildRoundedPath(lay.Content, lay.ContentRadius))
            {
                using (SolidBrush wb = new SolidBrush(pal.ViewportWell)) g.FillPath(wb, well);

                Color topCol = pressed ? pal.ShadowLight : pal.ShadowDark;
                Color botCol = pressed ? pal.ShadowDark : pal.ShadowLight;

                g.SetClip(new RectangleF(lay.Content.X, lay.Content.Y,
                                         lay.Content.Width, lay.Content.Height * 0.5f));
                using (Pen tp = new Pen(topCol, 2.2f)) g.DrawPath(tp, well);
                g.ResetClip();

                g.SetClip(new RectangleF(lay.Content.X, lay.Content.Y + lay.Content.Height * 0.5f,
                                         lay.Content.Width, lay.Content.Height * 0.5f));
                using (Pen bt = new Pen(botCol, 2.2f)) g.DrawPath(bt, well);
                g.ResetClip();
            }
        }

        // STYLE 5 — CYBERPUNK / INDUSTRIAL
        private void DrawCyberCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            RectangleF v = lay.View;
            float r = lay.ViewRadius;

            using (GraphicsPath face = BuildRoundedPath(v, r))
            using (SolidBrush fill = new SolidBrush(pal.ViewportFace))
            {
                g.FillPath(fill, face);
            }

            int glow = _pressed ? 3 : (_hover && Enabled ? 2 : 1);
            for (int i = glow; i >= 1; i--)
            {
                using (GraphicsPath gp = BuildRoundedPath(Expand(v, 1.4f * i + 1f), r + 1.4f * i + 1f))
                using (Pen pen = new Pen(Color.FromArgb(70 / i, AccentOf(pal)), 1.6f))
                {
                    g.DrawPath(pen, gp);
                }
            }

            using (GraphicsPath core = BuildRoundedPath(Deflate(v, 0.8f), Math.Max(2f, r - 0.8f)))
            using (Pen neon = new Pen(AccentOf(pal), 1.7f))
            {
                g.DrawPath(neon, core);
            }

            DrawCornerBrackets(g, v, 16f * lay.Dpi, 2.6f, Color.FromArgb(220, pal.AccentAlt));
        }

        // ----------------------------------------------------------------
        //  LAYER 3 — CONTENT
        // ----------------------------------------------------------------
        private void DrawContentLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            RectangleF c = lay.Content;
            if (c.Width < 2f || c.Height < 2f) return;

            using (GraphicsPath clip = BuildRoundedPath(c, lay.ContentRadius))
            {
                g.SetClip(clip);

                using (SolidBrush well = new SolidBrush(pal.ViewportWell)) g.FillPath(well, clip);

                if (_largeLockActive)
                {
                    DrawLargeLockCard(g, pal, lay);
                }
                else if (_frameDecoding && _image == null)
                {
                    DrawDecodingState(g, pal, lay);
                }
                else
                {
                    if (_showCheckerboard && _image != null)
                        DrawCheckerboard(g, c);

                    if (!TryDrawImage(g, pal, lay))
                        DrawEmptyState(g, pal, lay);
                }

                if (_controlStyle == ViewerStyle.DashboardPremium)
                    DrawEdgeTicks(g, pal, c, lay.Dpi);

                if (_controlStyle == ViewerStyle.Cyberpunk && _showScanlines)
                    DrawScanlines(g, pal, c);

                if (_controlStyle == ViewerStyle.MaterialFlat && Enabled)
                {
                    int alpha = _pressed ? 14 : (_hover ? 7 : 0);
                    if (alpha > 0)
                    {
                        using (SolidBrush ov = new SolidBrush(Color.FromArgb(alpha, pal.HoverOverlay)))
                            g.FillPath(ov, clip);
                    }
                }

                g.ResetClip();
            }
        }

        private void DrawCheckerboard(Graphics g, RectangleF content)
        {
            try
            {
                RectangleF ir = new RectangleF(_pan.X, _pan.Y,
                    _image.Width * _zoom, _image.Height * _zoom);
                RectangleF fill = RectangleF.Intersect(ir, content);
                if (fill.Width <= 0f || fill.Height <= 0f) return;

                EnsureCheckerTile();
                if (_checkerTile == null) return;

                using (TextureBrush tb = new TextureBrush(_checkerTile, WrapMode.Tile))
                {
                    g.FillRectangle(tb, fill);
                }
            }
            catch { /* dead bitmap — the draw path handles it */ }
        }

        private void EnsureCheckerTile()
        {
            if (_checkerTile != null) return;

            _checkerTile = new Bitmap(16, 16, PixelFormat.Format32bppPArgb);
            using (Graphics cg = Graphics.FromImage(_checkerTile))
            {
                bool dark = _themeMode == ThemeMode.Dark;
                Color a = dark ? C(0x191D26) : C(0xFFFFFF);
                Color b = dark ? C(0x22262F) : C(0xE4E8F0);
                using (SolidBrush ba = new SolidBrush(a)) cg.FillRectangle(ba, 0, 0, 16, 16);
                using (SolidBrush bb = new SolidBrush(b))
                {
                    cg.FillRectangle(bb, 0, 0, 8, 8);
                    cg.FillRectangle(bb, 8, 8, 8, 8);
                }
            }
        }

        private bool TryDrawImage(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            if (_image == null) return false;

            try
            {
                int iw = _image.Width;
                int ih = _image.Height;
                float swf = iw * _zoom;
                float shf = ih * _zoom;
                if (swf <= 0f || shf <= 0f) return false;

                RectangleF screen = new RectangleF(_pan.X, _pan.Y, swf, shf);
                RectangleF vis = RectangleF.Intersect(screen, lay.Content);

                if (vis.Width < 1f || vis.Height < 1f)
                    return true;   // exists but fully off-screen

                if (!_enhanceImage)
                {
                    // =========================================================
                    //  PIXEL-PERFECT PATH — NearestNeighbor + integer source +
                    //  integer destination. Every source pixel becomes ONE
                    //  exact integer block. Zero interpolation. Zero blur.
                    //  Mipmaps NEVER touch this path.
                    // =========================================================
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;

                    int sx = (int)Math.Floor((vis.X - _pan.X) / _zoom);
                    int sy = (int)Math.Floor((vis.Y - _pan.Y) / _zoom);
                    int ex = (int)Math.Ceiling((vis.Right - _pan.X) / _zoom);
                    int ey = (int)Math.Ceiling((vis.Bottom - _pan.Y) / _zoom);

                    if (sx < 0) sx = 0;
                    if (sy < 0) sy = 0;
                    if (ex > iw) ex = iw;
                    if (ey > ih) ey = ih;

                    int cw = ex - sx;
                    int ch = ey - sy;
                    if (cw < 1 || ch < 1) return true;

                    int dx = (int)Math.Round(_pan.X + sx * _zoom);
                    int dy = (int)Math.Round(_pan.Y + sy * _zoom);
                    int dw = Math.Max(1, (int)Math.Round(cw * _zoom));
                    int dh = Math.Max(1, (int)Math.Round(ch * _zoom));

                    g.DrawImage(_image,
                                new Rectangle(dx, dy, dw, dh),
                                new Rectangle(sx, sy, cw, ch),
                                GraphicsUnit.Pixel);

                    using (Pen ip = new Pen(Color.FromArgb(70, pal.TextDim), 1f))
                        g.DrawRectangle(ip,
                                        (int)Math.Round(_pan.X), (int)Math.Round(_pan.Y),
                                        (int)Math.Round(swf), (int)Math.Round(shf));

                    if (_controlStyle == ViewerStyle.Cyberpunk)
                        DrawCornerBrackets(g, screen, 10f, 2.2f,
                                           Color.FromArgb(200, pal.AccentAlt));

                    return true;
                }

                // =========================================================
                //  ENHANCED PATH — opt-in only (EnhanceImage == true).
                // =========================================================
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;

                Bitmap source = _image;
                RectangleF src = ClampToSource(new RectangleF(
                    (vis.X - _pan.X) / _zoom,
                    (vis.Y - _pan.Y) / _zoom,
                    vis.Width / _zoom,
                    vis.Height / _zoom), iw, ih);

                if (_useMipmaps && _mips != null && _mips.Count > 0 && _zoom <= 0.5f)
                {
                    double ratio = 1.0 / (double)_zoom;
                    int level = 0;
                    while (level + 1 < _mips.Count && Math.Pow(2, level + 2) <= ratio) level++;

                    Bitmap mip = _mips[level];
                    if (mip != null && mip.Width > 1 && mip.Height > 1)
                    {
                        float kx = (float)mip.Width / iw;
                        float ky = (float)mip.Height / ih;
                        src = RectangleF.FromLTRB(src.X * kx, src.Y * ky,
                                                  src.Right * kx, src.Bottom * ky);
                        src = ClampToSource(src, mip.Width, mip.Height);
                        source = mip;
                    }
                }

                g.DrawImage(source, vis, src, GraphicsUnit.Pixel);
                return true;
            }
            catch (ArgumentException)
            {
                _image = null;
                _needsAutoFit = true;
                _imageLostPending = true;
                _hudSourceText = "—";
                return false;
            }
            finally
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            }
        }

        private static RectangleF ClampToSource(RectangleF r, int iw, int ih)
        {
            float x = ClampF(r.X, 0f, iw);
            float y = ClampF(r.Y, 0f, ih);
            float right = ClampF(r.Right, x, iw);
            float bottom = ClampF(r.Bottom, y, ih);
            return RectangleF.FromLTRB(x, y, right, bottom);
        }

        private void DrawDecodingState(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            RectangleF c = lay.Content;
            float cx = c.X + c.Width / 2f;
            float cy = c.Y + c.Height / 2f;

            Font f = GetFont(true, 10f, FontStyle.Bold);   // cache-owned — no using
            using (StringFormat fmt = new StringFormat())
            {
                fmt.Alignment = StringAlignment.Center;
                fmt.LineAlignment = StringAlignment.Center;
                using (SolidBrush b = new SolidBrush(pal.TextSecondary))
                    g.DrawString("DECODING…", f, b, cx, cy, fmt);
            }
        }

        private void DrawLargeLockCard(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            RectangleF c = lay.Content;
            float s = lay.Dpi;

            float w = 340f * s, h = 170f * s;
            if (w > c.Width - 16f) w = c.Width - 16f;
            if (h > c.Height - 16f) h = c.Height - 16f;
            if (w < 80f || h < 60f) return;

            RectangleF card = new RectangleF(
                c.X + (c.Width - w) / 2f, c.Y + (c.Height - h) / 2f, w, h);

            using (GraphicsPath cp = BuildRoundedPath(card, 14f * s))
            {
                using (SolidBrush cb = new SolidBrush(Color.FromArgb(242, pal.Canvas)))
                    g.FillPath(cb, cp);
                using (Pen border = new Pen(Color.FromArgb(160, pal.ZoneWarn), 1.2f))
                    g.DrawPath(border, cp);
            }

            float lockW = Math.Min(26f * s, w * 0.14f);
            float lockH = lockW * 0.78f;
            RectangleF lockR = new RectangleF(
                card.X + card.Width / 2f - lockW / 2f, card.Y + 24f * s, lockW, lockH);
            DrawPadlock(g, lockR, pal.ZoneWarn, Color.FromArgb(210, pal.HudFore));

            Font tf = GetFont(false, 11f, FontStyle.Bold);
            Font sf = GetFont(false, 8.75f, FontStyle.Regular);
            float tx = card.X + 14f * s;
            float tw = card.Width - 28f * s;

            using (StringFormat fmt = new StringFormat())
            {
                fmt.Trimming = StringTrimming.EllipsisCharacter;
                fmt.FormatFlags |= StringFormatFlags.NoWrap;

                float y = lockR.Bottom + 12f * s;
                using (SolidBrush tb = new SolidBrush(pal.TextPrimary))
                    g.DrawString("LARGE IMAGE — PRO FEATURE", tf, tb, new RectangleF(tx, y, tw, 18f * s), fmt);

                y += 24f * s;
                using (SolidBrush db = new SolidBrush(pal.TextSecondary))
                    g.DrawString(_largeLockSizeText + " exceeds the " + _largeThresholdMB + " MB free limit.",
                                 sf, db, new RectangleF(tx, y, tw, 15f * s), fmt);

                y += 19f * s;
                using (SolidBrush hb = new SolidBrush(pal.TextDim))
                    g.DrawString("Activate a license with the Large Images feature (L), or set EnforceLicense = false for development.",
                                 sf, hb, new RectangleF(tx, y, tw, 15f * s), fmt);
            }
        }

        private static void DrawPadlock(Graphics g, RectangleF r, Color body, Color shackle)
        {
            using (GraphicsPath bp = BuildRoundedPath(r, Math.Min(r.Width, r.Height) * 0.28f))
            using (SolidBrush b = new SolidBrush(body))
            {
                g.FillPath(b, bp);
            }

            float sw = r.Width * 0.62f;
            RectangleF sr = new RectangleF(
                r.X + (r.Width - sw) / 2f, r.Y - r.Height * 0.52f, sw, r.Height * 0.72f);
            using (Pen p = new Pen(shackle, Math.Max(1.6f, r.Width * 0.11f)))
            {
                g.DrawArc(p, sr, 180f, 180f);
            }

            float kd = r.Width * 0.22f;
            using (SolidBrush kb = new SolidBrush(Color.FromArgb(140, shackle)))
            {
                g.FillEllipse(kb, r.X + r.Width / 2f - kd / 2f, r.Y + r.Height * 0.38f, kd, kd);
            }
        }

        private void DrawEmptyState(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            RectangleF c = lay.Content;
            float s = lay.Dpi;

            using (GraphicsPath gridPath = new GraphicsPath())
            using (Pen grid = new Pen(pal.Grid, 1f))
            {
                float step = 28f * s;
                for (float x = c.X + step; x < c.Right; x += step)
                {
                    gridPath.AddLine(x, c.Y, x, c.Bottom);
                    gridPath.StartFigure();
                }
                for (float y = c.Y + step; y < c.Bottom; y += step)
                {
                    gridPath.AddLine(c.X, y, c.Right, y);
                    gridPath.StartFigure();
                }
                if (gridPath.PointCount > 0) g.DrawPath(grid, gridPath);
            }

            bool cyber = _controlStyle == ViewerStyle.Cyberpunk;
            float cx = c.X + c.Width / 2f;
            float cy = c.Y + c.Height / 2f;

            RectangleF body = new RectangleF(cx - 43f * s, cy - 24f * s, 86f * s, 60f * s);
            RectangleF bump = new RectangleF(cx - 15f * s, cy - 35f * s, 30f * s, 13f * s);
            float lensR = 13f * s;
            float innerR = 5f * s;
            float lensCy = cy + 6f * s;
            Color stroke = Color.FromArgb(165, pal.TextSecondary);

            using (GraphicsPath bodyPath = BuildRoundedPath(body, 12f * s))
            using (GraphicsPath bumpPath = BuildRoundedPath(bump, 5f * s))
            using (Pen pen = new Pen(stroke, 2.6f))
            {
                g.DrawPath(pen, bumpPath);
                g.DrawPath(pen, bodyPath);
                using (SolidBrush fill = new SolidBrush(pal.ViewportWell))
                {
                    g.FillPath(fill, bumpPath);
                    g.FillPath(fill, bodyPath);
                }
                g.DrawEllipse(pen, cx - lensR, lensCy - lensR, lensR * 2f, lensR * 2f);
                using (SolidBrush fill2 = new SolidBrush(pal.ViewportWell))
                    g.FillEllipse(fill2, cx - lensR, lensCy - lensR, lensR * 2f, lensR * 2f);
                using (Pen inner = new Pen(Color.FromArgb(120, pal.TextSecondary), 2f))
                    g.DrawEllipse(inner, cx - innerR, lensCy - innerR, innerR * 2f, innerR * 2f);
            }

            string title = cyber ? "NO SIGNAL" : "NO IMAGE LOADED";
            string hint = cyber ? "AWAITING INPUT · DROP FILE TO SCAN"
                                : "Drag & drop an image file, or assign the Image property";
            float ty = body.Bottom + 22f * s;

            // Cache-owned fonts — NEVER wrapped in using.
            Font tf = GetFont(cyber, 10.5f, FontStyle.Bold);
            Font sf = GetFont(false, 8.75f, FontStyle.Regular);

            using (StringFormat fmt = new StringFormat())
            {
                fmt.Alignment = StringAlignment.Center;
                fmt.LineAlignment = StringAlignment.Near;
                fmt.FormatFlags |= StringFormatFlags.NoWrap;

                using (SolidBrush tb = new SolidBrush(pal.TextSecondary))
                    g.DrawString(title, tf, tb, cx, ty, fmt);
                using (SolidBrush sb = new SolidBrush(pal.TextDim))
                    g.DrawString(hint, sf, sb, cx, ty + 19f * s, fmt);
            }
        }

        // ----------------------------------------------------------------
        //  LAYER 5 — HUD READOUTS
        // ----------------------------------------------------------------
        private void DrawHudLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            if (!_showHud || !Enabled) return;
            RectangleF c = lay.Content;
            if (c.Width < 160f || c.Height < 96f) return;

            Font capFont = GetFont(false, 6.75f, FontStyle.Bold);
            Font valFont = GetFont(true, 10f, FontStyle.Bold);

            using (StringFormat fmt = new StringFormat(
                StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces))
            {
                SizeF capS = MeasureCached(g, "SOURCE", capFont, fmt, ref _mCapSrc, ref _szCapSrc);
                SizeF valS = MeasureCached(g, _hudSourceText, valFont, fmt, ref _mValSrc, ref _szValSrc);
                DrawHudChip(g, pal, lay, ChipAnchor.TopLeft, "SOURCE", _hudSourceText,
                            capFont, valFont, capS, valS,
                            _image != null ? AccentOf(pal) : pal.TextDim, false, pal.TextDim);

                bool flat = _controlStyle == ViewerStyle.MaterialFlat;
                Color zone = ZoomZoneColor(pal);

                SizeF capZ = MeasureCached(g, "ZOOM", capFont, fmt, ref _mCapZoom, ref _szCapZoom);
                SizeF valZ = MeasureCached(g, _hudZoomText, valFont, fmt, ref _mValZoom, ref _szValZoom);
                DrawHudChip(g, pal, lay, ChipAnchor.BottomRight, "ZOOM", _hudZoomText,
                            capFont, valFont, capZ, valZ,
                            flat ? (Color?)null : zone, true, flat ? pal.HudFore : zone);

                if (_hover && _image != null && _hudProbeText.Length > 0)
                {
                    SizeF capP = MeasureCached(g, "PROBE", capFont, fmt, ref _mCapProbe, ref _szCapProbe);
                    SizeF valP = MeasureCached(g, _hudProbeText, valFont, fmt, ref _mValProbe, ref _szValProbe);
                    DrawHudChip(g, pal, lay, ChipAnchor.BottomLeft, "PROBE", _hudProbeText,
                                capFont, valFont, capP, valP,
                                null, false, pal.TextDim);
                }
            }
        }

        private SizeF MeasureCached(Graphics g, string text, Font font, StringFormat fmt,
                                    ref string key, ref SizeF size)
        {
            if (!ReferenceEquals(key, text))
            {
                size = g.MeasureString(text, font, int.MaxValue, fmt);
                key = text;
            }
            return size;
        }

        private void DrawHudChip(Graphics g, ViewerPalette pal, ViewerLayout lay, ChipAnchor anchor,
                                 string caption, string value, Font capFont, Font valFont,
                                 SizeF capS, SizeF valS,
                                 Color? dot, bool showBar, Color barColor)
        {
            float dpi = lay.Dpi;
            RectangleF c = lay.Content;
            float padX = 9f * dpi;
            float padY = 6f * dpi;

            float dotW = dot.HasValue ? 14f * dpi : 0f;
            float w = Math.Max(capS.Width, valS.Width) + dotW + padX * 2f;
            float h = padY + capS.Height + 2f * dpi + valS.Height + (showBar ? 6f * dpi : 0f) + padY;

            if (w > c.Width - 12f || h > c.Height - 12f) return;

            float inset = 10f * dpi;
            PointF loc;
            switch (anchor)
            {
                case ChipAnchor.BottomLeft: loc = new PointF(c.X + inset, c.Bottom - inset - h); break;
                case ChipAnchor.BottomRight: loc = new PointF(c.Right - inset - w, c.Bottom - inset - h); break;
                default: loc = new PointF(c.X + inset, c.Y + inset); break;
            }
            RectangleF chip = new RectangleF(loc.X, loc.Y, w, h);

            using (GraphicsPath path = BuildRoundedPath(chip, 7f * dpi))
            {
                using (SolidBrush bg = new SolidBrush(pal.HudBack)) g.FillPath(bg, path);
                if (pal.HudBorder.A > 0)
                {
                    using (Pen bp = new Pen(pal.HudBorder, 1f)) g.DrawPath(bp, path);
                }

                if (_controlStyle == ViewerStyle.SoftNeumorphic)
                {
                    g.SetClip(new RectangleF(chip.X, chip.Y, chip.Width, chip.Height * 0.5f));
                    using (Pen tp = new Pen(pal.ShadowDark, 1f)) g.DrawPath(tp, path);
                    g.ResetClip();
                    g.SetClip(new RectangleF(chip.X, chip.Y + chip.Height * 0.5f,
                                             chip.Width, chip.Height * 0.5f));
                    using (Pen bt = new Pen(pal.ShadowLight, 1f)) g.DrawPath(bt, path);
                    g.ResetClip();
                }
            }

            using (StringFormat fmt = new StringFormat(StringFormatFlags.NoWrap))
            {
                float tx = chip.X + padX;
                using (SolidBrush capBrush = new SolidBrush(pal.HudForeDim))
                    g.DrawString(caption, capFont, capBrush, tx, chip.Y + padY, fmt);

                float vy = chip.Y + padY + capS.Height + 2f * dpi;
                using (SolidBrush valBrush = new SolidBrush(pal.HudFore))
                    g.DrawString(value, valFont, valBrush, tx, vy, fmt);

                if (dot.HasValue)
                {
                    float d = 6.8f * dpi;
                    using (SolidBrush db = new SolidBrush(dot.Value))
                        g.FillEllipse(db, chip.Right - padX - d, vy + valS.Height / 2f - d / 2f, d, d);
                }

                if (showBar)
                {
                    float by = chip.Bottom - padY - 2.2f * dpi;
                    float trackW = w - padX * 2f;

                    using (GraphicsPath track = BuildRoundedPath(
                        new RectangleF(tx, by, trackW, 3f * dpi), 1.5f * dpi))
                    using (SolidBrush tb = new SolidBrush(Color.FromArgb(70, pal.HudFore)))
                        g.FillPath(tb, track);

                    float t = ZoomBarT();
                    if (t > 0.01f)
                    {
                        using (GraphicsPath fill = BuildRoundedPath(
                            new RectangleF(tx, by, trackW * t, 3f * dpi), 1.5f * dpi))
                        using (SolidBrush fb = new SolidBrush(barColor))
                            g.FillPath(fb, fill);
                    }
                }
            }
        }

        // ----------------------------------------------------------------
        //  LAYER 6 — STATE CHROME
        // ----------------------------------------------------------------
        private void DrawStateLayer(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            if (_showCrosshair && _hover && !_panning && Enabled &&
                (_controlStyle == ViewerStyle.Cyberpunk || _controlStyle == ViewerStyle.DashboardPremium))
            {
                using (GraphicsPath clip = BuildRoundedPath(lay.Content, lay.ContentRadius))
                {
                    g.SetClip(clip);
                    float px = _hoverPos.X;
                    float py = _hoverPos.Y;
                    float gap = 11f * lay.Dpi;
                    Color accent = AccentOf(pal);

                    using (Pen pen = new Pen(Color.FromArgb(115, accent), 1f))
                    {
                        g.DrawLine(pen, lay.Content.X, py, px - gap, py);
                        g.DrawLine(pen, px + gap, py, lay.Content.Right, py);
                        g.DrawLine(pen, px, lay.Content.Y, px, py - gap);
                        g.DrawLine(pen, px, py + gap, px, lay.Content.Bottom);
                    }
                    using (Pen cp = new Pen(Color.FromArgb(190, accent), 1.2f))
                        g.DrawEllipse(cp, px - 3.5f, py - 3.5f, 7f, 7f);

                    g.ResetClip();
                }
            }

            if (Focused && Enabled)
            {
                using (GraphicsPath fp = BuildRoundedPath(Expand(lay.View, 3f), lay.ViewRadius + 3f))
                using (Pen pen = new Pen(Color.FromArgb(215, AccentOf(pal)), 1.4f))
                    g.DrawPath(pen, fp);
            }

            if (!Enabled)
            {
                using (SolidBrush veil = new SolidBrush(Color.FromArgb(110, pal.Canvas)))
                    g.FillRectangle(veil, 0f, 0f, Width, Height);
            }
        }

        // ----------------------------------------------------------------
        //  LAYER 7 — EMBEDDED NAVIGATION CONTROLLER
        //          [« first] [‹ prev] [ PAGE n / N ] [next ›] [last »]
        // ----------------------------------------------------------------
        private void DrawNavigationBar(Graphics g, ViewerPalette pal, ViewerLayout lay)
        {
            _navRectsValid = false;
            if (!_showNavigationBar || !Enabled) return;
            if (_frameCount <= 1) return;
            if (_mode != ContentMode.Multipage && _mode != ContentMode.Collection) return;

            RectangleF c = lay.Content;
            float s = lay.Dpi;
            if (c.Width < 300f * s || c.Height < 150f * s) return;

            bool locked = _multipageLocked;
            string label = _frameDecoding ? "DECODING"
                : (_mode == ContentMode.Multipage ? "PAGE " : "IMAGE ") +
                  (_frameIndex + 1).ToString() + " / " + _frameCount.ToString();

            Font f = GetFont(true, 9f, FontStyle.Bold);       // cache-owned — no using
            Color accent = AccentOf(pal);

            using (StringFormat fmt = new StringFormat(
                StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces))
            {
                SizeF txt = MeasureCached(g, label, f, fmt, ref _mNavKey, ref _szNavSize);

                float padX = 10f * s;
                float padY = 7f * s;
                float lockW = locked ? 18f * s : 0f;
                float barW = txt.Width + padX * 2f + lockW;
                float barH = txt.Height + padY * 2f;
                float btnD = Math.Max(28f * s, barH);
                float gap = 6f * s;
                float totalW = btnD * 4f + barW + gap * 4f;
                if (totalW > c.Width - 24f) return;

                float x = c.X + c.Width / 2f - totalW / 2f;
                float y = c.Bottom - 12f * s - btnD;

                // [« first] [‹ prev]
                for (int i = 0; i < 2; i++)
                {
                    RectangleF r = new RectangleF(x, y, btnD, btnD);
                    DrawNavButton(g, pal, r, (i == 0) ? -2 : -1, _navHover == i + 1,
                                  locked, i == 0 && _frameIndex == 0, accent);
                    _navRects[i] = r;
                    x += btnD + gap;
                }

                // [ PAGE n / N ]
                RectangleF barR = new RectangleF(x, y + (btnD - barH) / 2f, barW, barH);
                using (GraphicsPath bp = BuildRoundedPath(barR, barH / 2f))
                {
                    using (SolidBrush bb = new SolidBrush(pal.HudBack)) g.FillPath(bb, bp);
                    if (pal.HudBorder.A > 0)
                    {
                        using (Pen p = new Pen(pal.HudBorder, 1f)) g.DrawPath(p, bp);
                    }
                }
                using (StringFormat sf = new StringFormat(StringFormatFlags.NoWrap))
                using (SolidBrush tb = new SolidBrush(_frameDecoding ? pal.HudForeDim : pal.HudFore))
                {
                    g.DrawString(label, f, tb, barR.X + padX, barR.Y + padY, sf);
                }
                if (locked)
                {
                    RectangleF lockR = new RectangleF(
                        barR.Right - padX - 8f * s, barR.Y + (barH - 11f * s) / 2f, 8f * s, 10.5f * s);
                    DrawPadlock(g, lockR, Color.FromArgb(200, pal.ZoneWarn),
                                Color.FromArgb(170, pal.HudFore));
                }
                x += barW + gap;

                // [next ›] [last »]
                for (int i = 2; i < 4; i++)
                {
                    RectangleF r = new RectangleF(x, y, btnD, btnD);
                    DrawNavButton(g, pal, r, (i == 2) ? 1 : 2, _navHover == i + 1,
                                  locked, i == 3 && _frameIndex == _frameCount - 1, accent);
                    _navRects[i] = r;
                    x += btnD + gap;
                }

                _navRectsValid = true;
            }
        }

        private void DrawNavButton(Graphics g, ViewerPalette pal, RectangleF r, int dir,
                                   bool hover, bool locked, bool dim, Color accent)
        {
            using (GraphicsPath p = BuildRoundedPath(r, r.Width / 2f))
            {
                Color bg = (hover && !locked) ? Color.FromArgb(70, accent) : pal.HudBack;
                using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
                if (pal.HudBorder.A > 0)
                {
                    Color bc = (hover && !locked) ? accent : pal.HudBorder;
                    using (Pen bp = new Pen(bc, 1f)) g.DrawPath(bp, p);
                }
            }

            float m = r.Width * 0.15f;
            PointF ctr = new PointF(r.X + r.Width / 2f, r.Y + r.Height / 2f);
            float off = r.Width * 0.16f;

            Color glyph = locked ? Color.FromArgb(110, pal.HudFore)
                                 : (dim ? Color.FromArgb(140, pal.HudForeDim) : pal.HudFore);

            using (GraphicsPath tri = new GraphicsPath())
            using (SolidBrush gb = new SolidBrush(glyph))
            {
                if (dir == -2)      // « first
                {
                    AddTriangle(tri, new PointF(ctr.X - off, ctr.Y), m, -1);
                    AddTriangle(tri, new PointF(ctr.X + off, ctr.Y), m, -1);
                }
                else if (dir == 2)  // » last
                {
                    AddTriangle(tri, new PointF(ctr.X - off, ctr.Y), m, +1);
                    AddTriangle(tri, new PointF(ctr.X + off, ctr.Y), m, +1);
                }
                else                // ‹ or ›
                {
                    AddTriangle(tri, ctr, m * 1.25f, dir);
                }
                g.FillPath(gb, tri);
            }
        }

        private static void AddTriangle(GraphicsPath p, PointF ctr, float m, int dir)
        {
            if (dir < 0)
            {
                p.AddLine(ctr.X + m * 0.7f, ctr.Y - m, ctr.X - m * 0.7f, ctr.Y);
                p.AddLine(ctr.X - m * 0.7f, ctr.Y, ctr.X + m * 0.7f, ctr.Y + m);
            }
            else
            {
                p.AddLine(ctr.X - m * 0.7f, ctr.Y - m, ctr.X + m * 0.7f, ctr.Y);
                p.AddLine(ctr.X + m * 0.7f, ctr.Y, ctr.X - m * 0.7f, ctr.Y + m);
            }
            p.CloseFigure();
        }

        private bool NavHitTest(Point p, out int navIndex)   // 0 = none, 1..4
        {
            navIndex = 0;
            if (!_navRectsValid) return false;
            for (int i = 0; i < 4; i++)
                if (_navRects[i].Contains(p)) { navIndex = i + 1; return true; }
            return false;
        }

        // ----------------------------------------------------------------
        //  BATCHED MICRO-DETAILS
        // ----------------------------------------------------------------
        private static void DrawEdgeTicks(Graphics g, ViewerPalette pal, RectangleF c, float dpi)
        {
            SmoothingMode prev = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;

            using (GraphicsPath minorPath = new GraphicsPath())
            using (GraphicsPath majorPath = new GraphicsPath())
            using (Pen minor = new Pen(Color.FromArgb(110, pal.Tick), 1f))
            using (Pen major = new Pen(Color.FromArgb(210, pal.Tick), 1f))
            {
                float step = 9f * dpi;

                int i = 0;
                for (float x = c.X + 3f; x < c.Right - 2f; x += step)
                {
                    GraphicsPath target = (i % 5 == 0) ? majorPath : minorPath;
                    float len = (i % 5 == 0) ? 7f * dpi : 4f * dpi;
                    target.AddLine(x, c.Y + 1f, x, c.Y + 1f + len);
                    target.StartFigure();
                    i++;
                }

                i = 0;
                for (float y = c.Y + 3f; y < c.Bottom - 2f; y += step)
                {
                    GraphicsPath target = (i % 5 == 0) ? majorPath : minorPath;
                    float len = (i % 5 == 0) ? 7f * dpi : 4f * dpi;
                    target.AddLine(c.X + 1f, y, c.X + 1f + len, y);
                    target.StartFigure();
                    i++;
                }

                if (minorPath.PointCount > 0) g.DrawPath(minor, minorPath);
                if (majorPath.PointCount > 0) g.DrawPath(major, majorPath);
            }

            g.SmoothingMode = prev;
        }

        private static void DrawScanlines(Graphics g, ViewerPalette pal, RectangleF c)
        {
            SmoothingMode prev = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;

            using (GraphicsPath path = new GraphicsPath())
            using (Pen pen = new Pen(Color.FromArgb(9, pal.Accent), 1f))
            {
                for (float y = c.Y + 2f; y < c.Bottom; y += 4f)
                {
                    path.AddLine(c.X, y, c.Right, y);
                    path.StartFigure();
                }
                if (path.PointCount > 0) g.DrawPath(pen, path);
            }

            g.SmoothingMode = prev;
        }

        private static void DrawCornerBrackets(Graphics g, RectangleF rect, float len, float width, Color color)
        {
            using (Pen pen = new Pen(color, width))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                float l = rect.Left, t = rect.Top, r = rect.Right, b = rect.Bottom;

                g.DrawLine(pen, l, t, l + len, t); g.DrawLine(pen, l, t, l, t + len);
                g.DrawLine(pen, r, t, r - len, t); g.DrawLine(pen, r, t, r, t + len);
                g.DrawLine(pen, l, b, l + len, b); g.DrawLine(pen, l, b, l, b - len);
                g.DrawLine(pen, r, b, r - len, b); g.DrawLine(pen, r, b, r, b - len);
            }
        }

        private static void DrawSoftShadow(Graphics g, RectangleF rect, float radius, Color shadow,
                                           float depth, int steps)
        {
            int layerAlpha = Math.Max(4, shadow.A / steps);
            for (int i = steps; i >= 1; i--)
            {
                float off = 1f + (depth * (i - 1) / steps);
                using (GraphicsPath p = BuildRoundedPath(OffsetRect(rect, 0f, off), radius + i * 0.7f))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(layerAlpha, shadow)))
                {
                    g.FillPath(b, p);
                }
            }
        }

        // ----------------------------------------------------------------
        //  LAYOUT ENGINE
        // ----------------------------------------------------------------
        private ViewerLayout ComputeLayout()
        {
            float dpi = Math.Max(1f, _dpiScale);

            float maxPad = Math.Min(Math.Max(0f, (Width - 2f) / 2f),
                                    Math.Max(0f, (Height - 2f) / 2f));

            float pad;
            if (_viewportPadding >= 0)
            {
                pad = Math.Min(_viewportPadding * dpi, maxPad);
            }
            else
            {
                float padMul = _controlStyle == ViewerStyle.MaterialFlat ? 1.45f : 1f;
                float basePad = Math.Min(Width, Height) * 0.035f;
                pad = Math.Max(11f * dpi, Math.Min(26f * dpi, basePad)) * padMul;
                pad = Math.Min(pad, maxPad);
            }

            RectangleF view = RectangleF.FromLTRB(pad, pad,
                Math.Max(pad + 2f, Width - pad),
                Math.Max(pad + 2f, Height - pad));

            float radius;
            float inset;
            switch (_controlStyle)
            {
                case ViewerStyle.FluentGlass: radius = 14f * dpi; inset = 1.5f * dpi; break;
                case ViewerStyle.MaterialFlat: radius = 3f * dpi; inset = 0.75f * dpi; break;
                case ViewerStyle.SoftNeumorphic: radius = 20f * dpi; inset = 11f * dpi; break;
                case ViewerStyle.Cyberpunk: radius = 8f * dpi; inset = 1.25f * dpi; break;
                default: radius = 10f * dpi; inset = 1.25f * dpi; break;
            }

            float maxInset = Math.Min(Math.Max(0f, (view.Width - 2f) / 2f),
                                      Math.Max(0f, (view.Height - 2f) / 2f));
            inset = Math.Min(inset, maxInset);

            float cr = Math.Max(1f, radius - inset - 0.5f);
            float minDim = Math.Min(view.Width, view.Height);
            if (cr * 2f > minDim) cr = minDim / 2f;

            return new ViewerLayout
            {
                View = view,
                Content = Deflate(view, inset),
                ViewRadius = radius,
                ContentRadius = cr,
                Dpi = dpi
            };
        }

        private Color ZoomZoneColor(ViewerPalette pal)
        {
            if (_zoom < 2f) return pal.ZoneGood;
            if (_zoom < 8f) return pal.ZoneWarn;
            return pal.ZoneHot;
        }

        private float ZoomBarT()
        {
            double lo = Math.Log10((double)_minZoom);
            double hi = Math.Log10((double)_maxZoom);
            if (hi - lo < 0.0001) return 0f;

            double t = (Math.Log10((double)_zoom) - lo) / (hi - lo);
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;
            return (float)t;
        }

        // ----------------------------------------------------------------
        //  GEOMETRY & COLOR PRIMITIVES
        // ----------------------------------------------------------------
        private static GraphicsPath BuildRoundedPath(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();

            float w = rect.Width;
            float h = rect.Height;
            if (float.IsNaN(w) || float.IsNaN(h) || float.IsInfinity(w) || float.IsInfinity(h))
                return path;
            if (w < 0f) w = 0f;
            if (h < 0f) h = 0f;
            if (float.IsNaN(radius) || radius < 0f) radius = 0f;
            if (w < 0.5f || h < 0.5f) return path;

            rect = new RectangleF(rect.X, rect.Y, w, h);

            float maxR = Math.Min(w, h) / 2f;
            if (radius < 0.5f) { path.AddRectangle(rect); return path; }
            if (radius > maxR) radius = maxR;

            float d = radius * 2f;
            path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        private static RectangleF Deflate(RectangleF r, float v)
        { return new RectangleF(r.X + v, r.Y + v, r.Width - 2f * v, r.Height - 2f * v); }

        private static RectangleF Expand(RectangleF r, float v)
        { return new RectangleF(r.X - v, r.Y - v, r.Width + 2f * v, r.Height + 2f * v); }

        private static RectangleF OffsetRect(RectangleF r, float dx, float dy)
        { return new RectangleF(r.X + dx, r.Y + dy, r.Width, r.Height); }

        private static Color Lerp(Color a, Color b, float t)
        {
            return Color.FromArgb(
                a.R + (int)((b.R - a.R) * t),
                a.G + (int)((b.G - a.G) * t),
                a.B + (int)((b.B - a.B) * t));
        }

        private static Color AlphaScale(Color c, int percent)
        {
            int a = (int)(c.A * percent / 100.0);
            if (a > 255) a = 255;
            return Color.FromArgb(a, c);
        }

        private static float ClampF(float v, float lo, float hi)
        {
            if (float.IsNaN(v)) return lo;
            return v < lo ? lo : (v > hi ? hi : v);
        }

        // ----------------------------------------------------------------
        //  HUD TEXT UPDATES
        // ----------------------------------------------------------------
        private void UpdateZoomText()
        {
            _hudZoomText = (_zoom * 100f).ToString("0.#") + " %";
        }

        private void UpdateSourceText()
        {
            try
            {
                _hudSourceText = _image != null
                    ? string.Format("{0} × {1}", _image.Width, _image.Height)
                    : "—";
            }
            catch { _hudSourceText = "—"; }
        }

        private bool UpdateProbeText()
        {
            if (_image == null) return false;
            try
            {
                PointF ip = ScreenToImageF(_hoverPos);
                int px = (int)Math.Round(ip.X);
                int py = (int)Math.Round(ip.Y);
                if (px != _probeX || py != _probeY)
                {
                    _probeX = px;
                    _probeY = py;
                    _hudProbeText = string.Format("X {0}   Y {1}", px, py);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private void ResetProbe()
        {
            _probeX = int.MinValue;
            _probeY = int.MinValue;
            _hudProbeText = "";
        }

        // ----------------------------------------------------------------
        //  PALETTE ENGINE — 5 styles × 2 themes
        // ----------------------------------------------------------------
        private static ViewerPalette BuildPalette(ThemeMode theme, ViewerStyle style)
        {
            bool dark = theme == ThemeMode.Dark;
            switch (style)
            {
                case ViewerStyle.FluentGlass: return GlassPalette(dark);
                case ViewerStyle.MaterialFlat: return MaterialPalette(dark);
                case ViewerStyle.SoftNeumorphic: return NeumorphicPalette(dark);
                case ViewerStyle.Cyberpunk: return CyberPalette();
                default: return DashboardPalette(dark);
            }
        }

        private static Color C(int rgb)
        { return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF); }

        private static ViewerPalette DashboardPalette(bool dark)
        {
            if (dark)
            {
                return new ViewerPalette
                {
                    Canvas = C(0x0F1218),
                    BackdropGlow = Color.FromArgb(0, Color.White),
                    ViewportFace = C(0x151A22),
                    ViewportWell = C(0x11151C),
                    Border = C(0x28303E),
                    BorderHighlight = Color.White,
                    TextPrimary = C(0xE8EDF5),
                    TextSecondary = C(0x97A3B5),
                    TextDim = C(0x5E6A80),
                    Accent = C(0x4C8DFF),
                    AccentAlt = C(0x38BDF8),
                    ZoneGood = C(0x2FBF71),
                    ZoneWarn = C(0xF5A524),
                    ZoneHot = C(0xEF4B4B),
                    Grid = C(0x1B202A),
                    Tick = C(0x7A87A0),
                    HudBack = Color.FromArgb(190, C(0x0B0E14)),
                    HudBorder = Color.FromArgb(60, Color.White),
                    HudFore = C(0xE8EDF5),
                    HudForeDim = C(0x97A3B5),
                    Shadow = Color.FromArgb(150, Color.Black),
                    ShadowLight = Color.FromArgb(0, Color.White),
                    ShadowDark = Color.FromArgb(0, Color.Black),
                    HoverOverlay = Color.White,
                    FocusRing = C(0x4C8DFF)
                };
            }
            return new ViewerPalette
            {
                Canvas = C(0xF3F5F9),
                BackdropGlow = Color.FromArgb(0, Color.White),
                ViewportFace = C(0xFFFFFF),
                ViewportWell = C(0xFAFBFE),
                Border = C(0xDCE3EE),
                BorderHighlight = Color.White,
                TextPrimary = C(0x16202F),
                TextSecondary = C(0x5F6C7E),
                TextDim = C(0x9AA6B8),
                Accent = C(0x2563EB),
                AccentAlt = C(0x0EA5E9),
                ZoneGood = C(0x16A34A),
                ZoneWarn = C(0xD97706),
                ZoneHot = C(0xDC2626),
                Grid = C(0xE7ECF4),
                Tick = C(0xB9C4D4),
                HudBack = Color.FromArgb(172, C(0xF9FBFE)),
                HudBorder = Color.FromArgb(170, Color.White),
                HudFore = C(0x16202F),
                HudForeDim = C(0x5F6C7E),
                Shadow = Color.FromArgb(30, C(0x0E1626)),
                ShadowLight = Color.FromArgb(0, Color.White),
                ShadowDark = Color.FromArgb(0, Color.Black),
                HoverOverlay = Color.Black,
                FocusRing = C(0x2563EB)
            };
        }

        private static ViewerPalette GlassPalette(bool dark)
        {
            if (dark)
            {
                return new ViewerPalette
                {
                    Canvas = C(0x171B24),
                    BackdropGlow = Color.FromArgb(38, C(0x00C8FF)),
                    ViewportFace = Color.FromArgb(46, C(0x2A3140)),
                    ViewportWell = Color.FromArgb(235, C(0x1B2029)),
                    Border = Color.FromArgb(0, Color.White),
                    BorderHighlight = Color.White,
                    TextPrimary = C(0xEBF1FB),
                    TextSecondary = C(0x9BA8BD),
                    TextDim = C(0x63718A),
                    Accent = C(0x00C8FF),
                    AccentAlt = C(0x7C6CFF),
                    ZoneGood = C(0x1FBF6B),
                    ZoneWarn = C(0xF5A524),
                    ZoneHot = C(0xF4506C),
                    Grid = Color.FromArgb(26, C(0x6E82A6)),
                    Tick = Color.FromArgb(80, C(0x5D6F92)),
                    HudBack = Color.FromArgb(125, C(0x0E1219)),
                    HudBorder = Color.FromArgb(140, Color.White),
                    HudFore = C(0xEBF1FB),
                    HudForeDim = C(0x9BA8BD),
                    Shadow = Color.FromArgb(80, Color.Black),
                    ShadowLight = Color.FromArgb(0, Color.White),
                    ShadowDark = Color.FromArgb(0, Color.Black),
                    HoverOverlay = C(0x00C8FF),
                    FocusRing = C(0x00C8FF)
                };
            }
            return new ViewerPalette
            {
                Canvas = C(0xE8EEF7),
                BackdropGlow = Color.FromArgb(70, Color.White),
                ViewportFace = Color.FromArgb(52, Color.White),
                ViewportWell = Color.FromArgb(150, Color.White),
                Border = Color.FromArgb(0, Color.White),
                BorderHighlight = Color.White,
                TextPrimary = C(0x0F2440),
                TextSecondary = C(0x5D6F8C),
                TextDim = C(0x8FA0B8),
                Accent = C(0x0A84FF),
                AccentAlt = C(0x7C5CFF),
                ZoneGood = C(0x0E9F6E),
                ZoneWarn = C(0xF0A63A),
                ZoneHot = C(0xE5484D),
                Grid = Color.FromArgb(24, C(0x2A3C5E)),
                Tick = Color.FromArgb(80, C(0x334666)),
                HudBack = Color.FromArgb(150, Color.White),
                HudBorder = Color.FromArgb(200, Color.White),
                HudFore = C(0x0F2440),
                HudForeDim = C(0x5D6F8C),
                Shadow = Color.FromArgb(55, C(0x1E2E4C)),
                ShadowLight = Color.FromArgb(0, Color.White),
                ShadowDark = Color.FromArgb(0, Color.Black),
                HoverOverlay = C(0x0A84FF),
                FocusRing = C(0x0A84FF)
            };
        }

        private static ViewerPalette MaterialPalette(bool dark)
        {
            if (dark)
            {
                return new ViewerPalette
                {
                    Canvas = C(0x121212),
                    BackdropGlow = Color.FromArgb(0, Color.White),
                    ViewportFace = C(0x1E1E1E),
                    ViewportWell = C(0x1A1A1A),
                    Border = Color.FromArgb(0, Color.White),
                    BorderHighlight = Color.FromArgb(0, Color.White),
                    TextPrimary = C(0xF2F2F2),
                    TextSecondary = C(0xB0B0B0),
                    TextDim = C(0x8A8A8A),
                    Accent = C(0xBB86FC),
                    AccentAlt = C(0x03DAC6),
                    ZoneGood = C(0x66BB6A),
                    ZoneWarn = C(0xFFB74D),
                    ZoneHot = C(0xEF5350),
                    Grid = C(0x232323),
                    Tick = C(0x2E2E2E),
                    HudBack = C(0xBB86FC),
                    HudBorder = Color.FromArgb(0, Color.White),
                    HudFore = C(0x141218),
                    HudForeDim = Color.FromArgb(150, C(0x141218)),
                    Shadow = Color.FromArgb(0, Color.Black),
                    ShadowLight = Color.FromArgb(0, Color.White),
                    ShadowDark = Color.FromArgb(0, Color.Black),
                    HoverOverlay = Color.White,
                    FocusRing = C(0xBB86FC)
                };
            }
            return new ViewerPalette
            {
                Canvas = C(0xF5F5F5),
                BackdropGlow = Color.FromArgb(0, Color.White),
                ViewportFace = C(0xFFFFFF),
                ViewportWell = C(0xFFFFFF),
                Border = Color.FromArgb(0, Color.White),
                BorderHighlight = Color.FromArgb(0, Color.White),
                TextPrimary = C(0x212121),
                TextSecondary = C(0x757575),
                TextDim = C(0x9E9E9E),
                Accent = C(0x6200EE),
                AccentAlt = C(0x03DAC6),
                ZoneGood = C(0x2E7D32),
                ZoneWarn = C(0xEF6C00),
                ZoneHot = C(0xC62828),
                Grid = C(0xEEEEEE),
                Tick = C(0xE0E0E0),
                HudBack = C(0x6200EE),
                HudBorder = Color.FromArgb(0, Color.White),
                HudFore = Color.White,
                HudForeDim = Color.FromArgb(178, Color.White),
                Shadow = Color.FromArgb(0, Color.Black),
                ShadowLight = Color.FromArgb(0, Color.White),
                ShadowDark = Color.FromArgb(0, Color.Black),
                HoverOverlay = Color.Black,
                FocusRing = C(0x6200EE)
            };
        }

        private static ViewerPalette NeumorphicPalette(bool dark)
        {
            if (dark)
            {
                return new ViewerPalette
                {
                    Canvas = C(0x2A2F3A),
                    BackdropGlow = Color.FromArgb(0, Color.White),
                    ViewportFace = C(0x2A2F3A),
                    ViewportWell = C(0x252A33),
                    Border = Color.FromArgb(0, Color.White),
                    BorderHighlight = Color.FromArgb(30, Color.White),
                    TextPrimary = C(0xD6DCE8),
                    TextSecondary = C(0x8791A6),
                    TextDim = C(0x5F6879),
                    Accent = C(0x7C8CF8),
                    AccentAlt = C(0x9EA8FA),
                    ZoneGood = C(0x58B87E),
                    ZoneWarn = C(0xC9A55A),
                    ZoneHot = C(0xC96A5E),
                    Grid = Color.FromArgb(24, C(0x6A748C)),
                    Tick = Color.FromArgb(60, C(0x6A748C)),
                    HudBack = C(0x2A2F3A),
                    HudBorder = Color.FromArgb(0, Color.White),
                    HudFore = C(0xD6DCE8),
                    HudForeDim = C(0x8791A6),
                    Shadow = Color.FromArgb(70, C(0x1C2028)),
                    ShadowLight = Color.FromArgb(130, C(0x3B4250)),
                    ShadowDark = Color.FromArgb(160, C(0x1C2028)),
                    HoverOverlay = Color.White,
                    FocusRing = C(0x7C8CF8)
                };
            }
            return new ViewerPalette
            {
                Canvas = C(0xE4E9F1),
                BackdropGlow = Color.FromArgb(0, Color.White),
                ViewportFace = C(0xE4E9F1),
                ViewportWell = C(0xDCE2EC),
                Border = Color.FromArgb(0, Color.White),
                BorderHighlight = Color.White,
                TextPrimary = C(0x47536E),
                TextSecondary = C(0x8B96AD),
                TextDim = C(0xA9B3C7),
                Accent = C(0x6C7BF2),
                AccentAlt = C(0x9BA6F5),
                ZoneGood = C(0x6FBF8E),
                ZoneWarn = C(0xD2A24C),
                ZoneHot = C(0xD26A5C),
                Grid = Color.FromArgb(26, C(0x9FACC6)),
                Tick = Color.FromArgb(60, C(0x9FACC6)),
                HudBack = C(0xE4E9F1),
                HudBorder = Color.FromArgb(0, Color.White),
                HudFore = C(0x47536E),
                HudForeDim = C(0x8B96AD),
                Shadow = Color.FromArgb(60, C(0xC3CDDF)),
                ShadowLight = Color.FromArgb(210, Color.White),
                ShadowDark = Color.FromArgb(170, C(0xC3CDDF)),
                HoverOverlay = Color.White,
                FocusRing = C(0x6C7BF2)
            };
        }

        private static ViewerPalette CyberPalette()
        {
            return new ViewerPalette
            {
                Canvas = C(0x05070A),
                BackdropGlow = Color.FromArgb(22, C(0x00E5FF)),
                ViewportFace = C(0x0A0D13),
                ViewportWell = C(0x080A10),
                Border = C(0x00E5FF),
                BorderHighlight = Color.FromArgb(0, Color.White),
                TextPrimary = C(0xD9F5FF),
                TextSecondary = C(0x6E8CA0),
                TextDim = C(0x45586A),
                Accent = C(0x00E5FF),
                AccentAlt = C(0xFF2E97),
                ZoneGood = C(0x00FFA3),
                ZoneWarn = C(0xFFE066),
                ZoneHot = C(0xFF3860),
                Grid = Color.FromArgb(22, C(0x00E5FF)),
                Tick = Color.FromArgb(80, C(0x00E5FF)),
                HudBack = Color.FromArgb(200, C(0x05080E)),
                HudBorder = Color.FromArgb(90, C(0x00E5FF)),
                HudFore = C(0xD9F5FF),
                HudForeDim = C(0x5E7E93),
                Shadow = Color.FromArgb(140, Color.Black),
                ShadowLight = Color.FromArgb(0, Color.White),
                ShadowDark = Color.FromArgb(0, Color.Black),
                HoverOverlay = C(0x00E5FF),
                FocusRing = C(0xFF2E97)
            };
        }

        // ----------------------------------------------------------------
        //  FONT ENGINE
        // ----------------------------------------------------------------
        private static string UiFamily
        {
            get
            {
                if (_uiFamily == null) _uiFamily = ResolveFontFamily(UiFontCandidates);
                return _uiFamily;
            }
        }

        private static string MonoFamily
        {
            get
            {
                if (_monoFamily == null) _monoFamily = ResolveFontFamily(MonoFontCandidates);
                return _monoFamily;
            }
        }

        private static string ResolveFontFamily(string[] candidates)
        {
            try
            {
                using (InstalledFontCollection installed = new InstalledFontCollection())
                {
                    HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (FontFamily family in installed.Families) names.Add(family.Name);
                    foreach (string candidate in candidates)
                        if (names.Contains(candidate)) return candidate;
                }
            }
            catch { }
            return candidates[candidates.Length - 1];
        }

        private Font GetFont(bool mono, float sizePt, FontStyle style)
        {
            string family = mono ? MonoFamily : UiFamily;
            string key = family + "|" + sizePt.ToString("0.###") + "|" + (int)style;

            Font font;
            if (_fontCache.TryGetValue(key, out font))
                return font;   // cache-owned — callers must NOT dispose

            try
            {
                font = new Font(family, sizePt, style, GraphicsUnit.Point);
            }
            catch
            {
                font = new Font(FontFamily.GenericSansSerif, sizePt, style, GraphicsUnit.Point);
            }
            _fontCache[key] = font;
            return font;
        }

        // ----------------------------------------------------------------
        //  INPUT — MOUSE
        // ----------------------------------------------------------------
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            if (!IsDesignTime && Enabled && !Focused) Focus();
            InvalidateChrome();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _navHover = 0;
            _navRectsValid = false;
            InvalidateChrome();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled) return;
            Focus();

            // Embedded navigation takes priority over panning.
            int nav;
            if (NavHitTest(e.Location, out nav))
            {
                if (_multipageLocked)
                {
                    RaiseLocked(ViewerFeature.Multipage,
                        "Multipage navigation requires a license (feature 'M').");
                }
                else
                {
                    switch (nav)
                    {
                        case 1: GoToFrame(0); break;                    // « first
                        case 2: PreviousFrame(); break;                 // ‹ prev
                        case 3: NextFrame(); break;                     // next ›
                        case 4: GoToFrame(_frameCount - 1); break;      // last »
                    }
                }
                return;
            }

            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
            {
                _panning = true;
                _pressed = true;
                _lastMouse = e.Location;
                Cursor = Cursors.SizeAll;
                InvalidateChrome();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _hoverPos = e.Location;
            if (!Enabled) return;

            // Navigation hover + cursor
            int nav;
            bool overNav = NavHitTest(e.Location, out nav);
            int newHover = overNav ? nav : 0;
            if (newHover != _navHover) { _navHover = newHover; Invalidate(); }
            if (!_panning) Cursor = overNav ? Cursors.Hand : Cursors.Cross;

            bool needsRepaint = false;

            if (_panning)
            {
                _pan.X += e.X - _lastMouse.X;
                _pan.Y += e.Y - _lastMouse.Y;
                _lastMouse = e.Location;
                needsRepaint = true;
            }

            if (UpdateProbeText()) needsRepaint = true;

            bool crosshairActive = _showCrosshair &&
                (_controlStyle == ViewerStyle.DashboardPremium || _controlStyle == ViewerStyle.Cyberpunk);

            if (needsRepaint || crosshairActive) Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _panning = false;
            _pressed = false;
            Cursor = Cursors.Cross;
            InvalidateChrome();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (_panning)
            {
                _panning = false;
                _pressed = false;
                Cursor = Cursors.Cross;
                InvalidateChrome();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (e is HandledMouseEventArgs hme) hme.Handled = true;
            base.OnMouseWheel(e);

            if (!Enabled || _image == null) return;

            float factor = e.Delta > 0 ? 1.12f : 1f / 1.12f;
            SetZoom(_zoom * factor, new PointF(e.X, e.Y));
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            AutoFit();
        }

        // ----------------------------------------------------------------
        //  INPUT — KEYBOARD
        // ----------------------------------------------------------------
        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.PageUp:
                case Keys.PageDown:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled) return;

            float step = e.Shift ? 96f : 24f;
            switch (e.KeyCode)
            {
                case Keys.PageDown: NextFrame(); e.Handled = true; break;
                case Keys.PageUp: PreviousFrame(); e.Handled = true; break;
                case Keys.Left: PanBy(-step, 0f); e.Handled = true; break;
                case Keys.Right: PanBy(step, 0f); e.Handled = true; break;
                case Keys.Up: PanBy(0f, -step); e.Handled = true; break;
                case Keys.Down: PanBy(0f, step); e.Handled = true; break;
                case Keys.Add:
                case Keys.Oemplus: SetZoom(_zoom * 1.25f, ViewportCenter()); e.Handled = true; break;
                case Keys.Subtract:
                case Keys.OemMinus: SetZoom(_zoom / 1.25f, ViewportCenter()); e.Handled = true; break;
                case Keys.D0:
                case Keys.NumPad0: SetZoom(1f, ViewportCenter()); e.Handled = true; break;
                case Keys.Home:
                case Keys.F: AutoFit(); e.Handled = true; break;
            }
        }

        private PointF ViewportCenter() { return new PointF(Width / 2f, Height / 2f); }

        // ----------------------------------------------------------------
        //  DRAG & DROP
        // ----------------------------------------------------------------
        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            if (!Enabled) return;

            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                try { LoadFromFile(files[0]); }
                catch { /* unsupported / corrupt file — keep current state */ }
            }
        }

        // ----------------------------------------------------------------
        //  LIFECYCLE
        // ----------------------------------------------------------------
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                using (Graphics probe = CreateGraphics())
                {
                    _dpiScale = Math.Max(1f, probe.DpiX / 96f);
                }
            }
            catch { _dpiScale = 1f; }
            InvalidateChrome();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }
            if (_image != null && _needsAutoFit) AutoFit();
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            InvalidateChrome();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ReleaseModeState();               // image + pages + collection + mips + source
                if (_chromeCache != null) { _chromeCache.Dispose(); _chromeCache = null; }
                if (_checkerTile != null) { _checkerTile.Dispose(); _checkerTile = null; }
                foreach (Font font in _fontCache.Values) font.Dispose();
                _fontCache.Clear();
            }
            base.Dispose(disposing);
        }
    }
}






