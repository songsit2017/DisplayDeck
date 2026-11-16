using System;
using System.Windows;
using System.Windows.Threading;

namespace DisplayManagerClone
{
    public partial class IdentifyWindow : Window
    {
        private DispatcherTimer _timer;

        public IdentifyWindow(string number, double left, double top, double width, double height, double dpiScaleX = 1.0, double dpiScaleY = 1.0)
        {
            InitializeComponent();
            TxtNumber.Text = number;

            if (dpiScaleX <= 0) dpiScaleX = 1.0;
            if (dpiScaleY <= 0) dpiScaleY = 1.0;
            
            this.Left = left / dpiScaleX;
            this.Top = top / dpiScaleY;
            this.Width = width / dpiScaleX;
            this.Height = height / dpiScaleY;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(2);
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                this.Close();
            };
            _timer.Start();
        }
    }
}
