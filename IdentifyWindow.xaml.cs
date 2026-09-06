using System;
using System.Windows;
using System.Windows.Threading;

namespace DisplayManagerClone
{
    public partial class IdentifyWindow : Window
    {
        private DispatcherTimer _timer;

        public IdentifyWindow(string number, double left, double top, double width, double height)
        {
            InitializeComponent();
            TxtNumber.Text = number;
            
            this.Left = left;
            this.Top = top;
            this.Width = width;
            this.Height = height;

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
