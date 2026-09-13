using System;
using System.Windows;
using System.Windows.Media;

namespace DisplayManagerClone
{
    public partial class ColorPickerWindow : Window
    {
        public Color SelectedColor { get; private set; } = Colors.Black;

        public ColorPickerWindow()
        {
            InitializeComponent();
        }

        private void TxtHex_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(TxtHex.Text);
                ColorPreview.Background = new SolidColorBrush(color);
            }
            catch { }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SelectedColor = (Color)ColorConverter.ConvertFromString(TxtHex.Text);
                DialogResult = true;
            }
            catch
            {
                MessageBox.Show("Invalid hex color.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
