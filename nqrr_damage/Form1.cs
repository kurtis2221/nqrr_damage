using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace nqrr_damage
{
    public partial class Form1 : Form
    {
        class DamageData
        {
            public string name;
            public uint address;
            public uint val;
            public float perc;
            public Bitmap img;

            public DamageData(string name, uint address)
            {
                this.name = name;
                this.address = address;
            }
        }

        const int IMG_WIDTH = 200;
        const int IMG_HEIGHT = 22;
        const int IMG_MARGIN = 112;
        const int TXT_MARGIN = 32;

        string IMG_DIR = "img";
        string IMG_EXT = ".png";

        const string FILE_CFG = "nqrr_damage.ini";
        const string FILE_BAR_EMPTY = "bar_empty";
        const string FILE_BAR_FULL = "bar_full";
        const string FILE_BAR_FULL2 = "bar_full2";

        string proc_name = "DOSBox";
        int max_columns = 4;
        uint base_pointer = 0x01D3C370;
        bool show_numbers = true;
        bool show_diff = true;

        Font gfx_fnt = new Font(FontFamily.GenericSansSerif, 16, FontStyle.Bold);

        List<DamageData> data = new List<DamageData>()
        {
            new DamageData("electrics", 0x286AE8),
            new DamageData("bodywork", 0x286AE0),
            new DamageData("steering", 0x286AD0),
            new DamageData("turbo", 0x286AC8),
            new DamageData("engine", 0x286AC4),
            new DamageData("cooling", 0x286ACC),
            new DamageData("brakes", 0x286ADC),
            new DamageData("brakes2", 0x286AF4),
            new DamageData("tyres", 0x286AF0),
            new DamageData("clutch", 0x286AD8),
            new DamageData("suspension", 0x286AD4),
            new DamageData("exhaust", 0x286AE4),
            new DamageData("lights", 0x286AEC),
            new DamageData("gear1", 0x286AA8),
            new DamageData("gear2", 0x286AAC),
            new DamageData("gear3", 0x286AB0),
            new DamageData("gear4", 0x286AB4),
            new DamageData("gear5", 0x286AB8),
            new DamageData("gear6", 0x286ABC),
            new DamageData("gear7", 0x286AC0),
        };

        Bitmap img_bar_empty;
        Bitmap img_bar_full, img_bar_full2;

        int bar_width;
        int bar_height;

        MemoryEdit.Memory mem;
        Process game;

        public Form1()
        {
            InitializeComponent();
            SetStyle(
               ControlStyles.OptimizedDoubleBuffer |
               ControlStyles.AllPaintingInWmPaint |
               ControlStyles.UserPaint, true);
            LoadConfig();
            ClientSize = new Size(IMG_WIDTH * max_columns - IMG_WIDTH / 3, (data.Count + max_columns - 1) / max_columns * IMG_MARGIN);
            LoadImages();
            mem = new MemoryEdit.Memory();
            tmr.Start();
        }

        private uint ReadDamageData(uint offset)
        {
            uint address = mem.Read(base_pointer) + offset;
            return mem.Read(address);
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(FILE_CFG)) return;
                using (StreamReader sr = new StreamReader(FILE_CFG, Encoding.Default))
                {
                    if (sr.Peek() == -1) return;
                    proc_name = sr.ReadLine();
                    if (sr.Peek() == -1) return;
                    base_pointer = Convert.ToUInt32(sr.ReadLine(), 16);
                    if (sr.Peek() == -1) return;
                    tmr.Interval = Convert.ToInt32(sr.ReadLine());
                    if (sr.Peek() == -1) return;
                    max_columns = Convert.ToInt32(sr.ReadLine());
                    if (sr.Peek() == -1) return;
                    show_numbers = sr.ReadLine() != "0";
                    if (sr.Peek() == -1) return;
                    show_diff = sr.ReadLine() != "0";
                    if (sr.Peek() == -1) return;
                    data.Clear();
                    while (sr.Peek() > -1)
                    {
                        string[] line = sr.ReadLine().Split(';');
                        DamageData tmp = new DamageData(line[0], Convert.ToUInt32(line[1], 16));
                        data.Add(tmp);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadImages()
        {
            img_bar_empty = LoadImageFile(FILE_BAR_EMPTY);
            img_bar_full = LoadImageFile(FILE_BAR_FULL);
            img_bar_full2 = LoadImageFile(FILE_BAR_FULL2);
            for (int i = 0; i < data.Count; i++)
            {
                data[i].img = LoadImageFile(data[i].name);
            }
            bar_width = img_bar_full.Width;
            bar_height = img_bar_full.Height;
        }

        private Bitmap LoadImageFile(string fname)
        {
            string img_file = Path.Combine(IMG_DIR, fname + IMG_EXT);
            return new Bitmap(img_file);
        }

        private void DrawInfo(Graphics gfx)
        {
            Random rnd = new Random();
            for (int i = 0; i < data.Count; i++)
            {
                int x = (i % max_columns) * IMG_WIDTH;
                int y = (i / max_columns) * IMG_MARGIN;
                DamageData tmp = data[i];
                gfx.DrawImage(tmp.img, x, y);
                gfx.DrawImage(img_bar_empty, x, y + IMG_HEIGHT, bar_width, bar_height);
                uint val = ReadDamageData(tmp.address);
                float perc = val / (float)uint.MaxValue;
                int width = (int)(bar_width * perc + 3); //First 3 pixels are not showing data
                if (width > bar_width) width = bar_width;
                bool change = tmp.val == val;
                Bitmap img = change ? img_bar_full : img_bar_full2;
                gfx.DrawImage(img, new Rectangle(x, y + IMG_HEIGHT, width, bar_height), new Rectangle(0, 0, width, bar_height), GraphicsUnit.Pixel);
                perc *= 100;
                if (show_numbers)
                {
                    gfx.DrawString(perc.ToString("0") + "/100", gfx_fnt, Brushes.White, x, y + IMG_HEIGHT + TXT_MARGIN);
                }
                if (show_diff)
                {
                    if (!change)
                    {
                        gfx.DrawString(((perc - tmp.perc) * 100).ToString("0.00"), gfx_fnt, Brushes.Red, x, y + IMG_HEIGHT + TXT_MARGIN + TXT_MARGIN);
                    }
                }
                tmp.val = val;
                tmp.perc = perc;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            DrawInfo(e.Graphics);
            base.OnPaint(e);
        }

        private void tmr_Tick(object sender, EventArgs e)
        {
            if (game == null || game.HasExited) ScanForGame();
            else Invalidate();
        }

        private void ScanForGame()
        {
            Process[] procs = Process.GetProcessesByName(proc_name);
            if (procs.Length > 0)
            {
                game = procs[0];
                mem.Attach((uint)game.Id, MemoryEdit.Memory.ProcessAccessFlags.VirtualMemoryRead);
            }
        }
    }
}
