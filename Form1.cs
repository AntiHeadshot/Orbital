using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Orbital
{
    public partial class Form1 : Form
    {
        private readonly GravSystem _grav = new(800, 800);
        private readonly GravSystem _grav2 = new(800, 800);

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.Paint += Form1_OnPaint;
            this.Width = 1600;
            this.Height = 800;

            Bitmap trail = new(Width, Height);
            Brush fadeBrush = new SolidBrush(Color.FromArgb(5, 0, 0, 0));
            Brush fadeBrush2 = new SolidBrush(Color.FromArgb(10, 0, 0, 0));

            _grav.OnRendering += DrawTrail(trail, _grav, fadeBrush);

            _grav.Add(new Planet(new Vector2(400, 400), 50, (Pen)Pens.White.Clone(), 0.2f));

            _grav.Add(new Meteorite(new Vector2(550, 400), 5, (Pen)Pens.Magenta.Clone(), new Vector2(0, 0.5f)));
            _grav.Add(new Meteorite(new Vector2(550, 400), 5, (Pen)Pens.LawnGreen.Clone(), new Vector2(0, 1)));
            _grav.Add(new Meteorite(new Vector2(550, 400), 5, (Pen)Pens.Yellow.Clone(), new Vector2(0, 2.5f)));
            _grav.Add(new Meteorite(new Vector2(550, 400), 5, (Pen)Pens.Red.Clone(), new Vector2(0, 5)));
            _grav.Add(new Meteorite(new Vector2(550, 400), 5, (Pen)Pens.Cyan.Clone(), new Vector2(0, 7)));

            Bitmap trail2 = new(Width, Height);

            _grav2.OnRendering += DrawTrail(trail2, _grav2, fadeBrush2);

            _grav2.Add(new Planet(new Vector2(400, 400), 50, (Pen)Pens.White.Clone(), 0.2f));

            _grav2.Add(new Meteorite(new Vector2(550, 400), 5, (Pen)Pens.Magenta.Clone(), new Vector2(0, 0.5f)));
            _grav2.Add(new Meteorite(new Vector2(550, 400), 5, (Pen)Pens.LawnGreen.Clone(), new Vector2(0, 1)));
            _grav2.Add(new Meteorite(new Vector2(550, 400), 5, (Pen)Pens.Yellow.Clone(), new Vector2(0, 2.5f)));
            _grav2.Add(new Meteorite(new Vector2(550, 400), 5, (Pen)Pens.Red.Clone(), new Vector2(0, 5)));
            _grav2.Add(new Meteorite(new Vector2(550, 400), 5, (Pen)Pens.Cyan.Clone(), new Vector2(0, 7)));

            new Task(() =>
            {
                while (true)
                {
                    _grav.Tick();
                    _grav.Invalidate();
                    Invalidate();
                    Thread.Sleep(100 / 120);
                }
            }).Start();

            new Task(() =>
            {
                while (true)
                {
                    _grav2.Tick();
                    _grav2.Invalidate();
                    Thread.Sleep(500);
                }
            }).Start();
        }

        private GravSystem.OnRenderingEvent DrawTrail(Bitmap trail, GravSystem sys, Brush fadeBrush)
        {
            return g =>
            {
                using (Graphics gTrail = Graphics.FromImage(trail))
                {
                    gTrail.SmoothingMode = SmoothingMode.AntiAlias;

                    foreach (GravObject obj in sys.ObjsMoving)
                    {
                        gTrail.DrawLine(obj.Pen, obj.Pos.X, obj.Pos.Y, obj.PosOld.X, obj.PosOld.Y);
                    }
                    gTrail.FillRectangle(fadeBrush, 0, 0, trail.Width, trail.Height);
                }

                g.DrawImageUnscaled(trail, 0, 0);
            };
        }

        private void Form1_OnPaint(object source, PaintEventArgs args)
        {
            _grav.Draw(args.Graphics, 0, 0);
            _grav2.Draw(args.Graphics, 800, 0);
        }

    }

    public class GravSystem
    {
        public delegate void OnRenderingEvent(Graphics g);
        public event OnRenderingEvent OnRendering;

        private readonly List<GravObject> _objs = new();
        private readonly List<GravObject> _objsWithGrav = new();
        private readonly List<GravObject> _objsMoving = new();
        private readonly List<GravObject> _objsActive = new();

        public readonly IReadOnlyList<GravObject> Objs;
        public readonly IReadOnlyList<GravObject> ObjsWithGrav;
        public readonly IReadOnlyList<GravObject> ObjsMoving;
        public readonly IReadOnlyList<GravObject> ObjsActive;

        private Bitmap[] _bmp;
        private int _bmpIndex = 0;
        private Semaphore _renderLock = new(1, 1);
        private object _drawLock = new();

        public GravSystem(int width, int height)
        {
            _bmp = new Bitmap[2];
            _bmp[0] = new Bitmap(width, height);
            _bmp[1] = new Bitmap(width, height);

            Objs = new ReadOnlyCollection<GravObject>(_objs);
            ObjsWithGrav = new ReadOnlyCollection<GravObject>(_objsWithGrav);
            ObjsMoving = new ReadOnlyCollection<GravObject>(_objsMoving);
            ObjsActive = new ReadOnlyCollection<GravObject>(_objsActive);
        }


        private long lastT = Environment.TickCount;
        public void Tick()
        {
            long time = Environment.TickCount;
            float dt = (time - lastT) / 100f;
            lastT = time;

            foreach (GravObject obj in _objsActive)
                obj.UpdateAcc(this, dt);
            foreach (GravObject obj in _objsMoving)
                obj.Move(dt);
            foreach (GravObject obj in _objsMoving)
                obj.UpdateV(_objsWithGrav, dt);
        }

        public void Invalidate()
        {
            if (_renderLock.WaitOne(1))
                new Task(Render).Start();
        }

        private void Render()
        {
            lock (_drawLock)
                _bmpIndex++;
            using (Graphics g = Graphics.FromImage(_bmp[_bmpIndex & 1]))
            {
                g.Clear(Color.Black);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                OnRendering?.Invoke(g);

                foreach (GravObject obj in _objs)
                    obj.Draw(g);
            }

            _renderLock.Release();
        }

        public void Draw(Graphics g, int x, int y)
        {
            lock (_drawLock)
                g.DrawImageUnscaled(_bmp[~_bmpIndex & 1], x, y);// inverted index
        }

        public void Add(GravObject obj)
        {
            switch (obj.Movement)
            {
                case Movement.Active:
                    _objsActive.Add(obj);
                    goto case Movement.Moving;
                case Movement.Moving:
                    _objsMoving.Add(obj);
                    goto default;
                case Movement.Still:
                default:
                    _objs.Add(obj);
                    break;
            }

            if (obj.Grav != 0)
                _objsWithGrav.Add(obj);
        }
    }

    public abstract class GravObject
    {
        public float R { get; protected set; }
        public Vector2 Pos { get; protected set; }
        public Vector2 PosOld { get; protected set; }

        public Movement Movement { get; protected set; }

        public Vector2 V { get; protected set; }

        public Vector2 Acc { get; protected set; }
        protected Func<GravSystem, Vector2> _updateAcc;

        public float Grav { get; protected set; }

        public Pen Pen { get; protected set; }

        public GravObject(Vector2 pos, float r, Pen pen)
        {
            this.Pen = pen;
            this.R = r;
            this.Pos = pos;
            this.PosOld = pos;
        }

        public void Draw(Graphics g)
        {
            g.DrawEllipse(Pen, Pos.X - R, Pos.Y - R, R * 2, R * 2);
        }

        public void UpdateV(IEnumerable<GravObject> objs, float dt)
        {
            Vector2 g = Vector2.Zero;

            foreach (GravObject obj in objs)
            {
                Vector2 dist = (obj.Pos - Pos);
                float lenS = dist.LengthSquared();
                g += (dist * obj.Grav * 100) / lenS;//dist.Length = Grav*100/len;
            }

            V += g * dt;
        }

        public void UpdateAcc(GravSystem sys, float dt)
        {
            Acc = _updateAcc(sys);
            V += Acc * dt;
        }

        public void Move(float dt)
        {
            PosOld = Pos;
            Pos += V * dt;
        }
    }

    public class Planet : GravObject
    {
        public Planet(Vector2 pos, float r, Pen pen, float grav) : base(pos, r, pen)
        {
            this.Grav = grav;

            Movement = Movement.Still;
            Acc = Vector2.Zero;
            V = Vector2.Zero;
        }
    }

    public class Meteorite : GravObject
    {
        public Meteorite(Vector2 pos, float r, Pen pen, Vector2 v, float grav = 0) : base(pos, r, pen)
        {
            this.Grav = grav;
            V = v;

            Movement = Movement.Moving;
            Acc = Vector2.Zero;
        }
    }

    public class Ship : GravObject
    {
        public Ship(Vector2 pos, float r, Pen pen, Vector2 v, Func<GravSystem, Vector2> updateAcc, float grav = 0) : base(pos, r, pen)
        {
            this.Grav = grav;
            V = v;

            Movement = Movement.Active;
            Acc = Vector2.Zero;
            _updateAcc = updateAcc;
        }
    }

    public enum Movement
    {
        Still,
        Moving,
        Active,
    }
}
