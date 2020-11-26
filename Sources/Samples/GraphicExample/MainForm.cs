﻿using AngouriMath;
using AngouriMath.Extensions;
using AngouriMathPlot;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace GraphicExample
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            plotter = new AMPlotter(Chart);
        }
        readonly AMPlotter plotter;
        decimal t = 120;
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            button1.Location = new Point(0, Height - 98);
        }

        private void EveryFrame(object sender, EventArgs e)
        {
            var B = MathS.Var("B");
            var expr2 = B * MathS.Sin(t + B) * MathS.Pow(MathS.e, MathS.i * B * MathS.Cos(t));
            var niceFunc2 = expr2.Compile(B);
            plotter.Clear();
            plotter.PlotIterativeComplex(niceFunc2, 0, t);
            plotter.Render();
            t += 0.0005m;
        }

        private void JumpClick(object sender, EventArgs e)
        {
            t += 1.0m;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private CancellationTokenSource? cancellationTokenSource;

        private async void ButtonSolve_Click(object sender, EventArgs e)
        {
            if (cancellationTokenSource is not null)
                return;
            cancellationTokenSource = new();
            LabelState.Text = "Computing...";
            var currTask = Task.Run(() =>
            {
                MathS.Multithreading.SetLocalCancellationToken(cancellationTokenSource.Token);
                return InputText.Text.Solve("x");
            }, cancellationTokenSource.Token);
            try
            {
                await currTask;
                LabelState.Text = currTask.Result.ToString();
            }
            catch (OperationCanceledException)
            {
                LabelState.Text = "Operation canceled";
            }
            finally
            {
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
        }
            
        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            if (cancellationTokenSource is null)
                return;
            cancellationTokenSource.Cancel();
        }
    }
}
